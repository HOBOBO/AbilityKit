using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionInputExecution;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionLocalInputQueue;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionSettledTimeline;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 按原有优先级推进单个世界：准备、结算时间线、权威确认、重演和本地预测。
    /// </summary>
    internal static class ClientPredictionTickCoordinator
    {
        private const int ReplayWaitTimeoutTicks = 120;

        internal static bool Prepare(ClientPredictionWorldContext ctx, ClientPredictionDriverState state)
        {
            if (ctx.Progress.RollbackFaulted) return false;
            if (ctx.Inputs.PendingSettledFrame != null)
            {
                ClearPendingTimelineFrame(ctx);
                ctx.Progress.RollbackFaulted = true;
                Log.Warning("[ClientPredictionDriverModule] World tick did not complete the pending settled frame.");
                return false;
            }
            if (ctx.Bindings.World.Services?.TryResolve<IWorldFrameGuard>(out var preGuard) == true && !preGuard.CanAdvance) return false;
            ctx.Progress.PredictedBeforeTick = ctx.Progress.PredictedFrame;
            ctx.Progress.ConfirmedBeforeTick = ctx.Progress.ConfirmedFrame;

            state.LastConsumedConfirmedFrames = 0;
            state.LastConsumedPredictedFrames = 0;
            return true;
        }

        internal static void Advance(ClientPredictionWorldContext ctx, ClientPredictionDriverState state,
            IConsumableRemoteFrameSource<PlayerInputCommand[]> remote,
            IConsumableRemoteFrameSource<WorldSettledInputFrame> timelineSource,
            ILocalInputSource<LocalPlayerInputEvent[]> local, int ideal,
            ClientPredictionTickSettings settings, float deltaTime)
        {
            state.CurrentIdealFrameLimit = ideal;
            ctx.Window.IdealFrameLimit = ideal;
            ctx.Window.IdealFrameStalled = false;
            ctx.Window.IdealFrameCappedWindow = false;

            var predictionWindow = PredictionWindowCalculator.Calculate(
                timelineSource?.TargetFrame ?? remote?.TargetFrame ?? ctx.Progress.ConfirmedFrame.Value,
                ctx.Progress.ConfirmedFrame.Value, settings.InputDelayFrames, settings.MinPredictionWindow,
                settings.MaxPredictionAheadFrames, settings.BacklogEwmaAlpha, ideal,
                ref ctx.Window.HasBacklogEwma, ref ctx.Window.BacklogEwma);
            ctx.Window.BacklogRaw = predictionWindow.RawBacklog;
            state.CurrentBacklogRaw = predictionWindow.RawBacklog;
            state.CurrentBacklogEwma = ctx.Window.BacklogEwma;
            state.CurrentPredictionWindow = predictionWindow.Window;
            ctx.Window.PredictionWindow = predictionWindow.Window;
            ctx.Window.PredictionStalled = false;
            ctx.Window.IdealFrameCappedWindow = predictionWindow.CappedByIdeal;
            if (predictionWindow.StalledByIdeal)
            {
                state.IsPredictionStalledByIdealFrame = true;
                state.TotalIdealFrameStalls++;
                ctx.Window.IdealFrameStalled = true;
                ctx.Window.IdealFrameStallsTotal++;
            }

            /*
             * 拉取本地批次；空批次沿用队列入口的无操作行为。
             */
            if (local != null)
            {
                var evts = Array.Empty<LocalPlayerInputEvent>();
                if (local.TryDequeue(out var dequeued) && dequeued != null)
                {
                    evts = dequeued;
                }

                ScheduleLocalInputs(ctx, state, settings.MaxLocalDelayQueueDepth, evts);
            }

            if (ctx.Bindings.SettledTimeline != null && AdvanceSettledTimeline(ctx, state, timelineSource, deltaTime)) return;
            if (TryAdvanceAuthoritative(ctx, state, remote, settings, deltaTime)) return;
            if (TryAdvanceReplay(ctx, state, remote, deltaTime)) return;
            Predict(ctx, state, deltaTime);
        }

        /*
         * 返回 true 表示本世界本次宿主推进已结束；发现差异后仍在本次进入重演阶段。
         */
        private static bool TryAdvanceAuthoritative(ClientPredictionWorldContext ctx, ClientPredictionDriverState state,
            IConsumableRemoteFrameSource<PlayerInputCommand[]> remote, ClientPredictionTickSettings settings, float deltaTime)
        {
            var worldId = ctx.Bindings.World.Id;
            if (remote != null && ctx.Replay.Mode == ClientPredictionReplayMode.Normal)
            {
                var nextConfirmed = ctx.Progress.ConfirmedFrame.Value + 1;
                if (nextConfirmed <= remote.TargetFrame)
                {
                    var frame = new FrameIndex(nextConfirmed);

                    /*
                     * 先窥探输入，用于判断是否需要回滚。
                     */
                    PlayerInputCommand[] authInputs;
                    if (!remote.TryGet(nextConfirmed, out authInputs) || authInputs == null)
                    {
                        if (ctx.Bindings.SettledInputSink != null) return true;
                        authInputs = Array.Empty<PlayerInputCommand>();
                    }

                    var confirmsAlreadyPredictedFrame = false;
                    if (settings.EnableRollback && ctx.Bindings.Rollback != null && ctx.Progress.PredictedFrame.Value >= frame.Value)
                    {
                        PlayerInputCommand[] appliedAtFrame = null;
                        if (ctx.Inputs.AppliedInputs == null)
                        {
                            /*
                             * 未保留已执行输入时无法比较和重演，沿用已预测帧已完成的既定处理。
                             */
                            confirmsAlreadyPredictedFrame = true;
                        }
                        else if (!ctx.Inputs.AppliedInputs.TryGet(frame, out appliedAtFrame) || appliedAtFrame == null)
                        {
                            if (ctx.Replay.LastMissingAppliedHistoryFrame.Value != frame.Value)
                            {
                                ctx.Replay.LastMissingAppliedHistoryFrame = frame;
                                state.TotalRollbackRestoreFailed++;
                                Log.Warning(
                                    $"[ClientPredictionDriverModule] Authoritative frame is outside applied input history; " +
                                    $"stopping before stale submission. worldId={worldId.Value}, frame={frame.Value}, " +
                                    $"predicted={ctx.Progress.PredictedFrame.Value}, historyCapacity={ctx.Inputs.AppliedInputs.Capacity}");
                            }

                            if (ctx.Bindings.SettledInputSink != null) ctx.Progress.RollbackFaulted = true;
                            return true;
                        }

                        if (!InputsEqual(appliedAtFrame, authInputs))
                        {
                            var rollbackFrame = new FrameIndex(frame.Value - 1);
                            var ok = TryRestoreState(ctx, rollbackFrame);
                            if (ok)
                            {
                                state.TotalRollbackCount++;
                                ctx.Replay.Mode = ClientPredictionReplayMode.Replaying;
                                ctx.Replay.ReplayTo = ctx.Progress.PredictedFrame;
                                ctx.Progress.ConfirmedFrame = rollbackFrame;
                                ctx.Progress.PredictedFrame = rollbackFrame;
                                ctx.Replay.LastRollbackFrame = rollbackFrame;
                                ctx.Progress.LastProcessedRollbackFrame = rollbackFrame;
                                ctx.Replay.LastReplayWaitTargetFrame = -1;
                                BeginInputReplay(ctx, rollbackFrame, ctx.Replay.ReplayTo);

                                state.IsReplaying = true;
                                state.ReplayToFrame = ctx.Replay.ReplayTo;
                                state.LastRollbackFrame = rollbackFrame;
                            }
                            else
                            {
                                state.TotalRollbackRestoreFailed++;
                                if (ctx.Bindings.SettledInputSink != null) ctx.Progress.RollbackFaulted = true;
                                return true;
                            }
                        }
                        else
                        {
                            confirmsAlreadyPredictedFrame = true;
                        }
                    }

                    if (ctx.Replay.Mode == ClientPredictionReplayMode.Replaying)
                    {
                        /*
                         * 继续落入下方回放逻辑。
                         */
                    }
                    else
                    {
                        /*
                         * 消费并应用权威输入。
                         */
                        if (ctx.Bindings.SettledInputSink != null && !confirmsAlreadyPredictedFrame)
                        {
                            ScheduleSettledInput(ctx, state, frame, authInputs, remote, deltaTime);
                            return true;
                        }

                        if (!remote.TryConsume(nextConfirmed, out var consumed) || consumed == null)
                        {
                            if (ctx.Bindings.SettledInputSink != null) return true;
                            consumed = Array.Empty<PlayerInputCommand>();
                        }
                        if (consumed.Length > 0)
                        {
                            Log.Info($"[ClientPredictionDriverModule] Consuming authoritative inputs. worldId={worldId.Value}, frame={nextConfirmed}, count={consumed.Length}, firstOpCode={consumed[0].OpCode}");
                        }

                        ctx.Inputs.AuthoritativeInputs?.Store(frame, consumed);

                        ctx.Progress.ConfirmedFrame = frame;
                        state.LastConsumedConfirmedFrames = 1;
                        state.TotalConsumedConfirmedFrames++;

                        if (confirmsAlreadyPredictedFrame)
                        {
                            if (ctx.Bindings.SettledInputSink != null) return true;
                            /*
                             * 相同输入已经模拟，本次宿主推进继续准备下一预测帧，避免重复执行。
                             */
                        }
                        else
                        {
                            AlignFrameTime(ctx.Bindings.FrameTime, frame, deltaTime);
                            SubmitReceivedInput(ctx, frame, consumed);
                            state.ShouldRunWorldTick = true;
                            ctx.Inputs.AppliedInputs?.Store(frame, consumed);

                            if (ctx.Progress.PredictedFrame.Value < frame.Value)
                            {
                                ctx.Progress.PredictedFrame = frame;
                            }

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool TryAdvanceReplay(ClientPredictionWorldContext ctx, ClientPredictionDriverState state,
            IConsumableRemoteFrameSource<PlayerInputCommand[]> remote, float deltaTime)
        {
            var worldId = ctx.Bindings.World.Id;
            if (ctx.Replay.Mode == ClientPredictionReplayMode.Replaying)
            {
                state.IsReplaying = true;
                state.ReplayToFrame = ctx.Replay.ReplayTo;
                state.LastRollbackFrame = ctx.Replay.LastRollbackFrame;

                var next = new FrameIndex(ctx.Progress.PredictedFrame.Value + 1);

                if (next.Value > ctx.Replay.ReplayTo.Value)
                {
                    EndInputReplay(ctx);
                    ctx.Replay.Mode = ClientPredictionReplayMode.Normal;
                    ctx.Replay.ReplayWaitTicks = 0;
                    ctx.Replay.LastReplayWaitTargetFrame = -1;
                    state.IsReplaying = false;
                    return true;
                }

                if (ctx.Bindings.SettledInputSink != null)
                {
                    if (remote != null && remote.TryGet(next.Value, out var available) && available != null)
                        ScheduleSettledInput(ctx, state, next, available, remote, deltaTime);
                    return true;
                }

                /*
                 * 仅在该帧权威输入可用时重演；缺失时按下方既定超时规则等待。
                 */
                PlayerInputCommand[] inputs = null;
                if (ctx.Inputs.AuthoritativeInputs != null && ctx.Inputs.AuthoritativeInputs.TryGet(next, out var auth) && auth != null)
                {
                    inputs = auth;
                    if (ctx.Progress.ConfirmedFrame.Value < next.Value)
                    {
                        ctx.Progress.ConfirmedFrame = next;
                    }
                }
                else if (remote != null && next.Value <= remote.TargetFrame)
                {
                    if (!remote.TryConsume(next.Value, out var consumed) || consumed == null)
                    {
                        consumed = Array.Empty<PlayerInputCommand>();
                    }
                    if (consumed.Length > 0)
                    {
                        Log.Info($"[ClientPredictionDriverModule] Consuming replay authoritative inputs. worldId={worldId.Value}, frame={next.Value}, count={consumed.Length}, firstOpCode={consumed[0].OpCode}");
                    }
                    inputs = consumed;
                    ctx.Inputs.AuthoritativeInputs?.Store(next, consumed);
                    if (ctx.Progress.ConfirmedFrame.Value < next.Value)
                    {
                        ctx.Progress.ConfirmedFrame = next;
                    }
                }
                else
                {
                    /*
                     * 该帧尚无权威输入，暂停回放（不提交预测输入）。
                     */
                    var replayWaitTargetFrame = remote != null ? remote.TargetFrame : -1;
                    if (ctx.Replay.LastReplayWaitTargetFrame != replayWaitTargetFrame)
                    {
                        ctx.Replay.LastReplayWaitTargetFrame = replayWaitTargetFrame;
                        ctx.Replay.ReplayWaitTicks++;
                    }
                    if (ctx.Replay.ReplayWaitTicks >= ReplayWaitTimeoutTicks)
                    {
                        Log.Warning($"[ClientPredictionDriverModule] Replay wait timeout. worldId={worldId} rollbackFrame={ctx.Replay.LastRollbackFrame.Value} predicted={ctx.Progress.PredictedFrame.Value} replayTo={ctx.Replay.ReplayTo.Value} targetFrame={(remote != null ? remote.TargetFrame : -1)}. Disabling reconcile and exiting replay.");
                        state.TotalReplayTimeout++;
                        state.LastReplayTimeoutFrame = next;
                        state.TotalReconcileAutoDisabledByReplayTimeout++;
                        state.LastReconcileAutoDisabledByReplayTimeoutFrame = next;
                        ctx.Reconciliation.ReconcileEnabled = false;
                        EndInputReplay(ctx);
                        ctx.Replay.Mode = ClientPredictionReplayMode.Normal;
                        ctx.Replay.ReplayWaitTicks = 0;
                        ctx.Replay.LastReplayWaitTargetFrame = -1;
                    }
                    return true;
                }

                ctx.Replay.ReplayWaitTicks = 0;
                ctx.Replay.LastReplayWaitTargetFrame = -1;

                AlignFrameTime(ctx.Bindings.FrameTime, next, deltaTime);
                SubmitReplayInput(ctx, next, inputs);
                state.ShouldRunWorldTick = true;
                ctx.Inputs.AppliedInputs?.Store(next, inputs);
                ctx.Progress.PredictedFrame = next;
                state.LastConsumedPredictedFrames = 1;
                state.TotalPredictedFrames++;
                return true;
            }
            return false;
        }

        private static void Predict(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, float deltaTime)
        {
            if (state.CurrentPredictionWindow == 0)
            {
                if (ctx.Window.IdealFrameCappedWindow)
                {
                    state.IsPredictionStalledByIdealFrame = true;
                }
                return;
            }

            var next = new FrameIndex(ctx.Progress.PredictedFrame.Value + 1);

            var ahead = ctx.Progress.PredictedFrame.Value - ctx.Progress.ConfirmedFrame.Value;
            if (ahead >= state.CurrentPredictionWindow)
            {
                if (ctx.Window.IdealFrameCappedWindow)
                {
                    state.IsPredictionStalledByIdealFrame = true;
                    state.TotalIdealFrameStalls++;
                    ctx.Window.IdealFrameStalled = true;
                    ctx.Window.IdealFrameStallsTotal++;
                }
                else
                {
                    state.IsPredictionStalledByWindow = true;
                    state.TotalPredictionWindowStalls++;
                    ctx.Window.PredictionStalled = true;
                    ctx.Window.PredictionWindowStallsTotal++;
                }
                return;
            }

            var localBatch = TakeLocalInputs(ctx, next);
            PlayerInputCommand[] predictedInputs;
            if (localBatch.Length == 0)
            {
                predictedInputs = Array.Empty<PlayerInputCommand>();
            }
            else
            {
                predictedInputs = new PlayerInputCommand[localBatch.Length];
                for (int i = 0; i < localBatch.Length; i++)
                {
                    var e = localBatch[i];
                    predictedInputs[i] = new PlayerInputCommand(next, e.PlayerId, e.OpCode, e.Payload ?? Array.Empty<byte>());
                }
            }

            if (ctx.Inputs.RawPredictionInputs != null)
            {
                for (var index = 0; index < predictedInputs.Length; index++)
                {
                    var input = predictedInputs[index];
                    predictedInputs[index] = new PlayerInputCommand(next, input.Player, input.OpCode, (byte[])input.Payload.Clone());
                }
                ctx.Inputs.RawPredictionInputs.Store(next, predictedInputs);
            }
            SchedulePrediction(ctx, state, next, predictedInputs, deltaTime);
        }
    }
}

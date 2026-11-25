using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionInputExecution;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 结算时间线的准备、后缀恢复和提交；消费记录仍受帧完成与快照门禁约束。
    /// </summary>
    internal static class ClientPredictionSettledTimeline
    {
        internal static bool AdvanceSettledTimeline(ClientPredictionWorldContext ctx, ClientPredictionDriverState state,
            IConsumableRemoteFrameSource<WorldSettledInputFrame> source, float deltaTime)
        {
            try
            {
                if (ctx.Replay.Mode == ClientPredictionReplayMode.Replaying && ctx.Progress.PredictedFrame.Value >= ctx.Replay.ReplayTo.Value)
                    ctx.Replay.Mode = ClientPredictionReplayMode.Normal;
                var nextConfirmed = new FrameIndex(ctx.Progress.ConfirmedFrame.Value + 1);
                if (source != null && nextConfirmed.Value <= source.TargetFrame)
                {
                    if (!source.TryGet(nextConfirmed.Value, out var record) || record == null) return true;
                    if (record.Frame.Value != nextConfirmed.Value)
                        throw new InvalidOperationException("Unexpected settled frame identity.");

                    var update = ctx.Bindings.SettledTimeline.PrepareSettledFrame(record);
                    var matches = false;
                    if (ctx.Progress.PredictedFrame.Value >= nextConfirmed.Value)
                    {
                        if (!ctx.Inputs.AppliedInputs.TryGet(nextConfirmed, out var applied))
                            throw new InvalidOperationException("Settled frame is outside applied-input history.");
                        matches = InputsEqual(applied, record.ExecutedInputs);
                        if (!matches || update.InvalidatesPrediction && ctx.Progress.PredictedFrame.Value > nextConfirmed.Value)
                            RestoreSettledSuffix(ctx, state, nextConfirmed, matches);
                    }

                    if (matches)
                    {
                        CommitTimelineFrame(ctx, state, source, record, update);
                    }
                    else
                    {
                        AlignFrameTime(ctx.Bindings.FrameTime, nextConfirmed, deltaTime);
                        ctx.Bindings.SettledInputSink.ExecuteSettledInputs(nextConfirmed, record.ExecutedInputs);
                        ctx.Inputs.PendingSettledFrame = record;
                        ctx.Bindings.PendingSettledUpdate = update;
                        ctx.Bindings.PendingSettledSource = source;
                        state.ShouldRunWorldTick = true;
                    }
                }
                else if (ctx.Replay.Mode == ClientPredictionReplayMode.Replaying && ctx.Progress.PredictedFrame.Value < ctx.Replay.ReplayTo.Value)
                {
                    var next = new FrameIndex(ctx.Progress.PredictedFrame.Value + 1);
                    if (!ctx.Inputs.RawPredictionInputs.TryGet(next, out var inputs))
                        throw new InvalidOperationException("Missing original predicted input for suffix replay.");
                    SchedulePrediction(ctx, state, next, inputs, deltaTime);
                }
                else
                {
                    ctx.Replay.Mode = ClientPredictionReplayMode.Normal;
                    return false;
                }

                state.IsReplaying = ctx.Replay.Mode == ClientPredictionReplayMode.Replaying;
                state.ReplayToFrame = ctx.Replay.ReplayTo;
                state.LastRollbackFrame = ctx.Replay.LastRollbackFrame;
            }
            catch (Exception exception)
            {
                ctx.Progress.RollbackFaulted = true;
                Log.Warning($"[ClientPredictionDriverModule] Settled timeline paused: {exception}");
            }
            return true;
        }

        internal static void RestoreSettledSuffix(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, FrameIndex nextConfirmed, bool matches)
        {
            var boundary = matches ? nextConfirmed : ctx.Progress.ConfirmedFrame;
            var replayTo = Math.Max(ctx.Progress.PredictedFrame.Value, ctx.Replay.Mode == ClientPredictionReplayMode.Replaying ? ctx.Replay.ReplayTo.Value : 0);
            for (var frame = nextConfirmed.Value + 1; frame <= replayTo; frame++)
            {
                if (!ctx.Inputs.RawPredictionInputs.TryGet(new FrameIndex(frame), out _))
                    throw new InvalidOperationException("Unconfirmed suffix is outside raw prediction history.");
            }
            if (!TryRestoreState(ctx, boundary))
            {
                state.TotalRollbackRestoreFailed++;
                throw new InvalidOperationException($"Cannot restore settled correction boundary {boundary.Value}.");
            }

            state.TotalRollbackCount++;
            ctx.Replay.Mode = ClientPredictionReplayMode.Replaying;
            ctx.Replay.ReplayTo = new FrameIndex(replayTo);
            ctx.Progress.PredictedFrame = boundary;
            ctx.Progress.PredictedBeforeTick = boundary;
            ctx.Replay.LastRollbackFrame = boundary;
            ctx.Progress.LastProcessedRollbackFrame = boundary;
        }

        internal static void CommitTimelineFrame(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, IConsumableRemoteFrameSource<WorldSettledInputFrame> source,
            WorldSettledInputFrame record, IWorldSettledFrameUpdate update)
        {
            if (ctx.Bindings.World.Services?.TryResolve<IWorldFrameGuard>(out var guard) == true && !guard.CanCommit)
                throw new InvalidOperationException("World frame cannot commit the settled record.");
            if (!source.TryGet(record.Frame.Value, out var available) || !ReferenceEquals(available, record) ||
                !source.TryConsume(record.Frame.Value, out var consumed) || !ReferenceEquals(consumed, record))
                throw new InvalidOperationException("Settled record changed before commit.");

            update.Commit();
            ctx.Progress.ConfirmedFrame = record.Frame;
            if (ctx.Progress.PredictedFrame.Value < record.Frame.Value) ctx.Progress.PredictedFrame = record.Frame;
            state.LastConsumedConfirmedFrames = 1;
            state.TotalConsumedConfirmedFrames++;
        }

        internal static void ClearPendingTimelineFrame(ClientPredictionWorldContext ctx)
        {
            ctx.Inputs.PendingSettledFrame = null;
            ctx.Bindings.PendingSettledUpdate = null;
            ctx.Bindings.PendingSettledSource = null;
        }
    }
}

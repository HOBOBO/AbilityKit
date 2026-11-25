using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 通过世界输入能力执行预测、权威输入和重演；只在原有成功边界更新推进数据。
    /// </summary>
    internal static class ClientPredictionInputExecution
    {
        internal static bool TryRestoreState(ClientPredictionWorldContext ctx, FrameIndex frame)
        {
            if (ctx == null || ctx.Bindings.Rollback == null) return false;
            if (ctx.Bindings.Rollback.TryRestoreAndDiscardFuture(frame, out var result))
            {
                ctx.Reconciliation.PredictedHashes?.DiscardAfter(frame);
                return true;
            }
            ctx.Progress.RollbackFaulted = true;
            Log.Warning($"[ClientPredictionDriverModule] Rollback restore failed; world tick paused. result={result}");
            return false;
        }

        internal static void BeginInputReplay(
            ClientPredictionWorldContext ctx,
            FrameIndex restoredFrame,
            FrameIndex replayToFrame)
        {
            if (ctx == null || ctx.Replay.InputReplayActive || ctx.Bindings.SettledInputSink != null) return;
            if (!(ctx.Bindings.InputSink is IWorldInputReplaySink replaySink)) return;

            replaySink.BeginReplay(restoredFrame, replayToFrame);
            ctx.Replay.InputReplayActive = true;
        }

        internal static void SubmitReplayInput(
            ClientPredictionWorldContext ctx,
            FrameIndex frame,
            IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (ctx.Replay.InputReplayActive && ctx.Bindings.InputSink is IWorldInputReplaySink replaySink)
            {
                replaySink.Replay(frame, inputs);
                return;
            }

            SubmitReceivedInput(ctx, frame, inputs);
        }

        internal static void SubmitReceivedInput(ClientPredictionWorldContext ctx, FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (ctx.Bindings.InputSink is IWorldClientInputSink clientInput) clientInput.SubmitReceived(frame, inputs);
            else ctx.Bindings.InputSink?.Submit(frame, inputs);
        }

        internal static void ScheduleSettledInput(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, FrameIndex frame, PlayerInputCommand[] inputs,
            IConsumableRemoteFrameSource<PlayerInputCommand[]> source, float deltaTime)
        {
            AlignFrameTime(ctx.Bindings.FrameTime, frame, deltaTime);
            try
            {
                ctx.Bindings.SettledInputSink.ExecuteSettledInputs(frame, inputs);
            }
            catch (Exception exception)
            {
                ctx.Progress.RollbackFaulted = true;
                Log.Warning($"[ClientPredictionDriverModule] Settled input dispatch failed at {frame.Value}: {exception}");
                return;
            }

            ctx.Inputs.AppliedInputs.Store(frame, inputs);
            ctx.Progress.PredictedFrame = frame;
            ctx.Bindings.PendingSettlement = source;
            state.ShouldRunWorldTick = true;
        }

        internal static void EndInputReplay(ClientPredictionWorldContext ctx)
        {
            if (ctx == null || !ctx.Replay.InputReplayActive) return;
            try
            {
                (ctx.Bindings.InputSink as IWorldInputReplaySink)?.EndReplay();
            }
            finally
            {
                ctx.Replay.InputReplayActive = false;
            }
        }

        internal static void SchedulePrediction(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, FrameIndex next, PlayerInputCommand[] predictedInputs, float deltaTime)
        {
            AlignFrameTime(ctx.Bindings.FrameTime, next, deltaTime);
            if (ctx.Bindings.SettledInputSink != null)
            {
                try
                {
                    predictedInputs = ctx.Bindings.SettledInputSink.ExecutePredictedInputs(next, predictedInputs);
                }
                catch (Exception exception)
                {
                    ctx.Progress.RollbackFaulted = true;
                    Log.Warning($"[ClientPredictionDriverModule] Prediction input dispatch failed at {next.Value}: {exception}");
                    return;
                }
            }
            else if (ctx.Bindings.InputSink is IWorldClientInputSink clientInput) clientInput.SubmitPredicted(next, predictedInputs);
            else ctx.Bindings.InputSink.Submit(next, predictedInputs);
            state.ShouldRunWorldTick = true;
            ctx.Progress.PredictedFrame = next;
            ctx.Inputs.AppliedInputs?.Store(next, predictedInputs);
            state.LastConsumedPredictedFrames = 1;
            state.TotalPredictedFrames++;
        }

        internal static void AlignFrameTime(
            IFrameTimeAligner frameTime,
            FrameIndex targetFrame,
            float fallbackDeltaTime)
        {
            if (frameTime == null) return;

            /*
             * 默认时钟保持定点整数对齐，避免经浮点重建影响冷却边界。
             */
            frameTime.AlignTo(targetFrame, fallbackDeltaTime);
        }

        internal static void AlignFrameTimeToBaseline(IFrameTimeAligner frameTime, FrameIndex baselineFrame)
        {
            if (frameTime == null) return;

            frameTime.AlignTo(baselineFrame, 0f);
        }

        internal static bool InputsEqual(PlayerInputCommand[] a, IReadOnlyList<PlayerInputCommand> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null) a = Array.Empty<PlayerInputCommand>();
            if (b == null) b = Array.Empty<PlayerInputCommand>();
            if (a.Length != b.Count) return false;

            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i];
                var y = b[i];
                if (x.OpCode != y.OpCode) return false;
                if (x.Player.Value != y.Player.Value) return false;

                var xp = x.Payload ?? Array.Empty<byte>();
                var yp = y.Payload ?? Array.Empty<byte>();
                if (xp.Length != yp.Length) return false;
                for (int j = 0; j < xp.Length; j++)
                {
                    if (xp[j] != yp[j]) return false;
                }
            }

            return true;
        }
    }
}

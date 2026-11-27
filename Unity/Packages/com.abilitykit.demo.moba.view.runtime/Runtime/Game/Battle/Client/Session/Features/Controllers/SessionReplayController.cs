using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionReplayController
    {
        private const int StateHashRecordIntervalFrames = 10;

        public void SetupReplayOrRecord(
            BattleStartPlan plan,
            BattleContext ctx,
            BattleReplayRuntime replayResources)
        {
            if (plan.RunModeOptions.RunMode != BattleRunMode.Record || !plan.RunModeOptions.EnableInputRecording) return;
            if (replayResources == null) throw new ArgumentNullException(nameof(replayResources));

            SetupRecordWriter(plan, ctx, replayResources);
        }

        public void OnFrameReceived(BattleStartPlan plan, BattleSessionState state, BattleContext ctx, FramePacket packet)
        {
            if (state == null || ctx == null) return;

            RecordFrameIfNeeded(plan, state, ctx, packet);
        }

        private static void SetupRecordWriter(
            BattleStartPlan plan,
            BattleContext ctx,
            BattleReplayRuntime replayResources)
        {
            if (ctx == null) return;

            var options = plan.RunModeOptions;
            var meta = CreateRecordMeta(plan);
            var writer = BattleRecordFiles.OpenWriter(options.InputRecordOutputPath, options.RecordCoverage, meta,
                message => Log.Info(message));
            replayResources.BindRecordWriter(ctx, writer);
        }

        private static FrameRecordMeta CreateRecordMeta(BattleStartPlan plan)
        {
            var world = plan.World;
            return new FrameRecordMeta
            {
                WorldId = world.WorldId,
                WorldType = world.WorldType,
                TickRate = ResolveRecordTickRate(plan),
                RandomSeed = 0,
                PlayerId = world.PlayerId,
                StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        private static int ResolveRecordTickRate(BattleStartPlan plan)
        {
            var tickRate = plan.World.TickRate;
            return tickRate > 0 ? tickRate : 30;
        }

        private static void RecordFrameIfNeeded(BattleStartPlan plan, BattleSessionState state, BattleContext ctx, FramePacket packet)
        {
            if (!plan.RunModeOptions.EnableInputRecording || ctx.InputRecordWriter == null) return;

            if (packet.Snapshot.HasValue)
            {
                var s = packet.Snapshot.Value;
                ctx.InputRecordWriter.AppendSnapshot(state.Tick.LastFrame, s.OpCode, s.Payload);
            }

            RecordStateHashIfNeeded(state, ctx);
        }

        private static void RecordStateHashIfNeeded(BattleSessionState state, BattleContext ctx)
        {
            var interval = StateHashRecordIntervalFrames;
            if (interval <= 0) interval = 10;

            if ((state.Tick.LastFrame % interval) != 0) return;

            if (ctx.EntityNode.IsValid && ctx.EntityNode.TryGetRef(out BattleStateHashSnapshotComponent h) && h != null)
            {
                ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
            }
        }
    }
}

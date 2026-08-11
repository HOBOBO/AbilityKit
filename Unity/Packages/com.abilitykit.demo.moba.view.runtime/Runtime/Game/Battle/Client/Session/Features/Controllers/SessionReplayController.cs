using System;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class SessionReplayController
    {
        private const int StateHashRecordIntervalFrames = 10;

        public void SetupReplayOrRecord(BattleStartPlan plan, BattleContext ctx)
        {
            if (plan.RunModeOptions.RunMode != BattleRunMode.Record) return;

            BattleRecordCodecBootstrap.TryInstallMemoryPack();
            SetupRecordWriter(plan, ctx);
        }

        public void OnFrameReceived(BattleStartPlan plan, BattleSessionState state, BattleContext ctx, FramePacket packet)
        {
            if (state == null || ctx == null) return;

            RecordFrameIfNeeded(plan, state, ctx, packet);
        }

        private static void SetupRecordWriter(BattleStartPlan plan, BattleContext ctx)
        {
            if (ctx == null) return;

            ctx.InputRecordWriter?.Dispose();

            var outPath = plan.RunModeOptions.InputRecordOutputPath;
            EnsureOutputDirectory(outPath);

            var meta = CreateRecordMeta(plan);
            ctx.InputRecordWriter = FrameRecordCodecs.Current.CreateWriter(outPath, meta);
        }

        private static void EnsureOutputDirectory(string outPath)
        {
            var outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
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

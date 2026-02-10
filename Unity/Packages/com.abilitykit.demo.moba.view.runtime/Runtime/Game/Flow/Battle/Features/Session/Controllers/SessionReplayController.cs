using System;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal interface ISessionReplayHost
        {
            void StartSession();
            void StopSession();
            void ApplyAutoPlanActions();

            float GetFixedDeltaSeconds();
        }

        internal interface IBattleReplayDriverProvider
        {
            bool TryCreate(in BattleStartPlan plan, out LockstepReplayDriver driver);
        }

        private sealed partial class SessionReplayController
        {
            private const int StateHashRecordIntervalFrames = 10;
            private const int ReplaySeekChunkFrames = 300;
            private const int RollbackSeekProbeFrames = 120;

            public void PreTick(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, ISessionReplayHost host)
            {
                if (state == null || handles == null || ctx == null || host == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                HandleReplayDebugInput(plan, state, handles, ctx, host);
#endif
            }

            public void SetupReplayOrRecord(IBattleReplayDriverProvider provider, BattleStartPlan plan, BattleSessionHandles handles, BattleContext ctx)
            {
                if (handles == null) return;

                var runMode = plan.RunMode;
                if (runMode == BattleStartConfig.BattleRunMode.Replay)
                {
                    provider ??= new DefaultBattleReplayDriverProvider();
                    if (provider.TryCreate(in plan, out var injected) && injected != null)
                    {
                        handles.Replay.Driver = injected;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(plan.InputReplayPath))
                        {
                            Log.Error("[BattleReplay] 回放启动失败：InputReplayPath 为空，请在 RunMode 配置中选择回放文件。 ");
                        }
                        else
                        {
                            Log.Error($"[BattleReplay] 回放启动失败：无法创建回放驱动，path={plan.InputReplayPath}");
                        }
                    }
                }

                if (ctx != null)
                {
                    if (runMode == BattleStartConfig.BattleRunMode.Record)
                    {
                        ctx.InputRecordWriter?.Dispose();

                        var outPath = plan.InputRecordOutputPath;
                        var outDir = Path.GetDirectoryName(outPath);
                        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                        var tickRate = plan.TickRate;
                        if (tickRate <= 0) tickRate = 30;

                        var meta = new LockstepInputRecordMeta
                        {
                            WorldId = plan.WorldId,
                            WorldType = plan.WorldType,
                            TickRate = tickRate,
                            RandomSeed = 0,
                            PlayerId = plan.PlayerId,
                            StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        };

                        var ext = Path.GetExtension(outPath);
                        if (string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.InputRecordWriter = new LockstepBinaryInputRecordWriter(outPath, meta);
                        }
                        else
                        {
                            ctx.InputRecordWriter = new LockstepJsonInputRecordWriter(outPath, meta);
                        }
                    }
                }
            }

            public void OnFrameReceived(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, FramePacket packet)
            {
                if (state == null || handles == null || ctx == null) return;

                var replay = handles.Replay.Driver;
                if (replay != null)
                {
                    if (ctx.EntityNode.IsValid && ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hs) && hs != null)
                    {
                        if (!replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                        {
                            Log.Error($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                            replay.Pause();
                        }
                    }
                }

                if (plan.EnableInputRecording && ctx.InputRecordWriter != null)
                {
                    if (packet.Snapshot.HasValue)
                    {
                        var s = packet.Snapshot.Value;
                        ctx.InputRecordWriter.AppendSnapshot(state.Tick.LastFrame, s.OpCode, s.Payload);
                    }

                    var interval = StateHashRecordIntervalFrames;
                    if (interval <= 0) interval = 10;

                    if ((state.Tick.LastFrame % interval) == 0)
                    {
                        if (ctx.EntityNode.IsValid && ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent h) && h != null)
                        {
                            ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
                        }
                    }
                }
            }
        }
    }
}

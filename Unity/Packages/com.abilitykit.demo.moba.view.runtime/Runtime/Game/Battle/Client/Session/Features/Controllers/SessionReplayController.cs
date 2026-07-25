using System;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionReplayHost
    {
        bool RenderPresentation { get; }

        void StartSession();
        void StopSession();
        void ApplyAutoPlanActions();
        void SuspendReplayPresentation();
        void RestoreReplayPresentation();

        float GetFixedDeltaSeconds();
    }

    internal interface IBattleReplayDriverProvider
    {
        bool TryCreate(in BattleStartPlan plan, out FrameReplayDriver driver);
    }

    internal sealed partial class SessionReplayController
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

            BattleRecordCodecBootstrap.TryInstallMemoryPack();

            var runMode = plan.RunModeOptions.RunMode;
            if (runMode == BattleStartConfig.BattleRunMode.Replay)
            {
                SetupReplayDriver(provider, plan, handles);
            }

            if (runMode == BattleStartConfig.BattleRunMode.Record)
            {
                SetupRecordWriter(plan, ctx);
            }
        }

        public void OnFrameReceived(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, FramePacket packet)
        {
            if (state == null || handles == null || ctx == null) return;

            ValidateReplayStateHash(handles, ctx);
            RecordFrameIfNeeded(plan, state, ctx, packet);
        }

        private static void SetupReplayDriver(IBattleReplayDriverProvider provider, BattleStartPlan plan, BattleSessionHandles handles)
        {
            provider ??= new DefaultBattleReplayDriverProvider();
            if (provider.TryCreate(in plan, out var injected) && injected != null)
            {
                handles.Replay.Driver = injected;
                return;
            }

            var runMode = plan.RunModeOptions;
            if (string.IsNullOrEmpty(runMode.InputReplayPath))
            {
                Log.Error("[BattleReplay] Replay startup failed: InputReplayPath is empty. Select a replay file in RunMode settings.");
                return;
            }

            Log.Error($"[BattleReplay] Replay startup failed: unable to create replay driver, path={runMode.InputReplayPath}");
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

        private static void ValidateReplayStateHash(BattleSessionHandles handles, BattleContext ctx)
        {
            var replay = handles.Replay.Driver;
            if (replay == null) return;

            if (ctx.EntityNode.IsValid && ctx.EntityNode.TryGetRef(out BattleStateHashSnapshotComponent hs) && hs != null)
            {
                if (!replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                {
                    Log.Error($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                    replay.Pause();
                }
            }
        }

        public bool SeekToFrame(
            BattleStartPlan plan,
            BattleSessionState state,
            BattleSessionHandles handles,
            BattleContext ctx,
            ISessionReplayHost host,
            int targetFrame)
        {
            if (state == null || handles == null || ctx == null || host == null) return false;
            if (!plan.RunModeOptions.EnableInputReplay) return false;

            var session = handles.Session;
            var replay = handles.Replay.Driver;
            if (session == null || replay == null) return false;

            targetFrame = Math.Max(0, Math.Min(targetFrame, replay.LastFrame));
            var wasPlaying = replay.IsPlaying;
            var playbackSpeed = replay.PlaybackSpeed;
            var fixedDelta = host.GetFixedDeltaSeconds();

            if (targetFrame > state.Tick.LastFrame)
            {
                state.Tick.TickAcc = 0f;
                for (var frame = state.Tick.LastFrame + 1; frame <= targetFrame; frame++)
                {
                    replay.PumpFrame(session, frame);
                    session.Tick(fixedDelta);
                }

                state.Tick.LastFrame = targetFrame;
                SessionContextBinder.BindRuntimeSession(ctx, state, handles);
                RestorePlaybackState(replay, wasPlaying, playbackSpeed);
                return true;
            }

            if (targetFrame == state.Tick.LastFrame)
            {
                state.Tick.TickAcc = 0f;
                return true;
            }

            if (CanUseRollbackSeek(host.RenderPresentation, session.RollbackModule != null))
            {
                var worldId = new WorldId(plan.World.WorldId);
                var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                for (var frame = targetFrame; frame >= probeStart; frame--)
                {
                    if (!session.RollbackModule.TryRollbackAndReplay(
                            worldId,
                            new FrameIndex(frame),
                            new FrameIndex(targetFrame),
                            fixedDelta))
                    {
                        continue;
                    }

                    replay.SeekToFrame(targetFrame + 1);
                    state.Tick.TickAcc = 0f;
                    state.Tick.LastFrame = targetFrame;
                    SessionContextBinder.BindRuntimeSession(ctx, state, handles);
                    RestorePlaybackState(replay, wasPlaying, playbackSpeed);
                    return true;
                }
            }

            if (host.RenderPresentation) host.SuspendReplayPresentation();
            try
            {
                host.StopSession();
                host.StartSession();
                host.ApplyAutoPlanActions();

                session = handles.Session;
                replay = handles.Replay.Driver;
                if (session == null || replay == null) return false;

                replay.SeekToStart();
                state.Tick.TickAcc = 0f;
                for (var frame = 1; frame <= targetFrame; frame++)
                {
                    replay.PumpFrame(session, frame);
                    session.Tick(fixedDelta);
                }

                state.Tick.LastFrame = targetFrame;
                SessionContextBinder.BindRuntimeSession(ctx, state, handles);
                RestorePlaybackState(replay, wasPlaying, playbackSpeed);
                return true;
            }
            finally
            {
                if (host.RenderPresentation) host.RestoreReplayPresentation();
            }
        }

        internal static bool CanUseRollbackSeek(bool renderPresentation, bool hasRollbackModule)
        {
            return !renderPresentation && hasRollbackModule;
        }

        private static void RestorePlaybackState(FrameReplayDriver replay, bool wasPlaying, float playbackSpeed)
        {
            replay.PlaybackSpeed = playbackSpeed;
            if (wasPlaying) replay.Play();
            else replay.Pause();
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

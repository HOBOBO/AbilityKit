using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionReplaySubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionPreTickSubFeature<BattleSessionFeature>,
            ISessionReplaySetupSubFeature<BattleSessionFeature>,
            ISessionFrameReceivedSubFeature<BattleSessionFeature>
        {
            private const int StateHashRecordIntervalFrames = 10;
            private const int ReplaySeekChunkFrames = 300;
            private const int RollbackSeekProbeFrames = 120;

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                HandleReplayDebugInput(f);
#endif
            }

            public void SetupReplayOrRecord(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                var runMode = f._plan.RunMode;
                if (runMode == BattleStartConfig.BattleRunMode.Replay)
                {
                    var file = LockstepJsonInputRecordReader.Load(f._plan.InputReplayPath);
                    f._replay = new LockstepReplayDriver(new WorldId(f._plan.WorldId), file);
                }

                if (f._ctx != null)
                {
                    if (runMode == BattleStartConfig.BattleRunMode.Record)
                    {
                        f._ctx.InputRecordWriter?.Dispose();
                        f._ctx.InputRecordWriter = new LockstepJsonInputRecordWriter(
                            f._plan.InputRecordOutputPath,
                            new LockstepInputRecordMeta
                            {
                                WorldId = f._plan.WorldId,
                                WorldType = f._plan.WorldType,
                                TickRate = 30,
                                RandomSeed = 0,
                                PlayerId = f._plan.PlayerId,
                                StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            });
                    }
                }
            }

            public void OnFrameReceived(in FeatureModuleContext<BattleSessionFeature> ctx, FramePacket packet)
            {
                var f = ctx.Feature;
                if (f == null || f._ctx == null) return;

                if (f._replay != null)
                {
                    if (f._ctx.EntityNode.IsValid && f._ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hs) && hs != null)
                    {
                        if (!f._replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                        {
                            Log.Error($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                            f._replay.Pause();
                        }
                    }
                }

                if (f._plan.EnableInputRecording && f._ctx.InputRecordWriter != null)
                {
                    if (packet.Snapshot.HasValue)
                    {
                        var s = packet.Snapshot.Value;
                        f._ctx.InputRecordWriter.AppendSnapshot(f._lastFrame, s.OpCode, s.Payload);
                    }

                    var interval = StateHashRecordIntervalFrames;
                    if (interval <= 0) interval = 10;

                    if ((f._lastFrame % interval) == 0)
                    {
                        if (f._ctx.EntityNode.IsValid && f._ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent h) && h != null)
                        {
                            f._ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
                        }
                    }
                }
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static void HandleReplayDebugInput(BattleSessionFeature f)
            {
                if (f._replay == null) return;

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P))
                {
                    if (f._replay.IsPlaying) f._replay.Pause();
                    else f._replay.Play();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
                {
                    f._replay.SeekToStart();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus))
                {
                    var target = Math.Max(0, f._lastFrame + ReplaySeekChunkFrames);
                    SeekReplayToFrame(f, target);
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus))
                {
                    var target = Math.Max(0, f._lastFrame - ReplaySeekChunkFrames);
                    SeekReplayToFrame(f, target);
                }
            }

            private static void SeekReplayToFrame(BattleSessionFeature f, int targetFrame)
            {
                if (!f._plan.EnableInputReplay) return;
                if (targetFrame < 0) targetFrame = 0;

                var fixedDelta = f.GetFixedDeltaSeconds();

                if (f._session != null && f._replay != null && targetFrame > f._lastFrame)
                {
                    f._tickAcc = 0f;

                    for (int frame = f._lastFrame + 1; frame <= targetFrame; frame++)
                    {
                        f._replay.Pump(f._session, frame);
                        f._session.Tick(fixedDelta);
                    }

                    f._lastFrame = targetFrame;
                    if (f._ctx != null) f._ctx.LastFrame = f._lastFrame;
                    return;
                }

                if (f._session != null && f._session.RollbackModule != null && targetFrame <= f._lastFrame)
                {
                    var worldId = new WorldId(f._plan.WorldId);
                    var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                    for (int frame = targetFrame; frame >= probeStart; frame--)
                    {
                        if (f._session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(frame), new FrameIndex(targetFrame), fixedDelta))
                        {
                            f._lastFrame = targetFrame;
                            if (f._ctx != null) f._ctx.LastFrame = f._lastFrame;
                            return;
                        }
                    }
                }

                f.StopSession();
                f.StartSession();
                f.ApplyAutoPlanActions();

                if (f._replay == null) return;
                f._replay.SeekToStart();

                f._tickAcc = 0f;

                for (int frame = 1; frame <= targetFrame; frame++)
                {
                    f._replay.Pump(f._session, frame);
                    f._session.Tick(fixedDelta);
                }
            }
#endif
        }
    }
}

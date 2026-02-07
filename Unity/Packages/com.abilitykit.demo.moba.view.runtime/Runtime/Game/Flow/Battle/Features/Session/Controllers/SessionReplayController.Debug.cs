using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed partial class SessionReplayController
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static void HandleReplayDebugInput(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, ISessionReplayHost host)
            {
                if (state == null || handles == null || ctx == null || host == null) return;

                var replay = handles.Replay.Driver;
                if (replay == null) return;

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P))
                {
                    if (replay.IsPlaying) replay.Pause();
                    else replay.Play();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
                {
                    replay.SeekToStart();
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus))
                {
                    var target = Math.Max(0, state.Tick.LastFrame + ReplaySeekChunkFrames);
                    SeekReplayToFrame(plan, state, handles, ctx, host, target);
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus))
                {
                    var target = Math.Max(0, state.Tick.LastFrame - ReplaySeekChunkFrames);
                    SeekReplayToFrame(plan, state, handles, ctx, host, target);
                }
            }

            private static void SeekReplayToFrame(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, ISessionReplayHost host, int targetFrame)
            {
                if (state == null || handles == null || ctx == null || host == null) return;

                if (!plan.EnableInputReplay) return;
                if (targetFrame < 0) targetFrame = 0;

                var fixedDelta = host.GetFixedDeltaSeconds();

                var session = handles.Session;
                var replay = handles.Replay.Driver;

                if (session != null && replay != null && targetFrame > state.Tick.LastFrame)
                {
                    state.Tick.TickAcc = 0f;

                    for (int frame = state.Tick.LastFrame + 1; frame <= targetFrame; frame++)
                    {
                        replay.Pump(session, frame);
                        session.Tick(fixedDelta);
                    }

                    state.Tick.LastFrame = targetFrame;
                    ctx.LastFrame = state.Tick.LastFrame;
                    return;
                }

                if (session != null && session.RollbackModule != null && targetFrame <= state.Tick.LastFrame)
                {
                    var worldId = new WorldId(plan.WorldId);
                    var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                    for (int frame = targetFrame; frame >= probeStart; frame--)
                    {
                        if (session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(frame), new FrameIndex(targetFrame), fixedDelta))
                        {
                            state.Tick.LastFrame = targetFrame;
                            ctx.LastFrame = state.Tick.LastFrame;
                            return;
                        }
                    }
                }

                host.StopSession();
                host.StartSession();
                host.ApplyAutoPlanActions();

                session = handles.Session;
                replay = handles.Replay.Driver;
                if (session == null || replay == null) return;

                replay.SeekToStart();

                state.Tick.TickAcc = 0f;

                for (int frame = 1; frame <= targetFrame; frame++)
                {
                    replay.Pump(session, frame);
                    session.Tick(fixedDelta);
                }

                state.Tick.LastFrame = targetFrame;
                ctx.LastFrame = state.Tick.LastFrame;
            }
#endif
        }
    }
}

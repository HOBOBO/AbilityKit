using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class SessionReplayController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandleReplayDebugInput(BattleStartPlan plan, BattleSessionState state, BattleSessionHandles handles, BattleContext ctx, ISessionReplayHost host)
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
                SeekToFrame(plan, state, handles, ctx, host, 0);
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus))
            {
                var target = Math.Max(0, state.Tick.LastFrame + ReplaySeekChunkFrames);
                SeekToFrame(plan, state, handles, ctx, host, target);
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus))
            {
                var target = Math.Max(0, state.Tick.LastFrame - ReplaySeekChunkFrames);
                SeekToFrame(plan, state, handles, ctx, host, target);
            }
        }

#endif
    }
}

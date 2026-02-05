using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private const int ReplaySeekChunkFrames = 300;
        private const int RollbackSeekProbeFrames = 120;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandleReplayDebugInput()
        {
            if (_replay == null) return;

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P))
            {
                if (_replay.IsPlaying) _replay.Pause();
                else _replay.Play();
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
            {
                _replay.SeekToStart();
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus))
            {
                var target = Math.Max(0, _lastFrame + ReplaySeekChunkFrames);
                SeekReplayToFrame(target);
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus))
            {
                var target = Math.Max(0, _lastFrame - ReplaySeekChunkFrames);
                SeekReplayToFrame(target);
            }
        }
#endif

        private void SeekReplayToFrame(int targetFrame)
        {
            if (!_plan.EnableInputReplay) return;
            if (targetFrame < 0) targetFrame = 0;

            var fixedDelta = GetFixedDeltaSeconds();

            // Fast path: seek forward by fast-forwarding within the same session.
            if (_session != null && _replay != null && targetFrame > _lastFrame)
            {
                _tickAcc = 0f;

                for (int f = _lastFrame + 1; f <= targetFrame; f++)
                {
                    _replay.Pump(_session, f);
                    _session.Tick(fixedDelta);
                }

                _lastFrame = targetFrame;
                if (_ctx != null) _ctx.LastFrame = _lastFrame;
                return;
            }

            // Fast path: seek backward within rollback history without restarting.
            if (_session != null && _session.RollbackModule != null && targetFrame <= _lastFrame)
            {
                var worldId = new WorldId(_plan.WorldId);
                var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                for (int f = targetFrame; f >= probeStart; f--)
                {
                    if (_session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(f), new FrameIndex(targetFrame), fixedDelta))
                    {
                        _lastFrame = targetFrame;
                        if (_ctx != null) _ctx.LastFrame = _lastFrame;
                        return;
                    }
                }
            }

            StopSession();
            StartSession();
            ApplyAutoPlanActions();

            if (_replay == null) return;
            _replay.SeekToStart();

            _tickAcc = 0f;

            for (int f = 1; f <= targetFrame; f++)
            {
                _replay.Pump(_session, f);
                _session.Tick(fixedDelta);
            }
        }
    }
}

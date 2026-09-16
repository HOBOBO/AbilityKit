using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System;

namespace AbilityKit.Game.Flow
{
    public sealed class MonoCharacterHfsmView : MonoBehaviour, IFrameSeekableView, ICharacterPresentationSink
    {
        public event Action<CharacterPlaybackIntent> PlaybackRequested;
        public event Action<CharacterLogIntent> LogRequested;
        private BattleCharacterHfsmComponent _state;
        private Animator _animator;
        private float _secondsPerFrame;

        public void Bind(BattleCharacterHfsmComponent state, int tickRate)
        {
            _state = state;
            _secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (state != null) SeekToFrame(state.SnapshotFrame, _secondsPerFrame);
        }

        public void SeekToFrame(int frameIndex, float secondsPerFrame)
        {
            if (_state == null || frameIndex < _state.SnapshotFrame) return;
            var duration = secondsPerFrame > 0f ? secondsPerFrame : _secondsPerFrame;
            var tickRate = duration > 0f ? Mathf.RoundToInt(1f / duration) : 0;
            if (tickRate <= 0) return;
            var playback = _state.Evaluate(frameIndex, tickRate, this);
            if (!playback.HasValue || _animator == null ||
                string.IsNullOrEmpty(playback.Value.AnimatorState)) return;

            var intent = playback.Value;
            var hash = Animator.StringToHash(intent.AnimatorState);
            if (intent.Layer >= _animator.layerCount || !_animator.HasState(intent.Layer, hash)) return;
            var localFrame = intent.LocalFrame;
            if (intent.FrameCount > 0)
                localFrame = intent.Loop ? localFrame % intent.FrameCount :
                    Mathf.Min(localFrame, intent.FrameCount - 1);
            _animator.Play(hash, intent.Layer, 0f);
            _animator.Update(0f);
            var length = _animator.GetCurrentAnimatorStateInfo(intent.Layer).length;
            var normalized = length > Mathf.Epsilon && duration > 0f
                ? Mathf.Max(0, localFrame) * duration / length
                : 0f;
            if (!intent.Loop) normalized = Mathf.Clamp01(normalized);
            _animator.Play(hash, intent.Layer, normalized);
            _animator.Update(0f);
        }

        public void OnPlay(in CharacterPlaybackIntent intent)
        {
            PlaybackRequested?.Invoke(intent);
        }

        public void OnLog(in CharacterLogIntent intent)
        {
            LogRequested?.Invoke(intent);
            Debug.Log("[CharacterAction] " + intent.Message + " actorAction=" + intent.InstanceId);
        }
    }
}

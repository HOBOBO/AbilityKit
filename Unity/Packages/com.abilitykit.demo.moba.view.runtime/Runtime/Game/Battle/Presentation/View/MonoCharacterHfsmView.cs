using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System;
using System.Collections.Generic;
using AbilityKit.ActionSchema;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;

namespace AbilityKit.Game.Flow
{
    public sealed class MonoCharacterHfsmView : MonoBehaviour, IFrameSeekableView, ICharacterPresentationSink,
        IMobaSkillPresentationTimelineSink
    {
        private const int TimelineSkillId = 10020101;
        private const string TimelineResource = "moba/action_timeline/skill_10020101.moba.presentation";
        public event Action<CharacterPlaybackIntent> PlaybackRequested;
        public event Action<CharacterLogIntent> LogRequested;
        private BattleCharacterHfsmComponent _state;
        private Animator _animator;
        private float _secondsPerFrame;
        private MobaActorRegistry _registry;
        private int _actorId;
        private MobaSkillPresentationCastDriver _presentation;
        private readonly List<(GameObject Object, float EndSeconds)> _particles =
            new List<(GameObject, float)>();

        public void Bind(BattleCharacterHfsmComponent state, int tickRate)
        {
            Bind(state, tickRate, null, 0);
        }

        public void Bind(BattleCharacterHfsmComponent state, int tickRate,
            MobaActorRegistry registry, int actorId)
        {
            if (_registry != registry || _actorId != actorId || _state != state)
                StopPresentation();
            _registry = registry;
            _actorId = actorId;
            _state = state;
            _secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (state != null) SeekToFrame(state.SnapshotFrame, _secondsPerFrame);
        }

        public void SeekToFrame(int frameIndex, float secondsPerFrame)
        {
            if (_state == null) return;
            if (_registry != null && _registry.TryGet(_actorId, out var actor) &&
                actor.hasCharacterHfsm && actor.characterHfsm.Runtime != null)
            {
                var snapshot = actor.characterHfsm.Runtime.CaptureSnapshot();
                if (snapshot.Machine.Frame <= frameIndex) _state.ApplySnapshot(snapshot);
            }
            if (frameIndex < _state.SnapshotFrame)
            {
                StopPresentation();
                return;
            }
            var duration = secondsPerFrame > 0f ? secondsPerFrame : _secondsPerFrame;
            var tickRate = duration > 0f ? Mathf.RoundToInt(1f / duration) : 0;
            if (tickRate <= 0) return;
            SeekPresentation(frameIndex, duration);
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

        private void SeekPresentation(int frame, float secondsPerFrame)
        {
            var action = _state.Action;
            var casting = action.SkillId == TimelineSkillId && action.CastInstanceId > 0;
            if (!casting)
            {
                StopPresentation();
                return;
            }
            if (_presentation == null)
            {
                var asset = Resources.Load<TextAsset>(TimelineResource);
                if (asset == null)
                {
                    Debug.LogError("Missing MOBA presentation timeline: " + TimelineResource);
                    return;
                }
                _presentation = new MobaSkillPresentationCastDriver(asset.text, this);
            }
            var elapsed = Mathf.Max(0, frame - action.StartFrame) * secondsPerFrame;
            _presentation.Seek(action.CastInstanceId, elapsed);
            for (var i = _particles.Count - 1; i >= 0; i--)
            {
                if (elapsed < _particles[i].EndSeconds) continue;
                if (_particles[i].Object != null) Destroy(_particles[i].Object);
                _particles.RemoveAt(i);
            }
        }

        private void StopPresentation()
        {
            _presentation?.Stop();
            _presentation = null;
            for (var i = _particles.Count - 1; i >= 0; i--)
                if (_particles[i].Object != null) Destroy(_particles[i].Object);
            _particles.Clear();
        }

        public void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds)
        {
            if (clip.type != "AbilityKit.ActionEditorImpl.PlayParticle" || group.actorId != 0)
            {
                Debug.LogError("Unsupported XiaoQiao presentation clip or actor binding: " + clip.type);
                return;
            }
            var key = clip.args["resourceKey"];
            var prefab = Resources.Load<GameObject>(key);
            if (prefab == null)
            {
                Debug.LogError("Missing MOBA presentation prefab: " + key);
                return;
            }
            var instance = Instantiate(prefab, transform);
            instance.transform.localPosition = new Vector3(0f, 1.1f, 0.55f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale *= 0.55f;
            _particles.Add((instance, clip.start + clip.length));
        }

        public void OnTimelineStop(long skillInstanceId)
        {
            for (var i = _particles.Count - 1; i >= 0; i--)
                if (_particles[i].Object != null) Destroy(_particles[i].Object);
            _particles.Clear();
        }

        private void OnDisable() => StopPresentation();
    }
}

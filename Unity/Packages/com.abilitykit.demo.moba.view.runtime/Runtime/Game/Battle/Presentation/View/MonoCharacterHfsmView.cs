using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.ActionSchema;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;
using AbilityKit.Game.Platform.Unity.Presentation;

namespace AbilityKit.Game.Flow
{
    public sealed class MonoCharacterHfsmView : MonoBehaviour, IFrameSeekableView, ICharacterPresentationSink,
        IMobaSkillPresentationTimelineSink, IMobaSkillPresentationAsyncRequestSink,
        IMobaSkillPresentationCancellableAsyncRequestSink
    {
        private const string TimelineResourcePrefix = "moba/action_timeline/skill_";
        private const string TimelineResourceSuffix = ".moba.presentation";
        public event Action<CharacterPlaybackIntent> PlaybackRequested;
        public event Action<CharacterLogIntent> LogRequested;
        private BattleCharacterHfsmComponent _state;
        private Animator _animator;
        private float _secondsPerFrame;
        private MobaSkillPresentationCastDriver _presentation;
        private int _presentationSkillId;
        private PresentationUnityBackend _presentationBackend;
        private PresentationSession<PresentationUnityHandle> _presentationSession;
        private Task _presentationClearTask = Task.CompletedTask;
        private MobaPresentationTemplateResolver _presentationTemplateResolver;
        private readonly PresentationRateResolver _presentationRateResolver = new PresentationRateResolver();
        private readonly Dictionary<long, HashSet<PresentationHandle<PresentationUnityHandle>>> _timelineHandles =
            new Dictionary<long, HashSet<PresentationHandle<PresentationUnityHandle>>>();

        public PresentationRateResolver PresentationRateResolver => _presentationRateResolver;

        public void SetPresentationRate(in PresentationRateSource source)
        {
            _presentationRateResolver.Set(in source);
            if (_animator != null) _animator.speed = _presentationRateResolver.ResolveRate(PresentationRateChannel.Action);
        }

        public bool RemovePresentationRate(string sourceId)
        {
            var removed = _presentationRateResolver.Remove(sourceId);
            if (removed && _animator != null)
                _animator.speed = _presentationRateResolver.ResolveRate(PresentationRateChannel.Action);
            return removed;
        }

        public void Bind(BattleCharacterHfsmComponent state, int tickRate)
        {
            if (_state != state)
                StopPresentation();
            _state = state;
            EnsurePresentationBackend();
            _secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            // A snapshot can update the existing component instance. Re-seek
            // on every bind so action changes received after the initial spawn
            // also reach the Animator for characters and summons alike.
            if (state != null) SeekToFrame(state.SnapshotFrame, _secondsPerFrame);
        }

        public void SeekToFrame(int frameIndex, float secondsPerFrame)
        {
            if (_state == null) return;
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
            _animator.speed = _presentationRateResolver.ResolveRate(PresentationRateChannel.Action);
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
            var casting = action.SkillId > 0 && action.CastInstanceId > 0;
            if (!casting)
            {
                StopPresentation();
                return;
            }
            if (_presentationSkillId != action.SkillId)
            {
                _presentation?.Stop();
                _presentation = null;
                var skillId = action.SkillId;
                _presentationSkillId = skillId;
                var resource = TimelineResourcePrefix + skillId + TimelineResourceSuffix;
                var asset = Resources.Load<TextAsset>(resource);
                if (asset == null)
                {
                    Debug.LogError("Missing MOBA presentation timeline: " + resource);
                    return;
                }
                _presentationTemplateResolver ??= new MobaPresentationTemplateResolver();
                _presentation = new MobaSkillPresentationCastDriver(
                    asset.text, this, _presentationTemplateResolver, _presentationRateResolver);
            }
            if (_presentation == null) return;
            var elapsed = Mathf.Max(0, frame - action.StartFrame) * secondsPerFrame;
            _presentation.SeekWithRate(action.CastInstanceId, elapsed);
        }

        private void StopPresentation()
        {
            // Keep one backend/session for the lifetime of this view.  The
            // backend increments its generation before clearing entries, so
            // requests that were still loading for the old timeline are
            // rejected while requests for the next cast can use the same
            // session safely.  Replacing the backend here creates a window in
            // which an async timeline dispatch can observe a null session.
            _presentation?.Stop();
            _presentation = null;
            _presentationSkillId = 0;
            StopAllTimelineHandles();
            if (_presentationBackend != null)
                _presentationClearTask = QueuePresentationClearAsync(_presentationClearTask);
        }

        public void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds)
        {
            var request = CreatePresentationRequest(group, clip, offsetSeconds);
            if (request.IsValid) _ = PlayPresentationAsync(request, CancellationToken.None);
        }

        public async Task OnPresentationRequestAsync(
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds)
        {
            await PlayPresentationAsync(request, CancellationToken.None);
        }

        public Task OnPresentationRequestAsync(
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds,
            CancellationToken cancellationToken)
        {
            return PlayPresentationAsync(request, cancellationToken);
        }

        public void OnTimelineStop(long skillInstanceId)
        {
            StopTimelineHandles(skillInstanceId);
        }

        private async Task PlayPresentationAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            if (!request.IsValid || !this || !isActiveAndEnabled)
            {
                if (!request.IsValid)
                    Debug.LogWarning("[MobaPresentation] Ignored invalid timeline request key=" + request.LogicalKey +
                        " asset=" + request.AssetId + " resource=" + request.ResourceKey);
                return;
            }
            // A timeline transition can dispatch its next clip in the same
            // frame that the previous cast is being cleared.  Serialize the
            // backend reset so the new request cannot race the generation
            // increment/entry cleanup of the old cast.
            var clearTask = _presentationClearTask;
            if (clearTask != null && !clearTask.IsCompleted)
                await clearTask;
            EnsurePresentationBackend();
            var session = _presentationSession;
            if (session == null)
            {
                Debug.LogWarning("[MobaPresentation] Timeline session unavailable key=" + request.LogicalKey);
                return;
            }
            try
            {
                var handle = await session.EmitTransientAsync(request, cancellationToken);
                if (handle == null)
                {
                    Debug.LogWarning("[MobaPresentation] Timeline request produced no handle key=" + request.LogicalKey +
                        " asset=" + request.AssetId + " resource=" + request.ResourceKey +
                        " cancelled=" + cancellationToken.IsCancellationRequested);
                    return;
                }
                var skillInstanceId = ParseSkillInstanceId(request.LogicalKey);
                var stopImmediately = false;
                lock (_timelineHandles)
                {
                    if (skillInstanceId <= 0 || cancellationToken.IsCancellationRequested)
                    {
                        stopImmediately = true;
                    }
                    else
                    {
                        if (!_timelineHandles.TryGetValue(skillInstanceId, out var handles))
                        {
                            handles = new HashSet<PresentationHandle<PresentationUnityHandle>>();
                            _timelineHandles[skillInstanceId] = handles;
                        }
                        handles.Add(handle);
                    }
                }
                if (stopImmediately) await handle.StopAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private PresentationRequest CreatePresentationRequest(GroupDto group, ClipDto clip, float offsetSeconds)
        {
            if (clip == null || group == null || group.actorId != 0 || clip.args == null) return default;
            var isAnimation = clip.type == "AbilityKit.ActionEditorImpl.PlayAnimation";
            var isParticle = clip.type == "AbilityKit.ActionEditorImpl.PlayParticle";
            if (!isAnimation && !isParticle) return default;
            var keyName = isAnimation ? "clipKey" : "resourceKey";
            if (!clip.args.TryGetValue(keyName, out var resource) || string.IsNullOrWhiteSpace(resource)) return default;
            return new PresentationRequest(
                "timeline:" + _state?.Action.CastInstanceId + ":legacy:" + clip.start + ":" + clip.type,
                assetId: 0,
                position: new PresentationVector3(0f, 1.1f, 0.55f),
                attachment: new PresentationAttachment(PresentationAttachmentKind.Actor, 0),
                durationMs: clip.length > 0 ? Mathf.Max(1, Mathf.RoundToInt(clip.length * 1000f)) : 0,
                scale: 0.55f,
                parameters: new[] { new PresentationParameter("offsetSeconds", offsetSeconds) },
                resourceKey: isParticle ? resource : string.Empty,
                assetKind: isParticle ? PresentationAssetKind.Particle : PresentationAssetKind.Animation,
                playback: new PresentationPlaybackSpec(clipKey: isAnimation ? resource : null));
        }

        private static long ParseSkillInstanceId(string logicalKey)
        {
            if (string.IsNullOrEmpty(logicalKey) || !logicalKey.StartsWith("timeline:", StringComparison.Ordinal))
                return 0;
            var end = logicalKey.IndexOf(':', "timeline:".Length);
            return end < 0 || !long.TryParse(logicalKey.Substring("timeline:".Length,
                end - "timeline:".Length), out var id) ? 0 : id;
        }

        private void StopTimelineHandles(long skillInstanceId)
        {
            PresentationHandle<PresentationUnityHandle>[] handles;
            lock (_timelineHandles)
            {
                if (!_timelineHandles.TryGetValue(skillInstanceId, out var owned)) return;
                _timelineHandles.Remove(skillInstanceId);
                handles = new PresentationHandle<PresentationUnityHandle>[owned.Count];
                owned.CopyTo(handles);
            }
            for (var i = 0; i < handles.Length; i++)
                if (handles[i] != null) _ = handles[i].StopAsync();
        }

        private void StopAllTimelineHandles()
        {
            long[] instances;
            lock (_timelineHandles)
            {
                instances = new long[_timelineHandles.Count];
                _timelineHandles.Keys.CopyTo(instances, 0);
            }
            for (var i = 0; i < instances.Length; i++) StopTimelineHandles(instances[i]);
        }

        private void EnsurePresentationBackend()
        {
            if (_presentationBackend != null)
            {
                if (_presentationSession == null)
                    _presentationSession = new PresentationSession<PresentationUnityHandle>(_presentationBackend);
                return;
            }
            _presentationBackend = new PresentationUnityBackend(transform, rateResolver: _presentationRateResolver);
            _presentationSession = new PresentationSession<PresentationUnityHandle>(_presentationBackend);
        }

        private async Task QueuePresentationClearAsync(Task previous)
        {
            if (previous != null && !previous.IsCompleted)
            {
                try { await previous; }
                catch { }
            }
            if (_presentationBackend != null)
            {
                try { await _presentationBackend.ClearAsync(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void Update()
        {
            _presentationBackend?.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            StopPresentation();
        }
    }
}

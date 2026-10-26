using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System;
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
        private const int TimelineSkillId = 10020101;
        private const string TimelineResource = "moba/action_timeline/skill_10020101.moba.presentation";
        public event Action<CharacterPlaybackIntent> PlaybackRequested;
        public event Action<CharacterLogIntent> LogRequested;
        private BattleCharacterHfsmComponent _state;
        private Animator _animator;
        private float _secondsPerFrame;
        private MobaSkillPresentationCastDriver _presentation;
        private CancellationTokenSource _presentationCancellation;
        private PresentationUnityBackend _presentationBackend;
        private PresentationSession<PresentationUnityHandle> _presentationSession;

        public void Bind(BattleCharacterHfsmComponent state, int tickRate)
        {
            if (_state != state)
                StopPresentation();
            _state = state;
            EnsurePresentationBackend();
            _secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
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
            if (_presentationCancellation == null)
                _presentationCancellation = new CancellationTokenSource();
            var elapsed = Mathf.Max(0, frame - action.StartFrame) * secondsPerFrame;
            _presentation.Seek(action.CastInstanceId, elapsed);
        }

        private void StopPresentation()
        {
            CancelPendingPresentationLoads();
            _presentation?.Stop();
            _presentation = null;
            if (_presentationBackend != null) _ = _presentationBackend.ClearAsync();
            _presentationSession = null;
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
            await PlayPresentationAsync(request, _presentationCancellation?.Token ?? CancellationToken.None);
        }

        public Task OnPresentationRequestAsync(
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return PlayPresentationAsync(request, cancellationToken);
        }

        public void OnTimelineStop(long skillInstanceId)
        {
            CancelPendingPresentationLoads();
            if (_presentationBackend != null) _ = _presentationBackend.ClearAsync();
        }

        private async Task PlayPresentationAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            if (!request.IsValid || !this || !isActiveAndEnabled) return;
            EnsurePresentationBackend();
            var session = _presentationSession;
            if (session == null) return;
            try
            {
                await session.EmitTransientAsync(request, cancellationToken);
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
                "timeline:" + _state?.Action.CastInstanceId + ":" + clip.start,
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

        private void EnsurePresentationBackend()
        {
            if (_presentationBackend != null) return;
            _presentationBackend = new PresentationUnityBackend(transform);
            _presentationSession = new PresentationSession<PresentationUnityHandle>(_presentationBackend);
        }

        private void Update()
        {
            _presentationBackend?.Tick(Time.deltaTime);
        }

        private void CancelPendingPresentationLoads()
        {
            _presentationCancellation?.Cancel();
            _presentationCancellation?.Dispose();
            _presentationCancellation = null;
        }

        private void OnDisable() => StopPresentation();
    }
}

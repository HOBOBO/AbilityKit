using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using UnityEngine;
using UnityEngine.Playables;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    /// <summary>
    /// Unity implementation of the generic presentation backend. Resource loading,
    /// attachment and source-specific playback live here so timelines stay engine agnostic.
    /// </summary>
    public sealed class PresentationUnityHandle
    {
        internal PresentationUnityHandle(GameObject visual) { Visual = visual; }
        internal GameObject Visual { get; }
    }

    public sealed class PresentationUnityBackend : IPresentationBackend<PresentationUnityHandle>
    {
        private sealed class Entry
        {
            public GameObject Instance;
            public Vector3 BaseLocalScale;
            public PresentationRequest Request;
            public PresentationPlaybackLifecycle Lifecycle;
            public string PolicyKey;
            public bool OwnsInstance;
        }

        private readonly Transform _defaultParent;
        private readonly IPresentationAttachmentResolver<Transform> _attachments;
        private readonly PresentationSourceAdapterRegistry<GameObject> _adapters;
        private readonly Dictionary<string, Task<GameObject>> _loads =
            new Dictionary<string, Task<GameObject>>(StringComparer.Ordinal);
        private readonly PresentationEffectPolicyLedger<string> _policies =
            new PresentationEffectPolicyLedger<string>(StringComparer.Ordinal);
        private readonly Dictionary<PresentationUnityHandle, Entry> _entries =
            new Dictionary<PresentationUnityHandle, Entry>();

        public PresentationUnityBackend(
            Transform defaultParent,
            IPresentationAttachmentResolver<Transform> attachments = null,
            PresentationSourceAdapterRegistry<GameObject> adapters = null)
        {
            _defaultParent = defaultParent;
            _attachments = attachments;
            _adapters = adapters ?? CreateDefaultAdapters();
        }

        public bool IsAlive(PresentationUnityHandle handle) => handle != null && _entries.ContainsKey(handle);

        public Task<PresentationHandle<PresentationUnityHandle>> PlayAsync(
            in PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            return PlayCoreAsync(request, cancellationToken);
        }

        private async Task<PresentationHandle<PresentationUnityHandle>> PlayCoreAsync(
            PresentationRequest request,
            CancellationToken cancellationToken)
        {
            if (!request.IsValid) return null;
            cancellationToken.ThrowIfCancellationRequested();

            var policyGroup = string.IsNullOrEmpty(request.Policy.GroupKey)
                ? request.LogicalKey
                : request.Policy.GroupKey;
            var policyKey = policyGroup + ":owner:" + request.Attachment.ActorId;
            var nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            var policy = request.Policy;
            if (!_policies.TryAcquire(policyKey, in policy, nowMs)) return null;

            GameObject instance = null;
            try
            {
                if (request.AssetKind == PresentationAssetKind.Animation)
                    instance = _defaultParent != null ? _defaultParent.gameObject : null;
                else
                {
                    if (request.AssetKind == PresentationAssetKind.Image)
                        throw new NotSupportedException("Image presentation requires a Sprite backend.");
                    var prefab = await LoadPrefabAsync(request.ResourceKey, cancellationToken);
                    instance = UnityEngine.Object.Instantiate(prefab);
                }

                if (instance == null) throw new InvalidOperationException("Unable to create presentation instance.");
                var baseLocalScale = instance.transform.localScale;
                if (request.AssetKind != PresentationAssetKind.Animation)
                    ApplyTransform(instance, in request, in baseLocalScale);
                if (request.Playback.DelayMs <= 0 && request.Playback.PlayOnReady)
                    ApplySources(instance, in request);

                var duration = request.DurationMs > 0 ? request.DurationMs / 1000f : 0f;
                var entry = new Entry
                {
                    Instance = instance,
                    BaseLocalScale = baseLocalScale,
                    Request = request,
                    Lifecycle = new PresentationPlaybackLifecycle(request.Playback, duration),
                    PolicyKey = policyKey,
                    OwnsInstance = request.AssetKind != PresentationAssetKind.Animation || instance != (_defaultParent != null ? _defaultParent.gameObject : null),
                };
                var handle = new PresentationUnityHandle(instance);
                _entries[handle] = entry;
                return new PresentationHandle<PresentationUnityHandle>(handle, Task.CompletedTask, StopEntryAsync);
            }
            catch
            {
                if (instance != null && instance != (_defaultParent != null ? _defaultParent.gameObject : null))
                    UnityEngine.Object.Destroy(instance);
                _policies.Release(policyKey);
                throw;
            }
        }

        public void Update(PresentationUnityHandle handle, in PresentationRequest request)
        {
            if (!IsAlive(handle)) return;
            var entry = _entries[handle];
            entry.Request = request;
            if (request.AssetKind != PresentationAssetKind.Animation)
                ApplyTransform(entry.Instance, in request, in entry.BaseLocalScale);
            _adapters.TryApply(entry.Instance, in request);
        }

        public Task StopAsync(PresentationUnityHandle handle) => StopEntryAsync(handle);

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds)) return;
            _policies.Prune(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            var completed = new List<PresentationUnityHandle>();
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                var before = entry.Lifecycle.Snapshot.Phase;
                if (entry.Lifecycle.Tick(deltaSeconds)) completed.Add(pair.Key);
                else if (entry.Request.Playback.PlayOnReady &&
                         entry.Lifecycle.Snapshot.Phase == PresentationPlaybackPhase.Playing &&
                         before != PresentationPlaybackPhase.Playing)
                {
                    var request = entry.Request;
                    ApplySources(entry.Instance, in request);
                }
            }

            for (var i = 0; i < completed.Count; i++) _ = StopEntryAsync(completed[i]);
        }

        public async Task ClearAsync()
        {
            var handles = new List<PresentationUnityHandle>(_entries.Keys);
            for (var i = 0; i < handles.Count; i++) await StopEntryAsync(handles[i]).ConfigureAwait(false);
            _policies.Clear();
        }

        private async Task StopEntryAsync(PresentationUnityHandle handle)
        {
            if (handle == null || !_entries.TryGetValue(handle, out var entry)) return;
            _entries.Remove(handle);
            entry.Lifecycle.Stop();
            var sharedVisual = false;
            foreach (var other in _entries.Values)
            {
                if (ReferenceEquals(other.Instance, entry.Instance)) { sharedVisual = true; break; }
            }
            if (!sharedVisual) _adapters.Stop(entry.Instance, entry.Request.AssetKind);
            _policies.Release(entry.PolicyKey);
            if (entry.OwnsInstance && entry.Instance != null) UnityEngine.Object.Destroy(entry.Instance);
            await Task.CompletedTask;
        }

        private async Task<GameObject> LoadPrefabAsync(string resourceKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(resourceKey))
                throw new InvalidOperationException("Presentation resource key is empty.");
            if (!_loads.TryGetValue(resourceKey, out var pending))
            {
                pending = LoadPrefabCoreAsync(resourceKey);
                _loads[resourceKey] = pending;
            }

            cancellationToken.ThrowIfCancellationRequested();
            GameObject prefab;
            try
            {
                prefab = await pending;
            }
            catch
            {
                if (_loads.TryGetValue(resourceKey, out var current) && ReferenceEquals(current, pending))
                    _loads.Remove(resourceKey);
                throw;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (prefab == null) throw new InvalidOperationException("Presentation prefab not found: " + resourceKey);
            return prefab;
        }

        private static async Task<GameObject> LoadPrefabCoreAsync(string resourceKey)
        {
            var load = Resources.LoadAsync<GameObject>(resourceKey);
            while (!load.isDone) await Task.Yield();
            return load.asset as GameObject;
        }

        private void ApplyTransform(GameObject instance, in PresentationRequest request, in Vector3 baseLocalScale)
        {
            var target = _defaultParent;
            if (_attachments != null && request.Attachment.IsAttached &&
                _attachments.TryResolve(request.Attachment,
                    out var resolved, out var position, out var rotation))
            {
                target = resolved;
                instance.transform.position = new Vector3(position.X, position.Y, position.Z);
                instance.transform.rotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
            }
            else if (request.Attachment.Kind == PresentationAttachmentKind.Actor && request.Attachment.ActorId == 0)
            {
                if (target != null && instance.transform.parent != target)
                    instance.transform.SetParent(target, false);
                instance.transform.localPosition = new Vector3(request.Position.X, request.Position.Y, request.Position.Z);
                instance.transform.localRotation = new Quaternion(
                    request.Rotation.X, request.Rotation.Y, request.Rotation.Z, request.Rotation.W);
            }
            else
            {
                instance.transform.position = new Vector3(request.Position.X, request.Position.Y, request.Position.Z);
                instance.transform.rotation = new Quaternion(request.Rotation.X, request.Rotation.Y, request.Rotation.Z, request.Rotation.W);
            }

            if (target != null && instance.transform.parent != target)
                instance.transform.SetParent(target, true);
            instance.transform.localScale = baseLocalScale * request.Scale;
        }

        private void ApplySources(GameObject instance, in PresentationRequest request)
        {
            if (!_adapters.TryApply(instance, in request))
                ApplyDefaultPlayback(instance, in request);
        }

        private static void ApplyDefaultPlayback(GameObject instance, in PresentationRequest request)
        {
            if (!request.Playback.PlayOnReady) instance.SetActive(false);
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                if (request.Playback.PlayOnReady) particles[i].Play(true);
                else particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static PresentationSourceAdapterRegistry<GameObject> CreateDefaultAdapters()
        {
            var registry = new PresentationSourceAdapterRegistry<GameObject>();
            registry.Register(new UnityParticleSourceAdapter());
            registry.Register(new UnityAnimatorSourceAdapter());
            registry.Register(new UnityTimelineSourceAdapter());
            return registry;
        }
    }

    public sealed class UnityParticleSourceAdapter : IPresentationSourceAdapter<GameObject>
    {
        public bool CanHandle(PresentationAssetKind kind, GameObject visual) =>
            visual != null && (kind == PresentationAssetKind.Particle ||
                (kind == PresentationAssetKind.Prefab && visual.GetComponentInChildren<ParticleSystem>(true) != null));

        public void Apply(GameObject visual, in PresentationRequest request)
        {
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.simulationSpeed = request.Playback.Speed > 0f ? request.Playback.Speed : 1f;
                if (request.Playback.PlayOnReady) particles[i].Play(true);
            }
        }

        public void Stop(GameObject visual)
        {
            if (visual == null) return;
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public sealed class UnityAnimatorSourceAdapter : IPresentationSourceAdapter<GameObject>
    {
        public bool CanHandle(PresentationAssetKind kind, GameObject visual) =>
            visual != null && (kind == PresentationAssetKind.Animation ||
                (kind == PresentationAssetKind.Prefab && visual.GetComponentInChildren<Animator>(true) != null));

        public void Apply(GameObject visual, in PresentationRequest request)
        {
            if (visual == null || string.IsNullOrEmpty(request.Playback.ClipKey)) return;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            animator.speed = request.Playback.Speed > 0f ? request.Playback.Speed : 1f;
            animator.Play(Animator.StringToHash(request.Playback.ClipKey), request.Playback.TrackIndex, request.Playback.NormalizedTime);
        }

        public void Stop(GameObject visual)
        {
            if (visual == null) return;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.speed = 0f;
        }
    }

    public sealed class UnityTimelineSourceAdapter : IPresentationSourceAdapter<GameObject>
    {
        public bool CanHandle(PresentationAssetKind kind, GameObject visual) =>
            visual != null && (kind == PresentationAssetKind.Timeline ||
                (kind == PresentationAssetKind.Prefab && visual.GetComponentInChildren<PlayableDirector>(true) != null));

        public void Apply(GameObject visual, in PresentationRequest request)
        {
            var director = visual != null ? visual.GetComponentInChildren<PlayableDirector>(true) : null;
            if (director != null && request.Playback.PlayOnReady) director.Play();
        }

        public void Stop(GameObject visual)
        {
            var director = visual != null ? visual.GetComponentInChildren<PlayableDirector>(true) : null;
            if (director != null) director.Stop();
        }
    }

    /// <summary>Maps logical battle attachment ids and sockets to Unity transforms.</summary>
    public sealed class UnityPresentationAttachmentResolver : IPresentationAttachmentResolver<Transform>
    {
        private readonly Dictionary<int, Transform> _targets = new Dictionary<int, Transform>();

        public void Register(int actorId, Transform target)
        {
            if (actorId > 0 && target != null) _targets[actorId] = target;
        }

        public void Unregister(int actorId)
        {
            if (actorId > 0) _targets.Remove(actorId);
        }

        public bool TryResolve(
            in PresentationAttachment attachment,
            out Transform target,
            out PresentationVector3 position,
            out PresentationRotation rotation)
        {
            target = null;
            position = PresentationVector3.Zero;
            rotation = PresentationRotation.Identity;
            if (!_targets.TryGetValue(attachment.ActorId, out var root) || root == null) return false;
            target = root;
            if (attachment.Kind == PresentationAttachmentKind.Socket && !string.IsNullOrEmpty(attachment.Socket))
            {
                var socket = root.Find(attachment.Socket);
                if (socket != null) target = socket;
            }

            var world = target.TransformPoint(new Vector3(attachment.Offset.X, attachment.Offset.Y, attachment.Offset.Z));
            position = new PresentationVector3(world.x, world.y, world.z);
            var worldRotation = target.rotation;
            rotation = new PresentationRotation(worldRotation.x, worldRotation.y, worldRotation.z, worldRotation.w);
            return true;
        }
    }
}

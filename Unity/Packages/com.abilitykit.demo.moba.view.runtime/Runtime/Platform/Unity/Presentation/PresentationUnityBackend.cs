using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

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

    /// <summary>
    /// Unity-specific ordering implementation. Callers depend on the abstract ordering
    /// contract; this adapter can be replaced without changing template data or requests.
    /// </summary>
    public sealed class UnityPresentationOrderingAdapter : IPresentationOrderingAdapter<GameObject>
    {
            private sealed class Baseline
            {
                public SortingGroup[] Groups;
                public int[] GroupOrders;
                public Renderer[] Renderers;
                public int[] RendererOrders;
            }

            private readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameObject, Baseline> _baselines =
                new System.Runtime.CompilerServices.ConditionalWeakTable<GameObject, Baseline>();

            public void Apply(GameObject visual, GameObject attachmentTarget, in PresentationOrdering ordering)
            {
                if (visual == null) return;
                var baseline = _baselines.GetValue(visual, Capture);
                if (ordering.Mode == PresentationOrderingMode.ResourceDefault)
                {
                    Restore(baseline);
                    return;
                }

                var baseOrder = FirstOrder(baseline);
                var desiredOrder = ordering.Mode == PresentationOrderingMode.RelativeToAttachment
                    ? ResolveTargetOrder(attachmentTarget) + ordering.Priority
                    : ordering.Priority;
                var delta = desiredOrder - baseOrder;
                for (var i = 0; i < baseline.Groups.Length; i++)
                    if (baseline.Groups[i] != null) baseline.Groups[i].sortingOrder = ClampOrder(baseline.GroupOrders[i] + delta);
                for (var i = 0; i < baseline.Renderers.Length; i++)
                    if (baseline.Renderers[i] != null) baseline.Renderers[i].sortingOrder = ClampOrder(baseline.RendererOrders[i] + delta);
            }

            private static Baseline Capture(GameObject visual)
            {
                var groups = visual.GetComponentsInChildren<SortingGroup>(true);
                var renderers = groups.Length > 0 ? Array.Empty<Renderer>() : visual.GetComponentsInChildren<Renderer>(true);
                var groupOrders = new int[groups.Length];
                var rendererOrders = new int[renderers.Length];
                for (var i = 0; i < groups.Length; i++) groupOrders[i] = groups[i] != null ? groups[i].sortingOrder : 0;
                for (var i = 0; i < renderers.Length; i++) rendererOrders[i] = renderers[i] != null ? renderers[i].sortingOrder : 0;
                return new Baseline { Groups = groups, GroupOrders = groupOrders, Renderers = renderers, RendererOrders = rendererOrders };
            }

            private static int FirstOrder(Baseline baseline)
            {
                if (baseline.Groups.Length > 0) return baseline.GroupOrders[0];
                return baseline.RendererOrders.Length > 0 ? baseline.RendererOrders[0] : 0;
            }

            private static int ResolveTargetOrder(GameObject target)
            {
                if (target == null) return 0;
                var group = target.GetComponentInParent<SortingGroup>();
                if (group != null) return group.sortingOrder;
                var renderer = target.GetComponent<Renderer>() ?? target.GetComponentInChildren<Renderer>(true);
                return renderer != null ? renderer.sortingOrder : 0;
            }

            private static void Restore(Baseline baseline)
            {
                for (var i = 0; i < baseline.Groups.Length; i++)
                    if (baseline.Groups[i] != null) baseline.Groups[i].sortingOrder = baseline.GroupOrders[i];
                for (var i = 0; i < baseline.Renderers.Length; i++)
                    if (baseline.Renderers[i] != null) baseline.Renderers[i].sortingOrder = baseline.RendererOrders[i];
            }

            private static int ClampOrder(int value) => Mathf.Clamp(value, short.MinValue, short.MaxValue);
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
            public PresentationLifetimeState Lifetime = new PresentationLifetimeState();
            public Transform AttachmentTarget;
            public string LogicalKey;
            public int ActorId;
            public Vector3 LastTargetScale = Vector3.one;
            public UnityPresentationInstanceState Inspection;
        }

        private readonly Transform _defaultParent;
        private long _generation;
        private readonly IPresentationAttachmentResolver<Transform> _attachments;
        private readonly PresentationSourceAdapterRegistry<GameObject> _adapters;
        private readonly PresentationRateResolver _rateResolver;
        private readonly PresentationScaleResolver _scaleResolver = new PresentationScaleResolver();
        private readonly IPresentationOrderingAdapter<GameObject> _orderingAdapter;
        private readonly IPresentationLifetimeAdapter<GameObject> _lifetimeAdapter;
        private readonly Dictionary<string, Task<GameObject>> _loads =
            new Dictionary<string, Task<GameObject>>(StringComparer.Ordinal);
        private readonly PresentationEffectPolicyLedger<string> _policies =
            new PresentationEffectPolicyLedger<string>(StringComparer.Ordinal);
        private readonly Dictionary<PresentationUnityHandle, Entry> _entries =
            new Dictionary<PresentationUnityHandle, Entry>();

        public PresentationUnityBackend(
            Transform defaultParent,
            IPresentationAttachmentResolver<Transform> attachments = null,
            PresentationSourceAdapterRegistry<GameObject> adapters = null,
            PresentationRateResolver rateResolver = null,
            IPresentationOrderingAdapter<GameObject> orderingAdapter = null,
            IPresentationLifetimeAdapter<GameObject> lifetimeAdapter = null)
        {
            _defaultParent = defaultParent;
            _attachments = attachments;
            _adapters = adapters ?? CreateDefaultAdapters();
            _rateResolver = rateResolver ?? new PresentationRateResolver();
            _orderingAdapter = orderingAdapter ?? new UnityPresentationOrderingAdapter();
            _lifetimeAdapter = lifetimeAdapter ?? new UnityPresentationLifetimeAdapter();
        }

        public PresentationRateResolver RateResolver => _rateResolver;

        public void SetRate(in PresentationRateSource source)
        {
            _rateResolver.Set(in source);
            RefreshDiagnostics();
        }

        public bool RemoveRate(string sourceId)
        {
            var removed = _rateResolver.Remove(sourceId);
            if (removed) RefreshDiagnostics();
            return removed;
        }

        public PresentationScaleResolver ScaleResolver => _scaleResolver;

        public void SetScale(in PresentationScaleSource source)
        {
            _scaleResolver.Set(in source);
            RefreshScales();
        }

        public bool RemoveScale(string sourceId)
        {
            var removed = _scaleResolver.Remove(sourceId);
            if (removed) RefreshScales();
            return removed;
        }

        public bool IsAlive(PresentationUnityHandle handle) => handle != null &&
            _entries.TryGetValue(handle, out var entry) && entry.Instance != null && !entry.Lifetime.IsFinished;

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
            var generation = _generation;

            var policyGroup = string.IsNullOrEmpty(request.Policy.GroupKey)
                ? request.LogicalKey
                : request.Policy.GroupKey;
            var policyKey = policyGroup + ":owner:" + request.Attachment.ActorId;
            var policy = request.Policy;
            GameObject instance = null;
            var acquired = false;
            try
            {
                if (request.AssetKind == PresentationAssetKind.Animation)
                    instance = _defaultParent != null ? _defaultParent.gameObject : null;
                else
                {
                    if (request.AssetKind == PresentationAssetKind.Image)
                        throw new NotSupportedException("Image presentation requires a Sprite backend.");
                    var prefab = await LoadPrefabAsync(request.ResourceKey, cancellationToken);
                    // A scene/baseline clear invalidates requests that were still loading.
                    if (generation != _generation) return null;
                    if (!ApplyStackPolicy(in request)) return null;
                    if (!_policies.TryAcquire(policyKey, in policy, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)) return null;
                    acquired = true;
                    instance = UnityEngine.Object.Instantiate(prefab);
                }
                if (!acquired)
                {
                    if (!ApplyStackPolicy(in request) || !_policies.TryAcquire(policyKey, in policy, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)) return null;
                    acquired = true;
                }

                if (instance == null) throw new InvalidOperationException("Unable to create presentation instance.");
                var baseLocalScale = instance.transform.localScale;
                if (request.AssetKind != PresentationAssetKind.Animation)
                    ApplyTransform(instance, in request, in baseLocalScale);
                if (request.Playback.DelayMs <= 0 && request.Playback.PlayOnReady)
                    ApplySourcesAndRate(instance, in request);

                var duration = request.DurationMs > 0 ? request.DurationMs / 1000f : 0f;
                var entry = new Entry
                {
                    Instance = instance,
                    BaseLocalScale = baseLocalScale,
                    Request = request,
                    LogicalKey = request.LogicalKey,
                    ActorId = request.Attachment.ActorId,
                    Lifecycle = new PresentationPlaybackLifecycle(request.Playback, duration),
                    PolicyKey = policyKey,
                    OwnsInstance = request.AssetKind != PresentationAssetKind.Animation || instance != (_defaultParent != null ? _defaultParent.gameObject : null),
                };
                var handle = new PresentationUnityHandle(instance);
                if (_attachments != null && request.Attachment.IsAttached)
                    _attachments.TryResolve(request.Attachment, out entry.AttachmentTarget, out _, out _);
                if (entry.AttachmentTarget != null) entry.LastTargetScale = entry.AttachmentTarget.lossyScale;
                if (entry.OwnsInstance)
                {
                    entry.Inspection = instance.GetComponent<UnityPresentationInstanceState>() ?? instance.AddComponent<UnityPresentationInstanceState>();
                    entry.Inspection.Bind("UnityBackend", in request, baseLocalScale, _scaleResolver, _rateResolver, requestScaleInSources: false);
                    entry.Inspection.OrderingAdapter = _orderingAdapter;
                    entry.Inspection.RefreshResolution(GetRateChannel(in request), PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
                    if (entry.AttachmentTarget != null) entry.Inspection.RecordTarget(entry.AttachmentTarget, entry.LastTargetScale);
                }
                _entries[handle] = entry;
                return new PresentationHandle<PresentationUnityHandle>(handle, Task.CompletedTask, StopEntryAsync);
            }
            catch
            {
                if (instance != null && instance != (_defaultParent != null ? _defaultParent.gameObject : null))
                    UnityEngine.Object.Destroy(instance);
                if (acquired) _policies.Release(policyKey);
                throw;
            }
        }

        public void Update(PresentationUnityHandle handle, in PresentationRequest request)
        {
            if (!IsAlive(handle)) return;
            var entry = _entries[handle];
            if (entry.Lifetime.IsStopping) return;
            entry.Request = request;
            if (entry.Inspection != null) entry.Inspection.UpdateRequest(in request);
            if (request.AssetKind != PresentationAssetKind.Animation && !entry.Lifetime.IsDetached)
            {
                var explicitFollow = request.FollowAttachment && request.Attachment.IsAttached &&
                    request.Lifecycle.TargetLoss != PresentationTargetLossPolicy.BackendDefault;
                if (!explicitFollow || (_attachments != null && _attachments.TryResolve(request.Attachment, out _, out _, out _)))
                    ApplyTransform(entry.Instance, in request, in entry.BaseLocalScale);
            }
            ApplySourcesAndRate(entry.Instance, in request);
            entry.Inspection?.RefreshResolution(GetRateChannel(in request), PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
        }

        public Task StopAsync(PresentationUnityHandle handle) => StopEntryAsync(handle);

        private bool ApplyStackPolicy(in PresentationRequest request)
        {
            if (request.Lifecycle.Stack == PresentationStackPolicy.Independent) return true;
            var previous = new List<PresentationUnityHandle>();
            foreach (var pair in _entries)
            {
                if (pair.Value.Instance == null || pair.Value.Lifetime.IsFinished ||
                    pair.Value.LogicalKey != request.LogicalKey || pair.Value.ActorId != request.Attachment.ActorId) continue;
                if (request.Lifecycle.Stack == PresentationStackPolicy.RejectSameKey && !pair.Value.Lifetime.IsStopping) return false;
                previous.Add(pair.Key);
            }
            if (request.Lifecycle.Stack == PresentationStackPolicy.ReplaceSameKey)
            {
                var policy = request.Policy;
                var group = string.IsNullOrEmpty(policy.GroupKey) ? request.LogicalKey : policy.GroupKey;
                var key = group + ":owner:" + request.Attachment.ActorId;
                var replacing = 0;
                foreach (var handle in previous) if (_entries[handle].PolicyKey == key) replacing++;
                if (!_policies.CanAcquire(key, in policy, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond, replacing)) return false;
                for (var i = 0; i < previous.Count; i++) _ = StopEntryAsync(previous[i], force: true);
            }
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            Tick(deltaSeconds, Time.unscaledDeltaTime);
        }

        public void Tick(float deltaSeconds, float unscaledDeltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            if (unscaledDeltaSeconds < 0f || float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds)) return;
            _policies.Prune(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            var completed = new List<PresentationUnityHandle>();
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.Instance == null) { completed.Add(pair.Key); continue; }
                RefreshAttachment(entry);
                if (entry.Inspection != null) entry.Inspection.RecordLifetime(entry.Lifetime.IsDetached, entry.Lifetime.IsStopping);
                if (entry.Lifetime.IsFinished) { completed.Add(pair.Key); continue; }
                var before = entry.Lifecycle.Snapshot.Phase;
                var channel = GetRateChannel(entry.Request);
                var playbackRate = ResolvePlaybackRate(in entry.Request, channel);
                ApplyPlaybackRate(entry.Instance, in entry.Request, playbackRate);
                var durationComplete = !entry.Lifetime.IsStopping && entry.Lifecycle.Tick(deltaSeconds, playbackRate);
                var resourceComplete = (entry.Lifetime.IsStopping ||
                    (entry.Request.Playback.PlayOnReady && before == PresentationPlaybackPhase.Playing)) &&
                    _lifetimeAdapter.TryIsComplete(entry.Instance, out var complete) && complete;
                var lifetimePolicy = entry.Request.Lifecycle;
                if (entry.Lifetime.Tick(in lifetimePolicy, unscaledDeltaSeconds, durationComplete, resourceComplete)) completed.Add(pair.Key);
                else if (!entry.Lifetime.IsStopping && entry.Request.Playback.PlayOnReady &&
                         entry.Lifecycle.Snapshot.Phase == PresentationPlaybackPhase.Playing &&
                         before != PresentationPlaybackPhase.Playing)
                {
                    var request = entry.Request;
                    ApplySourcesAndRate(entry.Instance, in request);
                }
            }

            for (var i = 0; i < completed.Count; i++) _ = StopEntryAsync(completed[i], force: true);
        }

        public async Task ClearAsync()
        {
            _generation++;
            var handles = new List<PresentationUnityHandle>(_entries.Keys);
            for (var i = 0; i < handles.Count; i++) await StopEntryAsync(handles[i], force: true).ConfigureAwait(false);
            _policies.Clear();
        }

        private Task StopEntryAsync(PresentationUnityHandle handle) => StopEntryAsync(handle, false);

        private async Task StopEntryAsync(PresentationUnityHandle handle, bool force)
        {
            if (handle == null || !_entries.TryGetValue(handle, out var entry)) return;
            var lifetimePolicy = entry.Request.Lifecycle;
            var canDrain = !force && !entry.Lifetime.IsStopping &&
                lifetimePolicy.Stop == PresentationStopPolicy.Drain && entry.OwnsInstance &&
                _lifetimeAdapter.TryBeginDrain(entry.Instance);
            if (entry.Lifetime.IsStopping && !force) return;
            var finished = entry.Lifetime.RequestStop(in lifetimePolicy, canDrain, force);
            if (entry.Inspection != null) entry.Inspection.RecordLifetime(entry.Lifetime.IsDetached, entry.Lifetime.IsStopping);
            if (!finished) return;
            _entries.Remove(handle);
            entry.Lifecycle.Stop();
            var sharedVisual = false;
            foreach (var other in _entries.Values)
            {
                if (ReferenceEquals(other.Instance, entry.Instance)) { sharedVisual = true; break; }
            }
            if (!sharedVisual) _adapters.Stop(entry.Instance, entry.Request.AssetKind);
            _policies.Release(entry.PolicyKey);
            if (entry.OwnsInstance && entry.Instance != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(entry.Instance);
                else UnityEngine.Object.DestroyImmediate(entry.Instance);
            }
            await Task.CompletedTask;
        }

        private void RefreshAttachment(Entry entry)
        {
            var request = entry.Request;
            if (!request.FollowAttachment || !request.Attachment.IsAttached || entry.Lifetime.IsDetached) return;
            var policy = request.Lifecycle;
            if (_attachments != null && _attachments.TryResolve(request.Attachment, out var target, out var position, out var rotation))
            {
                if (entry.AttachmentTarget != target)
                {
                    entry.AttachmentTarget = target;
                    ApplyTransform(entry.Instance, in request, in entry.BaseLocalScale);
                }
                entry.LastTargetScale = target != null ? target.lossyScale : Vector3.one;
                ApplyScale(entry.Instance, in request, in entry.BaseLocalScale, entry.LastTargetScale);
                entry.Instance.transform.SetPositionAndRotation(new Vector3(position.X, position.Y, position.Z),
                    new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W));
                var ordering = request.Ordering;
                _orderingAdapter.Apply(entry.Instance, target != null ? target.gameObject : null, in ordering);
                if (entry.Inspection != null) entry.Inspection.RecordTarget(target, entry.LastTargetScale);
                return;
            }
            if (policy.TargetLoss == PresentationTargetLossPolicy.BackendDefault) return;
            if (entry.Lifetime.OnTargetMissing(in policy))
            {
                entry.Lifetime.RequestStop(in policy, false, force: true);
                return;
            }
            entry.Instance.transform.SetParent(_defaultParent, true);
            entry.AttachmentTarget = null;
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

            // Explicit loss policies survive target destruction by following from the persistent root.
            var desiredParent = request.FollowAttachment && request.Attachment.IsAttached &&
                request.Lifecycle.TargetLoss == PresentationTargetLossPolicy.BackendDefault
                ? target
                : _defaultParent;
            if (desiredParent != null && instance.transform.parent != desiredParent)
                instance.transform.SetParent(desiredParent, true);
            else if (desiredParent == null && instance.transform.parent != null)
                instance.transform.SetParent(null, true);
            ApplyScale(instance, in request, in baseLocalScale);
            var ordering = request.Ordering;
            _orderingAdapter.Apply(instance, target != null ? target.gameObject : null, in ordering);
        }

        private void ApplyScale(GameObject instance, in PresentationRequest request, in Vector3 baseLocalScale, Vector3? frozenTargetScale = null)
        {
            var sourceScale = _scaleResolver.Resolve(
                PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue,
                PresentationVector3.One);
            var radius = request.Radius > 0f && !float.IsInfinity(request.Radius) ? request.Radius : 1f;
            var desiredScale = new Vector3(
                baseLocalScale.x * request.Scale * radius * sourceScale.X,
                baseLocalScale.y * request.Scale * radius * sourceScale.Y,
                baseLocalScale.z * request.Scale * radius * sourceScale.Z);
            var inherited = Vector3.one;
            if (request.FollowAttachment && request.Attachment.IsAttached && request.ParentScaleMode == PresentationParentScaleMode.Inherit)
            {
                var targetScale = frozenTargetScale ?? Vector3.one;
                if (!frozenTargetScale.HasValue && _attachments != null &&
                    _attachments.TryResolve(request.Attachment, out var resolved, out _, out _) && resolved != null)
                    targetScale = resolved.lossyScale;
                inherited = targetScale;
            }
            UnityPresentationTransformUtility.ApplyWorldScale(instance.transform, desiredScale, inherited);
        }

        private void RefreshScales()
        {
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.Instance == null || entry.Request.AssetKind == PresentationAssetKind.Animation) continue;
                var request = entry.Request;
                ApplyScale(entry.Instance, in request, in entry.BaseLocalScale,
                    entry.Lifetime.IsDetached || entry.AttachmentTarget == null ? entry.LastTargetScale : (Vector3?)null);
                entry.Inspection?.RefreshResolution(GetRateChannel(in request), PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
            }
        }

        private void RefreshDiagnostics()
        {
            foreach (var entry in _entries.Values)
                if (entry.Inspection != null)
                    entry.Inspection.RefreshResolution(GetRateChannel(in entry.Request),
                        PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
        }

        private void RefreshWorldOffsetAttachments()
        {
            if (_attachments == null) return;
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.Instance == null ||
                    !entry.Request.FollowAttachment ||
                    !entry.Request.Attachment.IsAttached ||
                    entry.Request.Attachment.OffsetSpace != PresentationOffsetSpace.World)
                    continue;

                var attachment = entry.Request.Attachment;
                if (_attachments.TryResolve(in attachment, out _, out var position, out _))
                    entry.Instance.transform.position = new Vector3(position.X, position.Y, position.Z);
            }
        }

        private void ApplySourcesAndRate(GameObject instance, in PresentationRequest request)
        {
            if (!_adapters.TryApply(instance, in request))
                ApplyDefaultPlayback(instance, in request);
            ApplyPlaybackRate(instance, in request,
                ResolvePlaybackRate(in request, GetRateChannel(request)));
        }

        private float ResolvePlaybackRate(in PresentationRequest request, PresentationRateChannel channel)
        {
            var rate = request.Playback.Speed * _rateResolver.ResolveRate(channel);
            return float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0f ? 0f : rate;
        }

        private void ApplyPlaybackRate(GameObject instance, in PresentationRequest request, float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0f) rate = 0f;
            _adapters.TrySetRate(instance, request.AssetKind, rate);
        }

        private static PresentationRateChannel GetRateChannel(in PresentationRequest request)
        {
            var sourceChannel = request.AssetKind == PresentationAssetKind.Animation
                ? PresentationRateChannel.Action
                : request.AssetKind == PresentationAssetKind.Timeline
                    ? PresentationRateChannel.SkillTimeline
                    : PresentationRateChannel.Vfx;
            return sourceChannel | PresentationRateChannel.CueLifetime;
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

    public sealed class UnityParticleSourceAdapter : IPresentationSourceAdapter<GameObject>, IPresentationRateAwareSourceAdapter<GameObject>
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
                main.simulationSpeed = request.Playback.Speed;
                if (request.Playback.PlayOnReady) particles[i].Play(true);
            }
        }

        public void SetRate(GameObject visual, float rate)
        {
            if (visual == null) return;
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.simulationSpeed = rate;
            }
        }

        public void Stop(GameObject visual)
        {
            if (visual == null) return;
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public sealed class UnityAnimatorSourceAdapter : IPresentationSourceAdapter<GameObject>, IPresentationRateAwareSourceAdapter<GameObject>
    {
        public bool CanHandle(PresentationAssetKind kind, GameObject visual) =>
            visual != null && (kind == PresentationAssetKind.Animation ||
                (kind == PresentationAssetKind.Prefab && visual.GetComponentInChildren<Animator>(true) != null));

        public void Apply(GameObject visual, in PresentationRequest request)
        {
            if (visual == null || string.IsNullOrEmpty(request.Playback.ClipKey)) return;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            animator.speed = request.Playback.Speed;
            animator.Play(Animator.StringToHash(request.Playback.ClipKey), request.Playback.TrackIndex, request.Playback.NormalizedTime);
        }

        public void SetRate(GameObject visual, float rate)
        {
            var animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
            if (animator != null) animator.speed = rate;
        }

        public void Stop(GameObject visual)
        {
            if (visual == null) return;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.speed = 0f;
        }
    }

    public sealed class UnityTimelineSourceAdapter : IPresentationSourceAdapter<GameObject>, IPresentationRateAwareSourceAdapter<GameObject>
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

        public void SetRate(GameObject visual, float rate)
        {
            var director = visual != null ? visual.GetComponentInChildren<PlayableDirector>(true) : null;
            if (director == null || !director.playableGraph.IsValid()) return;
            var graph = director.playableGraph;
            for (var i = 0; i < graph.GetRootPlayableCount(); i++)
                graph.GetRootPlayable(i).SetSpeed(rate);
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
            target = UnityPresentationTransformUtility.ResolveSocket(root, in attachment);
            var world = UnityPresentationTransformUtility.ResolvePosition(target, in attachment);
            position = new PresentationVector3(world.x, world.y, world.z);
            var worldRotation = target.rotation;
            rotation = new PresentationRotation(worldRotation.x, worldRotation.y, worldRotation.z, worldRotation.w);
            return true;
        }
    }
}

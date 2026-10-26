using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public enum PresentationAssetKind
    {
        Prefab = 0,
        Animation = 1,
        Particle = 2,
        Model = 3,
        Timeline = 4,
        Image = 5,
    }

    public readonly struct PresentationPlaybackSpec
    {
        public PresentationPlaybackSpec(
            string clipKey = null,
            int trackIndex = 0,
            bool loop = false,
            float blendDuration = 0f,
            float normalizedTime = 0f,
            float speed = 1f,
            int playCount = 1,
            int delayMs = 0,
            int intervalMs = 0,
            bool playOnReady = true)
        {
            ClipKey = clipKey ?? string.Empty;
            TrackIndex = trackIndex < 0 ? 0 : trackIndex;
            Loop = loop;
            BlendDuration = IsFinite(blendDuration) && blendDuration > 0f ? blendDuration : 0f;
            NormalizedTime = IsFinite(normalizedTime) && normalizedTime >= 0f ? normalizedTime : 0f;
            Speed = IsFinite(speed) && speed >= 0f ? speed : 1f;
            PlayCount = playCount < 0 ? 0 : playCount;
            DelayMs = delayMs < 0 ? 0 : delayMs;
            IntervalMs = intervalMs < 0 ? 0 : intervalMs;
            PlayOnReady = playOnReady;
        }

        public string ClipKey { get; }
        public int TrackIndex { get; }
        public bool Loop { get; }
        public float BlendDuration { get; }
        public float NormalizedTime { get; }
        public float Speed { get; }
        public int PlayCount { get; }
        public int DelayMs { get; }
        public int IntervalMs { get; }
        public bool PlayOnReady { get; }

        public static PresentationPlaybackSpec Default => new PresentationPlaybackSpec();

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct PresentationEffectPolicy
    {
        public PresentationEffectPolicy(
            string groupKey = null,
            bool unique = false,
            int minIntervalMs = 0,
            int maxActive = 0)
        {
            GroupKey = groupKey ?? string.Empty;
            Unique = unique;
            MinIntervalMs = minIntervalMs < 0 ? 0 : minIntervalMs;
            MaxActive = maxActive < 0 ? 0 : maxActive;
        }

        public string GroupKey { get; }
        public bool Unique { get; }
        public int MinIntervalMs { get; }
        public int MaxActive { get; }
        public bool IsConfigured => Unique || MinIntervalMs > 0 || MaxActive > 0 || !string.IsNullOrEmpty(GroupKey);
    }
    public enum PresentationAttachmentKind
    {
        World = 0,
        Actor = 1,
        Socket = 2,
        Projectile = 3,
        Area = 4,
    }

    public readonly struct PresentationVector3
    {
        public PresentationVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static PresentationVector3 Zero => default;
    }

    public readonly struct PresentationRotation
    {
        public PresentationRotation(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }
        public static PresentationRotation Identity => new PresentationRotation(0f, 0f, 0f, 1f);
    }

    public readonly struct PresentationAttachment
    {
        public PresentationAttachment(
            PresentationAttachmentKind kind,
            int actorId = 0,
            string socket = null,
            PresentationVector3 offset = default)
        {
            Kind = kind;
            ActorId = actorId;
            Socket = socket ?? string.Empty;
            Offset = offset;
        }

        public PresentationAttachmentKind Kind { get; }
        public int ActorId { get; }
        public string Socket { get; }
        public PresentationVector3 Offset { get; }

        public bool IsAttached => Kind != PresentationAttachmentKind.World && ActorId > 0;
    }

    public readonly struct PresentationParameter
    {
        public PresentationParameter(string key, float value)
        {
            Key = key ?? string.Empty;
            Value = value;
        }

        public string Key { get; }
        public float Value { get; }
    }

    public readonly struct PresentationRequest
    {
        public PresentationRequest(
            string logicalKey,
            int assetId,
            PresentationVector3 position,
            PresentationRotation rotation = default,
            PresentationAttachment attachment = default,
            int durationMs = 0,
            float scale = 1f,
            float radius = 0f,
            IReadOnlyList<PresentationParameter> parameters = null,
            string resourceKey = null,
            PresentationAssetKind assetKind = PresentationAssetKind.Prefab,
            PresentationPlaybackSpec playback = default,
            PresentationEffectPolicy policy = default)
        {
            LogicalKey = logicalKey ?? string.Empty;
            AssetId = assetId;
            Position = position;
            Rotation = rotation.W == 0f && rotation.X == 0f && rotation.Y == 0f && rotation.Z == 0f
                ? PresentationRotation.Identity
                : rotation;
            Attachment = attachment;
            DurationMs = durationMs > 0 ? durationMs : 0;
            Scale = scale > 0f ? scale : 1f;
            Radius = radius;
            Parameters = parameters;
            ResourceKey = resourceKey ?? string.Empty;
            AssetKind = assetKind;
            Playback = playback.Equals(default(PresentationPlaybackSpec))
                ? PresentationPlaybackSpec.Default
                : playback;
            Policy = policy;
        }

        public string LogicalKey { get; }
        public int AssetId { get; }
        public PresentationVector3 Position { get; }
        public PresentationRotation Rotation { get; }
        public PresentationAttachment Attachment { get; }
        public int DurationMs { get; }
        public float Scale { get; }
        public float Radius { get; }
        public IReadOnlyList<PresentationParameter> Parameters { get; }
        public string ResourceKey { get; }
        public PresentationAssetKind AssetKind { get; }
        public PresentationPlaybackSpec Playback { get; }
        public PresentationEffectPolicy Policy { get; }

        public bool IsValid => !string.IsNullOrEmpty(LogicalKey) &&
            (AssetId > 0 || !string.IsNullOrEmpty(ResourceKey) ||
             (AssetKind == PresentationAssetKind.Animation && !string.IsNullOrEmpty(Playback.ClipKey)));
    }

    public enum PresentationDeferredCommandKind
    {
        SetVisible = 0,
        SetSpeed = 1,
        SetColor = 2,
        SetFloat = 3,
        SetTransform = 4,
        Play = 5,
        Stop = 6,
    }

    public readonly struct PresentationDeferredCommand
    {
        public PresentationDeferredCommand(
            PresentationDeferredCommandKind kind,
            string key = null,
            float value = 0f,
            PresentationVector3 position = default,
            PresentationRotation rotation = default,
            PresentationVector3 scale = default,
            bool flag = false)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            Value = value;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Flag = flag;
        }

        public PresentationDeferredCommandKind Kind { get; }
        public string Key { get; }
        public float Value { get; }
        public PresentationVector3 Position { get; }
        public PresentationRotation Rotation { get; }
        public PresentationVector3 Scale { get; }
        public bool Flag { get; }
    }

    public sealed class PresentationDeferredCommandBuffer
    {
        private readonly List<PresentationDeferredCommand> _commands = new List<PresentationDeferredCommand>(8);

        public int Count => _commands.Count;

        public void Add(in PresentationDeferredCommand command) => _commands.Add(command);

        public void Clear() => _commands.Clear();

        public void Replay(Action<PresentationDeferredCommand> consumer)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            for (var i = 0; i < _commands.Count; i++) consumer(_commands[i]);
        }
    }

    public interface IPresentationAttachmentResolver<TTarget>
    {
        bool TryResolve(
            in PresentationAttachment attachment,
            out TTarget target,
            out PresentationVector3 position,
            out PresentationRotation rotation);
    }

    public enum PresentationPlaybackPhase
    {
        Pending = 0,
        Delayed = 1,
        Playing = 2,
        Interval = 3,
        Completed = 4,
        Stopped = 5,
    }

    public readonly struct PresentationPlaybackSnapshot
    {
        public PresentationPlaybackSnapshot(PresentationPlaybackPhase phase, int completedPlays, float elapsedSeconds)
        {
            Phase = phase;
            CompletedPlays = completedPlays;
            ElapsedSeconds = elapsedSeconds;
        }

        public PresentationPlaybackPhase Phase { get; }
        public int CompletedPlays { get; }
        public float ElapsedSeconds { get; }
    }

    public sealed class PresentationPlaybackLifecycle
    {
        private readonly PresentationPlaybackSpec _spec;
        private readonly float _playDurationSeconds;
        private PresentationPlaybackPhase _phase;
        private int _completedPlays;
        private float _elapsed;
        private float _phaseElapsed;

        public PresentationPlaybackLifecycle(PresentationPlaybackSpec spec, float playDurationSeconds)
        {
            _spec = spec;
            _playDurationSeconds = playDurationSeconds > 0f ? playDurationSeconds : 0f;
            _phase = spec.DelayMs > 0 ? PresentationPlaybackPhase.Delayed : PresentationPlaybackPhase.Playing;
        }

        public PresentationPlaybackSnapshot Snapshot => new PresentationPlaybackSnapshot(_phase, _completedPlays, _elapsed);

        public bool Tick(float deltaSeconds)
        {
            if (_phase == PresentationPlaybackPhase.Completed || _phase == PresentationPlaybackPhase.Stopped) return false;
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            _elapsed += deltaSeconds;
            var delay = _spec.DelayMs / 1000f;
            var interval = _spec.IntervalMs / 1000f;
            var remaining = deltaSeconds;
            while (remaining > 0f && _phase != PresentationPlaybackPhase.Completed &&
                   _phase != PresentationPlaybackPhase.Stopped)
            {
                if (_phase == PresentationPlaybackPhase.Delayed)
                {
                    var available = Math.Max(0f, delay - _phaseElapsed);
                    var consumed = Math.Min(remaining, available);
                    _phaseElapsed += consumed;
                    remaining -= consumed;
                    if (_phaseElapsed + 0.000001f < delay) break;
                    _phase = PresentationPlaybackPhase.Playing;
                    _phaseElapsed = 0f;
                    continue;
                }

                if (_phase == PresentationPlaybackPhase.Interval)
                {
                    var available = Math.Max(0f, interval - _phaseElapsed);
                    var consumed = Math.Min(remaining, available);
                    _phaseElapsed += consumed;
                    remaining -= consumed;
                    if (_phaseElapsed + 0.000001f < interval) break;
                    _phase = PresentationPlaybackPhase.Playing;
                    _phaseElapsed = 0f;
                    continue;
                }

                if (_phase != PresentationPlaybackPhase.Playing || _playDurationSeconds <= 0f) break;
                var playRemaining = Math.Max(0f, _playDurationSeconds - _phaseElapsed);
                var playConsumed = Math.Min(remaining, playRemaining);
                _phaseElapsed += playConsumed;
                remaining -= playConsumed;
                if (_phaseElapsed + 0.000001f < _playDurationSeconds) break;

                _completedPlays++;
                if (_spec.Loop || _spec.PlayCount == 0 || _completedPlays < _spec.PlayCount)
                {
                    _phase = interval > 0f ? PresentationPlaybackPhase.Interval : PresentationPlaybackPhase.Playing;
                    _phaseElapsed = 0f;
                }
                else _phase = PresentationPlaybackPhase.Completed;
            }

            return _phase == PresentationPlaybackPhase.Completed;
        }

        public void Stop() => _phase = PresentationPlaybackPhase.Stopped;
    }

    public readonly struct PresentationEmission<TPayload>
    {
        public PresentationEmission(TPayload payload, int priority, long sequence)
        {
            Payload = payload;
            Priority = priority;
            Sequence = sequence;
        }

        public TPayload Payload { get; }
        public int Priority { get; }
        public long Sequence { get; }
    }

    public sealed class PresentationEmissionQueue<TPayload>
    {
        private readonly List<PresentationEmission<TPayload>> _pending = new List<PresentationEmission<TPayload>>(32);
        private readonly int _capacity;
        private long _sequence;

        public PresentationEmissionQueue(int capacity = 256)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Count => _pending.Count;
        public long DroppedCount { get; private set; }

        public bool Enqueue(TPayload payload, int priority = 0)
        {
            if (_pending.Count >= _capacity)
            {
                var lowest = 0;
                for (var i = 1; i < _pending.Count; i++)
                    if (_pending[i].Priority < _pending[lowest].Priority) lowest = i;
                if (_pending[lowest].Priority > priority)
                {
                    DroppedCount++;
                    return false;
                }
                _pending.RemoveAt(lowest);
                DroppedCount++;
            }
            _pending.Add(new PresentationEmission<TPayload>(payload, priority, ++_sequence));
            return true;
        }

        public int Drain(int maxItems, TimeSpan budget, Func<TPayload, bool> consumer)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            if (maxItems <= 0 || _pending.Count == 0) return 0;
            _pending.Sort((a, b) =>
            {
                var priority = b.Priority.CompareTo(a.Priority);
                return priority != 0 ? priority : a.Sequence.CompareTo(b.Sequence);
            });
            var start = Stopwatch.GetTimestamp();
            var processed = 0;
            while (_pending.Count > 0 && processed < maxItems)
            {
                if (budget > TimeSpan.Zero &&
                    TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency) >= budget)
                    break;
                var item = _pending[0];
                _pending.RemoveAt(0);
                if (consumer(item.Payload)) processed++;
            }
            return processed;
        }

        public void Clear() => _pending.Clear();
    }

    /// <summary>
    /// Pure policy gate shared by Unity and non-Unity presentation backends.
    /// Keys normally combine owner, group and logical effect identity.
    /// </summary>
    public sealed class PresentationEffectPolicyLedger<TKey>
    {
        private sealed class State
        {
            public int Active;
            public long LastEmissionMs = long.MinValue;
            public bool KeepHistory;
            public int HistoryDurationMs;
        }

        private readonly Dictionary<TKey, State> _states;
        private readonly IEqualityComparer<TKey> _comparer;

        public PresentationEffectPolicyLedger(IEqualityComparer<TKey> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _states = new Dictionary<TKey, State>(_comparer);
        }

        public int ActiveCount => _states.Count;

        public bool TryAcquire(TKey key, in PresentationEffectPolicy policy, long nowMs)
        {
            if (key is null) return true;
            Prune(nowMs);
            if (!_states.TryGetValue(key, out var state))
            {
                state = new State();
                _states.Add(key, state);
            }

            state.KeepHistory = policy.MinIntervalMs > 0;
            state.HistoryDurationMs = policy.MinIntervalMs;

            if (policy.Unique && state.Active > 0) return false;
            if (policy.MinIntervalMs > 0 && state.LastEmissionMs != long.MinValue &&
                nowMs - state.LastEmissionMs < policy.MinIntervalMs) return false;
            if (policy.MaxActive > 0 && state.Active >= policy.MaxActive) return false;

            state.Active++;
            state.LastEmissionMs = nowMs;
            return true;
        }

        public void Release(TKey key)
        {
            if (key is null || !_states.TryGetValue(key, out var state)) return;
            state.Active = Math.Max(0, state.Active - 1);
            if (state.Active == 0 && !state.KeepHistory) _states.Remove(key);
        }

        public void Clear() => _states.Clear();

        public int Prune(long nowMs)
        {
            List<TKey> expired = null;
            foreach (var pair in _states)
            {
                var state = pair.Value;
                if (state.Active != 0 || !state.KeepHistory || state.LastEmissionMs == long.MinValue) continue;
                if (nowMs - state.LastEmissionMs < state.HistoryDurationMs) continue;
                expired ??= new List<TKey>();
                expired.Add(pair.Key);
            }

            if (expired == null) return 0;
            for (var i = 0; i < expired.Count; i++) _states.Remove(expired[i]);
            return expired.Count;
        }

        public int GetActive(TKey key)
        {
            return key is not null && _states.TryGetValue(key, out var state) ? state.Active : 0;
        }
    }

    public interface IPresentationSourceAdapter<TVisual>
    {
        bool CanHandle(PresentationAssetKind kind, TVisual visual);
        void Apply(TVisual visual, in PresentationRequest request);
        void Stop(TVisual visual);
    }

    public sealed class PresentationSourceAdapterRegistry<TVisual>
    {
        private readonly List<IPresentationSourceAdapter<TVisual>> _adapters = new List<IPresentationSourceAdapter<TVisual>>();

        public void Register(IPresentationSourceAdapter<TVisual> adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            _adapters.Add(adapter);
        }

        public bool TryApply(TVisual visual, in PresentationRequest request)
        {
            for (var i = 0; i < _adapters.Count; i++)
            {
                if (!_adapters[i].CanHandle(request.AssetKind, visual)) continue;
                _adapters[i].Apply(visual, in request);
                return true;
            }
            return false;
        }

        public void Stop(TVisual visual, PresentationAssetKind kind)
        {
            for (var i = 0; i < _adapters.Count; i++)
                if (_adapters[i].CanHandle(kind, visual)) _adapters[i].Stop(visual);
        }
    }

    public enum PresentationHandleState
    {
        Pending = 0,
        Ready = 1,
        Stopped = 2,
        Failed = 3,
    }

    public interface IPresentationResourceLease : IDisposable
    {
        bool IsReleased { get; }
        Task Ready { get; }
        void Release();
    }

    public sealed class PresentationResourceLease : IPresentationResourceLease
    {
        private readonly Action _release;
        private int _released;

        public PresentationResourceLease(Task ready = null, Action release = null)
        {
            Ready = ready ?? Task.CompletedTask;
            _release = release;
        }

        public bool IsReleased => Volatile.Read(ref _released) != 0;
        public Task Ready { get; }

        public void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) _release?.Invoke();
        }

        public void Dispose() => Release();
    }

    public sealed class PresentationHandle<TBackendHandle>
    {
        private readonly Func<TBackendHandle, Task> _stop;
        private int _state;

        public PresentationHandle(
            TBackendHandle backendHandle,
            Task ready = null,
            Func<TBackendHandle, Task> stop = null,
            IPresentationResourceLease lease = null)
        {
            BackendHandle = backendHandle;
            Ready = ready ?? Task.CompletedTask;
            _stop = stop;
            Lease = lease;
            _state = (int)PresentationHandleState.Pending;
            Ready.ContinueWith(
                task => Interlocked.CompareExchange(
                    ref _state,
                    task.IsCanceled || task.IsFaulted
                        ? (int)PresentationHandleState.Failed
                        : (int)PresentationHandleState.Ready,
                    (int)PresentationHandleState.Pending),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public TBackendHandle BackendHandle { get; }
        public Task Ready { get; }
        public IPresentationResourceLease Lease { get; }
        public PresentationHandleState State => (PresentationHandleState)Volatile.Read(ref _state);

        public async Task StopAsync()
        {
            var previous = (PresentationHandleState)Interlocked.Exchange(
                ref _state,
                (int)PresentationHandleState.Stopped);
            if (previous == PresentationHandleState.Stopped) return;

            try
            {
                if (_stop != null) await _stop(BackendHandle).ConfigureAwait(false);
            }
            finally
            {
                Lease?.Release();
            }
        }
    }

    public interface IPresentationBackend<TBackendHandle>
    {
        bool IsAlive(TBackendHandle handle);
        Task<PresentationHandle<TBackendHandle>> PlayAsync(
            in PresentationRequest request,
            CancellationToken cancellationToken = default);
        void Update(TBackendHandle handle, in PresentationRequest request);
        Task StopAsync(TBackendHandle handle);
    }

    public sealed class PresentationInstanceRegistry<TKey, TPayload, THandle>
    {
        private readonly IPresentationCueViewPort<TKey, TPayload, THandle> _port;
        private readonly IEqualityComparer<TKey> _comparer;
        private readonly Dictionary<TKey, THandle> _active;
        private readonly List<TKey> _clearBuffer = new List<TKey>();

        public PresentationInstanceRegistry(
            IPresentationCueViewPort<TKey, TPayload, THandle> port,
            IEqualityComparer<TKey> comparer = null)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _active = new Dictionary<TKey, THandle>(_comparer);
        }

        public int ActiveCount => _active.Count;
        public long CreatedCount { get; private set; }
        public long UpdatedCount { get; private set; }
        public long DestroyedCount { get; private set; }
        public long FailedCreateCount { get; private set; }
        public long StaleHandleCount { get; private set; }

        public bool EnsureActive(TKey key, in TPayload payload)
        {
            if (_comparer.Equals(key, default(TKey))) return false;
            if (_active.TryGetValue(key, out var existing))
            {
                if (_port.IsAlive(existing))
                {
                    _port.Update(existing, in payload);
                    UpdatedCount++;
                    return true;
                }

                _active.Remove(key);
                StaleHandleCount++;
            }

            if (!_port.TryCreate(key, in payload, out var created))
            {
                FailedCreateCount++;
                return false;
            }

            _active.Add(key, created);
            CreatedCount++;
            return true;
        }

        public bool Stop(TKey key)
        {
            if (_comparer.Equals(key, default(TKey)) || !_active.TryGetValue(key, out var handle)) return false;
            if (_port.IsAlive(handle))
            {
                _port.Destroy(handle);
                DestroyedCount++;
            }
            else StaleHandleCount++;
            _active.Remove(key);
            return true;
        }

        public bool EmitTransient(TKey key, in TPayload payload)
        {
            if (_comparer.Equals(key, default(TKey))) return false;
            if (!_port.TryCreate(key, in payload, out var handle))
            {
                FailedCreateCount++;
                return false;
            }

            CreatedCount++;
            if (_port.IsAlive(handle))
            {
                _port.Destroy(handle);
                DestroyedCount++;
            }
            return true;
        }

        public bool TryGetHandle(TKey key, out THandle handle)
        {
            if (_comparer.Equals(key, default(TKey)))
            {
                handle = default(THandle);
                return false;
            }
            return _active.TryGetValue(key, out handle);
        }

        public void Clear()
        {
            _clearBuffer.Clear();
            foreach (var pair in _active) _clearBuffer.Add(pair.Key);
            List<Exception> failures = null;
            for (var i = 0; i < _clearBuffer.Count; i++)
            {
                try { Stop(_clearBuffer[i]); }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }
            _clearBuffer.Clear();
            if (failures != null)
                throw new AggregateException("One or more presentation instances failed to stop.", failures);
        }

        public void ReconcileActiveSet(IReadOnlyCollection<TKey> desiredKeys)
        {
            var desired = new HashSet<TKey>(_comparer);
            if (desiredKeys != null)
            {
                foreach (var key in desiredKeys)
                    if (!_comparer.Equals(key, default(TKey))) desired.Add(key);
            }

            _clearBuffer.Clear();
            foreach (var pair in _active)
                if (!desired.Contains(pair.Key)) _clearBuffer.Add(pair.Key);
            for (var i = 0; i < _clearBuffer.Count; i++) Stop(_clearBuffer[i]);
            _clearBuffer.Clear();
        }
    }

    public sealed class PresentationCompositeTemplate
    {
        private readonly PresentationRequest[] _children;

        public PresentationCompositeTemplate(string id, IReadOnlyList<PresentationRequest> children)
        {
            Id = id ?? string.Empty;
            _children = children == null ? Array.Empty<PresentationRequest>() : Copy(children);
        }

        public string Id { get; }
        public IReadOnlyList<PresentationRequest> Children => _children;

        private static PresentationRequest[] Copy(IReadOnlyList<PresentationRequest> source)
        {
            var result = new PresentationRequest[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            return result;
        }
    }

    public sealed class PresentationCompositeHandle<TBackendHandle>
    {
        private readonly IReadOnlyList<PresentationHandle<TBackendHandle>> _children;

        public PresentationCompositeHandle(IReadOnlyList<PresentationHandle<TBackendHandle>> children)
        {
            _children = children ?? Array.Empty<PresentationHandle<TBackendHandle>>();
            var readyTasks = new Task[_children.Count];
            for (var i = 0; i < _children.Count; i++)
                readyTasks[i] = _children[i]?.Ready ?? Task.CompletedTask;
            Ready = Task.WhenAll(readyTasks);
        }

        public IReadOnlyList<PresentationHandle<TBackendHandle>> Children => _children;
        public Task Ready { get; }

        public async Task StopAsync()
        {
            for (var i = _children.Count - 1; i >= 0; i--)
                if (_children[i] != null) await _children[i].StopAsync().ConfigureAwait(false);
        }
    }
}

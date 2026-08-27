#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Game.View.Presentation;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterSnapshotStream : IViewStream<ShooterSnapshotViewBatch>
    {
        public const int DefaultBufferCapacity = 64;
        public const float DefaultPlaybackFramesPerSecond = 30f;
        public const float DefaultInterpolationDelayFrames = 2f;
        public const float DefaultDelayConvergenceRate = 0.125f;
        public const float DefaultMaxTransformExtrapolationFrames = 6f;

        private readonly ShooterSnapshotViewBatch[] _buffer;
        private readonly ShooterSnapshotSamplingPolicy _samplingPolicy;
        private readonly SparseSnapshotTrackBuffer<ShooterViewEntityKey, ShooterViewTransformComponentChange> _transformTracks;
        private readonly HashSet<ShooterViewEntityKey> _sampledTransformKeys = new();
        private readonly HashSet<ShooterViewEntityKey> _batchEntityKeys = new();
        private readonly List<ShooterViewEntityKey> _tracksToRemove = new();
        private readonly ReusableTransformList _transientTransformBuffer = new();
        private int _start;
        private int _count;
        private float _playbackFrame;
        private float _interpolationDelayFrames;
        private float _targetInterpolationDelayFrames;
        private ShooterSnapshotViewBatchKey _lastSampledBatchKey;
        private bool _hasLastSampledBatchKey;
        private bool _playbackInitialized;
        private bool _isPrebuffering;
        private int _playbackSearchIndex;

        public ShooterSnapshotStream()
            : this(DefaultBufferCapacity)
        {
        }

        public ShooterSnapshotStream(int bufferCapacity)
            : this(bufferCapacity, new ShooterSnapshotSamplingPolicy(new ShooterSnapshotSamplingPolicyOptions()))
        {
        }

        public ShooterSnapshotStream(int bufferCapacity, ShooterSnapshotSamplingPolicy samplingPolicy)
        {
            if (bufferCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(bufferCapacity));

            _buffer = new ShooterSnapshotViewBatch[bufferCapacity];
            _samplingPolicy = samplingPolicy ?? throw new ArgumentNullException(nameof(samplingPolicy));
            _transformTracks = new SparseSnapshotTrackBuffer<ShooterViewEntityKey, ShooterViewTransformComponentChange>(
                InterpolateTransform,
                ExtrapolateTransform)
            {
                MaxExtrapolationTicks = DefaultMaxTransformExtrapolationFrames
            };
            PlaybackFramesPerSecond = DefaultPlaybackFramesPerSecond;
            InterpolationDelayFrames = DefaultInterpolationDelayFrames;
            DelayConvergenceRate = DefaultDelayConvergenceRate;
        }

        public event Action<ShooterSnapshotViewBatch>? SnapshotApplied;

        event Action<ShooterSnapshotViewBatch>? IViewStream<ShooterSnapshotViewBatch>.BatchApplied
        {
            add => SnapshotApplied += value;
            remove => SnapshotApplied -= value;
        }

        public int BufferedSnapshotCount => _count;

        public int BufferCapacity => _buffer.Length;

        public float PlaybackFrame => _playbackFrame;

        public float PlaybackFramesPerSecond { get; set; }

        public float MaxTransformExtrapolationFrames
        {
            get => (float)_transformTracks.MaxExtrapolationTicks;
            set => _transformTracks.MaxExtrapolationTicks = Math.Max(0f, value);
        }

        public float InterpolationDelayFrames
        {
            get => _interpolationDelayFrames;
            set
            {
                var delay = Math.Max(0f, value);
                _targetInterpolationDelayFrames = delay;
                if (!_playbackInitialized)
                {
                    _interpolationDelayFrames = delay;
                }
            }
        }

        public float TargetInterpolationDelayFrames => _targetInterpolationDelayFrames;

        /// <summary>
        /// Maximum fraction of normal playback progress used each tick to converge the active delay.
        /// A value below one prevents delay changes from moving the playback clock backwards or snapping it forwards.
        /// </summary>
        public float DelayConvergenceRate { get; set; }

        public float BufferedFrameSpan => _count > 1
            ? Math.Max(0f, GetAt(_count - 1).Frame - GetAt(0).Frame)
            : 0f;

        public float AvailablePlaybackLeadFrames => _count > 0 && _playbackInitialized
            ? Math.Max(0f, GetAt(_count - 1).Frame - _playbackFrame)
            : 0f;

        public bool IsPlaybackStarved => _count > 0
            && _playbackInitialized
            && !_isPrebuffering
            && _playbackFrame >= GetAt(_count - 1).Frame;

        public bool TrySetTargetInterpolationDelayFrames(float delayFrames)
        {
            if (float.IsNaN(delayFrames) || float.IsInfinity(delayFrames) || delayFrames < 0f)
            {
                return false;
            }

            _targetInterpolationDelayFrames = delayFrames;
            if (!_playbackInitialized)
            {
                _interpolationDelayFrames = delayFrames;
            }

            return true;
        }

        public void Publish(in ShooterSnapshotViewBatch batch)
        {
            Store(in batch);
            SnapshotApplied?.Invoke(batch);
        }

        public bool TrySampleLatest(out ShooterSnapshotViewBatch batch)
        {
            if (_count == 0)
            {
                batch = default;
                return false;
            }

            batch = GetAt(_count - 1);
            return true;
        }

        public bool TrySample(float playbackFrame, out ShooterSnapshotViewBatch batch)
        {
            return TrySample(playbackFrame, out batch, out _);
        }

        public bool TrySample(float playbackFrame, out ShooterSnapshotViewBatch batch, out bool isContinuousSample)
        {
            if (!TryFindSampleWindow(playbackFrame, out var from, out var to))
            {
                batch = default;
                isContinuousSample = false;
                return false;
            }

            batch = _samplingPolicy.Sample(in from, in to, playbackFrame, out isContinuousSample);
            batch = ApplyLowFrequencyTransforms(in batch, playbackFrame, useTransientBuffer: false, ref isContinuousSample);
            return true;
        }

        public bool TryAdvancePlayback(float deltaTime, out ShooterSnapshotViewBatch batch)
        {
            return TryAdvancePlaybackCore(deltaTime, useTransientBuffer: false, out batch);
        }

        /// <summary>
        /// Advances playback without allocating interpolated transform arrays. The returned
        /// transform list is valid only until the next transient playback sample on this stream.
        /// </summary>
        public bool TryAdvancePlaybackTransient(float deltaTime, out ShooterSnapshotViewBatch batch)
        {
            return TryAdvancePlaybackCore(deltaTime, useTransientBuffer: true, out batch);
        }

        private bool TryAdvancePlaybackCore(float deltaTime, bool useTransientBuffer, out ShooterSnapshotViewBatch batch)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            if (_count == 0)
            {
                batch = default;
                return false;
            }

            if (!_playbackInitialized)
            {
                var latest = GetAt(_count - 1);
                var oldestFrame = GetAt(0).Frame;
                _playbackFrame = Math.Max(oldestFrame, latest.Frame - Math.Max(0f, _interpolationDelayFrames));
                _isPrebuffering = latest.Frame - _playbackFrame < _interpolationDelayFrames;
                _playbackInitialized = true;
            }
            else
            {
                var playbackAdvance = deltaTime * Math.Max(0f, PlaybackFramesPerSecond);
                var latestFrame = GetAt(_count - 1).Frame;
                if (_isPrebuffering && latestFrame - _playbackFrame < _interpolationDelayFrames)
                {
                    playbackAdvance = 0f;
                }
                else
                {
                    _isPrebuffering = false;
                    playbackAdvance = ConvergePlaybackDelay(playbackAdvance);
                }

                _playbackFrame = Math.Min(latestFrame, _playbackFrame + playbackAdvance);
            }

            if (!TryFindPlaybackSampleWindow(_playbackFrame, out var from, out var to))
            {
                batch = default;
                return false;
            }

            bool isContinuousSample;
            if (useTransientBuffer)
            {
                batch = _samplingPolicy.SampleTransient(in from, in to, _playbackFrame, out isContinuousSample);
            }
            else
            {
                batch = _samplingPolicy.Sample(in from, in to, _playbackFrame, out isContinuousSample);
            }

            batch = ApplyLowFrequencyTransforms(in batch, _playbackFrame, useTransientBuffer, ref isContinuousSample);

            if (isContinuousSample)
            {
                return true;
            }

            var batchKey = ShooterSnapshotViewBatchKey.From(in batch);
            if (_hasLastSampledBatchKey && batchKey.Equals(_lastSampledBatchKey))
            {
                return false;
            }

            _lastSampledBatchKey = batchKey;
            _hasLastSampledBatchKey = true;
            return true;
        }

        public void Reset()
        {
            ReleaseStoredBatches();
            Array.Clear(_buffer, 0, _buffer.Length);
            _start = 0;
            _count = 0;
            _playbackFrame = 0f;
            _interpolationDelayFrames = _targetInterpolationDelayFrames;
            _lastSampledBatchKey = default;
            _hasLastSampledBatchKey = false;
            _playbackInitialized = false;
            _isPrebuffering = false;
            _playbackSearchIndex = 0;
            _transformTracks.Clear();
            _sampledTransformKeys.Clear();
            _batchEntityKeys.Clear();
            _tracksToRemove.Clear();
            _transientTransformBuffer.Clear();
        }

        private float ConvergePlaybackDelay(float playbackAdvance)
        {
            if (playbackAdvance <= 0f || _interpolationDelayFrames == _targetInterpolationDelayFrames)
            {
                return playbackAdvance;
            }

            var convergenceRate = Math.Max(0f, Math.Min(1f, DelayConvergenceRate));
            var maxDelayStep = playbackAdvance * convergenceRate;
            if (maxDelayStep <= 0f)
            {
                return playbackAdvance;
            }

            if (_interpolationDelayFrames < _targetInterpolationDelayFrames)
            {
                var delayStep = Math.Min(
                    maxDelayStep,
                    _targetInterpolationDelayFrames - _interpolationDelayFrames);
                _interpolationDelayFrames += delayStep;
                return Math.Max(0f, playbackAdvance - delayStep);
            }

            var catchUpStep = Math.Min(
                maxDelayStep,
                _interpolationDelayFrames - _targetInterpolationDelayFrames);
            _interpolationDelayFrames -= catchUpStep;
            return playbackAdvance + catchUpStep;
        }

        private bool TryFindSampleWindow(float playbackFrame, out ShooterSnapshotViewBatch from, out ShooterSnapshotViewBatch to)
        {
            if (_count == 0)
            {
                from = default;
                to = default;
                return false;
            }

            from = GetAt(0);
            to = from;
            for (var i = 0; i < _count; i++)
            {
                var candidate = GetAt(i);
                if (candidate.Frame > playbackFrame)
                {
                    to = candidate;
                    return true;
                }

                from = candidate;
                to = candidate;
            }

            return true;
        }

        private bool TryFindPlaybackSampleWindow(float playbackFrame, out ShooterSnapshotViewBatch from, out ShooterSnapshotViewBatch to)
        {
            if (_count == 0)
            {
                from = default;
                to = default;
                return false;
            }

            var searchIndex = Math.Min(_playbackSearchIndex, _count - 1);
            if (GetAt(searchIndex).Frame > playbackFrame)
            {
                searchIndex = 0;
            }

            from = GetAt(searchIndex);
            to = from;
            var fromIndex = searchIndex;
            for (var i = searchIndex; i < _count; i++)
            {
                var candidate = GetAt(i);
                if (candidate.Frame > playbackFrame)
                {
                    to = candidate;
                    _playbackSearchIndex = fromIndex;
                    return true;
                }

                from = candidate;
                to = candidate;
                fromIndex = i;
            }

            _playbackSearchIndex = fromIndex;
            return true;
        }

        private void Store(in ShooterSnapshotViewBatch batch)
        {
            ObserveTransformTracks(in batch);

            var insertIndex = (_start + _count) % _buffer.Length;
            if (_count == _buffer.Length)
            {
                insertIndex = _start;
                _buffer[insertIndex].ReleasePooledResources();
                _start = (_start + 1) % _buffer.Length;
                _playbackSearchIndex = Math.Max(0, _playbackSearchIndex - 1);
            }
            else
            {
                _count++;
            }

            _buffer[insertIndex] = batch;
        }

        private void ObserveTransformTracks(in ShooterSnapshotViewBatch batch)
        {
            if (batch.ShouldReplaceMissingEntities)
            {
                // 全量基线的语义是"权威集合替换"：裁剪掉不在基线中的实体轨道（防残留泄漏），
                // 但保留仍在场实体的轨道并继续喂样本——无差别 Clear 会让全部实体从基线姿势
                // 集体重启，表现为同帧整批实体统一距离跳变（拉扯）。
                PruneTracksToBatchEntities(in batch);
            }

            for (var i = 0; i < batch.TransformChanges.Count; i++)
            {
                var transform = batch.TransformChanges[i];
                _transformTracks.Observe(transform.Key, batch.Frame, in transform, transform.DeliveryHints);
            }

            for (var i = 0; i < batch.RemovedEntities.Count; i++)
            {
                _transformTracks.Remove(batch.RemovedEntities[i]);
            }

            for (var i = 0; i < batch.EntityChanges.Count; i++)
            {
                var entity = batch.EntityChanges[i];
                if (!entity.Alive)
                {
                    _transformTracks.Remove(entity.Key);
                }
            }
        }

        private void PruneTracksToBatchEntities(in ShooterSnapshotViewBatch batch)
        {
            if (_transformTracks.Count == 0)
            {
                return;
            }

            _batchEntityKeys.Clear();
            for (var i = 0; i < batch.TransformChanges.Count; i++)
            {
                _batchEntityKeys.Add(batch.TransformChanges[i].Key);
            }

            for (var i = 0; i < batch.EntityChanges.Count; i++)
            {
                _batchEntityKeys.Add(batch.EntityChanges[i].Key);
            }

            var keys = _transformTracks.GetKeyEnumerator();
            while (keys.MoveNext())
            {
                if (!_batchEntityKeys.Contains(keys.Current))
                {
                    _tracksToRemove.Add(keys.Current);
                }
            }

            for (var i = 0; i < _tracksToRemove.Count; i++)
            {
                _transformTracks.Remove(_tracksToRemove[i]);
            }

            _tracksToRemove.Clear();
        }

        private ShooterSnapshotViewBatch ApplyLowFrequencyTransforms(
            in ShooterSnapshotViewBatch batch,
            float playbackFrame,
            bool useTransientBuffer,
            ref bool isContinuousSample)
        {
            if (_transformTracks.Count == 0)
            {
                return batch;
            }

            var source = batch.TransformChanges;
            _sampledTransformKeys.Clear();
            var replacementCount = 0;
            for (var i = 0; i < source.Count; i++)
            {
                var key = source[i].Key;
                _sampledTransformKeys.Add(key);
                if (_transformTracks.TrySample(key, playbackFrame, out _, out _))
                {
                    replacementCount++;
                }
            }

            var appendedCount = 0;
            var trackKeys = _transformTracks.GetKeyEnumerator();
            while (trackKeys.MoveNext())
            {
                var key = trackKeys.Current;
                if (!_sampledTransformKeys.Contains(key) &&
                    _transformTracks.TrySample(key, playbackFrame, out _, out _))
                {
                    appendedCount++;
                }
            }

            if (replacementCount == 0 && appendedCount == 0)
            {
                return batch;
            }

            var outputCount = source.Count + appendedCount;
            IReadOnlyList<ShooterViewTransformComponentChange> sampledTransforms;
            if (useTransientBuffer)
            {
                _transientTransformBuffer.Resize(outputCount);
                FillLowFrequencyTransforms(source, playbackFrame, _transientTransformBuffer);
                sampledTransforms = _transientTransformBuffer;
            }
            else
            {
                var owned = new ShooterViewTransformComponentChange[outputCount];
                FillLowFrequencyTransforms(source, playbackFrame, owned);
                sampledTransforms = owned;
            }

            isContinuousSample = true;
            return new ShooterSnapshotViewBatch(
                batch.WorldId,
                batch.Frame,
                batch.Sequence,
                batch.SnapshotKind,
                batch.Source,
                batch.EntityChanges,
                batch.RemovedEntities,
                sampledTransforms,
                batch.HealthChanges,
                batch.ScoreChanges,
                batch.ProjectileLifetimeChanges,
                batch.Events,
                batch.SampleFrame);
        }

        private void FillLowFrequencyTransforms(
            IReadOnlyList<ShooterViewTransformComponentChange> source,
            float playbackFrame,
            IList<ShooterViewTransformComponentChange> destination)
        {
            var outputIndex = 0;
            for (var i = 0; i < source.Count; i++)
            {
                var transform = source[i];
                if (_transformTracks.TrySample(transform.Key, playbackFrame, out var sampled, out _))
                {
                    transform = sampled;
                }

                destination[outputIndex++] = transform;
            }

            var trackKeys = _transformTracks.GetKeyEnumerator();
            while (trackKeys.MoveNext())
            {
                var key = trackKeys.Current;
                if (!_sampledTransformKeys.Contains(key) &&
                    _transformTracks.TrySample(key, playbackFrame, out var sampled, out _))
                {
                    destination[outputIndex++] = sampled;
                }
            }
        }

        private void ReleaseStoredBatches()
        {
            for (var i = 0; i < _count; i++)
            {
                GetAt(i).ReleasePooledResources();
            }
        }

        private ShooterSnapshotViewBatch GetAt(int index)
        {
            return _buffer[(_start + index) % _buffer.Length];
        }

        private static ShooterViewTransformComponentChange InterpolateTransform(
            in ShooterViewTransformComponentChange from,
            in ShooterViewTransformComponentChange to,
            float t)
        {
            return new ShooterViewTransformComponentChange(
                from.Key,
                Lerp(from.X, to.X, t),
                Lerp(from.Y, to.Y, t),
                Lerp(from.FacingX, to.FacingX, t),
                Lerp(from.FacingY, to.FacingY, t),
                Lerp(from.VelocityX, to.VelocityX, t),
                Lerp(from.VelocityY, to.VelocityY, t),
                from.DeliveryHints | to.DeliveryHints);
        }

        private ShooterViewTransformComponentChange ExtrapolateTransform(
            in ShooterViewTransformComponentChange sample,
            double deltaFrames)
        {
            var deltaSeconds = (float)(deltaFrames / Math.Max(1f, PlaybackFramesPerSecond));
            return new ShooterViewTransformComponentChange(
                sample.Key,
                sample.X + (sample.VelocityX * deltaSeconds),
                sample.Y + (sample.VelocityY * deltaSeconds),
                sample.FacingX,
                sample.FacingY,
                sample.VelocityX,
                sample.VelocityY,
                sample.DeliveryHints);
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }

        private sealed class ReusableTransformList : IList<ShooterViewTransformComponentChange>, IReadOnlyList<ShooterViewTransformComponentChange>
        {
            private ShooterViewTransformComponentChange[] _items = Array.Empty<ShooterViewTransformComponentChange>();

            public int Count { get; private set; }

            public bool IsReadOnly => false;

            public ShooterViewTransformComponentChange this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[index];
                }
                set
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    _items[index] = value;
                }
            }

            public void Resize(int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (_items.Length < count)
                {
                    var capacity = Math.Max(count, Math.Max(16, _items.Length * 2));
                    _items = new ShooterViewTransformComponentChange[capacity];
                }

                Count = count;
            }

            public void Clear()
            {
                Count = 0;
            }

            public IEnumerator<ShooterViewTransformComponentChange> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return _items[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int IndexOf(ShooterViewTransformComponentChange item) => throw new NotSupportedException();
            public void Insert(int index, ShooterViewTransformComponentChange item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            public void Add(ShooterViewTransformComponentChange item) => throw new NotSupportedException();
            public bool Contains(ShooterViewTransformComponentChange item) => throw new NotSupportedException();
            public void CopyTo(ShooterViewTransformComponentChange[] array, int arrayIndex) => throw new NotSupportedException();
            public bool Remove(ShooterViewTransformComponentChange item) => throw new NotSupportedException();
        }

        private readonly struct ShooterSnapshotViewBatchKey : IEquatable<ShooterSnapshotViewBatchKey>
        {
            private ShooterSnapshotViewBatchKey(ulong sequence, int frame, ShooterViewBatchSource source, ShooterViewSnapshotKind snapshotKind)
            {
                Sequence = sequence;
                Frame = frame;
                Source = source;
                SnapshotKind = snapshotKind;
            }

            private ulong Sequence { get; }

            private int Frame { get; }

            private ShooterViewBatchSource Source { get; }

            private ShooterViewSnapshotKind SnapshotKind { get; }

            public static ShooterSnapshotViewBatchKey From(in ShooterSnapshotViewBatch batch)
            {
                return new ShooterSnapshotViewBatchKey(batch.Sequence, batch.Frame, batch.Source, batch.SnapshotKind);
            }

            public bool Equals(ShooterSnapshotViewBatchKey other)
            {
                return Sequence == other.Sequence &&
                    Frame == other.Frame &&
                    Source == other.Source &&
                    SnapshotKind == other.SnapshotKind;
            }

            public override bool Equals(object? obj)
            {
                return obj is ShooterSnapshotViewBatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Sequence, Frame, Source, SnapshotKind);
            }
        }
    }
}

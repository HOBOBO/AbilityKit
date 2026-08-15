#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime
{
    /// <summary>使用归一化 alpha 在两个样本之间插值。</summary>
    public delegate TSample SparseSnapshotInterpolator<TSample>(
        in TSample from,
        in TSample to,
        float alpha);

    /// <summary>描述稀疏轨道采样结果的生成方式。</summary>
    public enum SparseSnapshotSampleKind
    {
        /// <summary>请求时间点没有可用的稀疏样本。</summary>
        None = 0,

        /// <summary>保持某个权威端点样本。</summary>
        Held = 1,

        /// <summary>结果由两个权威样本插值得到。</summary>
        Interpolated = 2
    }

    /// <summary>
    /// 面向稀疏状态流的有界逐实体历史。此类状态流会在中间快照中主动省略未变化或低频实体。
    /// 每条轨道只保留最近两个样本；基线重置与实体移除由所属数据流负责。
    /// </summary>
    public sealed class SparseSnapshotTrackBuffer<TKey, TSample>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, Track> _tracks;
        private readonly SparseSnapshotInterpolator<TSample> _interpolate;

        /// <summary>使用默认键比较器创建缓冲区。</summary>
        public SparseSnapshotTrackBuffer(SparseSnapshotInterpolator<TSample> interpolate)
            : this(interpolate, comparer: null)
        {
        }

        /// <summary>使用调用方提供的键比较器创建缓冲区。</summary>
        public SparseSnapshotTrackBuffer(
            SparseSnapshotInterpolator<TSample> interpolate,
            IEqualityComparer<TKey>? comparer)
        {
            _interpolate = interpolate ?? throw new ArgumentNullException(nameof(interpolate));
            _tracks = new Dictionary<TKey, Track>(comparer);
        }

        /// <summary>当前保留的实体轨道数量。</summary>
        public int Count => _tracks.Count;

        /// <summary>
        /// 当样本时间不早于已保留端点时，添加或替换实体样本。
        /// </summary>
        /// <returns>样本早于已保留端点时返回 <c>false</c>。</returns>
        public bool Observe(
            TKey key,
            long timelineTicks,
            in TSample sample,
            SnapshotDeliveryHints hints)
        {
            if (!_tracks.TryGetValue(key, out var track))
            {
                track = default;
            }

            if (!track.Observe(timelineTicks, in sample, hints))
            {
                return false;
            }

            _tracks[key] = track;
            return true;
        }

        /// <summary>在指定时间线位置采样实体轨道，该位置可以包含小数。</summary>
        public bool TrySample(
            TKey key,
            double targetTimelineTicks,
            out TSample sample,
            out SparseSnapshotSampleKind sampleKind)
        {
            if (!_tracks.TryGetValue(key, out var track))
            {
                sample = default!;
                sampleKind = SparseSnapshotSampleKind.None;
                return false;
            }

            return track.TrySample(targetTimelineTicks, _interpolate, out sample, out sampleKind);
        }

        /// <summary>移除一条实体轨道。</summary>
        public bool Remove(TKey key)
        {
            return _tracks.Remove(key);
        }

        /// <summary>移除全部实体轨道。</summary>
        public void Clear()
        {
            _tracks.Clear();
        }

        /// <summary>返回用于遍历已保留实体键的结构体枚举器。</summary>
        public KeyEnumerator GetKeyEnumerator()
        {
            return new KeyEnumerator(_tracks.Keys.GetEnumerator());
        }

        /// <summary>遍历已保留实体键的无分配枚举器。</summary>
        public struct KeyEnumerator
        {
            private Dictionary<TKey, Track>.KeyCollection.Enumerator _enumerator;

            internal KeyEnumerator(Dictionary<TKey, Track>.KeyCollection.Enumerator enumerator)
            {
                _enumerator = enumerator;
            }

            /// <summary>当前已保留的实体键。</summary>
            public TKey Current => _enumerator.Current;

            /// <summary>移动到下一个已保留实体键。</summary>
            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }
        }

        internal struct Track
        {
            private long _previousTimelineTicks;
            private long _latestTimelineTicks;
            private TSample _previous;
            private TSample _latest;
            private bool _hasPrevious;
            private bool _hasLatest;
            private bool _isSparse;
            private bool _noInterpolation;

            public bool Observe(
                long timelineTicks,
                in TSample sample,
                SnapshotDeliveryHints hints)
            {
                if (!_hasLatest)
                {
                    ApplyHints(hints);
                    _latestTimelineTicks = timelineTicks;
                    _latest = sample;
                    _hasLatest = true;
                    return true;
                }

                if (timelineTicks < _latestTimelineTicks)
                {
                    return false;
                }

                ApplyHints(hints);
                if (timelineTicks == _latestTimelineTicks)
                {
                    _latest = sample;
                    return true;
                }

                _previousTimelineTicks = _latestTimelineTicks;
                _previous = _latest;
                _hasPrevious = true;
                _latestTimelineTicks = timelineTicks;
                _latest = sample;
                return true;
            }

            private void ApplyHints(SnapshotDeliveryHints hints)
            {
                _isSparse |= (hints & SnapshotDeliveryHints.SparseUpdate) != 0;
                _noInterpolation = (hints & (SnapshotDeliveryHints.Teleport | SnapshotDeliveryHints.NoInterpolation)) != 0;
            }

            public bool TrySample(
                double targetTimelineTicks,
                SparseSnapshotInterpolator<TSample> interpolate,
                out TSample sample,
                out SparseSnapshotSampleKind sampleKind)
            {
                if (!_isSparse || !_hasLatest ||
                    (_hasPrevious && targetTimelineTicks < _previousTimelineTicks) ||
                    (!_hasPrevious && targetTimelineTicks < _latestTimelineTicks))
                {
                    sample = default!;
                    sampleKind = SparseSnapshotSampleKind.None;
                    return false;
                }

                if (!_hasPrevious || targetTimelineTicks >= _latestTimelineTicks)
                {
                    sample = _latest;
                    sampleKind = SparseSnapshotSampleKind.Held;
                    return true;
                }

                if (_noInterpolation)
                {
                    sample = _previous;
                    sampleKind = SparseSnapshotSampleKind.Held;
                    return true;
                }

                var alpha = (float)((targetTimelineTicks - _previousTimelineTicks) /
                    (double)(_latestTimelineTicks - _previousTimelineTicks));
                sample = interpolate(in _previous, in _latest, alpha);
                sampleKind = SparseSnapshotSampleKind.Interpolated;
                return true;
            }
        }
    }
}

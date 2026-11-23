using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Buffer
{
    /// <summary>
    /// 按帧缓存世界快照，保存和读取均隔离副本；不承担网络准入或基线重建。
    /// </summary>
    public sealed class SnapshotBuffer : IBufferCapacityControl
    {
        private readonly IFrameIndexedBuffer<WorldStateSnapshot> _storage;
        private readonly object _lock = new object();

        /// <summary>
        /// 返回读取时保留的帧数。
        /// </summary>
        public int Count
        {
            get { lock (_lock) return _storage.Count; }
        }

        /// <summary>
        /// 返回当前帧容量，与 <see cref="Capacity"/> 等价。
        /// </summary>
        public int MaxBufferSize => Capacity;

        /// <summary>
        /// 返回当前帧容量，不表示毫秒或网络窗口。
        /// </summary>
        public int Capacity
        {
            get { lock (_lock) return _storage.Capacity; }
        }

        /// <summary>
        /// 创建按保留帧数限制的稀疏缓冲，默认容量为 128 帧。
        /// </summary>
        public SnapshotBuffer(int maxBufferSize = 128)
            : this(new SparseFrameIndexedBuffer<WorldStateSnapshot>(maxBufferSize))
        {
        }

        /// <summary>
        /// 使用指定的底层缓冲；使用期间不得从外部并发操作该实例。
        /// </summary>
        public SnapshotBuffer(IFrameIndexedBuffer<WorldStateSnapshot> storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// 将快照的副本保存到指定帧；不核对快照内部帧号。
        /// </summary>
        public void Store(int frame, WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                _storage.Store(frame, snapshot.Clone());
            }
        }

        /// <summary>
        /// 读取独立副本；帧缺失时返回失败和空值。
        /// </summary>
        public bool TryGet(int frame, out WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_storage.TryGet(frame, out var s))
                {
                    snapshot = s.Clone();
                    return true;
                }
                snapshot = null;
                return false;
            }
        }

        /// <summary>
        /// 返回相应帧的副本，或在未命中时返回空值。
        /// </summary>
        public WorldStateSnapshot Get(int frame)
        {
            TryGet(frame, out var snapshot);
            return snapshot;
        }

        /// <summary>
        /// 检查指定帧是否保留，不复制快照。
        /// </summary>
        public bool Contains(int frame)
        {
            lock (_lock)
            {
                return _storage.Contains(frame);
            }
        }

        /// <summary>
        /// 返回帧号升序的独立列表。
        /// </summary>
        public IReadOnlyList<int> GetCapturedFrames()
        {
            lock (_lock)
            {
                var result = new int[_storage.Count];
                for (var index = 0; index < result.Length; index++)
                {
                    result[index] = _storage.GetFrameAt(index);
                }
                return result;
            }
        }

        /// <summary>
        /// 返回保留的最大帧号；空缓冲返回负一。
        /// </summary>
        public int GetLatestFrame()
        {
            lock (_lock)
            {
                return _storage.Count > 0 ? _storage.GetFrameAt(_storage.Count - 1) : -1;
            }
        }

        /// <summary>
        /// 返回保留的最小帧号；空缓冲返回负一。
        /// </summary>
        public int GetEarliestFrame()
        {
            lock (_lock)
            {
                return _storage.Count > 0 ? _storage.GetFrameAt(0) : -1;
            }
        }

        /// <summary>
        /// 清空所有快照，不更改容量。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _storage.Clear();
            }
        }

        /// <summary>
        /// 移除指定帧，返回是否存在并被移除。
        /// </summary>
        public bool Remove(int frame)
        {
            lock (_lock)
            {
                return _storage.Remove(frame);
            }
        }

        /// <summary>
        /// 移除严格早于给定帧的快照。
        /// </summary>
        public void RemoveBefore(int frame)
        {
            lock (_lock)
            {
                _storage.RemoveBefore(frame);
            }
        }

        /// <summary>
        /// 移除严格晚于给定帧的快照。
        /// </summary>
        public void RemoveAfter(int frame)
        {
            lock (_lock)
            {
                _storage.RemoveAfter(frame);
            }
        }

        /// <summary>
        /// 尝试改变帧容量；非正容量直接拒绝，其余由底层缓冲处理。
        /// </summary>
        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;

            lock (_lock)
            {
                return _storage.TrySetCapacity(capacity);
            }
        }

        internal void CopyCapturedFrames(List<int> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            lock (_lock)
            {
                destination.Clear();
                for (var index = 0; index < _storage.Count; index++)
                {
                    destination.Add(_storage.GetFrameAt(index));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    /// <summary>
    /// 快照存储管理器
    /// 负责持久化保存实体的快照
    /// </summary>
    public sealed class SnapshotStorage
    {
        private readonly Dictionary<long, IContextSnapshot> _snapshots = new();
        private readonly Dictionary<long, List<long>> _bySource = new();
        private readonly Dictionary<long, List<long>> _byOwner = new();

        private readonly object _lock = new();

        /// <summary>
        /// 保存快照
        /// </summary>
        public void Save(IContextSnapshot snapshot)
        {
            lock (_lock)
            {
                _snapshots[snapshot.EntityId] = snapshot;

                // 按来源索引
                if (snapshot is ISourceContext src && src.SourceEntityId > 0)
                {
                    if (!_bySource.TryGetValue(src.SourceEntityId, out var sourceList))
                    {
                        sourceList = new List<long>();
                        _bySource[src.SourceEntityId] = sourceList;
                    }
                    sourceList.Add(snapshot.EntityId);
                }

                // 按 Owner 索引
                if (snapshot is IOwnerContext owner && owner.OwnerEntityId > 0)
                {
                    if (!_byOwner.TryGetValue(owner.OwnerEntityId, out var ownerList))
                    {
                        ownerList = new List<long>();
                        _byOwner[owner.OwnerEntityId] = ownerList;
                    }
                    ownerList.Add(snapshot.EntityId);
                }
            }
        }

        /// <summary>
        /// 获取快照
        /// </summary>
        public IContextSnapshot? Get(long entityId)
        {
            lock (_lock)
            {
                return _snapshots.TryGetValue(entityId, out var snapshot) ? snapshot : null;
            }
        }

        /// <summary>
        /// 获取指定来源的快照
        /// </summary>
        public IEnumerable<IContextSnapshot> GetBySource(long sourceEntityId)
        {
            lock (_lock)
            {
                if (!_bySource.TryGetValue(sourceEntityId, out var ids))
                    return Enumerable.Empty<IContextSnapshot>();

                return ids
                    .Where(_snapshots.ContainsKey)
                    .Select(id => _snapshots[id])
                    .ToList();
            }
        }

        /// <summary>
        /// 获取指定 Owner 的快照
        /// </summary>
        public IEnumerable<IContextSnapshot> GetByOwner(long ownerEntityId)
        {
            lock (_lock)
            {
                if (!_byOwner.TryGetValue(ownerEntityId, out var ids))
                    return Enumerable.Empty<IContextSnapshot>();

                return ids
                    .Where(_snapshots.ContainsKey)
                    .Select(id => _snapshots[id])
                    .ToList();
            }
        }

        /// <summary>
        /// 标记快照对应的实体已销毁
        /// </summary>
        public void MarkDestroyed(long entityId)
        {
            lock (_lock)
            {
                if (_snapshots.TryGetValue(entityId, out var snapshot) && snapshot is IDestroyableSnapshot ds)
                {
                    ds.MarkDestroyed();
                }
            }
        }

        /// <summary>
        /// 移除快照
        /// </summary>
        public bool Remove(long entityId)
        {
            lock (_lock)
            {
                if (!_snapshots.TryGetValue(entityId, out var snapshot))
                    return false;

                _snapshots.Remove(entityId);

                if (snapshot is ISourceContext src && src.SourceEntityId > 0)
                {
                    if (_bySource.TryGetValue(src.SourceEntityId, out var sourceList))
                        sourceList.Remove(entityId);
                }

                if (snapshot is IOwnerContext owner && owner.OwnerEntityId > 0)
                {
                    if (_byOwner.TryGetValue(owner.OwnerEntityId, out var ownerList))
                        ownerList.Remove(entityId);
                }

                return true;
            }
        }

        /// <summary>
        /// 清空所有快照
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                _bySource.Clear();
                _byOwner.Clear();
            }
        }

        /// <summary>
        /// 快照数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _snapshots.Count;
                }
            }
        }
    }

    /// <summary>
    /// 快照来源接口
    /// </summary>
    public interface ISourceContext
    {
        long SourceEntityId { get; }
    }

    /// <summary>
    /// 快照所有者接口
    /// </summary>
    public interface IOwnerContext
    {
        long OwnerEntityId { get; }
    }

    /// <summary>
    /// 可销毁快照接口
    /// </summary>
    public interface IDestroyableSnapshot
    {
        bool IsDestroyed { get; }
        void MarkDestroyed();
    }
}

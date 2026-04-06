using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 轻量级实体组件世界。
    /// 
    /// 设计特点：
    /// - 线程安全注释（实际为单线程使用模型）
    /// - 零分配查询API
    /// - 支持值类型和引用类型组件
    /// - 支持父子层级关系
    /// - 可注入的组件注册表和事件总线
    /// </summary>
    public sealed class EntityWorld : IECWorld
    {
        #region 字段

        private readonly IComponentRegistry _componentRegistry;
        private readonly IWorldEventBus _events;
        private readonly int _maxCapacity;
        private readonly Action<string> _logHandler;

        // 实体数据存储
        private int[] _versions = Array.Empty<int>();
        private bool[] _alive = Array.Empty<bool>();
        private int[] _parentIndex = Array.Empty<int>();
        private object[][] _components = Array.Empty<object[]>();
        private List<int>[] _children = Array.Empty<List<int>>();
        private List<int>[] _childIds = Array.Empty<List<int>>();
        private Dictionary<int, int>[] _childIdToIndex = Array.Empty<Dictionary<int, int>>();

#if UNITY_EDITOR || DEBUG
        private string[] _names = Array.Empty<string>();
#endif

        // 空闲索引池（线程安全）
        private readonly System.Collections.Concurrent.ConcurrentStack<int> _freeIndices;

        // 组件索引（类型ID -> 实体索引集合）
        private readonly Dictionary<int, HashSet<int>> _componentIndex;

        // 列表对象池（用于查询快照）
        private readonly Pool<List<int>> _listPool;

        // 统计
        private int _aliveCount;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建实体世界。
        /// </summary>
        /// <param name="initialCapacity">初始容量，默认64。</param>
        /// <param name="maxCapacity">最大容量，默认int.MaxValue。</param>
        /// <param name="componentRegistry">组件注册表，null则使用全局共享注册表以确保类型ID一致性。</param>
        /// <param name="eventBus">事件总线，null则创建默认实例。</param>
        /// <param name="logHandler">日志处理器。</param>
        public EntityWorld(
            int initialCapacity = 64,
            int maxCapacity = int.MaxValue,
            IComponentRegistry componentRegistry = null,
            IWorldEventBus eventBus = null,
            Action<string> logHandler = null)
        {
            _maxCapacity = maxCapacity;
            _logHandler = logHandler;
            _componentRegistry = componentRegistry ?? ComponentRegistry.Shared;
            _events = eventBus ?? new WorldEventBus();
            _freeIndices = new System.Collections.Concurrent.ConcurrentStack<int>();
            _componentIndex = new Dictionary<int, HashSet<int>>();

            _listPool = Pool<List<int>>.Create(
                factory: () => new List<int>(16),
                onRelease: list => list.Clear(),
                defaultCapacity: 16,
                maxSize: 256
            );

            Allocate(initialCapacity);
        }

        #endregion

        #region IECWorld 成员

        public IWorldEventBus Events => _events;
        public int AliveCount => _aliveCount;
        public int TotalCapacity => _versions.Length;

        public IEntity Create()
        {
            var id = AllocateEntity();
            var entity = new IEntity(this, id);
            _events.Publish(new EntityCreated(id, entity, null));
            return entity;
        }

        public IEntity Create(string name)
        {
            var id = AllocateEntity();
            var entity = new IEntity(this, id);

#if UNITY_EDITOR || DEBUG
            _names[id.Index] = name;
#endif

            _events.Publish(new EntityCreated(id, entity, name));
            return entity;
        }

        public IEntity CreateChild(IEntity parent)
        {
            var child = Create();
            SetParent(child.Id, parent.Id);
            return child;
        }

        public IEntity CreateChild(IEntity parent, int logicalChildId)
        {
            var child = Create();
            SetParent(child.Id, parent.Id, logicalChildId);
            return child;
        }

        public void Destroy(IEntityId id)
        {
            if (!TryValidateId(id)) return;
            InternalDestroy(id, recursive: false);
        }

        public void DestroyRecursive(IEntityId id)
        {
            if (!TryValidateId(id)) return;
            InternalDestroy(id, recursive: true);
        }

        public bool IsAlive(IEntityId id)
        {
            if (id.Index < 0 || id.Index >= _versions.Length) return false;
            return _alive[id.Index] && _versions[id.Index] == id.Version;
        }

        public IEntity Wrap(IEntityId id)
        {
            if (!TryValidateId(id))
                throw new ArgumentException($"Invalid entity id: {id}");
            return new IEntity(this, id);
        }

        #endregion

        #region 组件操作

        public void SetComponent<T>(IEntityId id, T component) where T : struct
        {
            if (!TryValidateId(id)) return;
            var typeId = _componentRegistry.GetId<T>();
            SetComponentInternal(id.Index, typeId, component);
        }

        public void SetComponentRef<T>(IEntityId id, T component) where T : class
        {
            if (!TryValidateId(id)) return;
            if (component == null)
            {
                RemoveComponentById(id.Index, _componentRegistry.GetId<T>());
                return;
            }
            var typeId = _componentRegistry.GetId<T>();

            SetComponentInternal(id.Index, typeId, component);
        }

        public T GetComponent<T>(IEntityId id) where T : struct
        {
            if (!TryGetComponent<T>(id, out T result))
                throw new KeyNotFoundException($"Component {typeof(T)} not found on entity {id}");
            return result;
        }

        public T GetComponentRef<T>(IEntityId id) where T : class
        {
            TryGetComponentRef<T>(id, out var result);
            return result;
        }

        public bool TryGetComponent<T>(IEntityId id, out T component) where T : struct
        {
            component = default(T);
            if (!IsAlive(id)) return false;

            var typeId = _componentRegistry.GetId<T>();
            var store = _components[id.Index];
            if (store == null || typeId >= store.Length) return false;

            if (store[typeId] is T typed)
            {
                component = typed;
                return true;
            }
            return false;
        }

        public bool TryGetComponentRef<T>(IEntityId id, out T component) where T : class
        {
            component = default(T);
            if (!IsAlive(id)) return false;

            var typeId = _componentRegistry.GetId<T>();
            var store = _components[id.Index];
            if (store == null || typeId >= store.Length) return false;

            component = store[typeId] as T;
            return component != null;
        }

        public bool HasComponent<T>() where T : struct
        {
            var typeId = _componentRegistry.GetId<T>();
            if (!_componentIndex.TryGetValue(typeId, out var set) || set.Count == 0) return false;
            foreach (var index in set)
            {
                if (index < 0 || index >= _alive.Length) continue;
                if (!_alive[index]) continue;
                var id = new IEntityId(index, _versions[index]);
                if (IsAlive(id)) return true;
            }
            return false;
        }

        public bool RemoveComponent<T>(IEntityId id) where T : struct
        {
            if (!TryValidateId(id)) return false;
            return RemoveComponentById(id.Index, _componentRegistry.GetId<T>());
        }

        #endregion

        #region 查询

        public EntityQuery<T1> Query<T1>() where T1 : struct
        {
            return new EntityQuery<T1>(_componentRegistry.GetId<T1>(), this);
        }

        public EntityQuery<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct
        {
            return new EntityQuery<T1, T2>(
                _componentRegistry.GetId<T1>(),
                _componentRegistry.GetId<T2>(),
                this);
        }

        public EntityQuery<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : struct where T2 : struct where T3 : struct
        {
            return new EntityQuery<T1, T2, T3>(
                _componentRegistry.GetId<T1>(),
                _componentRegistry.GetId<T2>(),
                _componentRegistry.GetId<T3>(),
                this);
        }

        public void ForEachAlive(Action<IEntity> visitor)
        {
            for (int i = 0; i < _alive.Length; i++)
            {
                if (!_alive[i]) continue;
                var id = new IEntityId(i, _versions[i]);
                if (!IsAlive(id)) continue;
                visitor(new IEntity(this, id));
            }
        }

        public void ForEachComponent(IEntityId id, Action<int, object> visitor)
        {
            if (!IsAlive(id)) return;
            var store = _components[id.Index];
            if (store == null) return;

            for (int typeId = 0; typeId < store.Length; typeId++)
            {
                var c = store[typeId];
                if (c == null) continue;
                visitor(typeId, c);
            }
        }

        // ============ 内部查询实现（供 EntityQuery 调用）============

        internal void QueryImpl<T1>(int typeId1, Action<IEntity, T1> visitor) where T1 : struct
        {
            if (!_componentIndex.TryGetValue(typeId1, out var set) || set.Count == 0) return;

            var snapshot = _listPool.Get();
            try
            {
                snapshot.AddRange(set);
                foreach (var index in snapshot)
                {
                    if (index < 0 || index >= _alive.Length) continue;
                    if (!_alive[index]) continue;

                    var id = new IEntityId(index, _versions[index]);
                    if (!IsAlive(id)) continue;

                    var store = _components[index];
                    if (store == null || typeId1 >= store.Length) continue;

                    if (store[typeId1] is T1 comp)
                    {
                        visitor(new IEntity(this, id), comp);
                    }
                }
            }
            finally
            {
                _listPool.Release(snapshot);
            }
        }

        internal void QueryImpl<T1, T2>(int typeId1, int typeId2, Action<IEntity, T1, T2> visitor)
            where T1 : struct where T2 : struct
        {
            if (!_componentIndex.TryGetValue(typeId1, out var set) || set.Count == 0) return;

            var snapshot = _listPool.Get();
            try
            {
                snapshot.AddRange(set);
                foreach (var index in snapshot)
                {
                    if (index < 0 || index >= _alive.Length) continue;
                    if (!_alive[index]) continue;

                    var id = new IEntityId(index, _versions[index]);
                    if (!IsAlive(id)) continue;

                    var store = _components[index];
                    if (store == null) continue;
                    if (typeId1 >= store.Length || store[typeId1] == null) continue;
                    if (typeId2 >= store.Length || store[typeId2] == null) continue;

                    if (store[typeId1] is T1 comp1 && store[typeId2] is T2 comp2)
                    {
                        visitor(new IEntity(this, id), comp1, comp2);
                    }
                }
            }
            finally
            {
                _listPool.Release(snapshot);
            }
        }

        internal void QueryImpl<T1, T2, T3>(int typeId1, int typeId2, int typeId3, Action<IEntity, T1, T2, T3> visitor)
            where T1 : struct where T2 : struct where T3 : struct
        {
            if (!_componentIndex.TryGetValue(typeId1, out var set) || set.Count == 0) return;

            var snapshot = _listPool.Get();
            try
            {
                snapshot.AddRange(set);
                foreach (var index in snapshot)
                {
                    if (index < 0 || index >= _alive.Length) continue;
                    if (!_alive[index]) continue;

                    var id = new IEntityId(index, _versions[index]);
                    if (!IsAlive(id)) continue;

                    var store = _components[index];
                    if (store == null) continue;
                    if (typeId1 >= store.Length || store[typeId1] == null) continue;
                    if (typeId2 >= store.Length || store[typeId2] == null) continue;
                    if (typeId3 >= store.Length || store[typeId3] == null) continue;

                    if (store[typeId1] is T1 comp1 && store[typeId2] is T2 comp2 && store[typeId3] is T3 comp3)
                    {
                        visitor(new IEntity(this, id), comp1, comp2, comp3);
                    }
                }
            }
            finally
            {
                _listPool.Release(snapshot);
            }
        }

        #endregion

        #region 父子关系

        public IEntity GetParent(IEntityId id)
        {
            if (!TryValidateId(id)) return default(IEntity);
            var parentIndex = _parentIndex[id.Index];
            if (parentIndex < 0) return default(IEntity);
            var parentId = new IEntityId(parentIndex, _versions[parentIndex]);
            return IsAlive(parentId) ? new IEntity(this, parentId) : default(IEntity);
        }

        public void SetParent(IEntityId child, IEntityId parent)
        {
            if (!TryValidateId(child) || !TryValidateId(parent)) return;
            if (child.Index == parent.Index)
                throw new ArgumentException("Entity cannot be parent of itself");

            var oldParentIndex = _parentIndex[child.Index];
            if (oldParentIndex == parent.Index) return;

            // 从旧父级移除
            if (oldParentIndex >= 0 && _children[oldParentIndex] != null)
            {
                _children[oldParentIndex].Remove(child.Index);
            }

            // 添加到新父级
            _parentIndex[child.Index] = parent.Index;
            if (_children[parent.Index] == null)
            {
                _children[parent.Index] = _listPool.Get();
            }
            _children[parent.Index].Add(child.Index);

            _events.Publish(new ParentChanged(
                child,
                oldParentIndex >= 0 ? new IEntityId(oldParentIndex, _versions[oldParentIndex]) : default(IEntityId),
                parent
            ));
        }

        public void SetParent(IEntityId child, IEntityId parent, int logicalChildId)
        {
            if (!TryValidateId(child) || !TryValidateId(parent)) return;
            if (child.Index == parent.Index)
                throw new ArgumentException("Entity cannot be parent of itself");

            // 验证逻辑ID不重复
            if (_childIdToIndex[parent.Index] != null && _childIdToIndex[parent.Index].ContainsKey(logicalChildId))
                throw new InvalidOperationException($"Duplicate logicalChildId {logicalChildId} under parent {parent}");

            // 先从旧父级移除
            var oldParentIndex = _parentIndex[child.Index];
            if (oldParentIndex >= 0 && oldParentIndex != parent.Index)
            {
                RemoveChildLink(oldParentIndex, child.Index);
            }

            _parentIndex[child.Index] = parent.Index;

            if (_children[parent.Index] == null)
            {
                _children[parent.Index] = _listPool.Get();
            }
            if (_childIds[parent.Index] == null)
            {
                _childIds[parent.Index] = _listPool.Get();
            }
            if (_childIdToIndex[parent.Index] == null)
            {
                _childIdToIndex[parent.Index] = new Dictionary<int, int>();
            }

            _children[parent.Index].Add(child.Index);
            _childIds[parent.Index].Add(logicalChildId);
            _childIdToIndex[parent.Index][logicalChildId] = _children[parent.Index].Count - 1;

            _events.Publish(new ParentChanged(
                child,
                oldParentIndex >= 0 ? new IEntityId(oldParentIndex, _versions[oldParentIndex]) : default(IEntityId),
                parent
            ));
        }

        public int GetChildCount(IEntityId id)
        {
            if (!TryValidateId(id)) return 0;
            return _children[id.Index] != null ? _children[id.Index].Count : 0;
        }

        public IEntity GetChild(IEntityId id, int index)
        {
            if (!TryValidateId(id))
                throw new ArgumentException($"Invalid entity id: {id}");

            var list = _children[id.Index];
            if (list == null || index < 0 || index >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var childIndex = list[index];
            var childId = new IEntityId(childIndex, _versions[childIndex]);
            return new IEntity(this, childId);
        }

        public bool TryGetChildById(IEntityId parent, int logicalChildId, out IEntity child)
        {
            child = default(IEntity);
            if (!TryValidateId(parent)) return false;

            var map = _childIdToIndex[parent.Index];
            if (map == null || !map.TryGetValue(logicalChildId, out var idx)) return false;

            var list = _children[parent.Index];
            if (list == null || idx < 0 || idx >= list.Count) return false;

            var childIndex = list[idx];
            var childId = new IEntityId(childIndex, _versions[childIndex]);
            if (!IsAlive(childId)) return false;

            child = new IEntity(this, childId);
            return true;
        }

        #endregion

        #region 元数据

        public string GetName(IEntityId id)
        {
#if UNITY_EDITOR || DEBUG
            if (!TryValidateId(id)) return null;
            return _names[id.Index];
#else
            return null;
#endif
        }

        public void SetName(IEntityId id, string name)
        {
#if UNITY_EDITOR || DEBUG
            if (!TryValidateId(id)) return;
            _names[id.Index] = name;
#endif
        }

        #endregion

        #region 内部方法

        private IEntityId AllocateEntity()
        {
            int index;
            if (!_freeIndices.TryPop(out index))
            {
                if (_versions.Length >= _maxCapacity)
                    throw new InvalidOperationException($"Entity world capacity exceeded: {_maxCapacity}");
                index = Allocate(_versions.Length == 0 ? 64 : _versions.Length * 2);
            }

            var version = _versions[index];
            _alive[index] = true;
            _parentIndex[index] = -1;
            _aliveCount++;

            return new IEntityId(index, version);
        }

        private int Allocate(int newSize)
        {
            var oldSize = _versions.Length;

            Array.Resize(ref _versions, newSize);
            Array.Resize(ref _alive, newSize);
            Array.Resize(ref _parentIndex, newSize);
            Array.Resize(ref _children, newSize);
            Array.Resize(ref _childIds, newSize);
            Array.Resize(ref _childIdToIndex, newSize);
            Array.Resize(ref _components, newSize);

#if UNITY_EDITOR || DEBUG
            Array.Resize(ref _names, newSize);
#endif

            for (int i = oldSize; i < newSize; i++)
            {
                _versions[i] = 1;
                _alive[i] = false;
                _parentIndex[i] = -1;
            }

            return oldSize;
        }

        private bool TryValidateId(IEntityId id)
        {
            if (id.Index < 0 || id.Index >= _versions.Length)
            {
                _logHandler?.Invoke($"EntityWorld: Invalid entity id index: {id.Index}");
                return false;
            }
            if (!_alive[id.Index])
            {
                _logHandler?.Invoke($"EntityWorld: Entity already destroyed: {id}");
                return false;
            }
            if (_versions[id.Index] != id.Version)
            {
                _logHandler?.Invoke($"EntityWorld: Entity version mismatch: {id} (current: {_versions[id.Index]})");
                return false;
            }
            return true;
        }

        private void InternalDestroy(IEntityId id, bool recursive)
        {
            var index = id.Index;

            // 递归销毁子实体
            if (recursive)
            {
                var childList = _children[index];
                if (childList != null && childList.Count > 0)
                {
                    var snapshot = _listPool.Get();
                    snapshot.AddRange(childList);
                    try
                    {
                        foreach (var childIndex in snapshot)
                        {
                            if (childIndex < 0 || childIndex >= _versions.Length) continue;
                            var childId = new IEntityId(childIndex, _versions[childIndex]);
                            if (IsAlive(childId)) InternalDestroy(childId, recursive: true);
                        }
                    }
                    finally
                    {
                        _listPool.Release(snapshot);
                    }
                }
            }
            else
            {
                // 非递归：解除父子关系，但不销毁子实体
                var childList = _children[index];
                if (childList != null)
                {
                    foreach (var childIndex in childList)
                    {
                        if (childIndex >= 0 && childIndex < _parentIndex.Length)
                            _parentIndex[childIndex] = -1;
                    }
                }
            }

            // 从父级移除
            var parentIndex = _parentIndex[index];
            if (parentIndex >= 0 && _children[parentIndex] != null)
            {
                _children[parentIndex].Remove(index);
            }

            // 清理组件
            var store = _components[index];
            if (store != null)
            {
                for (int i = 0; i < store.Length; i++)
                {
                    if (store[i] == null) continue;
                    if (_componentIndex.TryGetValue(i, out var set))
                        set.Remove(index);
                }
                _components[index] = null;
            }

            // 清理子级列表
            var childListRef = _children[index];
            if (childListRef != null)
            {
                _listPool.Release(childListRef);
                _children[index] = null;
            }

            // 标记销毁
            _alive[index] = false;
            _versions[index]++;
            _aliveCount--;

            _freeIndices.Push(index);
            _events.Publish(new EntityDestroyed(id));
        }

        private void SetComponentInternal<T>(int entityIndex, int typeId, T component)
        {
            if (entityIndex < 0 || entityIndex >= _components.Length)
            {
                _logHandler?.Invoke($"EntityWorld.SetComponentInternal: entityIndex={entityIndex} out of bounds, _components.Length={_components.Length}");
                return;
            }
            if (typeId < 0)
            {
                _logHandler?.Invoke($"EntityWorld.SetComponentInternal: typeId={typeId} is negative! EntityIndex={entityIndex}");
                return;
            }
            var store = _components[entityIndex];

            // 延迟分配组件数组
            if (store == null)
            {
                store = new object[8];
                _components[entityIndex] = store;
            }

            // 确保 store 有足够的容量
            if (typeId >= store.Length)
            {
                var newSize = store.Length * 2;
                while (newSize <= typeId) newSize *= 2;
                Array.Resize(ref store, newSize);
                _components[entityIndex] = store;
            }

            var had = store[typeId] != null;
            store[typeId] = component;

            if (!had)
            {
                if (!_componentIndex.TryGetValue(typeId, out var set))
                {
                    set = new HashSet<int>();
                    _componentIndex[typeId] = set;
                }
                set.Add(entityIndex);
            }

            _events.Publish(new ComponentSet(
                new IEntityId(entityIndex, _versions[entityIndex]),
                typeId,
                component
            ));
        }

        private bool RemoveComponentById(int entityIndex, int typeId)
        {
            var store = _components[entityIndex];
            if (store == null || typeId >= store.Length || store[typeId] == null)
                return false;

            store[typeId] = null;

            if (_componentIndex.TryGetValue(typeId, out var set))
                set.Remove(entityIndex);

            _events.Publish(new ComponentRemoved(
                new IEntityId(entityIndex, _versions[entityIndex]),
                typeId
            ));

            return true;
        }

        private void RemoveChildLink(int parentIndex, int childIndex)
        {
            var list = _children[parentIndex];
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != childIndex) continue;

                list.RemoveAt(i);

                var ids = _childIds[parentIndex];
                var map = _childIdToIndex[parentIndex];
                if (ids != null && i < ids.Count)
                {
                    var removedChildId = ids[i];
                    ids.RemoveAt(i);
                    if (map != null) map.Remove(removedChildId);

                    if (map != null)
                    {
                        for (int k = i; k < list.Count; k++)
                        {
                            map[ids[k]] = k;
                        }
                    }
                }

                return;
            }
        }

        #endregion
    }
}

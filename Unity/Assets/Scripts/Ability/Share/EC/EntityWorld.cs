using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.EC
{
    public sealed class EntityWorld
    {
        private readonly Stack<int> _free = new Stack<int>();

        private int[] _versions = Array.Empty<int>();
        private bool[] _alive = Array.Empty<bool>();

        private int[] _parentIndex = Array.Empty<int>();
        private List<int>[] _children = Array.Empty<List<int>>();
        private List<int>[] _childIds = Array.Empty<List<int>>();
        private Dictionary<int, int>[] _childIdToIndex = Array.Empty<Dictionary<int, int>>();

        private object[][] _components = Array.Empty<object[]>();

        public Entity Create()
        {
            var index = _free.Count > 0 ? _free.Pop() : AllocateNewIndex();
            var version = _versions[index];

            _alive[index] = true;
            _parentIndex[index] = -1;
            _children[index]?.Clear();
            _components[index] = null;

            return new Entity(this, new EntityId(index, version));
        }

        public Entity CreateChild(EntityId parent)
        {
            EnsureAlive(parent);
            var child = Create();
            SetParent(child.Id, parent);
            return child;
        }

        public Entity CreateChild(EntityId parent, int childId)
        {
            EnsureAlive(parent);
            var child = Create();
            SetParent(child.Id, parent, childId);
            return child;
        }

        public Entity CreateChild<T1>(EntityId parent, int childId, T1 arg1, Action<Entity, T1> init)
        {
            var child = CreateChild(parent, childId);
            init?.Invoke(child, arg1);
            return child;
        }

        public Entity CreateChild<T1, T2>(EntityId parent, int childId, T1 arg1, T2 arg2, Action<Entity, T1, T2> init)
        {
            var child = CreateChild(parent, childId);
            init?.Invoke(child, arg1, arg2);
            return child;
        }

        public Entity CreateChild<T1, T2, T3>(EntityId parent, int childId, T1 arg1, T2 arg2, T3 arg3, Action<Entity, T1, T2, T3> init)
        {
            var child = CreateChild(parent, childId);
            init?.Invoke(child, arg1, arg2, arg3);
            return child;
        }

        public Entity CreateChild<T1, T2, T3, T4>(EntityId parent, int childId, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<Entity, T1, T2, T3, T4> init)
        {
            var child = CreateChild(parent, childId);
            init?.Invoke(child, arg1, arg2, arg3, arg4);
            return child;
        }

        public Entity CreateChild<T1, T2, T3, T4, T5>(EntityId parent, int childId, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, Action<Entity, T1, T2, T3, T4, T5> init)
        {
            var child = CreateChild(parent, childId);
            init?.Invoke(child, arg1, arg2, arg3, arg4, arg5);
            return child;
        }

        public bool IsAlive(EntityId id)
        {
            if (id.Index < 0 || id.Index >= _alive.Length) return false;
            return _alive[id.Index] && _versions[id.Index] == id.Version;
        }

        public void Destroy(EntityId id)
        {
            EnsureAlive(id);

            var index = id.Index;

            if (_parentIndex[index] != -1)
            {
                var p = _parentIndex[index];
                var list = _children[p];
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == index)
                        {
                            list.RemoveAt(i);
                            var ids = _childIds[p];
                            if (ids != null && i < ids.Count)
                            {
                                var removedChildId = ids[i];
                                ids.RemoveAt(i);

                                var map = _childIdToIndex[p];
                                if (map != null)
                                {
                                    map.Remove(removedChildId);
                                    for (int k = i; k < list.Count; k++)
                                    {
                                        var cid = ids != null && k < ids.Count ? ids[k] : 0;
                                        if (ids != null) map[cid] = k;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
                _parentIndex[index] = -1;
            }

            var childList = _children[index];
            if (childList != null && childList.Count > 0)
            {
                for (int i = 0; i < childList.Count; i++)
                {
                    var c = childList[i];
                    if (c >= 0 && c < _parentIndex.Length)
                    {
                        _parentIndex[c] = -1;
                    }
                }
                childList.Clear();
            }

            _components[index] = null;
            _alive[index] = false;
            _versions[index]++;
            _free.Push(index);
        }

        public void DestroyRecursive(EntityId id)
        {
            EnsureAlive(id);
            var index = id.Index;

            var childList = _children[index];
            if (childList != null && childList.Count > 0)
            {
                var snapshot = childList.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    var cIndex = snapshot[i];
                    if (cIndex < 0 || cIndex >= _versions.Length) continue;
                    var cid = new EntityId(cIndex, _versions[cIndex]);
                    if (!IsAlive(cid)) continue;
                    DestroyRecursive(cid);
                }
            }

            Destroy(id);
        }

        public Entity GetParent(EntityId id)
        {
            EnsureAlive(id);
            if (_parentIndex[id.Index] == -1) return default;

            var pIndex = _parentIndex[id.Index];
            return new Entity(this, new EntityId(pIndex, _versions[pIndex]));
        }

        public bool TryGetParent(EntityId id, out Entity parent)
        {
            if (!IsAlive(id))
            {
                parent = default;
                return false;
            }

            var pIndex = _parentIndex[id.Index];
            if (pIndex == -1)
            {
                parent = default;
                return false;
            }

            parent = new Entity(this, new EntityId(pIndex, _versions[pIndex]));
            return true;
        }

        public void SetParent(EntityId child, EntityId parent)
        {
            EnsureAlive(child);
            EnsureAlive(parent);

            if (child.Index == parent.Index) throw new InvalidOperationException("Entity cannot be parent of itself");

            var oldParent = _parentIndex[child.Index];
            if (oldParent == parent.Index) return;

            if (oldParent != -1)
            {
                var oldList = _children[oldParent];
                if (oldList != null)
                {
                    for (int i = 0; i < oldList.Count; i++)
                    {
                        if (oldList[i] == child.Index)
                        {
                            oldList.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            _parentIndex[child.Index] = parent.Index;
            var list = _children[parent.Index];
            if (list == null)
            {
                list = new List<int>(4);
                _children[parent.Index] = list;
            }
            list.Add(child.Index);
        }

        public void SetParent(EntityId child, EntityId parent, int childId)
        {
            EnsureAlive(child);
            EnsureAlive(parent);

            if (child.Index == parent.Index) throw new InvalidOperationException("Entity cannot be parent of itself");

            var map = _childIdToIndex[parent.Index];
            if (map == null)
            {
                map = new Dictionary<int, int>(4);
                _childIdToIndex[parent.Index] = map;
            }
            if (map.ContainsKey(childId)) throw new InvalidOperationException($"Duplicate childId {childId} under parent {parent}");

            // Remove from old parent first.
            var oldParent = _parentIndex[child.Index];
            if (oldParent != -1)
            {
                RemoveChildLink(oldParent, child.Index);
            }

            _parentIndex[child.Index] = parent.Index;

            var list = _children[parent.Index];
            if (list == null)
            {
                list = new List<int>(4);
                _children[parent.Index] = list;
            }
            var ids = _childIds[parent.Index];
            if (ids == null)
            {
                ids = new List<int>(4);
                _childIds[parent.Index] = ids;
            }

            var idx = list.Count;
            list.Add(child.Index);
            ids.Add(childId);
            map[childId] = idx;
        }

        public bool TryGetChildById(EntityId parent, int childId, out Entity child)
        {
            if (!IsAlive(parent))
            {
                child = default;
                return false;
            }

            var map = _childIdToIndex[parent.Index];
            if (map == null || !map.TryGetValue(childId, out var idx))
            {
                child = default;
                return false;
            }

            var list = _children[parent.Index];
            if (list == null || idx < 0 || idx >= list.Count)
            {
                child = default;
                return false;
            }

            var cIndex = list[idx];
            child = new Entity(this, new EntityId(cIndex, _versions[cIndex]));
            return IsAlive(child.Id);
        }

        public Entity GetChildById(EntityId parent, int childId)
        {
            if (TryGetChildById(parent, childId, out var child)) return child;
            throw new KeyNotFoundException($"Child not found: parent={parent} childId={childId}");
        }

        public int GetChildCount(EntityId id)
        {
            EnsureAlive(id);
            return _children[id.Index]?.Count ?? 0;
        }

        public Entity GetChild(EntityId id, int childIndex)
        {
            EnsureAlive(id);
            var list = _children[id.Index];
            if (list == null) throw new ArgumentOutOfRangeException(nameof(childIndex));
            if (childIndex < 0 || childIndex >= list.Count) throw new ArgumentOutOfRangeException(nameof(childIndex));

            var idx = list[childIndex];
            return new Entity(this, new EntityId(idx, _versions[idx]));
        }

        public void SetComponent<T>(EntityId id, T component) where T : class
        {
            EnsureAlive(id);
            var typeId = ComponentTypeId.Get<T>();

            var store = _components[id.Index];
            if (store == null || store.Length <= typeId)
            {
                var newSize = Math.Max(typeId + 1, store?.Length ?? 0);
                if (newSize < 8) newSize = 8;
                while (newSize <= typeId) newSize *= 2;

                var next = new object[newSize];
                if (store != null) Array.Copy(store, next, store.Length);
                store = next;
                _components[id.Index] = store;
            }

            store[typeId] = component;
        }

        public T GetComponent<T>(EntityId id) where T : class
        {
            EnsureAlive(id);
            if (TryGetComponent(id, out T c)) return c;
            throw new KeyNotFoundException(typeof(T).FullName);
        }

        public bool TryGetComponent<T>(EntityId id, out T component) where T : class
        {
            if (!IsAlive(id))
            {
                component = null;
                return false;
            }

            var typeId = ComponentTypeId.Get<T>();
            var store = _components[id.Index];
            if (store == null || typeId >= store.Length)
            {
                component = null;
                return false;
            }

            component = store[typeId] as T;
            return component != null;
        }

        public bool HasComponent<T>(EntityId id) where T : class
        {
            return TryGetComponent<T>(id, out _);
        }

        public bool RemoveComponent<T>(EntityId id) where T : class
        {
            EnsureAlive(id);
            var typeId = ComponentTypeId.Get<T>();
            var store = _components[id.Index];
            if (store == null || typeId >= store.Length) return false;

            if (store[typeId] == null) return false;
            store[typeId] = null;
            return true;
        }

        private int AllocateNewIndex()
        {
            var index = _versions.Length;
            var newLen = index == 0 ? 64 : index * 2;

            Array.Resize(ref _versions, newLen);
            Array.Resize(ref _alive, newLen);
            Array.Resize(ref _parentIndex, newLen);
            Array.Resize(ref _children, newLen);
            Array.Resize(ref _childIds, newLen);
            Array.Resize(ref _childIdToIndex, newLen);
            Array.Resize(ref _components, newLen);

            for (int i = index; i < newLen; i++)
            {
                _versions[i] = 1;
                _alive[i] = false;
                _parentIndex[i] = -1;
                _children[i] = null;
                _childIds[i] = null;
                _childIdToIndex[i] = null;
                _components[i] = null;
            }

            return index;
        }

        private void EnsureAlive(EntityId id)
        {
            if (!IsAlive(id)) throw new InvalidOperationException($"Entity is not alive: {id}");
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
                    map?.Remove(removedChildId);

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
    }
}

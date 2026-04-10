#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Core.Common.Pool
{
    public static class UnityPools
    {
        private readonly struct UnityPoolId : IEquatable<UnityPoolId>
        {
            public readonly int PrefabId;
            public readonly string Key;

            public UnityPoolId(int prefabId, string key)
            {
                PrefabId = prefabId;
                Key = key ?? string.Empty;
            }

            public bool Equals(UnityPoolId other)
            {
                return PrefabId == other.PrefabId && string.Equals(Key, other.Key, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is UnityPoolId other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PrefabId * 397) ^ StringComparer.Ordinal.GetHashCode(Key);
                }
            }
        }

        private sealed class PoolEntry
        {
            public ObjectPool<GameObject> Pool;
            public Transform Root;
            public PoolStats Stats => Pool.Stats;
        }

        private static readonly Dictionary<UnityPoolId, PoolEntry> _pools = new Dictionary<UnityPoolId, PoolEntry>();

        public static GameObject Spawn(GameObject prefab, Transform parent = null, Transform poolRoot = null, string key = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            var id = new UnityPoolId(prefab.GetInstanceID(), key);
            if (!_pools.TryGetValue(id, out var entry))
            {
                entry = CreateEntry(prefab, poolRoot, key, defaultCapacity, maxSize, collectionCheck);
                _pools.Add(id, entry);
            }

            var go = entry.Pool.Get();
            if (go == null) return null;

            if (!go.TryGetComponent<UnityPoolHandle>(out var handle)) handle = go.AddComponent<UnityPoolHandle>();
            handle.PrefabInstanceId = id.PrefabId;
            handle.Key = id.Key;

            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static void Despawn(GameObject go)
        {
            if (go == null) return;

            if (!go.TryGetComponent<UnityPoolHandle>(out var handle))
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            var id = new UnityPoolId(handle.PrefabInstanceId, handle.Key);
            if (!_pools.TryGetValue(id, out var entry))
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            entry.Pool.Release(go);
        }

        public static bool DestroyPool(GameObject prefab, string key = null, bool destroyInactive = true)
        {
            if (prefab == null) return false;

            var id = new UnityPoolId(prefab.GetInstanceID(), key);
            if (!_pools.TryGetValue(id, out var entry)) return false;

            _pools.Remove(id);
            entry.Pool.Clear(destroyInactive);
            return true;
        }

        public static void DestroyAll(bool destroyInactive = true)
        {
            foreach (var kv in _pools)
            {
                kv.Value?.Pool?.Clear(destroyInactive);
            }

            _pools.Clear();
        }

        public static bool TryGetStats(GameObject prefab, out PoolStats stats, string key = null)
        {
            if (prefab == null)
            {
                stats = default;
                return false;
            }

            var id = new UnityPoolId(prefab.GetInstanceID(), key);
            if (_pools.TryGetValue(id, out var entry))
            {
                stats = entry.Stats;
                return true;
            }

            stats = default;
            return false;
        }

        private static PoolEntry CreateEntry(GameObject prefab, Transform root, string key, int defaultCapacity, int maxSize, bool collectionCheck)
        {
            var options = new ObjectPoolOptions<GameObject>(() => UnityEngine.Object.Instantiate(prefab, root))
            {
                DefaultCapacity = defaultCapacity,
                MaxSize = maxSize,
                CollectionCheck = collectionCheck,
                OnGet = go =>
                {
                    if (go != null) go.SetActive(true);
                },
                OnRelease = go =>
                {
                    if (go != null)
                    {
                        go.SetActive(false);
                        if (root != null) go.transform.SetParent(root, false);
                    }
                },
                OnDestroy = go =>
                {
                    if (go != null) UnityEngine.Object.Destroy(go);
                }
            };

            var entry = new PoolEntry
            {
                Pool = new ObjectPool<GameObject>(options),
                Root = root
            };

            return entry;
        }
    }
}
#endif

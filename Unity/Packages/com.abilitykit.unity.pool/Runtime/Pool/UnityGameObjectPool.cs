#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace AbilityKit.Core.Common.Pool
{
    public sealed class UnityGameObjectPool
    {
        private readonly ObjectPool<GameObject> _pool;
        private readonly Transform _root;

        public UnityGameObjectPool(GameObject prefab, Transform root = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            _root = root;

            var options = new ObjectPoolOptions<GameObject>(() => UnityEngine.Object.Instantiate(prefab, _root))
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
                        if (_root != null) go.transform.SetParent(_root, false);
                    }
                },
                OnDestroy = go =>
                {
                    if (go != null) UnityEngine.Object.Destroy(go);
                }
            };

            _pool = new ObjectPool<GameObject>(options);
        }

        public GameObject Get() => _pool.Get();
        public void Release(GameObject go) => _pool.Release(go);
        public PoolStats Stats => _pool.Stats;
    }
}
#endif

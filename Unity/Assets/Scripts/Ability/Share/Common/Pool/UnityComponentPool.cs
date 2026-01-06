#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public sealed class UnityComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly Transform _root;

        public UnityComponentPool(T prefab, Transform root = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            _root = root;

            var options = new ObjectPoolOptions<T>(() =>
            {
                var go = UnityEngine.Object.Instantiate(prefab.gameObject, _root);
                return go.GetComponent<T>();
            })
            {
                DefaultCapacity = defaultCapacity,
                MaxSize = maxSize,
                CollectionCheck = collectionCheck,
                OnGet = c =>
                {
                    if (c != null) c.gameObject.SetActive(true);
                },
                OnRelease = c =>
                {
                    if (c != null)
                    {
                        c.gameObject.SetActive(false);
                        if (_root != null) c.transform.SetParent(_root, false);
                    }
                },
                OnDestroy = c =>
                {
                    if (c != null) UnityEngine.Object.Destroy(c.gameObject);
                }
            };

            _pool = new ObjectPool<T>(options);
        }

        public T Get() => _pool.Get();
        public void Release(T component) => _pool.Release(component);
        public PoolStats Stats => _pool.Stats;
    }
}
#endif

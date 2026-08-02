using System;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxGameObjectFactory
    {
        private readonly BattleVfxPrefabCache _prefabs;
        private readonly BattleViewPrimitiveFactory _primitives;
        private readonly BattleVfxGameObjectInstantiator _instantiator;
        private readonly BattleVfxGameObjectPool _pool;

        public BattleVfxGameObjectFactory(
            BattleVfxPrefabCache prefabs,
            BattleViewPrimitiveFactory primitives = null,
            BattleVfxGameObjectInstantiator instantiator = null,
            BattleVfxGameObjectPool pool = null)
        {
            _prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
            _primitives = primitives ?? new BattleViewPrimitiveFactory();
            _instantiator = instantiator ?? new BattleVfxGameObjectInstantiator();
            _pool = pool;
        }

        /// <summary>
        /// Pool rented by this factory. May be null when pooling is disabled.
        /// </summary>
        internal BattleVfxGameObjectPool Pool => _pool;

        public GameObject Create(int vfxId, string resource)
        {
            GameObject go;
            if (_pool != null && _pool.TryRent(vfxId, out var reused) && reused != null)
            {
                go = reused;
            }
            else if (_prefabs.TryGetPrefab(resource, out var prefab))
            {
                go = _instantiator.Instantiate(prefab);
            }
            else
            {
                go = CreatePlaceholder(vfxId);
            }

            if (go == null) return null;
            BattleVfxPoolableTag.Attach(go).VfxId = vfxId;
            go.name = $"Vfx_{vfxId}";
            return go;
        }

        public GameObject CreatePlaceholder(int vfxId)
        {
            var go = IsProjectileFallback(vfxId)
                ? _primitives.CreateProjectileFallback(vfxId)
                : _primitives.CreateVfxFallback(vfxId);
            if (go == null) return null;
            BattleVfxPoolableTag.Attach(go).VfxId = vfxId;
            go.name = $"VfxPlaceholder_{vfxId}";
            return go;
        }

        private static bool IsProjectileFallback(int vfxId)
        {
            return vfxId >= BattleViewPlaceholderIds.ProjectileVfx
                && vfxId <= BattleViewPlaceholderIds.ProjectileExpireVfx;
        }

        private static bool IsPlaceholderVfx(int vfxId)
        {
            return BattleViewPlaceholderIds.IsPlaceholderVfx(vfxId);
        }
    }

    internal sealed class BattleVfxGameObjectInstantiator
    {
        public GameObject Instantiate(GameObject prefab)
        {
            return prefab != null ? UnityEngine.Object.Instantiate(prefab) : null;
        }
    }
}

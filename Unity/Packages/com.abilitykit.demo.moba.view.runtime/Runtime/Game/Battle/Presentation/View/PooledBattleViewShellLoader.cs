using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Implements <see cref="IBattleViewShellLoader"/> by delegating to a
    /// <see cref="BattleViewShellPool"/> that uses the framework
    /// <see cref="Core.Pooling.ObjectPool{T}"/> per modelId bucket.
    /// </summary>
    public sealed class PooledBattleViewShellLoader : IBattleViewShellLoader
    {
        private readonly BattleViewShellPool _pool;
        private readonly BattleViewHierarchyManager _hierarchy;

        public PooledBattleViewShellLoader(
            BattleViewShellPool pool,
            BattleViewHierarchyManager hierarchy = null)
        {
            _pool = pool;
            _hierarchy = hierarchy;
        }

        public GameObject CreateShellGameObject(int actorId, int modelId)
        {
            if (modelId <= 0) return null;
            if (_pool == null) return null;

            var instance = _pool.Get(modelId);
            if (instance != null)
            {
                instance.name = $"Shell_{modelId}_{actorId}";
            }
            return instance;
        }

        public GameObject CreateShellGameObject(int actorId, int modelId, BattleEntityKind kind)
        {
            if (modelId <= 0) return null;
            if (_pool == null) return null;

            // All entity kinds share the same shell pool keyed by modelId. Renting
            // moves the shell out of its inactive model bucket into its semantic type.
            var instance = _pool.Get(modelId);
            if (instance != null)
            {
                instance.name = $"Shell_{kind}_{modelId}_{actorId}";
                var category = BattleViewCategoryPaths.FromEntityKind(kind);
                if (category != BattleViewCategory.Unknown)
                {
                    _hierarchy?.ParentActive(category, instance);
                }
            }
            return instance;
        }
    }
}

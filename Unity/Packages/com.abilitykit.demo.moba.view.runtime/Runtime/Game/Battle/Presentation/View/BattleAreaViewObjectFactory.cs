using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal class BattleAreaViewObjectFactory
    {
        protected readonly BattleViewResourceProvider _resources;
        protected readonly BattleAreaViewObjectPlacer _placer;

        public BattleAreaViewObjectFactory(
            BattleViewResourceProvider resources = null,
            BattleAreaViewObjectPlacer placer = null,
            BattleViewHierarchyManager hierarchy = null)
        {
            _resources = BattleViewResourceProvider.OrDefault(resources);
            _placer = placer ?? new BattleAreaViewObjectPlacer(hierarchy);
        }

        protected virtual GameObject CreateModelCore(int templateId, int modelId, Transform attach, in Vector3 position)
        {
            var go = _resources.CreateModelGo(modelId);
            if (go == null) return null;
            _placer.Place(go, templateId, attach, in position);
            return go;
        }

        protected virtual GameObject CreateRangeCore(int templateId, float radius, int delayMs, Transform attach, in Vector3 position)
        {
            var go = _resources.CreateAoeRangeGo(templateId, radius, delayMs);
            if (go == null) return null;
            _placer.Place(go, templateId, attach, in position);
            return go;
        }

        protected virtual GameObject CreateVfxCore(int templateId, int vfxId, Transform attach, in Vector3 position)
        {
            var go = _resources.CreateVfxGo(vfxId);
            if (go == null) return null;
            _placer.Place(go, templateId, attach, in position);
            return go;
        }

        public GameObject CreateModel(int templateId, int modelId, Transform attach, in Vector3 position)
        {
            return CreateModelCore(templateId, modelId, attach, in position);
        }

        public GameObject CreateRange(int templateId, float radius, int delayMs, Transform attach, in Vector3 position)
        {
            return CreateRangeCore(templateId, radius, delayMs, attach, in position);
        }

        public GameObject CreateVfx(int templateId, int vfxId, Transform attach, in Vector3 position)
        {
            return CreateVfxCore(templateId, vfxId, attach, in position);
        }
    }

    /// <summary>
    /// Pooled version of <see cref="BattleAreaViewObjectFactory"/>.
    /// The pool is constructed with factory lambdas that delegate to the underlying
    /// non-pooled creation methods so all AOE objects go through the same creation path.
    /// </summary>
    internal sealed class PooledBattleAreaViewObjectFactory : BattleAreaViewObjectFactory
    {
        public BattleAreaVfxPool Pool { get; }

        public PooledBattleAreaViewObjectFactory(
            BattleViewResourceProvider resources,
            BattleAreaVfxPool pool,
            BattleViewHierarchyManager hierarchy = null)
            : base(resources, null, hierarchy)
        {
            Pool = pool;
        }

        protected override GameObject CreateModelCore(int templateId, int modelId, Transform attach, in Vector3 position)
        {
            if (Pool != null && Pool.TryRent(templateId, BattleAreaVfxPool.PoolKind.Model, out var reused) && reused != null)
            {
                reused.name = $"AreaModel_{templateId}";
                _placer.Place(reused, templateId, attach, in position);
                return reused;
            }
            return base.CreateModelCore(templateId, modelId, attach, in position);
        }

        protected override GameObject CreateRangeCore(int templateId, float radius, int delayMs, Transform attach, in Vector3 position)
        {
            if (Pool != null && Pool.TryRent(templateId, BattleAreaVfxPool.PoolKind.Range, out var reused) && reused != null)
            {
                reused.name = $"AreaRange_{templateId}";
                _resources.ConfigureAoeRangeGo(reused, templateId, radius, delayMs);
                _placer.Place(reused, templateId, attach, in position);
                return reused;
            }
            return base.CreateRangeCore(templateId, radius, delayMs, attach, in position);
        }

        protected override GameObject CreateVfxCore(int templateId, int vfxId, Transform attach, in Vector3 position)
        {
            if (Pool != null && Pool.TryRent(templateId, BattleAreaVfxPool.PoolKind.Vfx, out var reused) && reused != null)
            {
                reused.name = $"AreaVfx_{templateId}";
                _placer.Place(reused, templateId, attach, in position);
                return reused;
            }
            return base.CreateVfxCore(templateId, vfxId, attach, in position);
        }
    }

    internal sealed class BattleAreaViewObjectPlacer
    {
        private readonly BattleViewHierarchyManager _hierarchy;

        public BattleAreaViewObjectPlacer(BattleViewHierarchyManager hierarchy = null)
        {
            _hierarchy = hierarchy;
        }

        public void Place(GameObject go, int templateId, Transform attach, in Vector3 position)
        {
            if (go == null) return;

            if (attach != null)
            {
                go.transform.SetParent(attach, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                return;
            }

            _hierarchy?.ParentActive(BattleViewCategory.ActiveArea, templateId, go);
            go.transform.position = position;
        }
    }
}

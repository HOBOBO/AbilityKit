using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewModelFactory
    {
        private readonly BattleViewPrimitiveFactory _primitives;
        private readonly BattleViewModelPrefabResolver _prefabs;
        private readonly BattleViewAttachRootUtility _attachRoots;
        private BattleViewHierarchyManager _hierarchy;

        public BattleViewModelFactory(
            BattleViewPrimitiveFactory primitives = null,
            BattleViewModelPrefabResolver prefabs = null,
            BattleViewAttachRootUtility attachRoots = null,
            BattleViewHierarchyManager hierarchy = null)
        {
            _primitives = primitives ?? new BattleViewPrimitiveFactory();
            _prefabs = prefabs ?? new BattleViewModelPrefabResolver();
            _attachRoots = attachRoots ?? new BattleViewAttachRootUtility();
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Optional setter for callers (e.g. <see cref="BattleViewFeature"/>) that
        /// need to inject the hierarchy manager after construction (e.g. when the
        /// root is created lazily during feature attach).
        /// </summary>
        public void SetHierarchyManager(BattleViewHierarchyManager hierarchy)
        {
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Active category corresponding to the shell type, derived from <see cref="BattleEntityKind"/>.
        /// </summary>
        private static BattleViewCategory CategoryForKind(BattleEntityKind kind)
        {
            return BattleViewCategoryPaths.FromEntityKind(kind);
        }

        private void ParentUnderActive(GameObject go, BattleEntityKind kind)
        {
            if (_hierarchy == null || go == null) return;
            var cat = CategoryForKind(kind);
            if (cat == BattleViewCategory.Unknown) return;
            _hierarchy.ParentActive(cat, go);
        }

        public GameObject CreateActorShell(MobaConfigDatabase configs, int actorId, int modelId)
        {
            var go = InstantiateOrFallback(configs, modelId, () => _primitives.CreateActorFallback(actorId, modelId));
            if (go == null) return null;
            go.name = $"Actor_{actorId}";
            _attachRoots.Ensure(go);
            ParentUnderActive(go, BattleEntityKind.Character);
            return go;
        }

        public GameObject CreateSummonShell(MobaConfigDatabase configs, int actorId, int modelId)
        {
            var go = InstantiateOrFallback(configs, modelId, () => _primitives.CreateSummonFallback(actorId, modelId));
            if (go == null) return null;
            go.name = $"Summon_{actorId}";
            _attachRoots.Ensure(go);
            ParentUnderActive(go, BattleEntityKind.Summon);
            return go;
        }

        public GameObject CreateTurretShell(MobaConfigDatabase configs, int actorId, int modelId)
        {
            var go = InstantiateOrFallback(configs, modelId, () => _primitives.CreateTurretFallback(actorId, modelId));
            if (go == null) return null;
            go.name = $"Turret_{actorId}";
            _attachRoots.Ensure(go);
            ParentUnderActive(go, BattleEntityKind.Turret);
            return go;
        }

        public GameObject CreateMonsterShell(MobaConfigDatabase configs, int actorId, int modelId)
        {
            var go = InstantiateOrFallback(configs, modelId, () => _primitives.CreateMonsterFallback(actorId, modelId));
            if (go == null) return null;
            go.name = $"Monster_{actorId}";
            _attachRoots.Ensure(go);
            ParentUnderActive(go, BattleEntityKind.Monster);
            return go;
        }

        public GameObject CreateBuildingShell(MobaConfigDatabase configs, int actorId, int modelId)
        {
            var go = InstantiateOrFallback(configs, modelId, () => _primitives.CreateBuildingFallback(actorId, modelId));
            if (go == null) return null;
            go.name = $"Building_{actorId}";
            _attachRoots.Ensure(go);
            ParentUnderActive(go, BattleEntityKind.Building);
            return go;
        }

        public GameObject CreateAoeModel(MobaConfigDatabase configs, int aoeTemplateId)
        {
            if (aoeTemplateId <= 0) return _primitives.CreateAoeModelFallback(aoeTemplateId);

            // aoeTemplateId is an AOE template ID, not a model ID.
            // Resolve the AOE template first to get its ModelId, then resolve the model prefab.
            if (!configs.TryGetAoe(aoeTemplateId, out var aoe))
            {
                Log.Warning($"[BattleViewModelFactory] AoeMO not found: id={aoeTemplateId}, using fallback.");
                return _primitives.CreateAoeModelFallback(aoeTemplateId);
            }

            var model = _prefabs.Resolve(configs, aoe.ModelId);
            GameObject go;
            if (model.HasPrefab)
            {
                go = Object.Instantiate(model.Prefab);
            }
            else
            {
                go = _primitives.CreateAoeModelFallback(aoeTemplateId);
            }

            if (go != null) go.name = $"AoeModel_{aoeTemplateId}";
            return go;
        }

        public GameObject CreateAoeRange(int templateId, float radius, int delayMs)
        {
            var go = _primitives.CreateAoeRangeFallback(templateId, radius, delayMs);
            go.name = $"AoeRange_{templateId}";
            return go;
        }

        public void ConfigureAoeRange(GameObject go, int templateId, float radius, int delayMs)
        {
            _primitives.ConfigureAoeRange(go, templateId, radius, delayMs);
        }

        /// <summary>
        /// Creates a projectile shell GameObject for the given projectileTemplateId.
        /// </summary>
        public GameObject CreateProjectileShell(MobaConfigDatabase configs, int actorId, int projectileTemplateId)
        {
            var model = _prefabs.Resolve(configs, projectileTemplateId);
            GameObject go;
            if (model.HasPrefab)
            {
                go = Object.Instantiate(model.Prefab);
                var s = model.Scale <= 0f ? 1f : model.Scale;
                go.transform.localScale = new Vector3(s, s, s);
            }
            else
            {
                go = _primitives.CreateProjectileFallback(projectileTemplateId);
            }

            if (go == null) return null;
            go.name = $"Projectile_{actorId}";
            _attachRoots.Ensure(go);
            return go;
        }

        private GameObject InstantiateOrFallback(MobaConfigDatabase configs, int modelId, System.Func<GameObject> fallback)
        {
            var model = _prefabs.Resolve(configs, modelId);
            if (model.HasPrefab)
            {
                var go = Object.Instantiate(model.Prefab);
                var s = model.Scale <= 0f ? 1f : model.Scale;
                go.transform.localScale = new Vector3(s, s, s);
                return go;
            }
            return fallback();
        }
    }
}

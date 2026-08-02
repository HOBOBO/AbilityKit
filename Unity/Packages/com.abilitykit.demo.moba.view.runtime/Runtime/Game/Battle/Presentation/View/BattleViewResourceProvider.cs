using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewResourceProvider
    {
        internal static readonly BattleViewResourceProvider Default = new BattleViewResourceProvider();

        private MobaConfigDatabase _configs;
        private VfxDatabase _vfxDb;
        private readonly BattleViewResourceCache _cache;
        private readonly BattleViewConfigLookup _configLookup;
        private readonly BattleViewModelFactory _models;
        private readonly BattleViewVfxFactory _vfx;

        internal static BattleViewResourceProvider OrDefault(BattleViewResourceProvider resources)
        {
            return resources ?? Default;
        }

        public BattleViewResourceProvider()
            : this(null, null)
        {
        }

        public BattleViewResourceProvider(MobaConfigDatabase configs, VfxDatabase vfxDb)
            : this(configs, vfxDb, null)
        {
        }

        public BattleViewResourceProvider(
            MobaConfigDatabase configs,
            VfxDatabase vfxDb,
            IBattleAssetLookup assets)
            : this(configs, vfxDb, null, null, null, null, null, assets)
        {
        }

        internal BattleViewResourceProvider(
            MobaConfigDatabase configs,
            VfxDatabase vfxDb,
            BattleViewResourceCache cache,
            BattleViewConfigLookup configLookup,
            BattleViewModelFactory models,
            BattleViewVfxFactory vfx,
            BattleViewResourceProviderComponentFactory components = null,
            IBattleAssetLookup assets = null)
        {
            components ??= new BattleViewResourceProviderComponentFactory();

            _configs = configs;
            _vfxDb = vfxDb;
            _cache = cache ?? components.CreateCache(assets);
            _configLookup = configLookup ?? components.CreateConfigLookup();

            var primitives = components.CreatePrimitives();
            _models = models ?? components.CreateModels(primitives, assets);
            _vfx = vfx ?? components.CreateVfx(primitives, assets);
        }

        /// <summary>
        /// Inject a hierarchy manager so that subsequently created shells are
        /// parented under the matching active-view category root. Pass null to
        /// restore the legacy "detach-to-scene-root" behaviour.
        /// </summary>
        public void SetHierarchyManager(BattleViewHierarchyManager hierarchy)
        {
            _models.SetHierarchyManager(hierarchy);
        }

        public MobaConfigDatabase Configs
        {
            get => _configs;
            set => _configs = value;
        }

        public VfxDatabase VfxDb
        {
            get => _vfxDb;
            set => _vfxDb = value;
        }

        public MobaConfigDatabase GetOrLoadConfigs()
        {
            return _cache.GetOrLoadConfigs(ref _configs);
        }

        public VfxDatabase GetOrLoadVfxDb()
        {
            return _cache.GetOrLoadVfxDb(ref _vfxDb);
        }

        public GameObject CreateShellGameObject(int actorId, int modelId)
        {
            return _models.CreateActorShell(GetOrLoadConfigs(), actorId, modelId);
        }

        /// <summary>Creates a shell for a Summon entity.</summary>
        public GameObject CreateSummonShell(int actorId, int modelId)
        {
            return _models.CreateSummonShell(GetOrLoadConfigs(), actorId, modelId);
        }

        /// <summary>Creates a shell for a Turret entity.</summary>
        public GameObject CreateTurretShell(int actorId, int modelId)
        {
            return _models.CreateTurretShell(GetOrLoadConfigs(), actorId, modelId);
        }

        /// <summary>Creates a shell for a Monster entity.</summary>
        public GameObject CreateMonsterShell(int actorId, int modelId)
        {
            return _models.CreateMonsterShell(GetOrLoadConfigs(), actorId, modelId);
        }

        /// <summary>Creates a shell for a Building entity.</summary>
        public GameObject CreateBuildingShell(int actorId, int modelId)
        {
            return _models.CreateBuildingShell(GetOrLoadConfigs(), actorId, modelId);
        }

        /// <summary>Creates a shell for a Projectile entity.</summary>
        public GameObject CreateProjectileShell(int actorId, int projectileTemplateId)
        {
            return _models.CreateProjectileShell(GetOrLoadConfigs(), actorId, projectileTemplateId);
        }

        public GameObject CreateModelGo(int modelId)
        {
            return _models.CreateAoeModel(GetOrLoadConfigs(), modelId);
        }

        public GameObject CreateAoeRangeGo(int templateId, float radius, int delayMs)
        {
            return _models.CreateAoeRange(templateId, radius, delayMs);
        }

        public void ConfigureAoeRangeGo(GameObject go, int templateId, float radius, int delayMs)
        {
            _models.ConfigureAoeRange(go, templateId, radius, delayMs);
        }

        public GameObject CreateVfxGo(int vfxId)
        {
            return _vfx.CreateAoeVfx(GetOrLoadVfxDb(), vfxId);
        }

        public int ResolveModelId(BattleEntityMetaComponent meta)
        {
            return _configLookup.ResolveModelId(GetOrLoadConfigs(), meta);
        }

        public int ResolveProjectileVfxId(BattleEntityMetaComponent meta)
        {
            return _configLookup.ResolveProjectileVfxId(GetOrLoadConfigs(), meta);
        }

        public ProjectileMO TryGetProjectile(int templateId)
        {
            return _configLookup.TryGetProjectile(GetOrLoadConfigs(), templateId);
        }

        public AoeMO TryGetAoe(int templateId)
        {
            return _configLookup.TryGetAoe(GetOrLoadConfigs(), templateId);
        }
    }

    internal sealed class BattleViewResourceProviderComponentFactory
    {
        public BattleViewResourceCache CreateCache(IBattleAssetLookup assets = null)
        {
            return new BattleViewResourceCache(assets);
        }

        public BattleViewConfigLookup CreateConfigLookup()
        {
            return new BattleViewConfigLookup();
        }

        public BattleViewPrimitiveFactory CreatePrimitives()
        {
            return new BattleViewPrimitiveFactory();
        }

        public BattleViewModelFactory CreateModels(
            BattleViewPrimitiveFactory primitives,
            IBattleAssetLookup assets = null)
        {
            var prefabs = assets != null
                ? new BattleViewModelPrefabResolver(new BattleAssetViewModelPrefabLoader(assets))
                : null;
            return new BattleViewModelFactory(primitives, prefabs);
        }

        public BattleViewVfxFactory CreateVfx(
            BattleViewPrimitiveFactory primitives,
            IBattleAssetLookup assets = null)
        {
            var loader = assets != null ? new BattleAssetViewVfxPrefabLoader(assets) : null;
            return new BattleViewVfxFactory(primitives, loader);
        }
    }
}

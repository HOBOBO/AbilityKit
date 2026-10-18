using System;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Presentation.Features.Scene;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Flow
{
    internal sealed class UnityMobaFeatureFactoryProvider : IMobaFeatureFactoryProvider
    {
        private readonly GameplayPresentationProfileCatalog _profiles;
        private readonly GameplayPresentationModuleRegistry _modules;
        private readonly MobaLobbyRuntimeOptions _lobbyOptions;
        private readonly IBattleAssetLoadService _assetLoadService;
        private readonly IBattleAssetDependencyProvider _assetDependencyProvider;

        internal UnityMobaFeatureFactoryProvider(
            GameplayPresentationProfileCatalog profiles = null,
            GameplayPresentationModuleRegistry modules = null,
            MobaLobbyRuntimeOptions lobbyOptions = null,
            IBattleAssetLoadService assetLoadService = null,
            IBattleAssetDependencyProvider assetDependencyProvider = null)
        {
            _profiles = profiles ?? UnityGameplayPresentationProfileCatalogLoader.Default;
            _modules = modules ?? GameplayPresentationModuleRegistry.CreateStandard();
            _lobbyOptions = lobbyOptions ?? MobaLobbyRuntimeOptionsCatalog.Default;
            _assetLoadService = assetLoadService ?? ResourcesBattleAssetLoadService.Default;
            _assetDependencyProvider = assetDependencyProvider ?? ResourcesBattleAssetDependencyProvider.Default;
            _lobbyOptions.ValidateOrThrow();
        }

        public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(Func<IBattleSessionFeature> createBattleSessionFeature)
        {
            if (createBattleSessionFeature == null) throw new ArgumentNullException(nameof(createBattleSessionFeature));
            _modules.ValidateCatalog(_profiles);

            var registry = new MobaFeatureFactoryRegistry();
            MobaBattleFeatureRegistration.Register(registry, createBattleSessionFeature, _profiles, _modules,
                _assetLoadService, _assetDependencyProvider);
            MobaLobbyFeatureRegistration.Register(registry, _lobbyOptions);
            MobaUiFeatureRegistration.Register(registry);

            return registry;
        }

        /// <summary>
        /// 创建副本扩展注册中心。Unity 侧在这里把真实的场景装配器注册进去，
        /// 使副本配置里的 <c>SceneProviderId</c> 能解析到平台实现。
        /// </summary>
        public MobaDungeonRegistry CreateDungeonRegistry() =>
            MobaDungeonRegistry.CreateStandard()
                .RegisterSceneProvider(new UnityBattleSceneProvider());

        /// <summary>
        /// 从配置数据库构建副本解析器。返回 null 表示当前运行环境没有配置库
        /// （例如纯逻辑测试），此时战斗仍可按默认 Profile 进入。
        /// </summary>
        public IBattleDungeonResolver CreateDungeonResolver(MobaConfigDatabase database) =>
            database == null ? null : MobaDungeonConfigResolver.FromDatabase(database);
    }
}

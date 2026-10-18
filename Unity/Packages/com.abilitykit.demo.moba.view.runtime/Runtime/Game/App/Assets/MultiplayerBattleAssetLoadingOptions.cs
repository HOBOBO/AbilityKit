using System;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Flow
{
    public sealed class MultiplayerBattleAssetLoadingOptions
    {
        public MultiplayerBattleAssetLoadingOptions(
            IBattleAssetLoadService loadService,
            IBattleAssetDependencyProvider dependencyProvider,
            string localTagBundleVersion)
        {
            LoadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
            DependencyProvider = dependencyProvider ?? throw new ArgumentNullException(nameof(dependencyProvider));
            LocalTagBundleVersion = localTagBundleVersion ?? throw new ArgumentNullException(nameof(localTagBundleVersion));
        }

        public IBattleAssetLoadService LoadService { get; }
        public IBattleAssetDependencyProvider DependencyProvider { get; }
        public string LocalTagBundleVersion { get; }
    }
}

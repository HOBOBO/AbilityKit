using System;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    internal sealed class UnityBattlePresentationDataSource : IBattlePresentationDataSource
    {
        private const string VfxResourcePath = "vfx/vfx";
        internal static readonly UnityBattlePresentationDataSource Shared = new UnityBattlePresentationDataSource();

        public MobaConfigDatabase LoadConfigs(IBattleAssetLookup assets) => MobaConfigLoader.LoadDefault();

        public VfxDatabase LoadVfx(IBattleAssetLookup assets)
        {
            if (assets == null) return UnityVfxDatabaseLoader.LoadFromResources(VfxResourcePath);
            if (!assets.TryGetAsset(VfxResourcePath, out var loaded) || !(loaded is TextAsset textAsset))
                throw new InvalidOperationException(
                    "The active battle lease does not contain VFX config: " + VfxResourcePath);
            return UnityVfxDatabaseLoader.LoadFromJson(textAsset.text, VfxResourcePath);
        }
    }
}

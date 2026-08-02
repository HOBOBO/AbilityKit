using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewResourceCache
    {
        private const string DefaultVfxResourcePath = "vfx/vfx";
        private readonly IBattleAssetLookup _assets;

        public BattleViewResourceCache(IBattleAssetLookup assets = null)
        {
            _assets = assets;
        }

        public MobaConfigDatabase GetOrLoadConfigs(ref MobaConfigDatabase configs)
        {
            if (configs == null)
            {
                if (_assets != null)
                {
                    throw new System.InvalidOperationException(
                        "The runtime MobaConfigDatabase is unavailable for the active battle lease.");
                }

                configs = MobaConfigLoader.LoadDefault();
            }

            return configs;
        }

        public VfxDatabase GetOrLoadVfxDb(ref VfxDatabase vfxDb)
        {
            if (vfxDb == null)
            {
                if (_assets != null)
                {
                    if (!_assets.TryGetAsset(DefaultVfxResourcePath, out var loaded) ||
                        !(loaded is TextAsset textAsset))
                    {
                        throw new System.InvalidOperationException(
                            "The active battle lease does not contain VFX config: " + DefaultVfxResourcePath);
                    }

                    vfxDb = VfxDatabase.LoadFromJson(textAsset.text, DefaultVfxResourcePath);
                }
                else
                {
                    vfxDb = VfxDatabase.LoadFromResources(DefaultVfxResourcePath);
                }
            }

            return vfxDb;
        }
    }
}

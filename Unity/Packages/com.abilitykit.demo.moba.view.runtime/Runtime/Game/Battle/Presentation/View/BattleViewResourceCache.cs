using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewResourceCache
    {
        private readonly IBattleAssetLookup _assets;
        private readonly IBattlePresentationDataSource _dataSource;

        public BattleViewResourceCache(IBattlePresentationDataSource dataSource, IBattleAssetLookup assets = null)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _assets = assets;
        }

        public MobaConfigDatabase GetOrLoadConfigs(ref MobaConfigDatabase configs)
        {
            if (configs == null)
            {
                if (_assets != null)
                    throw new InvalidOperationException(
                        "The runtime MobaConfigDatabase is unavailable for the active battle lease.");
                configs = _dataSource.LoadConfigs(_assets);
            }

            return configs;
        }

        public VfxDatabase GetOrLoadVfxDb(ref VfxDatabase vfxDb)
        {
            if (vfxDb == null)
            {
                vfxDb = _dataSource.LoadVfx(_assets);
            }

            return vfxDb;
        }
    }
}

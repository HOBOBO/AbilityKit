using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;

namespace AbilityKit.Game.Flow
{
    public interface IBattlePresentationDataSource
    {
        MobaConfigDatabase LoadConfigs(IBattleAssetLookup assets);
        VfxDatabase LoadVfx(IBattleAssetLookup assets);
    }
}

using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleAssetLoadSessionPort
    {
        BattleStartPlan Plan { get; }
        IBattleAssetLookup AssetLookup { get; }
        void AdoptAssetLease(IBattleAssetLease lease);
        void NotifyAssetsLoadCompleted();
    }
}

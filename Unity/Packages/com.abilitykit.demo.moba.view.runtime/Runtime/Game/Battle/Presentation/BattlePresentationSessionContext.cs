using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;

namespace AbilityKit.Game.Flow
{
    internal interface IBattlePresentationSessionFactory
    {
        BattlePresentationSessionContext Create(in GamePhaseContext ctx);
    }

    internal sealed class BattlePresentationSessionFactory : IBattlePresentationSessionFactory
    {
        public BattlePresentationSessionContext Create(in GamePhaseContext ctx)
        {
            MobaConfigDatabase configs = null;
            if (ctx.Features.TryGet(out BattleContext battleContext) &&
                battleContext != null &&
                battleContext.TryGetRuntimeWorld(out var runtimeWorld) &&
                runtimeWorld?.Services != null)
            {
                runtimeWorld.Services.TryResolve(out configs);
            }

            IBattleAssetLookup assets = null;
            if (ctx.Features.TryGet(out BattleSessionFeature battleSession))
            {
                assets = battleSession?.AssetLease as IBattleAssetLookup;
            }

            return BattlePresentationSessionContext.CreateDefault(configs, null, assets);
        }
    }

    public sealed class BattlePresentationSessionContext
    {
        private int _retainCount;

        public BattlePresentationSessionContext(BattleViewResourceProvider resources)
        {
            Resources = resources ?? new BattleViewResourceProvider();
        }

        public BattleViewResourceProvider Resources { get; }

        internal void Retain()
        {
            _retainCount++;
        }

        internal bool Release()
        {
            if (_retainCount > 0)
            {
                _retainCount--;
            }

            return _retainCount == 0;
        }

        public static BattlePresentationSessionContext CreateDefault(
            MobaConfigDatabase configs = null,
            VfxDatabase vfxDb = null,
            IBattleAssetLookup assets = null)
        {
            return new BattlePresentationSessionContext(
                new BattleViewResourceProvider(configs, vfxDb, assets));
        }

        internal static BattlePresentationSessionContext CreateFromDefaultResources()
        {
            return CreateDefault();
        }
    }
}

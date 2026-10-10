using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Moba.Config;
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
            IBattleAssetLookup assets = null;
            if (ctx.Features.TryGet(out IBattleAssetLoadSessionPort assetSession))
            {
                assets = assetSession?.AssetLookup;
            }

            var configs = assets != null ? MobaConfigLoader.LoadDefault() : null;
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

        internal BattleViewResourceProvider CreateViewResources() =>
            new BattleViewResourceProvider(Resources.Configs, Resources.VfxDb,
                Resources.AssetLookup, Resources.ModelResolvers);

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
            IBattleAssetLookup assets = null,
            BattleViewModelResolverRegistry modelResolvers = null)
        {
            return new BattlePresentationSessionContext(
                new BattleViewResourceProvider(configs, vfxDb, assets, modelResolvers));
        }

    }
}

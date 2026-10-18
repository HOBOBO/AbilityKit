using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using System;

namespace AbilityKit.Game.Flow
{
    internal interface IBattlePresentationSessionFactory
    {
        BattlePresentationSessionContext Create(in GamePhaseContext ctx);
    }

    internal sealed class BattlePresentationSessionFactory : IBattlePresentationSessionFactory
    {
        private readonly IBattlePresentationDataSource _dataSource;

        public BattlePresentationSessionFactory(IBattlePresentationDataSource dataSource)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        public BattlePresentationSessionContext Create(in GamePhaseContext ctx)
        {
            IBattleAssetLookup assets = null;
            if (ctx.Features.TryGet(out IBattleAssetLoadSessionPort assetSession))
            {
                assets = assetSession?.AssetLookup;
            }

            var configs = assets != null ? _dataSource.LoadConfigs(assets) : null;
            return BattlePresentationSessionContext.CreateDefault(configs, null, assets,
                dataSource: _dataSource);
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
                Resources.AssetLookup, Resources.ModelResolvers, Resources.DataSource);

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
            BattleViewModelResolverRegistry modelResolvers = null,
            IBattlePresentationDataSource dataSource = null)
        {
            return new BattlePresentationSessionContext(
                new BattleViewResourceProvider(configs, vfxDb, assets, modelResolvers, dataSource));
        }

    }
}

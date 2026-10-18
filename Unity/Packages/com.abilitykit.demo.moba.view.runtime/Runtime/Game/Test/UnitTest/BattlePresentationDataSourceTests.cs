using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattlePresentationDataSourceTests
    {
        [Test]
        public void ResourceProvider_LoadsEachDataSetOnceFromInjectedSource()
        {
            var source = new TestDataSource();
            var provider = new BattleViewResourceProvider(null, null, null, null, source);

            Assert.That(provider.GetOrLoadConfigs(), Is.SameAs(source.Configs));
            Assert.That(provider.GetOrLoadConfigs(), Is.SameAs(source.Configs));
            Assert.That(provider.GetOrLoadVfxDb(), Is.SameAs(source.Vfx));
            Assert.That(provider.GetOrLoadVfxDb(), Is.SameAs(source.Vfx));
            Assert.That(source.ConfigLoads, Is.EqualTo(1));
            Assert.That(source.VfxLoads, Is.EqualTo(1));
        }

        [Test]
        public void ResourceProvider_WithLeaseDoesNotLoadMissingConfigsOutsideLease()
        {
            var source = new TestDataSource();
            var provider = new BattleViewResourceProvider(null, null, new EmptyAssetLookup(), null, source);

            Assert.Throws<InvalidOperationException>(() => provider.GetOrLoadConfigs());
            Assert.That(source.ConfigLoads, Is.Zero);
        }

        private sealed class TestDataSource : IBattlePresentationDataSource
        {
            public readonly MobaConfigDatabase Configs = new MobaConfigDatabase();
            public readonly VfxDatabase Vfx = new VfxDatabase(new Dictionary<int, VfxDTO>());
            public int ConfigLoads;
            public int VfxLoads;

            public MobaConfigDatabase LoadConfigs(IBattleAssetLookup assets)
            {
                ConfigLoads++;
                return Configs;
            }

            public VfxDatabase LoadVfx(IBattleAssetLookup assets)
            {
                VfxLoads++;
                return Vfx;
            }
        }

        private sealed class EmptyAssetLookup : IBattleAssetLookup
        {
            public bool TryGetAsset(string assetPath, out object asset)
            {
                asset = null;
                return false;
            }
        }
    }
}

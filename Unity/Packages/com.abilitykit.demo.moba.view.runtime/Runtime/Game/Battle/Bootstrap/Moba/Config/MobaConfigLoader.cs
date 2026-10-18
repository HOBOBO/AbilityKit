using System;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.View.Config;

namespace AbilityKit.Game.Battle.Moba.Config
{
    public static class MobaConfigLoader
    {
        public static MobaConfigDatabase LoadDefault()
        {
            var assets = new ResourcesTextAssetLoader();
            var db = new MobaConfigDatabase(textAssetLoader: assets);
            new LubanGroupsMobaConfigLoadProfile(assets).Load(db);
            return db;
        }
    }
}

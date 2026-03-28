using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo;

namespace AbilityKit.Game.Battle.Moba.Config
{
    public static class MobaConfigLoader
    {
        public static MobaConfigDatabase LoadDefault()
        {
            var db = new MobaConfigDatabase();
            db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
            return db;
        }
    }
}

using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Game.Battle.Moba.Config
{
    public static class MobaConfigLoader
    {
        public static MobaConfigDatabase LoadDefault()
        {
            return Load(new ResourcesJsonMobaConfigSource(MobaConfigPaths.DefaultResourcesDir));
        }

        public static MobaConfigDatabase Load(IMobaConfigSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var db = new MobaConfigDatabase();
            db.Load(source);
            return db;
        }
    }
}

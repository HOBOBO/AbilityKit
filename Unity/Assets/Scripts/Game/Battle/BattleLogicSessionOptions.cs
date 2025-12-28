using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using System.Reflection;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSessionOptions
    {
        public BattleLogicMode Mode = BattleLogicMode.Local;

        public WorldId WorldId = new WorldId("room_1");
        public string WorldType = "battle";

        public WorldContainerBuilder WorldServices;

        public WorldServiceProfile Profile = WorldServiceProfile.Client;

        public Assembly[] ScanAssemblies =
        {
            typeof(WorldServiceContainerFactory).Assembly,
            typeof(BattleLogicSession).Assembly
        };

        public bool ScanAllLoadedAssemblies;
        public string[] NamespacePrefixes = new[] { "AbilityKit" };

        public bool AutoCreateWorld = true;
        public bool AutoConnect = true;
        public bool AutoJoin = true;

        public string ClientId = "battle_client";
        public string PlayerId = "p1";
    }
}

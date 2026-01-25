using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Game.Battle
{
    public static class BattleLogicClientFactory
    {
        public static IBattleLogicClient CreateLocal(IWorldManager worlds)
        {
            if (worlds == null) throw new ArgumentNullException(nameof(worlds));
            var server = new AbilityKit.Ability.Server.LogicWorldServer(worlds);
            return new LocalBattleLogicClient(server);
        }

        public static IBattleLogicClient CreateRemoteInMemory(IWorldManager worlds, string clientId = "in_memory")
        {
            if (worlds == null) throw new ArgumentNullException(nameof(worlds));
            var server = new AbilityKit.Ability.Server.LogicWorldServer(worlds);
            var transport = new InMemoryBattleLogicTransport(server, clientId);
            return new RemoteBattleLogicClient(transport);
        }

        public static IBattleLogicClient CreateRemote(IBattleLogicTransport transport)
        {
            return new RemoteBattleLogicClient(transport);
        }
    }
}

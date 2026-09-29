using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Demo.Tiny.Turn;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Tiny;

namespace AbilityKit.Demo.Tiny.Server;

public static class TinyTurnGameplayModule
{
    public static ServerGameplayModule Create() => new(
        new GameplayRoomDescriptor(TinyTurnBattle.RoomType, "Tiny Turn Battle", 2,
            false, TinyTurnBattle.WorldType, TinyTurnBattle.TickRate,
            TinyTurnBattle.StateTemplate),
        ServerSyncCapabilityDeclaration.FromTemplates(
            TinyTurnBattle.StateTemplate,
            new ServerSyncTemplateDeclaration(
                TinyTurnBattle.StateTemplate, ServerBattleSyncMode.StateSync,
                ServerBattleRuntimeMode.BattleWorld, 1, 1,
                NetworkSyncProfiles.AuthoritativeInterpolation, 1, 1)),
        static () => new TinyRoomGameplayAdapter(TinyTurnBattle.RoomType,
            TinyTurnBattle.WorldType, TinyTurnBattle.TickRate,
            "tiny.turn.room.v1", TinyTurnBattle.AssetKey, TinyTurnBattle.RulesKey),
        static manager => new TinyTurnBattleRuntimeAdapter(manager),
        new Func<IWorldBlueprint>[]
        {
            static () => new DelegateWorldBlueprint(TinyTurnBattle.WorldType,
                options => options.WorldType = TinyTurnBattle.WorldType)
        });
}

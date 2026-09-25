using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Demo.Tiny;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Tiny;

namespace AbilityKit.Demo.Tiny.Server;

public static class TinyServerGameplayModule
{
    public static ServerGameplayModule Create() => new(
        new GameplayRoomDescriptor(
            TinyGameplay.RoomType, "Tiny Battle", 2, false, TinyGameplay.WorldType,
            TinyGameplay.TickRate, TinyGameplay.StateSyncTemplate),
        ServerBattleSyncProfile.FromTemplates(new ServerBattleSyncTemplate(
            TinyGameplay.StateSyncTemplate, ServerBattleSyncMode.StateSync,
            ServerBattleRuntimeMode.BattleWorld, 1, 1),
            new ServerBattleSyncTemplate(
                TinyGameplay.FrameSyncTemplate, ServerBattleSyncMode.FrameSync,
                ServerBattleRuntimeMode.BattleWorldWithFrameSync, 1000000, 1000000),
            new ServerBattleSyncTemplate(
                TinyGameplay.HybridSyncTemplate, ServerBattleSyncMode.FrameSync,
                ServerBattleRuntimeMode.BattleWorldWithFrameSync, 5, 5)),
        static () => new TinyRoomGameplayAdapter(),
        static manager => new TinyBattleRuntimeAdapter(manager),
        new Func<IWorldBlueprint>[]
        {
            static () => new DelegateWorldBlueprint(TinyGameplay.WorldType,
                options => options.WorldType = TinyGameplay.WorldType)
        },
        ResolveSyncCapabilities);

    private static ServerSyncCapabilityDefinition ResolveSyncCapabilities(
        BattleSyncStartOptions? _, string templateId)
    {
        if (string.Equals(templateId, TinyGameplay.StateSyncTemplate, StringComparison.OrdinalIgnoreCase))
            return new ServerSyncCapabilityDefinition(nameof(NetworkSyncModel.AuthoritativeInterpolation),
                NetworkSyncProfiles.AuthoritativeInterpolation, 1, 1);
        if (string.Equals(templateId, TinyGameplay.FrameSyncTemplate, StringComparison.OrdinalIgnoreCase))
            return new ServerSyncCapabilityDefinition(nameof(NetworkSyncModel.Lockstep),
                NetworkSyncProfiles.LockstepWithSnapshotRecovery, 1, 1);
        if (string.Equals(templateId, TinyGameplay.HybridSyncTemplate, StringComparison.OrdinalIgnoreCase))
            return new ServerSyncCapabilityDefinition(nameof(NetworkSyncModel.HybridHeroPrediction),
                NetworkSyncProfiles.FrameWithSnapshotRecovery, 1, 1);
        throw new ArgumentException("Unsupported Tiny sync template.", nameof(templateId));
    }
}

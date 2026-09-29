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
        // 模板集合与能力协商一次声明：State 全量、Frame 关周期快照、Hybrid 每 5 帧校验。
        ServerSyncCapabilityDeclaration.FromTemplates(
            TinyGameplay.StateSyncTemplate,
            new ServerSyncTemplateDeclaration(
                TinyGameplay.StateSyncTemplate, ServerBattleSyncMode.StateSync,
                ServerBattleRuntimeMode.BattleWorld, 1, 1,
                NetworkSyncProfiles.AuthoritativeInterpolation, 1, 1),
            new ServerSyncTemplateDeclaration(
                TinyGameplay.FrameSyncTemplate, ServerBattleSyncMode.FrameSync,
                ServerBattleRuntimeMode.BattleWorldWithFrameSync, 1000000, 1000000,
                NetworkSyncProfiles.LockstepWithSnapshotRecovery, 1, 1),
            new ServerSyncTemplateDeclaration(
                TinyGameplay.HybridSyncTemplate, ServerBattleSyncMode.FrameSync,
                ServerBattleRuntimeMode.BattleWorldWithFrameSync, 5, 5,
                NetworkSyncProfiles.FrameWithSnapshotRecovery, 1, 1)),
        static () => new TinyRoomGameplayAdapter(),
        static manager => new TinyBattleRuntimeAdapter(manager),
        new Func<IWorldBlueprint>[]
        {
            static () => new DelegateWorldBlueprint(TinyGameplay.WorldType,
                options => options.WorldType = TinyGameplay.WorldType)
        });
}

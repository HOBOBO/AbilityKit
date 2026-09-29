using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomNetworkSyncCapabilityResolverTests
{
    [Fact]
    public void Resolve_ThirdGameplay_UsesItsRegisteredSyncCapabilities()
    {
        const string roomType = "tiny";
        const string templateId = "tiny-state-authority";
        // 用装配外玩法唯一可用的声明入口构造第三方玩法，声明 schema 2..2。
        var tiny = new ServerGameplayModule(
            new GameplayRoomDescriptor(roomType, "Tiny", 2, false, roomType, 30, templateId),
            ServerSyncCapabilityDeclaration.FromTemplates(
                templateId,
                new ServerSyncTemplateDeclaration(
                    templateId, ServerBattleSyncMode.StateSync,
                    ServerBattleRuntimeMode.BattleWorld, 1, 1,
                    NetworkSyncProfiles.AuthoritativeInterpolation, 2, 2)),
            static () => throw new NotImplementedException(),
            static _ => throw new NotImplementedException(),
            new Func<IWorldBlueprint>[] { static () => throw new NotImplementedException() });
        var modules = new ServerGameplayModuleCatalog(new[]
        {
            ServerGameplayModuleCatalog.Default.ResolveModule(GameplayRoomTypes.Moba), tiny
        });
        var summary = new RoomSummary("dev", "local", "tiny-room", roomType, "Tiny", false, 2, 2, "owner", 1, null);
        var initParams = new BattleInitParams { RoomType = roomType };

        var metadata = RoomNetworkSyncCapabilityResolver.Resolve(summary, initParams, templateId, modules);

        Assert.Equal(nameof(NetworkSyncModel.AuthoritativeInterpolation), metadata.ProfileName);
        Assert.Equal(2, metadata.MinimumSchemaVersion);
        Assert.Equal(2, metadata.MaximumSchemaVersion);
        Assert.Equal((int)ClientPlaybackPolicy.AuthoritativeInterpolation, metadata.ClientPlayback);
        Assert.Throws<InvalidOperationException>(() =>
            RoomNetworkSyncCapabilityResolver.Resolve(summary, initParams, "unknown-template", modules));
    }

    [Fact]
    public void Resolve_MobaFrameSync_DeclaresLockstep()
    {
        var metadata = Resolve(GameplayRoomTypes.Moba);

        Assert.Equal(nameof(NetworkSyncModel.Lockstep), metadata.ProfileName);
        Assert.Equal((int)ClientPlaybackPolicy.None, metadata.ClientPlayback);
        Assert.Equal((int)InputPolicy.DeterministicBroadcast, metadata.Input);
        Assert.Equal((int)SnapshotPolicy.None, metadata.Snapshot);
    }

    [Fact]
    public void Resolve_LegacyMobaAlias_DeclaresLockstep()
    {
        var metadata = Resolve(GameplayRoomTypes.LegacyMoba);

        Assert.Equal(nameof(NetworkSyncModel.Lockstep), metadata.ProfileName);
        Assert.Equal((int)ClientPlaybackPolicy.None, metadata.ClientPlayback);
        Assert.Equal((int)InputPolicy.DeterministicBroadcast, metadata.Input);
        Assert.Equal((int)SnapshotPolicy.None, metadata.Snapshot);
    }

    private static NetworkSyncCapabilityMetadata Resolve(string roomType)
    {
        var summary = new RoomSummary(
            Region: "dev",
            ServerId: "local",
            RoomId: "room-1",
            RoomType: roomType,
            Title: "MOBA Room",
            IsPublic: false,
            MaxPlayers: 2,
            PlayerCount: 2,
            OwnerAccountId: "owner",
            CreatedAtUnixMs: 1,
            Tags: null);
        var initParams = new BattleInitParams
        {
            RoomType = roomType,
            SyncOptions = new BattleSyncStartOptions(
                "frame-sync-authority",
                (int)NetworkSyncModel.Lockstep,
                null,
                null,
                true,
                false,
                0)
        };

        return RoomNetworkSyncCapabilityResolver.Resolve(
            summary,
            initParams,
            "frame-sync-authority");
    }
}

using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Rooms;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomNetworkSyncCapabilityResolverTests
{
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

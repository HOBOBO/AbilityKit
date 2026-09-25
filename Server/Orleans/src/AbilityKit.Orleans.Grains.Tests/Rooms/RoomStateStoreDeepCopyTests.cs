using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomStateStoreDeepCopyTests
{
    [Fact]
    public async Task WriteThenMutateOriginal_DoesNotAffectSubsequentRead()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-1", "a", "b");

        await store.WriteRuntimeStateAsync("room-1", state, CancellationToken.None);

        state.Members[0] = state.Members[0] with { AccountId = "tampered" };
        state.GameplayState.Payload[0] = 0xFF;
        state.Launch.LockedRoster.Add("injected");

        var read = await store.TryGetRuntimeStateAsync("room-1", CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal("a", read!.Members[0].AccountId);
        Assert.NotEqual(0xFF, read.GameplayState.Payload[0]);
        Assert.DoesNotContain("injected", read.Launch.LockedRoster);
    }

    [Fact]
    public async Task ReadTwice_ReturnsIndependentCopies()
    {
        var store = new InMemoryRoomStateStore();
        await store.WriteRuntimeStateAsync("room-2", CreateState("room-2", "a"), CancellationToken.None);

        var first = await store.TryGetRuntimeStateAsync("room-2", CancellationToken.None);
        first!.Members[0] = first.Members[0] with { AccountId = "mutated" };

        var second = await store.TryGetRuntimeStateAsync("room-2", CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal("a", second!.Members[0].AccountId);
    }

    [Fact]
    public async Task GetOrCreateNumericRoomId_UsesSharedProtocolIdAndSupportsReverseLookup()
    {
        var store = new InMemoryRoomStateStore();
        const string roomId = "room-numeric-contract";

        var numericRoomId = await store.GetOrCreateNumericRoomIdAsync(roomId, CancellationToken.None);
        var resolvedRoomId = await store.TryGetRoomIdAsync(numericRoomId, CancellationToken.None);

        Assert.Equal(RoomGatewayIds.CreateNumericRoomId(roomId), numericRoomId);
        Assert.Equal(roomId, resolvedRoomId);
    }

    [Fact]
    public async Task UpsertRoom_RegistersWireNumericRoomIdForReverseLookup()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-upsert-mapping", "owner");
        var numericRoomId = RoomGatewayIds.CreateNumericRoomId(state.Summary.RoomId);

        await store.UpsertRoomAsync("dir", state.Summary, CancellationToken.None);

        var resolvedRoomId = await store.TryGetRoomIdAsync(numericRoomId, CancellationToken.None);
        Assert.Equal(state.Summary.RoomId, resolvedRoomId);
    }

    [Fact]
    public async Task RemoveRuntimeState_ClearsRecord()
    {
        var store = new InMemoryRoomStateStore();
        await store.WriteRuntimeStateAsync("room-3", CreateState("room-3", "a"), CancellationToken.None);

        await store.RemoveRuntimeStateAsync("room-3", CancellationToken.None);

        var read = await store.TryGetRuntimeStateAsync("room-3", CancellationToken.None);
        Assert.Null(read);
    }

    [Fact]
    public async Task PublishAfterClose_DoesNotRestoreDirectoryOrAccountMapping()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-closed", "owner");
        await store.WriteRuntimeStateAsync(state.Summary.RoomId, state);
        await store.WriteRuntimeStateAsync(state.Summary.RoomId, state with
        {
            Phase = RoomPhase.Closed,
            Members = new List<RoomPersistentMember>()
        });

        Assert.False(await store.TryPublishActiveRoomAsync("dir", state.Summary));
        Assert.False(await store.TryBindAccountRoomIfActiveAsync("owner", state.Summary.RoomId));
        Assert.Empty(await store.ListRoomsAsync("dir"));
        Assert.Null(await store.TryGetAccountRoomAsync("owner"));
    }

    [Fact]
    public async Task CloseAfterPublish_RemovesRoomAndPreventsLateBinding()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-closing", "owner");
        await store.WriteRuntimeStateAsync(state.Summary.RoomId, state);
        Assert.True(await store.TryPublishActiveRoomAsync("dir", state.Summary));
        Assert.Single(await store.ListRoomsAsync("dir"));

        await store.WriteRuntimeStateAsync(state.Summary.RoomId, state with
        {
            Phase = RoomPhase.Closed,
            Members = new List<RoomPersistentMember>()
        });

        Assert.Empty(await store.ListRoomsAsync("dir"));
        Assert.False(await store.TryBindAccountRoomIfActiveAsync("owner", state.Summary.RoomId));
    }

    [Fact]
    public async Task PublishAfterCreatorLeaves_DoesNotAcceptTransferredOwnership()
    {
        var store = new InMemoryRoomStateStore();
        var original = CreateState("room-transferred", "owner", "peer");
        var transferred = original with
        {
            Summary = original.Summary with { OwnerAccountId = "peer", PlayerCount = 1 },
            Members = new List<RoomPersistentMember> { original.Members[1] }
        };
        await store.WriteRuntimeStateAsync(original.Summary.RoomId, transferred);

        Assert.False(await store.TryPublishActiveRoomAsync("dir", original.Summary));
        Assert.Empty(await store.ListRoomsAsync("dir"));
    }

    [Fact]
    public async Task ConcurrentPublishAndBindWithClose_LeavesNoStaleRoom()
    {
        for (var iteration = 0; iteration < 32; iteration++)
        {
            var store = new InMemoryRoomStateStore();
            var state = CreateState($"room-race-{iteration}", "owner");
            await store.WriteRuntimeStateAsync(state.Summary.RoomId, state);
            var closed = state with
            {
                Phase = RoomPhase.Closed,
                Members = new List<RoomPersistentMember>()
            };

            await Task.WhenAll(
                Task.Run(() => store.TryPublishActiveRoomAsync("dir", state.Summary)),
                Task.Run(() => store.TryBindAccountRoomIfActiveAsync("owner", state.Summary.RoomId)),
                Task.Run(async () =>
                {
                    await store.WriteRuntimeStateAsync(state.Summary.RoomId, closed);
                    await store.ClearAccountRoomAsync("owner", state.Summary.RoomId);
                }));

            Assert.Empty(await store.ListRoomsAsync("dir"));
            Assert.Null(await store.TryGetAccountRoomAsync("owner"));
        }
    }

    private static RoomPersistentState CreateState(string roomId, params string[] accountIds)
    {
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: roomId,
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 8,
            PlayerCount: accountIds.Length,
            OwnerAccountId: accountIds.Length == 0 ? "owner" : accountIds[0],
            CreatedAtUnixMs: 0,
            Tags: null);

        var members = accountIds
            .Select((accountId, index) => new RoomPersistentMember(
                accountId,
                new RoomMemberState(true, 0, 0, false, index + 1)))
            .ToList();

        return new RoomPersistentState(
            SchemaVersion: RoomPersistentState.CurrentSchemaVersion,
            Summary: summary,
            DirectoryKey: "dir",
            Phase: RoomPhase.Lobby,
            PhaseReason: string.Empty,
            Members: members,
            NextJoinOrdinal: accountIds.Length + 1,
            GameplayState: new RoomGameplayPersistentState("moba.room.v1", 1, new byte[] { 1, 2, 3 }),
            Revision: 1,
            EventSequence: 1,
            Launch: new RoomLaunchPersistentState(0, 0, 0, null, new List<string>()),
            BattleCommit: new RoomBattleCommitPersistentState(0, null, RoomBattleCommitStatus.None, null, null, 0, null, 0, null),
            CommandDedupEntries: new List<RoomCommandDedupEntry>(),
            TerminalReason: null,
            UpdatedAtUnixMs: 0);
    }
}

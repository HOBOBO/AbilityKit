using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class AbandonedRoomCleanupPolicyTests
{
    private static readonly long StartTicks = TimeSpan.FromHours(1).Ticks;

    [Fact]
    public void GracePeriod_IsOneMinute()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), AbandonedRoomCleanupPolicy.GracePeriod);
    }

    [Fact]
    public void ShouldCleanup_WhenAllClientsHaveBeenOfflineForOneMinute_ReturnsTrue()
    {
        var state = CreateState(
            Client("a", online: false, offlineSinceTicks: StartTicks),
            Client("b", online: false, offlineSinceTicks: StartTicks + TimeSpan.FromMinutes(1).Ticks));

        Assert.False(AbandonedRoomCleanupPolicy.ShouldCleanup(
            state,
            StartTicks + TimeSpan.FromMinutes(2).Ticks - 1));
        Assert.True(AbandonedRoomCleanupPolicy.ShouldCleanup(
            state,
            StartTicks + TimeSpan.FromMinutes(2).Ticks));
    }

    [Fact]
    public void ShouldCleanup_WhenAnyClientReconnects_ReturnsFalse()
    {
        var state = CreateState(
            Client("a", online: false, offlineSinceTicks: StartTicks),
            Client("b", online: true, offlineSinceTicks: 0L));

        Assert.False(AbandonedRoomCleanupPolicy.ShouldCleanup(
            state,
            StartTicks + TimeSpan.FromHours(1).Ticks));
    }

    [Fact]
    public void ShouldCleanup_WhenOfflineTimestampIsMissing_ReturnsFalse()
    {
        var state = CreateState(
            Client("a", online: false, offlineSinceTicks: StartTicks),
            Client("b", online: false, offlineSinceTicks: 0L));

        Assert.False(AbandonedRoomCleanupPolicy.ShouldCleanup(
            state,
            StartTicks + TimeSpan.FromHours(1).Ticks));
    }

    [Fact]
    public void ShouldCleanup_IgnoresOnlineBotsWhenAllClientsAreOffline()
    {
        var state = CreateState(
            Client("owner", online: false, offlineSinceTicks: StartTicks),
            new RoomPersistentMember(
                "bot",
                new RoomMemberState(true, StartTicks, 0L, IsBot: true, JoinOrdinal: 2L)));

        Assert.True(AbandonedRoomCleanupPolicy.ShouldCleanup(
            state,
            StartTicks + TimeSpan.FromMinutes(1).Ticks));
    }

    private static RoomPersistentMember Client(
        string accountId,
        bool online,
        long offlineSinceTicks)
    {
        return new RoomPersistentMember(
            accountId,
            new RoomMemberState(
                online,
                StartTicks,
                offlineSinceTicks,
                IsBot: false,
                JoinOrdinal: accountId == "a" ? 1L : 2L));
    }

    private static RoomPersistentState CreateState(params RoomPersistentMember[] members)
    {
        var summary = new RoomSummary(
            "local",
            "server-a",
            "room-a",
            GameplayRoomTypes.Default,
            "Room",
            true,
            8,
            members.Length,
            members[0].AccountId,
            0L,
            null);

        return new RoomPersistentState(
            RoomPersistentState.CurrentSchemaVersion,
            summary,
            "local:server-a",
            RoomPhase.Lobby,
            string.Empty,
            members.ToList(),
            members.Length + 1L,
            new RoomGameplayPersistentState("empty", 1, Array.Empty<byte>()),
            0L,
            0L,
            new RoomLaunchPersistentState(0L, 0L, 0, null, new List<string>()),
            new RoomBattleCommitPersistentState(
                0L,
                null,
                RoomBattleCommitStatus.None,
                null,
                null,
                0UL,
                null,
                0,
                null),
            new List<RoomCommandDedupEntry>(),
            null,
            0L);
    }
}

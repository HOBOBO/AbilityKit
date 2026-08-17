using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class MobaRoomGameplayPersistenceTests
{
    [Fact]
    public void ExportRestore_RoundTripsPlayersAndRevisionWithoutDrift()
    {
        var adapter = new MobaRoomGameplayAdapter();
        var summary = CreateSummary();
        var state = adapter.CreateState(summary);
        adapter.Join(state, summary, new HashSet<string>(), "player-a");
        adapter.Join(state, summary, new HashSet<string> { "player-a" }, "player-b");
        adapter.SetReady(state, new RoomReadyRequest("player-a", true));
        adapter.SubmitCommand(state, RoomGameplayCommandRequest.CreateMobaLoadout(
            "player-a",
            heroId: 1001,
            teamId: 1,
            spawnPointId: 2,
            level: 3,
            attributeTemplateId: 4,
            basicAttackSkillId: 5,
            skillIds: new[] { 6, 7 }));

        var envelope = adapter.ExportPersistentState(state);
        var restored = adapter.RestorePersistentState(summary, envelope);
        var restoredState = Assert.IsType<MobaRoomState>(restored);

        Assert.Equal("moba.room.v1", envelope.Format);
        Assert.Equal(1, envelope.Version);
        Assert.Equal(((MobaRoomState)state).Revision, restoredState.Revision);
        Assert.Equal(2, restoredState.Players.Count);
        Assert.True(restoredState.Players["player-a"].Ready);
        Assert.Equal(1001, restoredState.Players["player-a"].HeroId);
        Assert.Equal(new[] { 6, 7 }, restoredState.Players["player-a"].SkillIds);
    }

    [Fact]
    public void Restore_WhenFormatMismatch_Throws()
    {
        var adapter = new MobaRoomGameplayAdapter();
        var summary = CreateSummary();
        var invalid = new RoomGameplayPersistentState("shooter.room.v1", 1, new byte[] { 0x01 });

        Assert.Throws<System.InvalidOperationException>(() => adapter.RestorePersistentState(summary, invalid));
    }

    [Theory]
    [InlineData(GameplayRoomTypes.Moba)]
    [InlineData(GameplayRoomTypes.LegacyMoba)]
    public void RegistryResolve_WhenRoomTypeIsMoba_ReturnsMobaAdapter(string roomType)
    {
        var registry = new RoomGameplayRegistry();

        var adapter = registry.Resolve(roomType);

        Assert.IsType<MobaRoomGameplayAdapter>(adapter);
    }

    private static RoomSummary CreateSummary()
    {
        return new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "moba-room-1",
            RoomType: GameplayRoomTypes.Moba,
            Title: "Moba Room",
            IsPublic: true,
            MaxPlayers: 5,
            PlayerCount: 0,
            OwnerAccountId: "player-a",
            CreatedAtUnixMs: 0,
            Tags: null);
    }
}

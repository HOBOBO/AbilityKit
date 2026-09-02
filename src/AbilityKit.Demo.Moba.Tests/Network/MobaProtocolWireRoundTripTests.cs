using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Core.Mathematics;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Network;

public sealed class MobaProtocolWireRoundTripTests
{
    [Fact]
    public void GeneratedDtos_RoundTripExternalValueTypesAndNestedArrays()
    {
        var aimPos = new Vec3(1.25f, -2.5f, 3.75f);
        var aimDir = new Vec3(0.5f, 0.25f, -1f);
        var input = new SkillInputEvent(
            2,
            SkillInputPhase.Release,
            pointerId: 7,
            targetActorId: 41,
            aimPos: in aimPos,
            aimDir: in aimDir,
            opCode: 99,
            payload: new byte[] { 3, 1, 4 });

        var decodedInput = MemoryPackSerializer.Deserialize<SkillInputEvent>(
            MemoryPackSerializer.Serialize(input));

        Assert.Equal(input.Slot, decodedInput.Slot);
        Assert.Equal(input.Phase, decodedInput.Phase);
        Assert.Equal(input.AimPos, decodedInput.AimPos);
        Assert.Equal(input.AimDir, decodedInput.AimDir);
        Assert.Equal(input.Payload, decodedInput.Payload);

        var room = new MobaRoomSnapshot(
            12,
            "match-wire",
            7,
            12345,
            30,
            2,
            1,
            10,
            true,
            new[]
            {
                new MobaRoomPlayerSnapshot(
                    new PlayerId("player-1"),
                    2,
                    true,
                    101,
                    201,
                    3,
                    301,
                    401,
                    new[] { 501, 502 })
            });

        var decodedRoom = MemoryPackSerializer.Deserialize<MobaRoomSnapshot>(
            MemoryPackSerializer.Serialize(room));

        Assert.Equal(room.Revision, decodedRoom.Revision);
        Assert.Equal(room.MatchId, decodedRoom.MatchId);
        Assert.Equal("player-1", Assert.Single(decodedRoom.Players).PlayerId.Value);
        Assert.Equal(new[] { 501, 502 }, decodedRoom.Players[0].SkillIds);
    }

    [Fact]
    public void GeneratedStateSyncDtos_RoundTripRepresentativeCodecs()
    {
        var transform = Assert.Single(MobaActorTransformSnapshotCodec.Deserialize(
            MobaActorTransformSnapshotCodec.Serialize(new[]
            {
                new MobaActorTransformSnapshotEntry(11, 1f, 2f, 3f, 0f, 1f, 0f)
            })));
        Assert.Equal(11, transform.ActorId);
        Assert.Equal(1f, transform.ForwardY);

        var spawn = Assert.Single(MobaActorSpawnSnapshotCodec.Deserialize(
            MobaActorSpawnSnapshotCodec.Serialize(new[]
            {
                new MobaActorSpawnSnapshotEntry(12, 1, 200, 11, 4f, 5f, 6f)
            })));
        Assert.Equal(200, spawn.Code);

        var despawn = Assert.Single(MobaActorDespawnSnapshotCodec.Deserialize(
            MobaActorDespawnSnapshotCodec.Serialize(new[]
            {
                new MobaActorDespawnSnapshotEntry(12, 3)
            })));
        Assert.Equal((byte)3, despawn.Reason);

        var damage = Assert.Single(MobaDamageEventSnapshotCodec.Deserialize(
            MobaDamageEventSnapshotCodec.Serialize(new[]
            {
                new MobaDamageEventSnapshotEntry(1, 11, 12, 2, 25f, 3, 4, 75f, 100f)
            })));
        Assert.Equal(25f, damage.Value);

        var projectile = Assert.Single(MobaProjectileEventSnapshotCodec.Deserialize(
            MobaProjectileEventSnapshotCodec.Serialize(new[]
            {
                new MobaProjectileEventSnapshotEntry(1, 20, 11, 300, 11, 11, 1f, 2f, 3f, 4, 5, 21, 0f, 0f, 1f)
            })));
        Assert.Equal(21, projectile.ProjectileId);
        Assert.Equal(1f, projectile.ForwardZ);

        var cue = Assert.Single(MobaPresentationCueSnapshotCodec.Deserialize(
            MobaPresentationCueSnapshotCodec.Serialize(new[]
            {
                new MobaPresentationCueSnapshotEntry
                {
                    Stage = 4,
                    CueKind = "hit",
                    Targets = new[] { 12 },
                    ColorA = 1f,
                    ConfirmedFrame = 88,
                }
            })));
        Assert.Equal("hit", cue.CueKind);
        Assert.Equal(88, cue.ConfirmedFrame);
    }
}

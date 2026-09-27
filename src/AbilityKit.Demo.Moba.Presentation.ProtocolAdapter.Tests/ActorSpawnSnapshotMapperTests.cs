using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class ActorSpawnSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(ActorSpawnSnapshotDecoder.Decode(null!));
        Assert.Empty(ActorSpawnSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(ActorSpawnSnapshotDecoder.Decode(
            MobaActorSpawnSnapshotCodec.Serialize(Array.Empty<MobaActorSpawnSnapshotEntry>())));
    }

    [Fact]
    public void DecoderMapsAllWireFieldsAndPreservesCoordinates()
    {
        var payload = MobaActorSpawnSnapshotCodec.Serialize(new[]
        {
            new MobaActorSpawnSnapshotEntry(
                netId: 17,
                kind: (int)SpawnEntityKind.Projectile,
                code: 5001,
                ownerNetId: 11,
                x: 1.25f,
                y: 2.5f,
                z: 3.75f),
        });

        var actual = Assert.Single(ActorSpawnSnapshotDecoder.Decode(payload));

        Assert.Equal(ActorSpawnKind.Projectile, actual.Kind);
        Assert.True(actual.IsProjectile);
        Assert.Equal(17, actual.ActorId);
        Assert.Equal(11, actual.OwnerActorId);
        Assert.Equal(5001, actual.EntityCode);
        Assert.Equal(5001, actual.CharacterId);
        Assert.Equal(string.Empty, actual.Name);
        Assert.Equal(1.25f, actual.PositionX);
        Assert.Equal(2.5f, actual.PositionY);
        Assert.Equal(3.75f, actual.PositionZ);
        Assert.Equal(0f, actual.RotationY);
        Assert.Equal(1f, actual.Scale);
        Assert.Equal(0, actual.TeamId);
        Assert.Equal(0f, actual.MaxHp);
        Assert.Equal(0f, actual.Hp);
        Assert.Equal("11", actual.PlayerId);
    }

    [Fact]
    public void MapperFallsBackToActorIdWhenWireOwnerIsMissing()
    {
        var entry = new MobaActorSpawnSnapshotEntry(
            netId: 21,
            kind: (int)SpawnEntityKind.Character,
            code: 1001,
            ownerNetId: 0,
            x: 0f,
            y: 0f,
            z: 0f);

        var actual = ActorSpawnSnapshotMapper.Map(in entry);

        Assert.Equal(ActorSpawnKind.Character, actual.Kind);
        Assert.False(actual.IsProjectile);
        Assert.Equal(0, actual.OwnerActorId);
        Assert.Equal("21", actual.PlayerId);
    }

    [Fact]
    public void LegacyConstructorRetainsCharacterDefaults()
    {
        var actual = new ActorSpawnData(
            actorId: 9,
            entityCode: 1002,
            characterId: 1002,
            name: "hero",
            x: 10f,
            y: 20f,
            z: 30f,
            rotationY: 45f,
            scale: 2f,
            teamId: 3,
            maxHp: 120f,
            hp: 80f,
            playerId: "player-9");

        Assert.Equal(ActorSpawnKind.Character, actual.Kind);
        Assert.False(actual.IsProjectile);
        Assert.Equal(0, actual.OwnerActorId);
        Assert.Equal("player-9", actual.PlayerId);
        Assert.Equal(45f, actual.RotationY);
        Assert.Equal(2f, actual.Scale);
    }
}

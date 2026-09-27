using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class BattleEnterGameSnapshotMapperTests
{
    [Fact]
    public void DecoderPreservesResponseFieldsAndDerivesLocalActorPosition()
    {
        var payload = MobaEnterGamePayloadCodec.Serialize(new Vec3(1.5f, 2.5f, 3.5f));
        var response = new EnterMobaGameRes(
            new WorldId("world-1"),
            new PlayerId("player-1"),
            localActorId: 101,
            randomSeed: 202,
            tickRate: 30,
            inputDelayFrames: 2,
            players: new[]
            {
                new MobaPlayerEntry(new PlayerId("player-2"), 3, 404, 5),
            },
            opCode: MobaEnterGamePayloadCodec.PayloadOpCode,
            payload: payload,
            playersLoadout: new[]
            {
                CreateLoadout("player-1", new[] { 701, 702 }),
            });

        var actual = BattleEnterGameSnapshotDecoder.Decode(
            EnterMobaGameCodec.SerializeRes(in response));

        Assert.Equal("world-1", actual.WorldId);
        Assert.Equal("player-1", actual.PlayerId);
        Assert.Equal(101, actual.LocalActorId);
        Assert.Equal(202, actual.RandomSeed);
        Assert.Equal(30, actual.TickRate);
        Assert.Equal(2, actual.InputDelayFrames);
        Assert.Equal(MobaEnterGamePayloadCodec.PayloadOpCode, actual.OpCode);
        Assert.Equal(payload, actual.Payload);
        Assert.True(actual.HasLocalActorPosition);
        Assert.Equal(1.5f, actual.LocalActorX);
        Assert.Equal(2.5f, actual.LocalActorY);
        Assert.Equal(3.5f, actual.LocalActorZ);

        var player = Assert.Single(actual.Players);
        Assert.Equal("player-2", player.PlayerId);
        Assert.Equal(3, player.TeamId);
        Assert.Equal(404, player.HeroId);
        Assert.Equal(5, player.SpawnIndex);

        var loadout = Assert.Single(actual.PlayersLoadout);
        Assert.Equal("player-1", loadout.PlayerId);
        Assert.Equal(1, loadout.TeamId);
        Assert.Equal(601, loadout.HeroId);
        Assert.Equal(602, loadout.AttributeTemplateId);
        Assert.Equal(6, loadout.Level);
        Assert.Equal(700, loadout.BasicAttackSkillId);
        Assert.Equal(new[] { 701, 702 }, loadout.SkillIds);
        Assert.Equal(7, loadout.SpawnIndex);
        Assert.Equal(8, loadout.UnitSubType);
        Assert.Equal(9, loadout.MainType);
        Assert.Equal(1, loadout.HasSpawnPosition);
        Assert.Equal(10.5f, loadout.SpawnX);
        Assert.Equal(11.5f, loadout.SpawnY);
        Assert.Equal(12.5f, loadout.SpawnZ);
        Assert.Equal(13, loadout.BrainId);
        Assert.False(loadout.EnableBrainOnSpawn);
    }

    [Fact]
    public void MapperOwnsMutableProtocolArrays()
    {
        var payload = new byte[] { 1, 2 };
        var skills = new[] { 3, 4 };
        var players = new[]
        {
            new MobaPlayerEntry(new PlayerId("player"), 1, 2, 3),
        };
        var loadouts = new[]
        {
            CreateLoadout("player", skills),
        };
        var response = new EnterMobaGameRes(
            new WorldId("world"),
            new PlayerId("player"),
            5,
            6,
            30,
            1,
            players,
            payload: payload,
            playersLoadout: loadouts);

        var actual = BattleEnterGameSnapshotMapper.Map(in response);
        payload[0] = 99;
        skills[0] = 99;
        players[0] = default;
        loadouts[0] = default;

        Assert.Equal(new byte[] { 1, 2 }, actual.Payload);
        Assert.Equal("player", Assert.Single(actual.Players).PlayerId);
        Assert.Equal(new[] { 3, 4 }, Assert.Single(actual.PlayersLoadout).SkillIds);
        Assert.False(actual.HasLocalActorPosition);
    }

    private static MobaPlayerLoadout CreateLoadout(string playerId, int[] skillIds)
    {
        return new MobaPlayerLoadout(
            new PlayerId(playerId),
            teamId: 1,
            heroId: 601,
            attributeTemplateId: 602,
            level: 6,
            basicAttackSkillId: 700,
            skillIds: skillIds,
            spawnIndex: 7,
            unitSubType: 8,
            mainType: 9,
            hasSpawnPosition: 1,
            spawnX: 10.5f,
            spawnY: 11.5f,
            spawnZ: 12.5f,
            brainId: 13,
            enableBrainOnSpawn: false);
    }
}

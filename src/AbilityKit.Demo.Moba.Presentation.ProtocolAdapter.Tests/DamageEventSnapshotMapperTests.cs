using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;
using ContractDamageEventKind = AbilityKit.Demo.Moba.Share.DamageEventKind;
using ProtocolDamageEventKind = AbilityKit.Protocol.Moba.StateSync.DamageEventKind;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class DamageEventSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(DamageEventSnapshotDecoder.Decode(null!));
        Assert.Empty(DamageEventSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(DamageEventSnapshotDecoder.Decode(
            MobaDamageEventSnapshotCodec.Serialize(Array.Empty<MobaDamageEventSnapshotEntry>())));
    }

    [Fact]
    public void DecoderDeserializesAndMapsAllWireFields()
    {
        var payload = MobaDamageEventSnapshotCodec.Serialize(new[]
        {
            new MobaDamageEventSnapshotEntry(
                kind: (int)ProtocolDamageEventKind.Heal,
                attackerActorId: 11,
                targetActorId: 22,
                damageType: 3,
                value: 18.6f,
                reasonKind: 4,
                reasonParam: 7001,
                targetHp: 72.4f,
                targetMaxHp: 120.5f),
        });

        var actual = Assert.Single(DamageEventSnapshotDecoder.Decode(payload));

        Assert.Equal(ContractDamageEventKind.Heal, actual.Kind);
        Assert.True(actual.IsHeal);
        Assert.Equal(11, actual.AttackerId);
        Assert.Equal(22, actual.TargetId);
        Assert.Equal(3, actual.DamageType);
        Assert.Equal(18.6f, actual.Value);
        Assert.Equal(4, actual.ReasonKind);
        Assert.Equal(7001, actual.ReasonParam);
        Assert.Equal(72.4f, actual.TargetHp);
        Assert.Equal(120.5f, actual.TargetMaxHp);
        Assert.False(actual.IsKill);
        Assert.Equal(7001, actual.SourceId);
        Assert.Equal(19, actual.DamageValue);
        Assert.Equal(72, actual.TargetHpAfter);
    }

    [Fact]
    public void MapperDerivesKillStateFromTargetHp()
    {
        var entry = new MobaDamageEventSnapshotEntry(
            kind: (int)ProtocolDamageEventKind.Damage,
            attackerActorId: 1,
            targetActorId: 2,
            damageType: 0,
            value: 50f,
            reasonKind: 0,
            reasonParam: 0,
            targetHp: 0f,
            targetMaxHp: 50f);

        var actual = DamageEventSnapshotMapper.Map(in entry);

        Assert.Equal(ContractDamageEventKind.Damage, actual.Kind);
        Assert.False(actual.IsHeal);
        Assert.True(actual.IsKill);
    }
}


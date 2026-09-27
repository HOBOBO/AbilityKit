using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class SkillStateSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(SkillStateSnapshotDecoder.Decode(null!));
        Assert.Empty(SkillStateSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(SkillStateSnapshotDecoder.Decode(
            MobaSkillStateSnapshotCodec.Serialize(
                Array.Empty<MobaSkillStateSnapshotEntry>())));
    }

    [Fact]
    public void MapperPreservesAllPresentationFields()
    {
        var entry = new MobaSkillStateSnapshotEntry
        {
            ActorId = 17,
            Slot = 2,
            SkillId = 7102,
            Level = 3,
            CooldownTotalMs = 8000,
            CooldownRemainingMs = 6200,
            CooldownEndTimeMs = 15200,
            ServerTimeMs = 9000,
            Availability = MobaSkillAvailabilityState.CoolingDown,
            DisableReason = 4,
            MaxCharges = 3,
            CurrentCharges = 1,
            ChargeRecoveryRemainingMs = 2500,
            SharedCooldownRemainingMs = 300,
            GlobalCooldownRemainingMs = 120,
        };

        var actual = SkillStateSnapshotMapper.Map(in entry);

        Assert.Equal(17, actual.ActorId);
        Assert.Equal(2, actual.Slot);
        Assert.Equal(7102, actual.SkillId);
        Assert.Equal(3, actual.Level);
        Assert.Equal(8000, actual.CooldownTotalMs);
        Assert.Equal(6200, actual.CooldownRemainingMs);
        Assert.Equal(15200, actual.CooldownEndTimeMs);
        Assert.Equal(9000, actual.ServerTimeMs);
        Assert.Equal(SkillAvailabilityState.CoolingDown, actual.Availability);
        Assert.Equal(4, actual.DisableReason);
        Assert.Equal(3, actual.MaxCharges);
        Assert.Equal(1, actual.CurrentCharges);
        Assert.Equal(2500, actual.ChargeRecoveryRemainingMs);
        Assert.Equal(300, actual.SharedCooldownRemainingMs);
        Assert.Equal(120, actual.GlobalCooldownRemainingMs);
    }

    [Fact]
    public void MapperPreservesUnknownWireAvailability()
    {
        var entry = new MobaSkillStateSnapshotEntry
        {
            Availability = (MobaSkillAvailabilityState)91,
        };

        var actual = SkillStateSnapshotMapper.Map(in entry);

        Assert.Equal(91, (int)actual.Availability);
    }
}

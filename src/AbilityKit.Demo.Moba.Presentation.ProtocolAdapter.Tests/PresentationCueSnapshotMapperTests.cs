using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;
using ContractPredictionState = AbilityKit.Demo.Moba.Share.PresentationCuePredictionState;
using ContractStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;
using ProtocolPredictionState = AbilityKit.Protocol.Moba.StateSync.MobaPresentationCuePredictionState;
using ProtocolStage = AbilityKit.Protocol.Moba.StateSync.PresentationCueStage;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class PresentationCueSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(PresentationCueSnapshotDecoder.Decode(null!));
        Assert.Empty(PresentationCueSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(PresentationCueSnapshotDecoder.Decode(
            MobaPresentationCueSnapshotCodec.Serialize(Array.Empty<MobaPresentationCueSnapshotEntry>())));
    }

    [Fact]
    public void DecoderDeserializesAndMapsWirePayload()
    {
        var payload = MobaPresentationCueSnapshotCodec.Serialize(new[]
        {
            new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)ProtocolStage.Refreshed,
                RequestKey = "buff-refresh",
                TargetActorId = 22,
                InstanceKey = "buff:22:7:101",
                PredictionKey = 87,
                PredictionState = (int)ProtocolPredictionState.ServerConfirmed,
                ConfirmedFrame = 121,
            },
        });

        var actual = Assert.Single(PresentationCueSnapshotDecoder.Decode(payload));

        Assert.Equal(ContractStage.Refreshed, actual.Stage);
        Assert.Equal("buff-refresh", actual.RequestKey);
        Assert.Equal(22, actual.TargetActorId);
        Assert.Equal("buff:22:7:101", actual.InstanceKey);
        Assert.Equal(87, actual.PredictionKey);
        Assert.Equal(ContractPredictionState.ServerConfirmed, actual.PredictionState);
        Assert.Equal(121, actual.ConfirmedFrame);
    }

    [Fact]
    public void MapEmptyBatchReturnsEmptyContractBatch()
    {
        Assert.Empty(PresentationCueSnapshotMapper.Map(null!));
        Assert.Empty(PresentationCueSnapshotMapper.Map(Array.Empty<MobaPresentationCueSnapshotEntry>()));
    }

    [Fact]
    public void MapPreservesProtocolFieldsAndDecodesPositions()
    {
        var entry = new MobaPresentationCueSnapshotEntry
        {
            Stage = (int)ProtocolStage.Started,
            CueKind = "skill.cast",
            CueVfxId = "vfx.cast",
            CueSfxId = "sfx.cast",
            TemplateId = 6001,
            VfxId = 7101,
            SfxId = 8101,
            RequestKey = "cast-q-87",
            SourceActorId = 11,
            TargetActorId = 22,
            Targets = new[] { 22, 23 },
            Positions = new[] { 1f, 2f, 3f, 4f, 5f, 6f, 99f },
            NumericParamKeys = new[] { 2 },
            NumericParamValues = new[] { 8f },
            StringParamKeys = new[] { "variant" },
            StringParamValues = new[] { "critical" },
            PredictionKey = 87,
            PredictionState = (int)ProtocolPredictionState.Corrected,
            PredictedFrame = 120,
            ConfirmedFrame = 121,
        };

        var actual = PresentationCueSnapshotMapper.Map(in entry);

        Assert.Equal(ContractStage.Started, actual.Stage);
        Assert.Equal("skill.cast", actual.CueKind);
        Assert.Equal("cast-q-87", actual.RequestKey);
        Assert.Equal(new[] { 22, 23 }, actual.Targets);
        Assert.Collection(
            actual.Positions,
            first => AssertPosition(first, 1f, 2f, 3f),
            second => AssertPosition(second, 4f, 5f, 6f));
        Assert.Equal(new[] { 2 }, actual.NumericParamKeys);
        Assert.Equal(new[] { 8f }, actual.NumericParamValues);
        Assert.Equal(new[] { "variant" }, actual.StringParamKeys);
        Assert.Equal(new[] { "critical" }, actual.StringParamValues);
        Assert.Equal(87, actual.PredictionKey);
        Assert.Equal(ContractPredictionState.Corrected, actual.PredictionState);
        Assert.Equal(120, actual.PredictedFrame);
        Assert.Equal(121, actual.ConfirmedFrame);
    }

    [Fact]
    public void MapNormalizesNullCollections()
    {
        var entry = new MobaPresentationCueSnapshotEntry();

        var actual = PresentationCueSnapshotMapper.Map(in entry);

        Assert.Empty(actual.Targets);
        Assert.Empty(actual.Positions);
        Assert.Empty(actual.NumericParamKeys);
        Assert.Empty(actual.NumericParamValues);
        Assert.Empty(actual.StringParamKeys);
        Assert.Empty(actual.StringParamValues);
    }

    private static void AssertPosition(SnapshotVec3 actual, float x, float y, float z)
    {
        Assert.Equal(x, actual.X);
        Assert.Equal(y, actual.Y);
        Assert.Equal(z, actual.Z);
    }
}

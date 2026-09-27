using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Adapter.Tests;

public sealed class BattlePresentationCueResolverTests
{
    private readonly BattlePresentationCueResolver _resolver = new();

    [Fact]
    public void PredictionKeyCorrelatesClientAndAuthorityRequests()
    {
        var predicted = CreateCue(
            requestKey: "client-request",
            predictionKey: 87,
            predictionState: PresentationCuePredictionState.Predicted);
        var confirmed = CreateCue(
            requestKey: "authority-request",
            predictionKey: 87,
            predictionState: PresentationCuePredictionState.ServerConfirmed,
            confirmedFrame: 121);

        Assert.Equal(
            BattlePresentationCueRequestKey.From(in predicted),
            BattlePresentationCueRequestKey.From(in confirmed));
    }

    [Fact]
    public void RejectedStartedCueMapsToStop()
    {
        var cue = CreateCue(
            stage: PresentationCueStage.Started,
            predictionKey: 87,
            predictionState: PresentationCuePredictionState.Rejected,
            confirmedFrame: 121);

        var decision = _resolver.Resolve(in cue);

        Assert.Equal(BattlePresentationCueDecisionKind.Stop, decision.Kind);
        Assert.Equal(BattlePresentationCueRequestKey.FromPredictionKey(87), decision.RequestKey);
    }

    [Fact]
    public void PlayCueMapsPositionOffsetDurationScaleAndRadius()
    {
        var cue = CreateCue(
            positions: new[] { new SnapshotVec3(1f, 2f, 3f) },
            offsetX: 0.5f,
            offsetY: 1.5f,
            offsetZ: -0.25f,
            durationMsOverride: 5000,
            scale: 1.25f,
            numericParamKeys: new[] { 99, 2 },
            numericParamValues: new[] { 3f, 8f });

        var decision = _resolver.Resolve(in cue);
        var request = decision.SpawnRequest;

        Assert.Equal(BattlePresentationCueDecisionKind.Play, decision.Kind);
        Assert.True(request.HasExplicitPosition);
        Assert.Equal(1f, request.ExplicitPosition.X);
        Assert.Equal(2f, request.ExplicitPosition.Y);
        Assert.Equal(3f, request.ExplicitPosition.Z);
        Assert.Equal(0.5f, request.Offset.X);
        Assert.Equal(1.5f, request.Offset.Y);
        Assert.Equal(-0.25f, request.Offset.Z);
        Assert.Equal(5000, request.DurationMsOverride);
        Assert.Equal(1.25f, request.Scale);
        Assert.Equal(8f, request.Radius);
    }

    [Theory]
    [InlineData(PresentationCuePredictionState.Predicted, 120, 0, PresentationCuePredictionOutcome.Predicted, 120)]
    [InlineData(PresentationCuePredictionState.ServerConfirmed, 120, 121, PresentationCuePredictionOutcome.Confirmed, 121)]
    [InlineData(PresentationCuePredictionState.Corrected, 120, 122, PresentationCuePredictionOutcome.Corrected, 122)]
    public void ReconciliationMapperUsesOutcomeSpecificRevision(
        PresentationCuePredictionState state,
        int predictedFrame,
        int confirmedFrame,
        PresentationCuePredictionOutcome expectedOutcome,
        int expectedRevision)
    {
        var cue = CreateCue(
            predictionKey: 87,
            predictionState: state,
            predictedFrame: predictedFrame,
            confirmedFrame: confirmedFrame);
        var decision = _resolver.Resolve(in cue);

        var update = BattlePresentationCueReconciliationMapper.CreateUpdate(
            in cue,
            in decision,
            generation: 3);

        Assert.Equal(expectedOutcome, update.Outcome);
        Assert.Equal(expectedRevision, update.Revision);
        Assert.Equal(3, update.Generation);
    }

    [Fact]
    public void ReconciliationTrackerRejectsOlderAuthorityOutcome()
    {
        var tracker = new BattlePresentationCueReconciliationTracker(capacity: 4);
        var corrected = CreateCue(
            predictionKey: 87,
            predictionState: PresentationCuePredictionState.Corrected,
            confirmedFrame: 125);
        var staleRejected = CreateCue(
            predictionKey: 87,
            predictionState: PresentationCuePredictionState.Rejected,
            confirmedFrame: 124);
        var key = BattlePresentationCueRequestKey.From(in corrected);

        Assert.True(tracker.Accept(key, in corrected));
        Assert.False(tracker.Accept(key, in staleRejected));
        Assert.Equal(1, tracker.CorrectedCount);
        Assert.Equal(0, tracker.RejectedCount);
        Assert.Equal(1, tracker.StaleUpdateCount);
    }

    [Fact]
    public void PresentationCueContractNormalizesNullCollections()
    {
        var cue = CreateCue();

        Assert.Empty(cue.Targets);
        Assert.Empty(cue.Positions);
        Assert.Empty(cue.NumericParamKeys);
        Assert.Empty(cue.NumericParamValues);
        Assert.Empty(cue.StringParamKeys);
        Assert.Empty(cue.StringParamValues);
    }

    private static PresentationCueData CreateCue(
        PresentationCueStage stage = PresentationCueStage.Started,
        string requestKey = "cast-q",
        int predictionKey = 0,
        PresentationCuePredictionState predictionState = PresentationCuePredictionState.None,
        int predictedFrame = 120,
        int confirmedFrame = 0,
        IReadOnlyList<SnapshotVec3>? positions = null,
        float offsetX = 0f,
        float offsetY = 0f,
        float offsetZ = 0f,
        int durationMsOverride = 300,
        float scale = 1f,
        IReadOnlyList<int>? numericParamKeys = null,
        IReadOnlyList<float>? numericParamValues = null)
    {
        return new PresentationCueData(
            stage: stage,
            cueKind: "skill.cast",
            cueVfxId: string.Empty,
            cueSfxId: string.Empty,
            templateId: 0,
            vfxId: 7101,
            sfxId: 0,
            requestKey: requestKey,
            sourceActorId: 11,
            targetActorId: 22,
            triggerEventId: 501,
            triggerEventName: "SkillCast",
            triggerId: 6001,
            phase: 1,
            priority: 0,
            order: 1,
            actionIndex: -1,
            interruptReason: 0,
            interruptSourceName: null,
            interruptTriggerId: 0,
            interruptConditionPassed: false,
            targets: null,
            positions: positions,
            offsetX: offsetX,
            offsetY: offsetY,
            offsetZ: offsetZ,
            durationMsOverride: durationMsOverride,
            scale: scale,
            colorR: 1f,
            colorG: 1f,
            colorB: 1f,
            colorA: 1f,
            numericParamKeys: numericParamKeys,
            numericParamValues: numericParamValues,
            predictionKey: predictionKey,
            predictionState: predictionState,
            predictedFrame: predictedFrame,
            confirmedFrame: confirmedFrame);
    }
}

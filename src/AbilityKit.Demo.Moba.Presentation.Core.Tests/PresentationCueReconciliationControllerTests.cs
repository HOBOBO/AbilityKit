using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationCueReconciliationControllerTests
{
    [Fact]
    public void Predicted_cue_is_promoted_without_replacing_active_view()
    {
        var controller = CreateController();

        var predicted = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 120));
        var confirmed = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 121));

        Assert.Equal(PresentationCueCommandKind.EnsureActive, predicted.Command);
        Assert.Equal(PresentationCueCommandKind.EnsureActive, confirmed.Command);
        Assert.True(confirmed.IsPromotion);
        Assert.Equal(1, controller.ActiveCount);
        Assert.Equal(1, controller.ConfirmedCount);
    }

    [Fact]
    public void Rejected_prediction_emits_idempotent_stop()
    {
        var controller = CreateController();
        controller.Process(Update(
            "cast-w",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 40));

        var rejected = controller.Process(Update(
            "cast-w",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Rejected,
            revision: 42));
        var replayed = controller.Process(Update(
            "cast-w",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Rejected,
            revision: 42));

        Assert.Equal(PresentationCueCommandKind.Stop, rejected.Command);
        Assert.Equal(PresentationCueCommandKind.Stop, replayed.Command);
        Assert.True(replayed.IsDuplicate);
        Assert.Equal(0, controller.ActiveCount);
        Assert.Equal(1, controller.RejectedCount);
        Assert.Equal(1, controller.DuplicateUpdateCount);
    }

    [Fact]
    public void Older_authority_outcome_is_rejected()
    {
        var controller = CreateController();
        controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Refresh,
            PresentationCuePredictionOutcome.Corrected,
            revision: 125));

        var stale = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Rejected,
            revision: 124));

        Assert.False(stale.Accepted);
        Assert.Equal(PresentationCueReconciliationRejectReason.StaleRevision, stale.RejectReason);
        Assert.Equal(PresentationCueCommandKind.None, stale.Command);
        Assert.Equal(1, controller.CorrectedCount);
        Assert.Equal(0, controller.RejectedCount);
        Assert.Equal(1, controller.StaleUpdateCount);
    }

    [Fact]
    public void Late_prediction_cannot_demote_authority_outcome()
    {
        var controller = CreateController();
        controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 121));

        var latePrediction = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 120));
        var replayedConfirmation = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 121));

        Assert.False(latePrediction.Accepted);
        Assert.Equal(
            PresentationCueReconciliationRejectReason.SupersededPrediction,
            latePrediction.RejectReason);
        Assert.True(replayedConfirmation.Accepted);
        Assert.True(replayedConfirmation.IsDuplicate);
        Assert.Equal(1, controller.ConfirmedCount);
    }

    [Fact]
    public void Same_revision_authority_outcome_only_moves_forward()
    {
        var controller = CreateController();
        var confirmed = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 121));
        var corrected = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Refresh,
            PresentationCuePredictionOutcome.Corrected,
            revision: 121));
        var rejected = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Rejected,
            revision: 121));
        var lateCorrection = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Refresh,
            PresentationCuePredictionOutcome.Corrected,
            revision: 121));

        Assert.True(confirmed.Accepted);
        Assert.True(corrected.Accepted);
        Assert.True(rejected.Accepted);
        Assert.False(lateCorrection.Accepted);
        Assert.Equal(PresentationCueCommandKind.Stop, rejected.Command);
        Assert.Equal(PresentationCueReconciliationRejectReason.StaleRevision, lateCorrection.RejectReason);
        Assert.Equal(1, controller.ConfirmedCount);
        Assert.Equal(1, controller.CorrectedCount);
        Assert.Equal(1, controller.RejectedCount);
    }

    [Fact]
    public void Generation_change_returns_active_keys_and_rejects_old_updates()
    {
        var controller = CreateController();
        controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 10));
        controller.Process(Update(
            "buff-loop",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.None,
            revision: 10));

        var change = controller.BeginGeneration(2);
        var stale = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 11,
            generation: 1));
        var current = controller.Process(Update(
            "cast-q",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 12,
            generation: 2));

        Assert.True(change.Applied);
        Assert.Equal(2, change.KeysToStop.Count);
        Assert.Contains("cast-q", change.KeysToStop);
        Assert.Contains("buff-loop", change.KeysToStop);
        Assert.False(stale.Accepted);
        Assert.Equal(PresentationCueReconciliationRejectReason.GenerationMismatch, stale.RejectReason);
        Assert.True(current.Accepted);
        Assert.Equal(1, controller.GenerationMismatchCount);
    }

    [Fact]
    public void Generation_cannot_move_backwards_or_repeat()
    {
        var controller = CreateController();

        var repeated = controller.BeginGeneration(1);
        var backwards = controller.BeginGeneration(0);

        Assert.False(repeated.Applied);
        Assert.False(backwards.Applied);
        Assert.Equal(1, controller.CurrentGeneration);
    }

    [Fact]
    public void Empty_key_is_rejected_without_mutating_state()
    {
        var controller = CreateController();

        var decision = controller.Process(Update(
            null!,
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Predicted,
            revision: 1));

        Assert.False(decision.Accepted);
        Assert.Equal(PresentationCueReconciliationRejectReason.EmptyKey, decision.RejectReason);
        Assert.Equal(0, controller.ActiveCount);
    }

    [Fact]
    public void Missing_visual_is_tracked_without_emitting_view_command()
    {
        var controller = CreateController();

        var decision = controller.Process(Update(
            "logic-only",
            PresentationCueSignal.Activate,
            PresentationCuePredictionOutcome.Confirmed,
            revision: 7,
            hasVisual: false));

        Assert.True(decision.Accepted);
        Assert.Equal(PresentationCueCommandKind.None, decision.Command);
        Assert.Equal(0, controller.ActiveCount);
        Assert.Equal(1, controller.ConfirmedCount);
    }

    private static PresentationCueReconciliationController<string, int> CreateController()
    {
        return new PresentationCueReconciliationController<string, int>(
            capacity: 4,
            initialGeneration: 1,
            comparer: StringComparer.Ordinal);
    }

    private static PresentationCueUpdate<string, int> Update(
        string key,
        PresentationCueSignal signal,
        PresentationCuePredictionOutcome outcome,
        int revision,
        long generation = 1,
        bool hasVisual = true)
    {
        return new PresentationCueUpdate<string, int>(
            key,
            signal,
            outcome,
            revision,
            generation,
            hasVisual,
            payload: 7101);
    }
}

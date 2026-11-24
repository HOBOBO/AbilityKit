using AbilityKit.BattleScenario.Editor;
using Xunit;
using Phase = AbilityKit.BattleScenario.Editor.LocalCorrectionPreview.Phase;

namespace AbilityKit.BattleScenario.Tests;

public sealed class LocalCorrectionPreviewTests
{
    [Fact]
    public void SavedRecipe_RecreatesPredictionRestoreAndReplayInTheSameOrder()
    {
        var recipe = LocalCorrectionPreview.CreateRecipe();
        var restoredRecipe = BattleScenarioCodec.Parse(BattleScenarioCodec.Serialize(recipe));
        var first = LocalCorrectionPreview.Compile(BattleScenarioCompiler.Compile(recipe));
        var second = LocalCorrectionPreview.Compile(BattleScenarioCompiler.Compile(restoredRecipe));

        Assert.Equal(first, second.AsEnumerable());
        Assert.Equal(new[] { (Phase.Start, 0, 0, 0), (Phase.Predicted, 1, 1, 4),
                (Phase.Predicted, 2, 2, 7), (Phase.Predicted, 3, 3, 9), (Phase.Mismatch, 4, 1, 9),
                (Phase.Restored, 4, 1, 2), (Phase.Replayed, 4, 2, 5), (Phase.Replayed, 4, 3, 7) },
            first.Select(frame => (frame.Stage, frame.AtTick, frame.SimulationTick, frame.ViewPosition)));
        Assert.Equal(4, first[4].PreviousAnchorPosition);
        Assert.Equal(2, first[4].AuthorityPosition);
    }

    [Fact]
    public void EditedAuthorityThatMatches_DoesNotCreateSpuriousRestoreSteps()
    {
        var recipe = LocalCorrectionPreview.CreateRecipe();
        Correct(recipe).Parameters = new Dictionary<string, string> { ["anchorTick"] = "1", ["position"] = "4" };

        var frames = LocalCorrectionPreview.Compile(BattleScenarioCompiler.Compile(recipe));

        Assert.Equal(Phase.Match, frames.Last().Stage);
        Assert.Equal(9, frames.Last().ViewPosition);
        Assert.DoesNotContain(frames, frame => frame.Stage is Phase.Restored or Phase.Replayed);
    }

    [Fact]
    public void MissingAnchor_RejectsRecipeRatherThanGuessingState()
    {
        var recipe = LocalCorrectionPreview.CreateRecipe();
        Correct(recipe).Parameters = new Dictionary<string, string> { ["anchorTick"] = "5", ["position"] = "2" };

        var error = Assert.Throws<InvalidOperationException>(() =>
            LocalCorrectionPreview.Compile(BattleScenarioCompiler.Compile(recipe)));

        Assert.Contains("Tick 5", error.Message);
    }

    private static CommandBlock Correct(BattleScenarioDocument recipe) =>
        Assert.IsType<CommandBlock>(recipe.Sections.Timeline.Last());
}

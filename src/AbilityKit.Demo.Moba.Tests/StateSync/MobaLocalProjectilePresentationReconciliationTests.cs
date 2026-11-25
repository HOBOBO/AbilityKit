using AbilityKit.Demo.Moba.BattleScenario.Runner;
using AbilityKit.Game.Flow;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
[Trait("Gate", "BattleScenario")]
public sealed class MobaLocalProjectilePresentationReconciliationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jinx_w_visual_promotes_in_place_or_is_removed_without_respawn(bool accepted)
    {
        var replay = JinxProjectilePresentationReplay.Record(accepted);
        Assert.Equal(Enumerable.Range(37, 7), replay.Checkpoints.Select(point => point.DeliveryTick));
        var samples = replay.Checkpoints.Select(point => point.Presentation!).ToArray();
        Assert.Equal(accepted ? new int?[] { -2900, -1800, -700, 400, 1500, 2600, null }
                             : new int?[] { -2900, -1800, null, null, null, null, null }, samples.Select(sample => sample.View?.Z));
        Assert.Equal(accepted ? samples.Select(sample => sample.View?.Z) : Enumerable.Repeat<int?>(null, 7),
            samples.Select(sample => sample.Authority?.Z));
        foreach (var (point, index) in replay.Checkpoints.Select((point, index) => (point, index)))
        {
            var sample = samples[index];
            var active = index < (accepted ? 6 : 2);
            var command = active ? PresentationCueCommandKind.EnsureActive.ToString() :
                index == (accepted ? 6 : 2) ? PresentationCueCommandKind.Stop.ToString() : PresentationCueCommandKind.None.ToString();
            Assert.Equal(command, sample.CueCommand);
            Assert.Equal(accepted && index == 2, sample.Promoted);
            Assert.Equal(index == 2, sample.DuplicateIgnored);
            Assert.Equal((!accepted && index > 2 ? PresentationCueReconciliationRejectReason.SupersededPrediction :
                PresentationCueReconciliationRejectReason.None).ToString(), sample.RejectReason);
            Assert.Equal(1, sample.Created);
            Assert.Equal(active ? 1 : 0, sample.Active);
            Assert.Equal(active ? 0 : 1, sample.Destroyed);
            Assert.Equal(active ? 1 : (int?)null, sample.Handle);
            output.WriteLine($"Tick {point.DeliveryTick} | authority Z {sample.Authority?.Z} | view Z {sample.View?.Z} | " +
                $"active {sample.Active} | created {sample.Created} | destroyed {sample.Destroyed} | " +
                $"command {sample.CueCommand} | promoted {sample.Promoted} | rejected {sample.RejectReason}");
        }
    }
}

using AbilityKit.Demo.Moba.BattleScenario.Runner;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
[Trait("Gate", "BattleScenario")]
public sealed class AnnieLocalSkillStateReplayTests
{
    [Fact]
    public void Confirmed_w_keeps_the_original_simulation_without_reissuing_damage()
    {
        var replay = AnnieSkillStateReplay.Record(accepted: true);
        var anchor = replay.Ticks.Single(tick => tick.Tick == replay.AnchorTick);
        Assert.Equal("confirmed", replay.Variant);
        Assert.Equal(1, replay.TerminalThroughSequence);
        Assert.Equal(new[] { 2L }, replay.PendingSequences);
        Assert.Equal(anchor.Authority, anchor.OldPrediction);
        Assert.True(anchor.Authority.ConeCooldownEndMs > 0);
        Assert.Empty(replay.ReplayedSequences);
        Assert.Empty(replay.ReplaySteps);
        Assert.Equal(new[] { "second", "target" }, replay.PublishedConeHits.Select(hit => hit.Target).OrderBy(target => target));
        Assert.Equal(replay.OldConeHits, replay.PublishedConeHits);
        Assert.Equal("skill.start.alreadyRunning", replay.OldShieldRejectCode);
        Assert.Equal(0, replay.PublishedShieldCastCount);
        Assert.All(replay.Ticks, tick => Assert.Equal(tick.OldPrediction, tick.Published));
    }

    [Fact]
    public void Rejected_w_rebuilds_a_clean_anchor_and_retries_only_e_before_publishing()
    {
        var replay = AnnieSkillStateReplay.Record(accepted: false);
        Assert.Equal("rejected", replay.Variant);
        var anchor = replay.Ticks.Single(tick => tick.Tick == replay.AnchorTick);
        var delivery = replay.Ticks.Single(tick => tick.Tick == replay.DeliveryTick);
        Assert.InRange(replay.PendingInputTick, replay.AnchorTick + 1, replay.DeliveryTick - 1);
        Assert.True(anchor.Authority.ManaRaw > anchor.OldPrediction.ManaRaw);
        Assert.True(anchor.Authority.TargetHealthRaw > anchor.OldPrediction.TargetHealthRaw);
        Assert.True(anchor.Authority.SecondTargetHealthRaw > anchor.OldPrediction.SecondTargetHealthRaw);
        Assert.Equal(anchor.Authority.OutsideTargetHealthRaw, anchor.OldPrediction.OutsideTargetHealthRaw);
        Assert.Equal(new[] { "second", "target" }, replay.OldConeHits.Select(hit => hit.Target).OrderBy(target => target));
        Assert.Equal(new[] { 2L }, replay.ReplayedSequences);
        Assert.Equal(Enumerable.Range(replay.AnchorTick + 1, replay.DeliveryTick - replay.AnchorTick),
            replay.ReplaySteps.Select(step => step.Tick));
        Assert.All(replay.Ticks.Where(tick => tick.Tick < replay.DeliveryTick),
            tick => Assert.Equal(tick.OldPrediction, tick.Published));
        Assert.Equal(replay.ReplaySteps[^1].State, delivery.Published);
        Assert.Equal(delivery.Authority.TargetHealthRaw, delivery.Published.TargetHealthRaw);
        Assert.Equal(delivery.Authority.SecondTargetHealthRaw, delivery.Published.SecondTargetHealthRaw);
        Assert.Equal(delivery.Authority.OutsideTargetHealthRaw, delivery.Published.OutsideTargetHealthRaw);
        Assert.Equal(0, delivery.Published.ConeCooldownEndMs);
        Assert.True(delivery.Published.ShieldCooldownEndMs > 0);
        Assert.Empty(replay.PublishedConeHits);
        Assert.Equal(1, replay.PublishedShieldCastCount);
        Assert.Equal(delivery.Published.ShieldCooldownEndMs, replay.Ticks[^1].Published.ShieldCooldownEndMs);
    }
}

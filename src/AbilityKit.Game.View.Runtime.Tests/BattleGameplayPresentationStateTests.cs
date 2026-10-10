using System;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class BattleGameplayPresentationStateTests
{
    [Fact]
    public void ModuleSelection_IsStableWithinBattleAndClearedForNextBattle()
    {
        var state = new BattleGameplayPresentationState();
        state.SelectModules(1, "hud-a", "projection-a", "settlement-a");
        state.SelectModules(1, "hud-a", "projection-a", "settlement-a");

        Assert.Equal("projection-a", state.ProjectionModuleId);
        Assert.Throws<InvalidOperationException>(() =>
            state.SelectModules(1, "hud-b", "projection-a", "settlement-a"));
        Assert.Throws<InvalidOperationException>(() =>
            state.SelectModules(2, "hud-a", "projection-a", "settlement-a"));

        state.Reset();
        state.SelectModules(2, "hud-b", "projection-b", "settlement-b");
        Assert.Equal("settlement-b", state.SettlementModuleId);
    }
}

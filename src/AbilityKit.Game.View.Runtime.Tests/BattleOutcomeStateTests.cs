using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class BattleOutcomeStateTests
{
    [Fact]
    public void AcceptsOnlyMatchingLatestAuthoritativeResult()
    {
        var state = new BattleOutcomeState();
        state.SetIdentity(42, "match-a");

        Assert.False(state.Apply(Match(42, "match-a", 10, GameplayClockPhase.Running)));
        Assert.False(state.Apply(Match(42, "match-b", 11, GameplayClockPhase.Ended)));
        Assert.False(state.Apply(Match(43, "match-a", 11, GameplayClockPhase.Ended)));
        Assert.False(state.Apply(Match(42, "match-a", 9, GameplayClockPhase.Ended)));
        Assert.True(state.Apply(Match(42, "match-a", 12, GameplayClockPhase.Ended)));
        Assert.True(state.HasAuthoritativeResult);
        Assert.Equal(12, state.Result.SnapshotFrame);

        state.Reset();
        Assert.False(state.HasAuthoritativeResult);
    }

    private static GameplayMatchData Match(int gameplayId, string matchId, int frame,
        GameplayClockPhase phase) => new(gameplayId, phase, 0, 0, 0, 0, null, matchId, frame);
}

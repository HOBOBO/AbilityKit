using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleHudSessionModelTests
{
    [Fact]
    public void IdentityAndRevisionDriveLoadoutBindingDecision()
    {
        var model = new BattleHudSessionModel();

        model.Synchronize("p1", localActorId: 101, loadoutRevision: 7);

        Assert.Equal("p1", model.LocalPlayerId);
        Assert.Equal(101, model.LocalActorId);
        Assert.Equal(7, model.LoadoutRevision);
        Assert.True(model.RequiresLoadoutBinding);

        model.MarkLoadoutBound();
        Assert.False(model.RequiresLoadoutBinding);

        model.Synchronize("P1", localActorId: 101, loadoutRevision: 8);
        Assert.True(model.RequiresLoadoutBinding);

        model.MarkLoadoutBound();
        model.Synchronize("p2", localActorId: 202, loadoutRevision: 8);
        Assert.True(model.RequiresLoadoutBinding);
    }

    [Fact]
    public void EnterGameSnapshotFillsMissingIdentityWithoutReplacingSelectedPlayer()
    {
        var model = new BattleHudSessionModel();
        model.Synchronize("p1", localActorId: 0, loadoutRevision: 0);

        model.ApplyEnterGameSnapshot("p2", localActorId: 22);

        Assert.Equal("p1", model.LocalPlayerId);
        Assert.Equal(22, model.LocalActorId);
        Assert.False(model.ShouldUseEnterGameLoadout("p2", hasExplicitLocalControl: true));
        Assert.True(model.ShouldUseEnterGameLoadout("p2", hasExplicitLocalControl: false));

        model.Reset();
        model.ApplyEnterGameSnapshot("p2", localActorId: 22);
        Assert.Equal("p2", model.LocalPlayerId);
        Assert.Equal(22, model.LocalActorId);
    }

    [Fact]
    public void MatchingLoadoutResolvesLocalActorFromMultiActorSnapshot()
    {
        var model = new BattleHudSessionModel();
        model.Synchronize("p1", localActorId: 0, loadoutRevision: 0);
        var entries = new[]
        {
            SkillState(actorId: 10, slot: 1, skillId: 100),
            SkillState(actorId: 20, slot: 1, skillId: 200),
        };

        var actorId = model.ResolveLocalActorId(entries, entry => entry.SkillId == 200);

        Assert.Equal(20, actorId);
        Assert.Equal(20, model.LocalActorId);
    }

    [Fact]
    public void AmbiguousMatchingActorsAreRejected()
    {
        var model = new BattleHudSessionModel();
        var entries = new[]
        {
            SkillState(actorId: 10, slot: 1, skillId: 100),
            SkillState(actorId: 20, slot: 2, skillId: 200),
        };

        var actorId = model.ResolveLocalActorId(entries, entry => entry.Slot > 0);

        Assert.Equal(0, actorId);
        Assert.Equal(0, model.LocalActorId);
    }

    [Fact]
    public void SingleSnapshotActorIsFallbackWhenNoEntryMatchesLoadout()
    {
        var model = new BattleHudSessionModel();
        var entries = new[]
        {
            SkillState(actorId: 0, slot: 1, skillId: 100),
            SkillState(actorId: 30, slot: 1, skillId: 300),
            SkillState(actorId: 30, slot: 2, skillId: 301),
        };

        var actorId = model.ResolveLocalActorId(entries, entry => entry.SkillId == 999);

        Assert.Equal(30, actorId);
        Assert.Equal(30, model.LocalActorId);
    }

    [Fact]
    public void KnownLocalActorWinsOverSnapshotCandidates()
    {
        var model = new BattleHudSessionModel();
        model.Synchronize("p1", localActorId: 40, loadoutRevision: 0);

        var actorId = model.ResolveLocalActorId(
            new[] { SkillState(actorId: 50, slot: 1, skillId: 500) },
            _ => true);

        Assert.Equal(40, actorId);
        Assert.Equal(40, model.LocalActorId);
    }

    private static MobaSkillStateSnapshotEntry SkillState(int actorId, int slot, int skillId)
    {
        return new MobaSkillStateSnapshotEntry
        {
            ActorId = actorId,
            Slot = slot,
            SkillId = skillId,
        };
    }
}

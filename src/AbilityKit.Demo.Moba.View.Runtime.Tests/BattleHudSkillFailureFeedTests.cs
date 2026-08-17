using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleHudSkillFailureFeedTests
{
    private static readonly BattleDiagnosticSessionScope Scope =
        new("session", "world", 1);

    [Fact]
    public void InitialBindingDoesNotReplayHistoricalFailure()
    {
        var store = CreateStore();
        AppendFailure(store, sequence: 1, actorId: 10, code: "cooldown");
        var feed = new BattleHudSkillFailureFeed();

        feed.Bind(store, 10);

        Assert.False(feed.TryReadLatest(out var message));
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void RevisionChangeReturnsNewestFailureAfterCursor()
    {
        var store = CreateStore();
        AppendFailure(store, sequence: 1, actorId: 10, code: "historical");
        var feed = new BattleHudSkillFailureFeed();
        feed.Bind(store, 10);

        AppendFailure(store, sequence: 2, actorId: 10, code: "cooldown");
        AppendFailure(store, sequence: 3, actorId: 10, code: "not_enough_mana");

        Assert.True(feed.TryReadLatest(out var message));
        Assert.Equal("蓝量不足", message);
        Assert.False(feed.TryReadLatest(out _));
    }

    [Fact]
    public void ActorChangeResetsCursorToSelectedActorsHistory()
    {
        var store = CreateStore();
        AppendFailure(store, sequence: 1, actorId: 10, code: "cooldown");
        AppendFailure(store, sequence: 2, actorId: 20, code: "outofrange");
        var feed = new BattleHudSkillFailureFeed();

        feed.Bind(store, 10);
        feed.Bind(store, 20);
        Assert.False(feed.TryReadLatest(out _));

        AppendFailure(store, sequence: 3, actorId: 10, code: "not_enough_mana");
        Assert.False(feed.TryReadLatest(out _));

        AppendFailure(store, sequence: 4, actorId: 20, code: "targetmissing");
        Assert.True(feed.TryReadLatest(out var message));
        Assert.Equal("没有有效目标", message);
    }

    [Fact]
    public void StoreChangeDoesNotReplayReplacementStoreHistory()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        AppendFailure(firstStore, sequence: 1, actorId: 10, code: "cooldown");
        AppendFailure(secondStore, sequence: 1, actorId: 10, code: "outofrange");
        var feed = new BattleHudSkillFailureFeed();

        feed.Bind(firstStore, 10);
        feed.Bind(secondStore, 10);

        Assert.False(feed.TryReadLatest(out _));
        AppendFailure(secondStore, sequence: 2, actorId: 10, code: "cooldown");
        Assert.True(feed.TryReadLatest(out var message));
        Assert.Equal("技能冷却中", message);
    }

    [Theory]
    [InlineData("not_enough_mana", "", "蓝量不足")]
    [InlineData("", "Skill is on cooldown.", "技能冷却中")]
    [InlineData("alreadyrunning", "", "技能正在释放")]
    [InlineData("outofrange", "", "超出施法范围")]
    [InlineData("targetmissing", "", "没有有效目标")]
    [InlineData("missingskill", "", "技能不可用")]
    [InlineData("not_enough_energy", "", "资源不足")]
    [InlineData("unknown", "unknown", "技能释放失败")]
    public void FailureTextProjectsStablePlayerFacingMessage(
        string code,
        string detail,
        string expected)
    {
        Assert.Equal(expected, BattleHudSkillFailureText.Format(code, detail));
    }

    private static BattleDiagnosticEventRingStore CreateStore()
    {
        return new BattleDiagnosticEventRingStore(Scope, capacity: 32);
    }

    private static void AppendFailure(
        BattleDiagnosticEventRingStore store,
        long sequence,
        int actorId,
        string code)
    {
        var failure = new BattleDiagnosticSkillFailurePayload(
            slot: 1,
            source: "cast",
            stage: "validation",
            code,
            message: string.Empty);
        var payload = BattleDiagnosticEventPayload.FromSkillFailure(in failure);
        var diagnosticEvent = new BattleDiagnosticEvent(
            Scope,
            frame: (int)sequence,
            sequence,
            monotonicTimestamp: sequence,
            BattleDiagnosticEventKind.SkillFailure,
            BattleDiagnosticEventChannel.Skill,
            BattleDiagnosticEventOutcome.Failed,
            sourceActorId: actorId,
            payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
            payload: payload);

        Assert.True(store.TryAppend(diagnosticEvent));
    }
}

using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class CompositeStateActionTests
{
    private const string Config = """
    {"schemaVersion":1,"states":[{"behaviorKey":"test.sequence","action":
      {"id":"root","type":"sequence","actions":[
        {"id":"start","type":"log","message":"begin"},
        {"id":"attack","type":"play","playState":"Attack","frameCount":12},
        {"id":"delay","type":"wait","seconds":0.5},
        {"id":"branches","type":"parallel","actions":[
          {"id":"end","type":"log","message":"end"},
          {"id":"later","type":"sequence","actions":[
            {"id":"more","type":"wait","seconds":0.2},
            {"id":"done","type":"log","message":"done"}]}]}]}}]}
    """;

    [Fact]
    public void SequenceParallelAndPlayRunOnLogicalFrames()
    {
        var catalog = CompositeActionCatalog.LoadJson(Config);
        var owner = new Sink();
        var runner = new CompositeStateAction<Sink>(catalog.Get("test.sequence"), 10);
        runner.OnEnter(owner, Tick(10));
        Assert.Equal("begin", Assert.Single(owner.Logs).Message);
        Assert.Equal(0, Assert.Single(owner.Plays).LocalFrame);
        for (var frame = 11; frame <= 17; frame++) runner.OnTick(owner, Tick(frame));
        Assert.Equal(new[] { "begin", "end", "done" }, owner.Logs.Select(i => i.Message));
        Assert.Equal(7, owner.Plays.Last().LocalFrame);
        Assert.Equal(7, runner.LocalFrame);
    }

    [Fact]
    public void SnapshotSeekNeverReplaysLogAndRejectsDifferentBindings()
    {
        var owner = new Sink();
        var source = new CompositeStateAction<Sink>(CompositeActionCatalog.LoadJson(Config).Get("test.sequence"), 10);
        source.OnEnter(owner, Tick(10));
        for (var frame = 11; frame <= 16; frame++) source.OnTick(owner, Tick(frame));
        var payload = source.CaptureSnapshot();

        var restored = new CompositeStateAction<Sink>(CompositeActionCatalog.LoadJson(Config).Get("test.sequence"), 10);
        restored.RestoreSnapshot(1, payload);
        var headless = new Sink();
        restored.Seek(headless, 16);
        Assert.Empty(headless.Logs);
        Assert.Equal(6, Assert.Single(headless.Plays).LocalFrame);
        restored.OnTick(headless, Tick(17));
        Assert.Equal("done", Assert.Single(headless.Logs).Message);

        var changed = Config.Replace("\"Attack\"", "\"HeavyAttack\"");
        var other = new CompositeStateAction<Sink>(CompositeActionCatalog.LoadJson(changed).Get("test.sequence"), 10);
        Assert.Throws<InvalidOperationException>(() => other.ValidateSnapshot(1, payload));
        var differentRate = new CompositeStateAction<Sink>(CompositeActionCatalog.LoadJson(Config).Get("test.sequence"), 20);
        Assert.Throws<InvalidOperationException>(() => differentRate.ValidateSnapshot(1, payload));
        var invalid = JObject.Parse(payload);
        invalid["Logged"] = new JArray("start", "unknown");
        Assert.Throws<InvalidOperationException>(() => restored.ValidateSnapshot(1, invalid.ToString()));
    }

    [Fact]
    public void DuplicateActionIdsAndMalformedBindingsFailBeforeExecution()
    {
        Assert.Throws<InvalidOperationException>(() => CompositeActionCatalog.LoadJson(
            Config.Replace("\"id\":\"done\"", "\"id\":\"end\"")));
        Assert.ThrowsAny<Exception>(() => CompositeActionCatalog.LoadJson(
            Config.Replace("\"seconds\":0.5", "\"seconds\":-1")));
    }

    private static TickContext Tick(int frame) =>
        new TickContext(frame, Fixed64.FromRatio(frame, 10), Fixed64.FromRatio(1, 10));

    private sealed class Sink : ICompositeActionSink
    {
        public readonly List<CompositeActionIntent> Logs = new();
        public readonly List<CompositeActionIntent> Plays = new();
        public void OnCompositeAction(in CompositeActionIntent intent)
        {
            if (intent.Type == "log") Logs.Add(intent);
            if (intent.Type == "play") Plays.Add(intent);
        }
    }
}

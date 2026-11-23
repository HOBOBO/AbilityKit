using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TimedExecutionRebindTests
{
    [Fact]
    public void Value_checkpoint_rebinds_to_fresh_hosts_and_replays_twice_without_the_original_entries()
    {
        var source = new ReplayHost();
        source.Schedule(2);
        source.Schedule(3);
        var cancelled = source.Schedule(999);
        source.Scheduler.Tick(4f);
        var checkpoint = source.Capture();

        var baseline = Replay(source, cancelled);
        Assert.Empty(baseline[0].Calls);
        Assert.Equal(new[] { 2, 3 }, baseline[1].Calls);
        Assert.Equal(15, baseline[2].Score);
        Assert.Equal(TriggerScheduledExecutionState.Interrupted, baseline[2].Progress.Entries[2].State);
        Assert.Equal("cancel input", baseline[2].Progress.Entries[2].FailureReason);
        Assert.Equal(new[] { 1, 1, 0 }, baseline[2].Progress.Entries.Select(entry => entry.ExecutionCount));
        source.Scheduler.Clear();
        Assert.Equal(0, source.Scheduler.DefinitionCount);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var target = new ReplayHost();
            target.Schedule(777);
            target.Restore(checkpoint);
            Assert.Empty(target.Calls);
            Assert.Equal(10, target.Score);
            Assert.Equal(checkpoint.Progress.Entries, target.Scheduler.GetExecutions());
            Assert.Equal(checkpoint.Progress.NextExecutionId, target.Scheduler.NextExecutionId);
            Assert.Equal(checkpoint.Definitions, target.Capture().Definitions);

            var actual = Replay(target, cancelled);
            for (var index = 0; index < baseline.Length; index++)
            {
                Assert.Equal(baseline[index].Score, actual[index].Score);
                Assert.Equal(baseline[index].Calls, actual[index].Calls);
                Assert.Equal(baseline[index].Progress.Entries, actual[index].Progress.Entries);
                Assert.Equal(baseline[index].Progress.NextExecutionId, actual[index].Progress.NextExecutionId);
            }
            Assert.Equal(checkpoint.Progress.NextExecutionId, target.Schedule(8).Id);
        }

        Assert.Equal(new[] { 2, 3 }, source.Calls);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("node")]
    [InlineData("handler")]
    [InlineData("progress")]
    public void Invalid_rebind_leaves_the_live_scheduler_and_host_unchanged(string missing)
    {
        var origin = new ReplayHost();
        origin.Schedule(2);
        origin.Schedule(3);
        origin.Scheduler.Tick(4f);
        var checkpoint = origin.Capture();
        var target = new ReplayHost();
        target.Schedule(9);
        target.Scheduler.Tick(5f);
        var before = target.Capture();
        var live = target.Scheduler;

        var last = checkpoint.Definitions[1];
        if (missing == "version")
            checkpoint.Definitions[1] = last with { Invocation = last.Invocation with { PlanVersion = 0 } };
        if (missing == "node")
            checkpoint.Definitions[1] = last with { Invocation = last.Invocation with { NodeKey = 0 } };
        if (missing == "progress")
            checkpoint.Progress.Entries[1] = new TriggerScheduledExecutionInfo(last.Id,
                TriggerScheduledExecutionState.Waiting, double.NaN, 0, 0, string.Empty);

        Assert.Throws<InvalidOperationException>(() => target.Restore(checkpoint, handlerAvailable: missing != "handler"));
        Assert.Same(live, target.Scheduler);
        var after = target.Capture();
        Assert.Equal(before.Definitions, after.Definitions);
        Assert.Equal(before.Progress.Entries, after.Progress.Entries);
        Assert.Equal(before.Progress.NextExecutionId, after.Progress.NextExecutionId);
        Assert.Equal(1, live.ActiveCount);
        Assert.Empty(target.Calls);
        Assert.Equal(10, target.Score);

        live.Tick(5f);
        Assert.Equal(new[] { 9 }, target.Calls);
        Assert.Equal(19, target.Score);
    }

    private static Frame[] Replay(ReplayHost host, TriggerScheduledExecutionHandle cancelled)
    {
        Assert.True(host.Scheduler.Interrupt(in cancelled, "cancel input"));
        return new[] { Step(5f), Step(1f), Step(10f) };

        Frame Step(float deltaMs)
        {
            host.Scheduler.Tick(deltaMs);
            return new Frame(host.Score, host.Calls.ToArray(), host.Scheduler.CaptureProgress());
        }
    }

    private readonly record struct Invocation(int PlanVersion, int NodeKey, int Amount);
    private readonly record struct Definition(long Id, Invocation Invocation, float IntervalMs, bool CanBeInterrupted);
    private sealed record Checkpoint(Definition[] Definitions, TriggerExecutionProgress Progress);
    private sealed record Frame(int Score, int[] Calls, TriggerExecutionProgress Progress);

    private sealed class ReplayHost : ITriggerExecutionScheduler
    {
        private const int PlanVersion = 1;
        private const int NodeKey = 7;
        private static readonly ActionId ApplyAmount = new(1);
        private readonly ActionRegistry _actions = new();
        private readonly EventBus _events = new();
        private readonly FunctionRegistry _functions = new();
        private readonly ScheduledTriggerPlanExecutable _node = TriggerPlanExecutableDsl.Timed(
            new ActionCallTriggerPlanExecutable(new ActionCallPlan(ApplyAmount)), 10f);
        private Dictionary<long, Definition> _definitions = new();

        public ReplayHost()
        {
            _actions.Register<NamedAction0<object, object, ReplayHost>>(ApplyAmount, static (args, _, context) =>
            {
                var amount = (int)args;
                context.Context.Score += amount;
                context.Context.Calls.Add(amount);
            }, isDeterministic: true);
        }

        public int Score { get; private set; } = 10;
        public List<int> Calls { get; } = new();
        public TriggerExecutionScheduler Scheduler { get; private set; } = new(retainFinishedEntries: true);

        public TriggerScheduledExecutionHandle Schedule(int amount)
        {
            var expectedId = Scheduler.NextExecutionId;
            var context = MakeContext();
            Assert.True(_node.Execute(amount, in context).IsSuccess);
            Assert.True(Scheduler.ContainsDefinition(expectedId));
            return new TriggerScheduledExecutionHandle(expectedId);
        }

        TriggerScheduledExecutionHandle ITriggerExecutionScheduler.Schedule<TCtx>(ITriggerPlanExecutable executable, object args,
            in ExecCtx<TCtx> context, EScheduleMode mode, float intervalMs, int maxExecutions, bool canBeInterrupted)
        {
            if (!ReferenceEquals(executable, _node.Child) || mode != EScheduleMode.Timed || maxExecutions != 1 || args is not int amount)
                throw new InvalidOperationException("The replay fixture accepts only its registered timed node and integer payload.");

            var handle = Scheduler.Schedule(executable, args, in context, mode, intervalMs, maxExecutions, canBeInterrupted);
            var invocation = new Invocation(PlanVersion, NodeKey, amount);
            _definitions.Add(handle.Id, new Definition(handle.Id, invocation, intervalMs, canBeInterrupted));
            return handle;
        }

        public Checkpoint Capture()
        {
            var progress = Scheduler.CaptureProgress();
            var definitions = new Definition[progress.Entries.Length];
            for (var index = 0; index < definitions.Length; index++)
                definitions[index] = _definitions[progress.Entries[index].Id];
            return new Checkpoint(definitions, progress);
        }

        public void Restore(Checkpoint checkpoint, bool handlerAvailable = true)
        {
            if (checkpoint.Definitions.Length != checkpoint.Progress.Entries.Length)
                throw new InvalidOperationException("Each progress row requires one timed definition in the same order.");

            var staged = new TriggerExecutionScheduler(retainFinishedEntries: true);
            var stagedDefinitions = new Dictionary<long, Definition>(checkpoint.Definitions.Length);
            for (var index = 0; index < checkpoint.Definitions.Length; index++)
            {
                var definition = checkpoint.Definitions[index];
                if (definition.Id < staged.NextExecutionId || definition.Id != checkpoint.Progress.Entries[index].Id)
                    throw new InvalidOperationException("Timed definitions must preserve their IDs and registration order.");
                var executable = Resolve(definition.Invocation, handlerAvailable);
                var context = MakeContext();
                staged.SetNextExecutionId(definition.Id);
                var handle = staged.Schedule(executable, definition.Invocation.Amount, in context,
                    EScheduleMode.Timed, definition.IntervalMs, 1, definition.CanBeInterrupted);
                stagedDefinitions.Add(handle.Id, definition);
            }

            staged.RestoreProgress(checkpoint.Progress);
            _definitions = stagedDefinitions;
            Scheduler = staged;
        }

        private ITriggerPlanExecutable Resolve(Invocation invocation, bool handlerAvailable)
        {
            if (invocation.PlanVersion != PlanVersion || invocation.NodeKey != NodeKey)
                throw new InvalidOperationException("The checkpoint references an unavailable plan version or node.");
            if (!handlerAvailable || !_actions.TryGet<NamedAction0<object, object, ReplayHost>>(ApplyAmount, out _, out var deterministic) || !deterministic)
                throw new InvalidOperationException("The timed node's deterministic handler is unavailable.");
            return _node.Child;
        }

        private ExecCtx<ReplayHost> MakeContext() => new(this, _events, _functions, _actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: new ExecPolicy(requireDeterministic: true), control: new ExecutionControl(), executionScheduler: this);
    }
}

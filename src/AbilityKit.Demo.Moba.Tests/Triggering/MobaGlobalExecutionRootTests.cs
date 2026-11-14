using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Testing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaGlobalExecutionRootTests
{
    [Theory]
    [InlineData(71001, 72001)]
    [InlineData(71002, 72002)]
    public void Global_event_runs_published_tree_with_original_typed_payload(int triggerId, int eventId)
    {
        using var harness = new TriggeringTestHarness<IWorldResolver>();
        var root = new RecordingRoot();
        var plan = new TriggerPlan<object>(phase: 0, priority: 65, triggerId: triggerId);
        var record = new TriggerPlanJsonDatabase.Record(triggerId, "test.global.tree", eventId,
            TriggerPlanScope.Global, in plan, root);
        var register = typeof(PlanTriggeringStage).GetMethod("RegisterGlobalExecutionRoot",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        register.Invoke(null, new object[] { harness.Runner, typeof(TestPayload), record });

        var payload = new TestPayload();
        harness.Publish(new EventKey<TestPayload>(eventId + 100), payload);
        Assert.Equal(0, root.Count);
        harness.Publish(new EventKey<TestPayload>(eventId), payload);
        Assert.Equal(1, root.Count);
        Assert.Same(payload, root.Payload);
    }

    private sealed class TestPayload { }

    private sealed class RecordingRoot : ITriggerPlanExecutable
    {
        public string Name => "test tree";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Sequence;
        public float Weight => 1;
        public int Count { get; private set; }
        public object? Payload { get; private set; }
        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> ctx) where TCtx : class
        {
            Count++;
            Payload = args;
            return TriggerPlanExecutionResult.Success();
        }
    }
}

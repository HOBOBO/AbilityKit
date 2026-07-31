using System;
using AbilityKit.Ability.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class BehaviorManagerLifecycleTests
{
    [Fact]
    public void Interrupt_disposes_disposable_decision_once_and_removes_behavior()
    {
        var manager = new BehaviorManager();
        var decision = new DisposableTestDecision();
        var behavior = CreateBehavior(manager, decision);

        manager.Interrupt(behavior.InstanceId, "test interrupt");
        manager.Interrupt(behavior.InstanceId, "duplicate interrupt");

        Assert.Equal(1, decision.DisposeCount);
        Assert.Null(manager.GetBehavior(behavior.InstanceId));
        Assert.Equal(0, manager.TotalCount);
    }

    [Fact]
    public void Complete_disposes_disposable_decision_once_and_removes_behavior()
    {
        var manager = new BehaviorManager();
        var decision = new DisposableTestDecision();
        var behavior = CreateBehavior(manager, decision);

        behavior.Complete();
        behavior.Complete();

        Assert.Equal(1, decision.DisposeCount);
        Assert.Null(manager.GetBehavior(behavior.InstanceId));
        Assert.Equal(0, manager.TotalCount);
    }

    private static BehaviorRuntime CreateBehavior(BehaviorManager manager, IBehaviorDecision decision)
    {
        return manager.CreateBehavior(new BehaviorCreateConfig
        {
            BehaviorKind = "test",
            OwnerId = new BehaviorEntityId(1),
            Decision = decision,
        });
    }

    private sealed class DisposableTestDecision : IBehaviorDecision, IDisposable
    {
        public int DisposeCount { get; private set; }

        public string DecisionType => "test";
        public string CurrentState => "running";

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            return DecisionResult.Continue(CurrentState);
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}

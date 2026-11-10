using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaEntityRollbackBrainLifecycleTests
{
    [Fact]
    public void Entity_rollback_releases_predicted_actor_brain_before_destroy()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actors = new MobaActorRegistry();
        var catalog = new TestBrainCatalog();
        var drivers = new MobaBrainDecisionDriverRegistry(new[] { new NoopDecisionDriver() });
        using var brains = new MobaBrainService(actors, catalog, null, drivers);
        var provider = new MobaEntitasEntityRollbackProvider(
            context,
            ids,
            actors,
            entities: null,
            summons: null,
            allowMissingActorsRestoredByCommands: false,
            brains: brains);

        var frame = new FrameIndex(7);
        var checkpoint = provider.Export(frame);
        var predicted = context.CreateEntity();
        predicted.AddActorId(ids.Next());
        actors.Register(predicted.actorId.Value, predicted);
        Assert.True(brains.ActivateBrain(predicted, 1, sourceKind: 4, sourceId: 9));
        var behaviorId = predicted.actorBrain.BehaviorInstanceId;
        Assert.True(brains.TryGetBehavior(behaviorId, out _));

        provider.Import(frame, checkpoint);

        Assert.False(predicted.isEnabled);
        Assert.False(brains.TryGetBehavior(behaviorId, out _));
    }

    private sealed class TestBrainCatalog : IMobaActorBrainCatalog
    {
        private static readonly MobaActorBrainDefinition Definition =
            new MobaActorBrainDefinition(1, MobaBrainDriverKeys.BehaviorTree, "noop");

        public IReadOnlyList<MobaActorBrainDefinition> Definitions => new[] { Definition };

        public bool TryGet(int brainId, out MobaActorBrainDefinition definition)
        {
            definition = Definition;
            return brainId == Definition.BrainId;
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoopDecisionDriver : IMobaBrainDecisionDriver
    {
        public string Kind => MobaBrainDriverKeys.BehaviorTree;

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = new NoopDecision();
            return true;
        }
    }

    private sealed class NoopDecision : IBehaviorDecision
    {
        public string DecisionType => "Noop";
        public string CurrentState => "Holding";

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world) =>
            DecisionResult.Continue(CurrentState);
    }
}

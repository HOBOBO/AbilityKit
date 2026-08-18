using AbilityKit.Ability.Behavior;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBrainDecisionDriverRegistryTests
{
    [Fact]
    public void Brain_catalog_loader_accepts_only_canonical_data_driven_definitions()
    {
        const string json = """
            [
              {
                "BrainId": 7,
                "DriverKind": "behaviorTree",
                "DecisionName": "combat"
              }
            ]
            """;
        var catalog = new MobaActorBrainCatalog();

        Assert.Equal(1, MobaActorBrainCatalogJsonLoader.LoadJson(json, catalog));
        Assert.True(catalog.TryGet(7, out var definition));
        Assert.Equal(MobaBrainDriverKind.BTree, definition.DriverKind);
        Assert.False(catalog.TryGet(999, out _));

        const string removedCodeDriverJson = """
            [
              {
                "BrainId": 8,
                "DriverKind": "code",
                "DecisionName": "chase"
              }
            ]
            """;
        Assert.Throws<InvalidOperationException>(() =>
            MobaActorBrainCatalogJsonLoader.LoadJson(removedCodeDriverJson, new MobaActorBrainCatalog()));
    }

    [Fact]
    public void Hfsm_driver_creates_registered_state_machine_decision()
    {
        var driver = new MobaHfsmBrainDecisionDriver();
        driver.Register("combat", static (in MobaBrainDecisionCreateContext context) =>
            new DelegateDecision("CombatHfsm", (behaviorContext, world) =>
                DecisionResult.Continue("Combat")));

        var definition = new MobaActorBrainDefinition(
            brainId: 8,
            MobaBrainDriverKind.Hfsm,
            decisionName: "combat");
        var context = new MobaBrainDecisionCreateContext(
            in definition,
            registry: null,
            config: null,
            ownerActorId: 202,
            sourceKind: 0,
            sourceId: 0);

        var created = driver.TryCreate(in context, out var decision);

        Assert.True(created);
        Assert.NotNull(decision);
        Assert.Equal("CombatHfsm", decision.DecisionType);
    }

    [Fact]
    public void Registry_allows_custom_driver_without_service_changes()
    {
        var registry = new MobaBrainDecisionDriverRegistry();
        registry.Register(new TestDriver());
        var definition = new MobaActorBrainDefinition(
            brainId: 9,
            MobaBrainDriverKind.Hfsm,
            decisionName: "test");
        var context = new MobaBrainDecisionCreateContext(
            in definition,
            registry: null,
            config: null,
            ownerActorId: 303,
            sourceKind: 0,
            sourceId: 0);

        var created = registry.TryCreate(in context, out var decision);

        Assert.True(created);
        Assert.NotNull(decision);
        Assert.Equal("TestDriver", decision.DecisionType);
    }

    [Fact]
    public void Registered_hfsm_driver_is_used_as_behavior_controller()
    {
        var catalog = new MobaActorBrainCatalog();
        catalog.Register(new MobaActorBrainDefinition(12, MobaBrainDriverKind.Hfsm, "test"));
        var registry = new MobaBrainDecisionDriverRegistry(new[] { new TestDriver() });
        var service = new MobaBrainService(new MobaActorRegistry(), catalog, null, registry);
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(1200);

        Assert.True(service.ActivateBrain(actor, 12, sourceKind: 1, sourceId: 2));
        Assert.True(actor.hasActorBrain);
        Assert.True(actor.actorBrain.BehaviorInstanceId > 0);
        Assert.True(service.TryGetBehavior(actor.actorBrain.BehaviorInstanceId, out var behavior));
        Assert.NotNull(behavior);
        Assert.False(actor.hasActorStateMachine);
        service.Dispose();
    }

    [Fact]
    public void Default_registry_does_not_register_a_generic_hfsm_driver()
    {
        var definition = new MobaActorBrainDefinition(
            brainId: 10,
            MobaBrainDriverKind.Hfsm,
            decisionName: "combat");
        var context = new MobaBrainDecisionCreateContext(
            in definition,
            registry: null,
            config: null,
            ownerActorId: 404,
            sourceKind: 0,
            sourceId: 0);

        Assert.False(MobaBrainDecisionDriverRegistry.CreateDefault().TryCreate(in context, out var decision));
        Assert.Null(decision);
    }

    private sealed class TestDriver : IMobaBrainDecisionDriver
    {
        public MobaBrainDriverKind Kind => MobaBrainDriverKind.Hfsm;

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = new DelegateDecision("TestDriver", (behaviorContext, world) =>
                DecisionResult.Continue("Test"));
            return true;
        }
    }
}

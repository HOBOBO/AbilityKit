using AbilityKit.Ability.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBrainDecisionDriverRegistryTests
{
    [Fact]
    public void Default_registry_creates_code_decision_from_catalog_definition()
    {
        var registry = MobaBrainDecisionDriverRegistry.CreateDefault();
        var definition = new MobaActorBrainDefinition(
            brainId: 1,
            MobaBrainDriverKind.Code,
            decisionName: "chase",
            param0: 1.5f);
        var context = new MobaBrainDecisionCreateContext(
            in definition,
            registry: null,
            config: null,
            ownerActorId: 101,
            sourceKind: 0,
            sourceId: 0);

        var created = registry.TryCreate(in context, out var decision);

        Assert.True(created);
        Assert.NotNull(decision);
        Assert.Equal("NearestEnemyChase", decision.DecisionType);
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

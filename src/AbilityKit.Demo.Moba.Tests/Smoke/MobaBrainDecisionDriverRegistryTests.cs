using AbilityKit.Ability.Behavior;
using AbilityKit.AI.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.AI;
using AbilityKit.Demo.Moba.Services.StateMachine;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBrainDecisionDriverRegistryTests
{
    [Fact]
    public void Brain_catalog_loader_preserves_builtin_and_plugin_driver_keys()
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
        Assert.Equal(MobaBrainDriverKeys.BehaviorTree, definition.DriverKind);
        Assert.False(catalog.TryGet(999, out _));

        const string pluginDriverJson = """
            [
              {
                "BrainId": 8,
                "DriverKind": "goap",
                "DecisionName": "combat-goal"
              }
            ]
            """;
        var pluginCatalog = new MobaActorBrainCatalog();
        MobaActorBrainCatalogJsonLoader.LoadJson(pluginDriverJson, pluginCatalog);
        Assert.True(pluginCatalog.TryGet(8, out var pluginDefinition));
        Assert.Equal("goap", pluginDefinition.DriverKind);
    }

    [Fact]
    public void Custom_goap_driver_loads_validates_and_creates_without_central_registration_changes()
    {
        const string json = """
            [
              {
                "BrainId": 17,
                "DriverKind": "goap",
                "DecisionName": "combat-goal"
              }
            ]
            """;
        var catalog = new MobaActorBrainCatalog();
        MobaActorBrainCatalogJsonLoader.LoadJson(json, catalog);
        var driver = new GoapTestDriver();
        var drivers = new MobaBrainDecisionDriverRegistry(new IMobaBrainDecisionDriver[] { driver });

        MobaBrainConfigurationValidator.Validate(
            catalog,
            new MobaActorStateMachineProfileCatalog(),
            new MobaActorStateMachineRuntimeRegistry(),
            drivers);

        Assert.Equal(1, driver.ValidationCount);
        Assert.True(catalog.TryGet(17, out var definition));
        var context = new MobaBrainDecisionCreateContext(
            in definition,
            registry: null,
            config: null,
            ownerActorId: 1700,
            sourceKind: 0,
            sourceId: 0);
        Assert.True(drivers.TryCreate(in context, out var decision));
        Assert.Equal("GoapTest", decision.DecisionType);
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
            MobaBrainDriverKeys.Hfsm,
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
            MobaBrainDriverKeys.Hfsm,
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
        catalog.Register(new MobaActorBrainDefinition(12, MobaBrainDriverKeys.Hfsm, "test"));
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
            MobaBrainDriverKeys.Hfsm,
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

    [Fact]
    public void Machine_learning_driver_runs_registered_policy_and_restores_policy_state()
    {
        const int actorId = 1500;
        StatefulTestPolicy policy = null;
        var mlDriver = new MobaMlBrainDecisionDriver();
        mlDriver.Register("combat-model", (in MobaBrainDecisionCreateContext _) =>
            policy = new StatefulTestPolicy());
        var drivers = new MobaBrainDecisionDriverRegistry(new IMobaBrainDecisionDriver[] { mlDriver });
        var catalog = new MobaActorBrainCatalog();
        catalog.Register(new MobaActorBrainDefinition(
            15,
            MobaBrainDriverKeys.MachineLearning,
            "combat-model"));
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(actorId);
        actors.Register(actorId, actor);
        using var service = new MobaBrainService(actors, catalog, null, drivers);

        Assert.True(service.ActivateBrain(actor, 15, sourceKind: 1, sourceId: 2));
        service.Tick(1f / 30f, frame: 1);
        Assert.True(service.TryGetBehavior(actor.actorBrain.BehaviorInstanceId, out var behavior));

        var intent = MobaBrainIntentReader.Read(behavior);
        Assert.Equal(0.75f, intent.MoveX);
        Assert.Equal(-0.25f, intent.MoveZ);
        Assert.True(intent.HasCast);
        Assert.Equal(2, intent.SkillSlot);
        Assert.Equal(1, policy.DecisionCount);

        var snapshots = Assert.IsAssignableFrom<IBehaviorRuntimeSnapshot>(behavior.Decision);
        var payload = snapshots.CaptureSnapshot();
        service.Tick(1f / 30f, frame: 2);
        Assert.Equal(2, policy.DecisionCount);
        snapshots.RestoreSnapshot(payload);
        Assert.Equal(1, policy.DecisionCount);
    }

    [Fact]
    public void Brain_catalog_loader_accepts_machine_learning_driver()
    {
        const string json = """
            [
              {
                "BrainId": 16,
                "DriverKind": "machineLearning",
                "DecisionName": "combat-model"
              }
            ]
            """;
        var catalog = new MobaActorBrainCatalog();

        MobaActorBrainCatalogJsonLoader.LoadJson(json, catalog);

        Assert.True(catalog.TryGet(16, out var definition));
        Assert.Equal(MobaBrainDriverKeys.MachineLearning, definition.DriverKind);
    }

    private sealed class TestDriver : IMobaBrainDecisionDriver
    {
        public string Kind => MobaBrainDriverKeys.Hfsm;

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = new DelegateDecision("TestDriver", (behaviorContext, world) =>
                DecisionResult.Continue("Test"));
            return true;
        }
    }

    private sealed class StatefulTestPolicy : IAiPolicy, IAiPolicyRuntimeSnapshot
    {
        public AiActionSpec ActionSpec => MobaBrainActionCodec.ActionSpec;
        public string SnapshotType => "test.policy.v1";
        public int DecisionCount { get; private set; }

        public void Decide(in AiObservationBuffer observation, AiActionBuffer action)
        {
            DecisionCount++;
            action.Continuous[0] = 0.75f;
            action.Continuous[1] = -0.25f;
            action.Discrete[0] = 2;
        }

        public byte[] CaptureSnapshot() => BitConverter.GetBytes(DecisionCount);

        public void RestoreSnapshot(byte[] payload)
        {
            DecisionCount = BitConverter.ToInt32(payload, 0);
        }
    }

    private sealed class GoapTestDriver :
        IMobaBrainDecisionDriver,
        IMobaBrainDecisionDriverValidator
    {
        public string Kind => "goap";
        public int ValidationCount { get; private set; }

        public void ValidateDefinition(
            in MobaActorBrainDefinition definition,
            ICollection<string> errors)
        {
            ValidationCount++;
            if (!string.Equals(definition.DecisionName, "combat-goal", StringComparison.Ordinal))
                errors.Add($"Unknown GOAP goal set '{definition.DecisionName}'.");
        }

        public bool TryCreate(
            in MobaBrainDecisionCreateContext context,
            out IBehaviorDecision decision)
        {
            decision = new DelegateDecision(
                "GoapTest",
                static (behaviorContext, world) => DecisionResult.Continue("Planning"));
            return true;
        }
    }
}

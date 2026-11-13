using System.Runtime.CompilerServices;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle.Component;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Inspection;
using AbilityKit.Modifiers;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

[CollectionDefinition("Moba runtime inspection", DisableParallelization = true)]
public sealed class MobaRuntimeInspectionCollection { }

// Installing the inspection backend changes process-wide state. Other HFSM tests
// must not register into this test's recorder while its assertions are running.
[Collection("Moba runtime inspection")]
public sealed class MobaCharacterHfsmTests
{
    [Theory]
    [InlineData("Active", 7001L)]
    [InlineData("BasicAttack", 0L)]
    [InlineData("LoadoutSlot", 0L)]
    public void ExplicitCastCategoryDisambiguatesSkillsSharingTheBasicAttackSlot(string kind, long expectedCast)
    {
        var definition = MobaCharacterHfsmProfile.CreateGenericDefinition();
        MobaCharacterBindings.Attach(definition, new MobaCharacterBindings
        {
            DefinitionId = definition.DefinitionId,
            States = new() { new() { Path = "life/alive/action/casting", CastContext = "Current", CastKind = "Active" } },
            Casts = new() { new() { SkillId = 7654321, CastKind = kind } }
        });
        using var runtime = new MobaCharacterHfsmRuntime(91, definition, 0, Fixed64.Zero);
        runtime.Tick(1, Time(1), true, false, false, 7001, 7654321, 9, 9);
        Assert.Equal("life/alive/action/casting", runtime.Action.Path);
        Assert.Equal(expectedCast, runtime.Action.CastInstanceId);
        var snapshot = runtime.CaptureSnapshot();
        runtime.Tick(2, Time(2), true, false, false, 0, 0);
        runtime.RestoreSnapshot(snapshot);
        runtime.ReevaluateCurrentFrame();
        Assert.Equal(expectedCast, runtime.Action.CastInstanceId);
    }

    private static void SetCastParameter(MobaCharacterHfsmParameterService parameters, int actor, long owner, float value)
    {
        var modifier = ModifierData.Override(ModifierKey.FromPacked(12), value);
        parameters.Set(actor, owner, 12, in modifier);
    }

    [Fact]
    public void ParameterContributionsComposeAndClearBySource()
    {
        var parameters = new MobaCharacterHfsmParameterService();
        var definition = MobaCharacterHfsmProfile.CreateGenericDefinition();
        definition.Parameters.Add(new StateMachineParameterDefinition
        {
            Id = 99,
            Name = "AdditiveCounter",
            SupportedOperations = new List<int> { (int)ModifierOp.Add }
        });
        parameters.RegisterActorProfile(17, definition);
        var first = ModifierData.Add(ModifierKey.FromPacked(99), 2f, sourceId: 11);
        var second = ModifierData.Add(ModifierKey.FromPacked(99), 3f, sourceId: 12);
        parameters.Set(17, 11, 99, in first);
        parameters.Set(17, 12, 99, in second);

        Assert.Equal(5f, parameters.Resolve(17, 99));
        parameters.ClearSource(17, 11);
        Assert.Equal(3f, parameters.Resolve(17, 99));
    }

    [Fact]
    public void ProfileLocalParameterToggleUsesStableOwnerAndIsRollbackSnapshotSafe()
    {
        var parameters = new MobaCharacterHfsmParameterService();
        var jinx = MobaCharacterHfsmProfile.CreateGenericDefinition();
        jinx.DefinitionId = MobaCharacterHfsmProfile.HeroDefinitionId(3002);
        jinx.Parameters.Add(new StateMachineParameterDefinition
        {
            Id = 20,
            Name = "WeaponMode",
            SupportedOperations = new List<int> { (int)ModifierOp.Override }
        });
        parameters.RegisterActorProfile(17, jinx);
        parameters.RegisterActorProfile(18, MobaCharacterHfsmProfile.CreateGenericDefinition());

        const long qSkillOwnerId = 30020101;
        Assert.True(parameters.TryToggleOverride(17, qSkillOwnerId, 20, 0f, 1f, (int)qSkillOwnerId));
        Assert.Equal(1f, parameters.Resolve(17, 20));
        var snapshot = parameters.CaptureSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(qSkillOwnerId, snapshot[0].OwnerId);

        Assert.True(parameters.TryToggleOverride(17, qSkillOwnerId, 20, 0f, 1f, (int)qSkillOwnerId));
        Assert.Equal(0f, parameters.Resolve(17, 20));
        Assert.Single(parameters.CaptureSnapshot());
        Assert.Equal(0f, parameters.Resolve(18, 20));

        parameters.RestoreSnapshot(snapshot);
        Assert.Equal(1f, parameters.Resolve(17, 20));
        Assert.Equal(0f, parameters.Resolve(18, 20));
        Assert.False(parameters.TryToggleOverride(18, qSkillOwnerId, 20, 0f, 1f, (int)qSkillOwnerId));
    }

    [Fact]
    public void RuntimeOwnedParameterContributionsDoNotAliasWhenLegacySourceIdsMatch()
    {
        var parameters = CreateJhinParameterService();
        var key = ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase);
        var first = ModifierData.Override(key, MobaCharacterHfsmTestFixture.RPhaseAiming, sourceId: 30010401);
        var second = ModifierData.Override(key, MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId: 30010401);
        parameters.Set(17, 4_294_967_297L, MobaCharacterHfsmTestFixture.RPhase, in first);
        parameters.Set(17, 8_589_934_594L, MobaCharacterHfsmTestFixture.RPhase, in second);

        parameters.ClearSource(17, 4_294_967_297L);

        Assert.Equal(MobaCharacterHfsmTestFixture.RPhaseShooting,
            parameters.Resolve(17, MobaCharacterHfsmTestFixture.RPhase));
    }

    [Fact]
    public void CommittedAttackSlotKeepsCurrentCastGroupUntilCastEnds()
    {
        var parameters = CreateJhinParameterService();
        var nextSlot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 11);
        parameters.Set(17, 11, MobaCharacterHfsmTestFixture.AttackSlot, in nextSlot);

        SetCastParameter(parameters, 17, 501, 1);
        using var runtime = new MobaCharacterHfsmRuntime(17,
            MobaCharacterHfsmTestFixture.CreateDefinition(), 0, Fixed64.Zero, parameters: parameters);
        runtime.Tick(1, Time(1), true, false, false, 501, 30010001);
        Assert.Equal("life/alive/action/casting", runtime.Action.Path);
        runtime.Tick(2, Time(2), true, false, false, 501, 30010001);

        Assert.Equal(1f, parameters.Resolve(17, MobaCharacterHfsmTestFixture.CommittedAttackSlot));
        Assert.Equal("life/alive/action/attack.slot.1", runtime.Action.Path);

        parameters.ClearSource(17, 501);
        runtime.Tick(3, Time(3), true, false, false, 0, 0);

        Assert.Equal(0f, parameters.Resolve(17, MobaCharacterHfsmTestFixture.CommittedAttackSlot));
        Assert.Equal("life/alive/action/attack.slot.2", runtime.Action.Path);
    }

    [Fact]
    public void CommittedAttackSlotReplacementAndClearAreOwnedByCastInstance()
    {
        var parameters = CreateJhinParameterService();
        SetCastParameter(parameters, 17, 501, 1);
        SetCastParameter(parameters, 17, 502, 4);

        Assert.Equal(1f, parameters.Resolve(17, MobaCharacterHfsmTestFixture.CommittedAttackSlot));
        parameters.ClearSource(17, 501);
        Assert.Equal(4f, parameters.Resolve(17, MobaCharacterHfsmTestFixture.CommittedAttackSlot));
        parameters.ClearSource(17, 502);
        Assert.Equal(0f, parameters.Resolve(17, MobaCharacterHfsmTestFixture.CommittedAttackSlot));
    }

    [Fact]
    public void ParameterIdsAreProfileLocalAndModifierOperationsAreValidatedSeparately()
    {
        Assert.True(MobaCharacterHfsmParameterService.IsValidParameterId(999));
        Assert.False(MobaCharacterHfsmParameterService.IsValidParameterId(0));
        Assert.True(MobaCharacterHfsmParameterService.IsValidOperation(999, (int)ModifierOp.Override));
        Assert.False(MobaCharacterHfsmParameterService.IsValidOperation(999, (int)ModifierOp.Custom));

        var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(17, MobaCharacterHfsmProfile.CreateGenericDefinition());
        Assert.False(parameters.IsDeclared(17, 999));
        Assert.False(parameters.SupportsOperation(17, 999, (int)ModifierOp.Override));
    }

    [Fact]
    public void ParameterWritesAreScopedToTheActorsProfileAndDeclaredOperations()
    {
        var parameters = new MobaCharacterHfsmParameterService();
        var jhin = MobaCharacterHfsmTestFixture.CreateDefinition();
        var otherHero = MobaCharacterHfsmProfile.CreateGenericDefinition();
        otherHero.DefinitionId = MobaCharacterHfsmProfile.HeroDefinitionId(3002);
        otherHero.Parameters.Add(new StateMachineParameterDefinition
        {
            Id = 20,
            Name = "WeaponMode",
            SupportedOperations = new List<int> { (int)ModifierOp.Override }
        });
        parameters.RegisterActorProfile(17, jhin);
        parameters.RegisterActorProfile(18, otherHero);
        Assert.True(parameters.UsesProfile(17, MobaCharacterHfsmProfile.HeroDefinitionId(3001)));
        Assert.False(parameters.UsesProfile(18, MobaCharacterHfsmProfile.HeroDefinitionId(3001)));

        var attackSlot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 11);
        parameters.Set(17, 11, MobaCharacterHfsmTestFixture.AttackSlot, in attackSlot);
        Assert.Throws<InvalidOperationException>(() => parameters.Set(
            18, 11, MobaCharacterHfsmTestFixture.AttackSlot, in attackSlot));

        var weaponMode = ModifierData.Override(ModifierKey.FromPacked(20), 1f, sourceId: 12);
        parameters.Set(18, 12, 20, in weaponMode);
        Assert.Equal(1f, parameters.Resolve(18, 20));
        Assert.Throws<InvalidOperationException>(() => parameters.Set(17, 12, 20, in weaponMode));

        var additiveAttackSlot = ModifierData.Add(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 1f, sourceId: 13);
        Assert.Throws<InvalidOperationException>(() => parameters.Set(
            17, 13, MobaCharacterHfsmTestFixture.AttackSlot, in additiveAttackSlot));
    }

    [Fact]
    public void GenericProfileDoesNotAcceptJhinSpecificConditions()
    {
        var generic = MobaCharacterHfsmProfile.CreateGenericDefinition();

        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(generic, "attack.cast.slot.1"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(generic, "attack.slot.1.ready"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(generic,
            "parameter." + MobaCharacterHfsmTestFixture.RPhase + ".eq.1"));
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey(generic, "controlled"));
    }

    [Fact]
    public void NonJhinProfileCannotOptIntoJhinConditionsByReusingParameterIds()
    {
        var otherHero = MobaCharacterHfsmProfile.CreateGenericDefinition();
        otherHero.DefinitionId = MobaCharacterHfsmProfile.HeroDefinitionId(3002);
        otherHero.Parameters.AddRange(new[]
        {
            new StateMachineParameterDefinition
            {
                Id = MobaCharacterHfsmTestFixture.AttackSlot,
                Name = "WeaponMode",
                SupportedOperations = new List<int> { (int)ModifierOp.Override }
            },
            new StateMachineParameterDefinition
            {
                Id = MobaCharacterHfsmTestFixture.RPhase,
                Name = "WeaponPhase",
                SupportedOperations = new List<int> { (int)ModifierOp.Override }
            },
            new StateMachineParameterDefinition
            {
                Id = MobaCharacterHfsmTestFixture.CommittedAttackSlot,
                Name = "CommittedMode",
                SupportedOperations = new List<int> { (int)ModifierOp.Override }
            }
        });

        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(otherHero, "attack.cast.slot.1"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(otherHero, "attack.slot.1.ready"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(otherHero,
            "attack.slot.5.reload.complete"));
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey(otherHero, "parameter.10.eq.2"));

        otherHero.Machines.Single(machine => machine.Id == "action").Transitions.Add(new TransitionDefinition
        {
            Id = "invalid.jhin.condition",
            FromStateId = "idle",
            ToStateId = "casting",
            ConditionKey = "attack.cast.slot.1",
            Priority = 100
        });
        Assert.Throws<InvalidOperationException>(() => MobaCharacterHfsmProfile.CreateBindings(otherHero));
    }

    [Fact]
    public void ParameterConditionSyntaxAcceptsPositiveProfileLocalIds()
    {
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.10.eq.2"));
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.11.ge.1"));
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.999.eq.1"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey("attack.cast.slot.5"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.0.eq.1"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.10.contains.2"));
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey("parameter.10.eq.NaN"));
    }

    [Fact]
    public void ParameterConditionDrivesAuthoredHfsmTransition()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var action = definition.Machines.Single(machine => machine.Id == "action");
        action.Transitions.Add(new TransitionDefinition
        {
            Id = "parameter.attack-slot",
            FromStateId = "idle",
            ToStateId = "controlled",
            ConditionKey = "parameter.10.eq.2",
            Priority = 99
        });
        var parameters = CreateJhinParameterService();
        var modifier = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 21);
        parameters.Set(17, 21, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);

        Assert.Equal("life/alive/action/controlled", runtime.Action.Path);
    }

    [Fact]
    public void AttackSlotParameterSelectsNextAttackStateAndDoesNotOverrideControl()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var parameters = CreateJhinParameterService();
        var modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 1f, sourceId: 30010001);
        parameters.Set(17, 30010001, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/attack.slot.1", runtime.Action.Path);

        modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 4f, sourceId: 30010001);
        parameters.Set(17, 30010001, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);
        runtime.Tick(2, Time(2), true, true, false, 0, 0);
        Assert.Equal("life/alive/action/controlled", runtime.Action.Path);

        runtime.Tick(3, Time(3), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/idle", runtime.Action.Path);
        runtime.Tick(4, Time(4), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/attack.slot.4", runtime.Action.Path);

        modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 0f, sourceId: 30010001);
        parameters.Set(17, 30010001, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);
        runtime.Tick(5, Time(5), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/idle", runtime.Action.Path);
    }

    [Fact]
    public void RPhaseParameterSelectsAimRegularAndFinalShotStates()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var parameters = CreateJhinParameterService();
        var modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseAiming, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in modifier);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.aiming", runtime.Action.Path);

        modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in modifier);
        runtime.Tick(2, Time(2), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.shooting", runtime.Action.Path);

        modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseFinalShot, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in modifier);
        runtime.Tick(3, Time(3), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.final_shot", runtime.Action.Path);

        parameters.ClearSource(17, 30010401);
        runtime.Tick(4, Time(4), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/idle", runtime.Action.Path);
    }

    [Fact]
    public void RShotActionKeepsCastContextUntilTheSameShotStateEnds()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var parameters = CreateJhinParameterService();
        var phase = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in phase);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        // Enter the persistent aiming state first, as the authored graph
        // intentionally reaches shooting through the R mode.
        phase = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseAiming, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in phase);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.aiming", runtime.Action.Path);

        phase = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in phase);
        runtime.Tick(2, Time(2), true, false, false, 101, 30010501);
        Assert.Equal("life/alive/action/r.shooting", runtime.Action.Path);
        Assert.Equal(101, runtime.Action.CastInstanceId);
        Assert.Equal(30010501, runtime.Action.SkillId);

        // The independent single-shot pipeline has finished, but its HFSM
        // phase is still active until the gameplay modifier advances it.
        runtime.Tick(3, Time(3), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.shooting", runtime.Action.Path);
        Assert.Equal(101, runtime.Action.CastInstanceId);
        Assert.Equal(30010501, runtime.Action.SkillId);

        phase = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseFinalShot, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in phase);
        runtime.Tick(4, Time(4), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.final_shot", runtime.Action.Path);
        Assert.Equal(0, runtime.Action.CastInstanceId);
        Assert.Equal(0, runtime.Action.SkillId);

        runtime.Tick(5, Time(5), true, false, false, 202, 30010502);
        Assert.Equal("life/alive/action/r.final_shot", runtime.Action.Path);
        Assert.Equal(202, runtime.Action.CastInstanceId);
        Assert.Equal(30010502, runtime.Action.SkillId);
    }

    [Fact]
    public void BasicAttackSlotKeepsCastContextUntilTheSlotChanges()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var parameters = CreateJhinParameterService();
        var slot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 1f, sourceId: 30010004);
        parameters.Set(17, 30010004, MobaCharacterHfsmTestFixture.AttackSlot, in slot);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        runtime.Tick(1, Time(1), true, false, false, 101, 30010601, 5, 5);
        // The generic casting state is the commit boundary for every short
        // attack flow.  The parameter-driven slot transition is evaluated on
        // the following HFSM tick, after that boundary has been entered. The
        // launch trigger records the committed slot before it advances the
        // magazine parameter to the next attack.
        Assert.Equal("life/alive/action/casting", runtime.Action.Path);
        var committedSlot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.CommittedAttackSlot), 1f, sourceId: 30010601);
        parameters.Set(17, 30010601, MobaCharacterHfsmTestFixture.CommittedAttackSlot, in committedSlot);
        slot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 30010004);
        parameters.Set(17, 30010004, MobaCharacterHfsmTestFixture.AttackSlot, in slot);
        runtime.Tick(2, Time(2), true, false, false, 101, 30010601, 5, 5);
        Assert.Equal("life/alive/action/attack.slot.1", runtime.Action.Path);
        Assert.Equal(101, runtime.Action.CastInstanceId);
        Assert.Equal(30010601, runtime.Action.SkillId);
        parameters.ClearSource(17, 30010601);

        // Once the short flow completes, the already advanced magazine
        // parameter selects the next state and clears the completed cast cue.
        runtime.Tick(3, Time(3), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/attack.slot.2", runtime.Action.Path);
        Assert.Equal(0, runtime.Action.CastInstanceId);
        Assert.Equal(0, runtime.Action.SkillId);

        runtime.Tick(4, Time(4), true, false, false, 202, 30010602, 5, 5);
        Assert.Equal(202, runtime.Action.CastInstanceId);
        Assert.Equal(30010602, runtime.Action.SkillId);
    }

    [Fact]
    public void CompletedCastUsesReadyAttackSlotTransitionWithoutAnIdleFrame()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var parameters = CreateJhinParameterService();
        var slot = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 30010004);
        parameters.Set(17, 30010004, MobaCharacterHfsmTestFixture.AttackSlot, in slot);

        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters);
        runtime.SyncCastContext(101, 30010601);
        runtime.ReevaluateCurrentFrame();
        Assert.Equal("life/alive/action/casting", runtime.Action.Path);

        runtime.SyncCastContext(0, 0);
        runtime.ReevaluateCurrentFrame();
        Assert.Equal("life/alive/action/attack.slot.2", runtime.Action.Path);
    }


    [Fact]
    public void StateEntryTriggerChangesHfsmParameterAndRestoredSnapshotDoesNotReplayIt()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var aiming = definition.Machines.Single(machine => machine.Id == "action")
            .States.Single(state => state.Id == "r.aiming");
        aiming.LogicEntryActionKeys.Add("trigger.enter.30010490");

        var parameters = CreateJhinParameterService();
        var phase = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
            MobaCharacterHfsmTestFixture.RPhaseAiming, sourceId: 30010401);
        parameters.Set(17, 30010401, MobaCharacterHfsmTestFixture.RPhase, in phase);

        var triggerCalls = 0;
        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters,
            executeLogicTrigger: (actorId, triggerId, stateId, frame) =>
            {
                Assert.Equal(17, actorId);
                Assert.Equal(30010490, triggerId);
                Assert.Equal("r.aiming", stateId);
                Assert.Equal(1, frame);
                triggerCalls++;

                var shooting = ModifierData.Override(
                    ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.RPhase),
                    MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId: 30010401);
                parameters.Set(actorId, 30010401, MobaCharacterHfsmTestFixture.RPhase, in shooting);
            });

        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.aiming", runtime.Action.Path);
        Assert.Equal(1, triggerCalls);

        var snapshot = runtime.CaptureSnapshot();
        using var restored = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero,
            parameters: parameters,
            executeLogicTrigger: (_, triggerId, _, _) =>
            {
                if (triggerId == 30010490) triggerCalls++;
            });
        restored.RestoreSnapshot(snapshot);
        Assert.Equal(1, triggerCalls);

        restored.Tick(2, Time(2), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/r.shooting", restored.Action.Path);
        Assert.Equal(1, triggerCalls);
    }

    [Fact]
    public void RuntimeRegistersNamedMachineAndUnregistersItOnDispose()
    {
        var backend = new RecordingInspectionBackend();
        using (RuntimeInspectionHub.InstallBackend(backend))
        {
            var runtime = new MobaCharacterHfsmRuntime(17,
                MobaCharacterHfsmTestFixture.CreateDefinition(), 0, Fixed64.Zero);
            Assert.Equal("MobaHero/Actor_17", backend.RegisteredName);
            Assert.NotNull(backend.RegisteredRuntime);
            runtime.Dispose();
            Assert.Same(backend.RegisteredRuntime, backend.UnregisteredRuntime);
        }
    }

    [Fact]
    public void RecoveryValidationRuntimeDoesNotRegisterWithInspectionHub()
    {
        var backend = new RecordingInspectionBackend();
        using (RuntimeInspectionHub.InstallBackend(backend))
        using (var runtime = new MobaCharacterHfsmRuntime(17,
                   MobaCharacterHfsmTestFixture.CreateDefinition(), 0, Fixed64.Zero,
                   registerForInspection: false))
        {
            Assert.Null(backend.RegisteredRuntime);
            Assert.Null(backend.UnregisteredRuntime);
        }
    }

    [Fact]
    public void AuthoredDefinitionMatchesDefaultRuntimeGraph()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/character_hfsm_3001.json"));
        var definition = DefinitionJson.Load(File.ReadAllText(path));
        Assert.Equal(MobaCharacterHfsmProfile.HeroDefinitionId(3001), definition.DefinitionId);
        Assert.Equal(MobaCharacterHfsmTestFixture.CreateDefinition().ComputeDefinitionHash(),
            definition.ComputeDefinitionHash());
    }

    [Fact]
    public void ActionInstanceAndLocalFrameSurviveSnapshotRestore()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        using var source = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        source.Tick(1, Time(1), true, false, true, 0, 0);
        source.Tick(2, Time(2), true, false, true, 0, 0);
        Assert.Equal("life/alive/action/moving", source.Action.Path);
        Assert.Equal(1, source.Action.LocalFrame);

        source.Tick(3, Time(3), true, false, true, 321, 10001);
        source.Tick(4, Time(4), true, false, false, 321, 10001);
        var cast = source.CaptureSnapshot();
        Assert.Equal("life/alive/action/casting", cast.Action.Path);
        Assert.Equal(1, cast.Action.LocalFrame);
        Assert.Equal(321, cast.Action.CastInstanceId);
        Assert.Equal(10001, cast.Action.SkillId);

        using var restored = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        restored.RestoreSnapshot(cast);
        Assert.Equal(cast.Action.InstanceId, restored.Action.InstanceId);
        Assert.Equal(cast.Action.LocalFrame, restored.Action.LocalFrame);
        restored.Tick(5, Time(5), true, true, false, 321, 10001);
        Assert.Equal("life/alive/action/controlled", restored.Action.Path);
        restored.Tick(6, Time(6), false, true, false, 321, 10001);
        Assert.Equal("life/dead", restored.Action.Path);
        restored.Tick(7, Time(7), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/idle", restored.Action.Path);
    }

    [Fact]
    public void InvalidActionDoesNotPartiallyRestoreRuntime()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        var original = runtime.Action;
        var snapshot = runtime.CaptureSnapshot();
        snapshot.Action = new MobaCharacterActionState(original.Path, original.InstanceId,
            original.StartFrame, original.LocalFrame + 1, 0, 0);
        Assert.Throws<InvalidOperationException>(() => runtime.RestoreSnapshot(snapshot));
        Assert.Equal(1, runtime.Frame);
        Assert.Equal(original.LocalFrame, runtime.Action.LocalFrame);
    }

    [Fact]
    public void ViewRestoresLocalFrameWithoutTransitionReplay()
    {
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        logic.Tick(1, Time(1), true, false, false, 321, 10001);
        logic.Tick(2, Time(2), true, false, false, 321, 10001);
        var view = new BattleCharacterHfsmComponent(definition);
        view.ApplySnapshot(logic.CaptureSnapshot());
        Assert.Equal(1, view.Action.LocalFrame);
        Assert.Equal(logic.DefinitionHash, view.DefinitionHash);

        logic.Tick(3, Time(3), true, false, false, 321, 10001);
        var invalid = logic.CaptureSnapshot();
        invalid.Action = new MobaCharacterActionState(invalid.Action.Path,
            invalid.Action.InstanceId, invalid.Action.StartFrame, 99, 321, 10001);
        Assert.Throws<InvalidOperationException>(() => view.ApplySnapshot(invalid));
        Assert.Equal(2, view.SnapshotFrame);
        Assert.Equal(1, view.Action.LocalFrame);
    }

    [Fact]
    public void EntitasRollbackProviderRestoresActionFrame()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var registry = new MobaActorRegistry();
        try
        {
            registry.Register(17, actor);
            actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            actor.characterHfsm.Runtime.Tick(1, Time(1), true, false, false, 321, 10001);
            actor.characterHfsm.Runtime.Tick(2, Time(2), true, false, false, 321, 10001);
            var provider = new MobaCharacterHfsmRollbackProvider(registry, definition);
            var bytes = provider.Export(new FrameIndex(2));
            var expected = actor.characterHfsm.Runtime.Action;
            actor.characterHfsm.Runtime.Tick(3, Time(3), true, false, true, 0, 0);

            provider.Import(new FrameIndex(2), bytes);
            Assert.Equal(2, actor.characterHfsm.Runtime.Frame);
            Assert.Equal(expected.Path, actor.characterHfsm.Runtime.Action.Path);
            Assert.Equal(expected.InstanceId, actor.characterHfsm.Runtime.Action.InstanceId);
            Assert.Equal(1, actor.characterHfsm.Runtime.Action.LocalFrame);
        }
        finally
        {
            if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
            actor.Destroy();
        }
    }

    [Fact]
    public void RollbackCreatedRuntimeRetainsHfsmParameterService()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        definition.Machines.Single(machine => machine.Id == "action").Transitions.Add(
            new TransitionDefinition
            {
                Id = "parameter.attack-slot",
                FromStateId = "idle",
                ToStateId = "attack-slot-2",
                ConditionKey = "parameter.10.eq.2",
                Priority = 99
            });
        definition.Machines.Single(machine => machine.Id == "action").States.Add(
            new StateDefinition { Id = "attack-slot-2" });
        var registry = new MobaActorRegistry();
        var parameters = CreateJhinParameterService();
        var modifier = ModifierData.Override(
            ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 1f, sourceId: 21);
        parameters.Set(17, 21, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);
        try
        {
            registry.Register(17, actor);
            actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0,
                Fixed64.Zero, parameters: parameters));
            actor.characterHfsm.Runtime.Tick(1, Time(1), true, false, false, 0, 0);
            var provider = new MobaCharacterHfsmRollbackProvider(registry, definition, parameters);
            var snapshot = provider.Export(new FrameIndex(1));

            actor.RemoveAndDisposeCharacterHfsm();
            modifier = ModifierData.Override(
                ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot), 2f, sourceId: 21);
            parameters.Set(17, 21, MobaCharacterHfsmTestFixture.AttackSlot, in modifier);
            provider.Import(new FrameIndex(1), snapshot);
            actor.characterHfsm.Runtime.Tick(2, Time(2), true, false, false, 0, 0);

            Assert.Equal("life/alive/action/attack.slot.2", actor.characterHfsm.Runtime.Action.Path);
        }
        finally
        {
            if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
            actor.Destroy();
            parameters.Dispose();
        }
    }

    [Fact]
    public void InvalidSecondActorDoesNotPartiallyImportFirstActor()
    {
        var context = new ActorContext();
        var first = context.CreateEntity();
        var second = context.CreateEntity();
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        var registry = new MobaActorRegistry();
        try
        {
            registry.Register(17, first);
            registry.Register(18, second);
            first.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            second.AddCharacterHfsm(new MobaCharacterHfsmRuntime(18, definition, 0, Fixed64.Zero));
            first.characterHfsm.Runtime.Tick(1, Time(1), true, false, true, 0, 0);
            second.characterHfsm.Runtime.Tick(1, Time(1), true, false, false, 0, 0);
            var provider = new MobaCharacterHfsmRollbackProvider(registry, definition);
            var entries = JArray.Parse(Encoding.UTF8.GetString(provider.Export(new FrameIndex(1))));
            entries[1]!["LocalFrame"] = 999;
            first.characterHfsm.Runtime.Tick(2, Time(2), true, false, false, 0, 0);
            var original = first.characterHfsm.Runtime.Action;

            Assert.Throws<InvalidOperationException>(() => provider.Import(new FrameIndex(1),
                Encoding.UTF8.GetBytes(entries.ToString())));
            Assert.Equal(2, first.characterHfsm.Runtime.Frame);
            Assert.Equal(original.Path, first.characterHfsm.Runtime.Action.Path);
            Assert.Equal(original.InstanceId, first.characterHfsm.Runtime.Action.InstanceId);
        }
        finally
        {
            if (first.hasCharacterHfsm) first.RemoveCharacterHfsm();
            if (second.hasCharacterHfsm) second.RemoveCharacterHfsm();
            first.Destroy();
            second.Destroy();
        }
    }

    [Fact]
    public void HeadlessSequenceWaitAndLiveReplacementSeekFromLocalFrame()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/character_view_actions.json"));
        var catalog = CharacterPresentationActionCatalog.LoadJson(File.ReadAllText(path),
            ModelAnimationCatalog.LoadJson(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "model_animations.json"))));
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        var view = new BattleCharacterHfsmComponent(definition, catalog);
        var sink = new HeadlessSink();

        logic.Tick(1, Time(1), true, false, false, 321, 10001);
        view.ApplySnapshot(logic.CaptureSnapshot());
        var firstPlayback = view.Evaluate(1, 30, sink);
        Assert.Null(firstPlayback);
        Assert.Single(sink.Logs);
        view.Evaluate(1, 30, sink);
        Assert.Single(sink.Logs);

        var replacement = new CharacterPresentationAction
        {
            Id = "ignored", Type = "sequence", Actions = new List<CharacterPresentationAction>
            {
                new() { Id = "attack.play", Type = "play", AnimatorState = "Attack", FrameCount = 12 },
                new() { Id = "attack.wait", Type = "wait", Seconds = 0.5m }
            }
        };
        catalog.ReplaceAction("life/alive/action/casting", "attack.phase", replacement);
        var start = view.Evaluate(1, 30, sink);
        Assert.Equal("Attack", start!.Value.AnimatorState);
        Assert.Equal(0, start.Value.LocalFrame);
        Assert.Equal(2, start.Value.BindingVersion);
        Assert.Single(sink.Plays);
        Assert.Single(sink.Logs);

        logic.Tick(2, Time(2), true, false, false, 321, 10001);
        view.ApplySnapshot(logic.CaptureSnapshot());
        var middle = view.Evaluate(2, 30, sink);
        Assert.Equal(1, middle!.Value.LocalFrame);
        Assert.Equal(2, sink.Plays.Count);

        var restored = new BattleCharacterHfsmComponent(definition, catalog);
        restored.ApplySnapshot(logic.CaptureSnapshot());
        var seek = restored.Evaluate(2, 30, sink);
        Assert.Equal(1, seek!.Value.LocalFrame);
        Assert.Equal(3, sink.Plays.Count);
        Assert.Single(sink.Logs);
    }

    [Fact]
    public void SequenceOffsetsAreDeterministicAndInvalidReplacementIsAtomic()
    {
        var catalog = CharacterPresentationActionCatalog.LoadJson("""
        {"schemaVersion":1,"states":[{"path":"life/alive/action/casting","action":
          {"id":"root","type":"sequence","actions":[
            {"id":"wait","type":"wait","seconds":0.5},
            {"id":"play","type":"play","animatorState":"Attack"},
            {"id":"log","type":"log","message":"done"}]}}]}
        """);
        var runner = new CharacterPresentationActionRunner(catalog);
        var sink = new HeadlessSink();
        Assert.Null(runner.Evaluate("life/alive/action/casting", 123, 14, 14, 30, sink));
        Assert.Empty(sink.Logs);
        Assert.Equal("Attack", runner.Evaluate("life/alive/action/casting", 123, 15, 15, 30, sink)!.Value.AnimatorState);
        Assert.Single(sink.Logs);

        Assert.Throws<InvalidOperationException>(() => catalog.ReplaceAction(
            "life/alive/action/casting", "wait",
            new CharacterPresentationAction { Type = "wait", Seconds = -1 }));
        Assert.Equal(1, catalog.Version);
        Assert.Equal("Attack", runner.Evaluate("life/alive/action/casting", 123, 16, 16, 30, sink)!.Value.AnimatorState);
    }

    [Fact]
    public void AuthoredHeroOverrideAndLiveActorReplacementAreIsolated()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/character_view_actions.json"));
        var catalog = CharacterPresentationActionCatalog.LoadJson(File.ReadAllText(path),
            ModelAnimationCatalog.LoadJson(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "model_animations.json"))));
        var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        logic.Tick(1, Time(1), true, false, false, 321, 10001);

        var hero = new BattleCharacterHfsmComponent(definition, catalog, 1001);
        var other = new BattleCharacterHfsmComponent(definition, catalog, 1002);
        hero.ApplySnapshot(logic.CaptureSnapshot());
        other.ApplySnapshot(logic.CaptureSnapshot());
        Assert.Equal("Attack", hero.Evaluate(1, 30)!.Value.AnimatorState);
        Assert.Null(other.Evaluate(1, 30));

        hero.ReplaceAction("life/alive/action/casting", "attack.phase",
            new CharacterPresentationAction { Type = "play", AnimatorState = "HeavyAttack" });
        Assert.Equal("HeavyAttack", hero.Evaluate(1, 30)!.Value.AnimatorState);
        Assert.Null(other.Evaluate(1, 30));
        Assert.Equal(logic.DefinitionHash, hero.DefinitionHash);
    }

    private sealed class HeadlessSink : ICharacterPresentationSink
    {
        public readonly List<CharacterPlaybackIntent> Plays = new();
        public readonly List<CharacterLogIntent> Logs = new();
        public void OnPlay(in CharacterPlaybackIntent intent) => Plays.Add(intent);
        public void OnLog(in CharacterLogIntent intent) => Logs.Add(intent);
    }

    private sealed class RecordingInspectionBackend : IRuntimeInspectionRegistryBackend,
        IRuntimeInspectionNamedRegistryBackend
    {
        public string? RegisteredName { get; private set; }
        public object? RegisteredRuntime { get; private set; }
        public object? UnregisteredRuntime { get; private set; }

        public void Register(string name, object runtime)
        { RegisteredName = name; RegisteredRuntime = runtime; }
        public void AutoRegister(object runtime) => RegisteredRuntime = runtime;
        public void Unregister(object runtime) => UnregisteredRuntime = runtime;
    }

    private static Fixed64 Time(int frame) => Fixed64.FromRaw(frame * 1000L);

    private static MobaCharacterHfsmParameterService CreateJhinParameterService()
    {
        var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(17, MobaCharacterHfsmTestFixture.CreateDefinition());
        return parameters;
    }

    private static string SourcePath([CallerFilePath] string sourceFile = "") => sourceFile;
}

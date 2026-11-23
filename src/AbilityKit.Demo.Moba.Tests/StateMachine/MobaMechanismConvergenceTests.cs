using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class MobaMechanismConvergenceTests
{
    [Theory]
    [InlineData(0f, 1.25f)]
    [InlineData(6f, 1f)]
    [InlineData(12f, .75f)]
    [InlineData(30f, .75f)]
    public void Distance_scaling_is_explicit_and_independent_of_direction(float distance, float expected)
    {
        foreach (var direction in new[] {0, 1, 2, 3})
        {
            Assert.Equal(1f, new PullArgs(10f, 200f, direction).ResolveDistanceScale(distance));
            Assert.Equal(expected, new PullArgs(10f, 200f, direction,
                distanceScaleReference: 12, distanceScaleNear: 1.25f, distanceScaleFar: .75f).ResolveDistanceScale(distance));
        }
    }

    [Fact]
    public void Invalid_distance_scaling_fails_before_effect_submission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PullArgs(10, 200, distanceScaleReference: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PullArgs(10, 200, distanceScaleNear: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PullArgs(10, 200, distanceScaleReference: 3, distanceScaleFar: -1));
    }

    [Fact]
    public void Prepared_parameter_is_cast_owned_idempotent_and_restores_before_commit()
    {
        var definition = Graph();
        using var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(27, definition);
        var services = new Services(parameters);
        var request = new SkillCastRequest(98123, 2, 27, 0, Vec3.Zero, Vec3.Forward, services, null, null, null,
            new[] {new MobaPreparedParameterSnapshot(82, 7)});
        var original = new SkillPipelineContext();
        original.Initialize(new object(), request);
        original.SourceContextId = 500;
        Assert.Equal(0, parameters.Resolve(27, 82));
        var checkpoint = original.CaptureRollbackState();
        var restored = new SkillPipelineContext();
        restored.Initialize(new object(), request);
        restored.RestoreRollbackState(checkpoint);
        restored.SourceContextId = 500;
        Assert.True(restored.CommitPreparedParameters());
        Assert.True(restored.CommitPreparedParameters());
        Assert.Single(parameters.CaptureSnapshot());
        Assert.Equal(7, parameters.Resolve(27, 82));
        var next = ModifierData.Override(ModifierKey.FromPacked(81), 99);
        parameters.Set(27, 123, 81, in next);
        Assert.Equal(7, parameters.Resolve(27, 82));
        // Another cast can contribute to the same target; old cleanup removes only its own source.
        var newer = ModifierData.Override(ModifierKey.FromPacked(82), 9); newer.Priority = 1;
        parameters.Set(27, 501, 82, in newer);
        restored.RunAndClearCleanups();
        Assert.Equal(9, parameters.Resolve(27, 82));
        parameters.ClearSource(27, 501);
        Assert.Equal(0, parameters.Resolve(27, 82));
    }

    [Fact]
    public void Prepared_parameters_roundtrip_through_v4_after_commit_without_reapplying()
    {
        using var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(27, Graph());
        var request = new SkillCastRequest(98123, 2, 27, 0, Vec3.Zero, Vec3.Forward, new Services(parameters),
            null, null, null, new[] { new MobaPreparedParameterSnapshot(82, 7), new MobaPreparedParameterSnapshot(81, 3) });
        var context = new SkillPipelineContext();
        context.Initialize(new object(), request);
        context.SourceContextId = 500;
        request.ParameterSnapshots[0] = new MobaPreparedParameterSnapshot(82, 99);
        var copy = context.GetPreparedParameterSnapshots();
        Assert.Equal(new[] { 81, 82 }, Array.ConvertAll(copy, sample => sample.TargetParameterId));
        copy[1] = new MobaPreparedParameterSnapshot(82, 101);
        Assert.True(context.CommitPreparedParameters());
        Assert.Equal(3, parameters.Resolve(27, 81));
        Assert.Equal(7, parameters.Resolve(27, 82));

        var state = context.CaptureRollbackState();
        Assert.DoesNotContain(context.SharedData.Keys, key => key.StartsWith("cast.prepared.", StringComparison.Ordinal));
        Assert.Collection(Array.FindAll(state.SharedData, entry => entry.Key.StartsWith("cast.prepared.", StringComparison.Ordinal)),
            entry => { Assert.Equal("cast.prepared.committed", entry.Key); Assert.Equal(true, entry.ToValue()); },
            entry => { Assert.Equal("cast.prepared.parameter.81", entry.Key); Assert.Equal(3f, entry.ToValue()); },
            entry => { Assert.Equal("cast.prepared.parameter.82", entry.Key); Assert.Equal(7f, entry.ToValue()); });
        var payload = SerializeRunnerContext(state);
        context.ClearData();
        Assert.Empty(context.GetPreparedParameterSnapshots());

        var restored = new SkillPipelineContext();
        restored.Initialize(new object(), request);
        var decoded = SkillRunnerRollbackCodec.Deserialize(payload).Entries[0].Context;
        restored.RestoreRollbackState(decoded);
        Assert.Equal(payload, SerializeRunnerContext(restored.CaptureRollbackState()));

        // A later write by the same source must survive the restored commit path.
        var later = ModifierData.Override(ModifierKey.FromPacked(82), 19);
        parameters.Set(27, 500L, 82, in later);
        for (var frame = 1; frame <= 3; frame++)
        {
            restored.SetFrame(frame);
            Assert.True(restored.CommitPreparedParameters());
            Assert.Equal(19, parameters.Resolve(27, 82));
        }
        restored.RunAndClearCleanups();
        Assert.Equal(0, parameters.Resolve(27, 82));
    }

    [Theory]
    [InlineData("cast.prepared.parameter.82", "wrong type")]
    [InlineData("cast.prepared.parameter.082", "noncanonical key")]
    [InlineData("cast.prepared.committed", "wrong type")]
    public void Invalid_prepared_state_rejects_before_changing_the_target(string key, string problem)
    {
        var context = new SkillPipelineContext();
        var request = new SkillCastRequest(98123, 2, 27, 0, Vec3.Zero, Vec3.Forward, null,
            null, null, null, new[] { new MobaPreparedParameterSnapshot(82, 7) });
        context.Initialize(new object(), request);
        context.SetFrame(4);
        Assert.True(context.SharedData.TryAdd("unrelated", 20));
        var state = context.CaptureRollbackState();
        var index = Array.FindIndex(state.SharedData, entry => entry.Key == "cast.prepared.parameter.82");
        state.SharedData[index] = SkillSharedDataRollbackEntry.Capture(key, problem == "noncanonical key" ? (object)7f : problem);
        var payload = SerializeRunnerContext(state);
        Assert.Contains(key, Assert.Throws<System.IO.InvalidDataException>(() => SkillRunnerRollbackCodec.Deserialize(payload)).Message);

        context.SetFrame(9);
        Assert.Contains(key, Assert.Throws<System.IO.InvalidDataException>(() => context.RestoreRollbackState(state)).Message);
        Assert.Equal(9, context.Frame);
        Assert.Equal(20, context.GetData<int>("unrelated"));
        Assert.Equal(7f, Assert.Single(context.GetPreparedParameterSnapshots()).Value);
    }

    [Fact]
    public void Reserved_key_written_directly_to_shared_data_cannot_override_the_prepared_state()
    {
        using var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(27, Graph());
        var request = new SkillCastRequest(98123, 2, 27, 0, Vec3.Zero, Vec3.Forward, new Services(parameters),
            null, null, null, new[] { new MobaPreparedParameterSnapshot(82, 7) });
        var context = new SkillPipelineContext();
        context.Initialize(new object(), request);
        context.SourceContextId = 500;
        const string key = "cast.prepared.parameter.82";
        context.SharedData[key] = (Action)(() => throw new InvalidOperationException());
        context.SharedData["cast.prepared.committed"] = true;
        Assert.Equal(7f, Assert.Single(context.GetPreparedParameterSnapshots()).Value);
        Assert.True(context.CommitPreparedParameters());
        Assert.Equal(7, parameters.Resolve(27, 82));
        Assert.Contains(key, Assert.Throws<InvalidOperationException>(() => context.CaptureRollbackState()).Message);
    }

    private static byte[] SerializeRunnerContext(in SkillPipelineContextRollbackState state) =>
        SkillRunnerRollbackCodec.Serialize(new SkillRunnerRollbackSnapshot
        {
            Entries = new[]
            {
                new SkillRunnerRollbackEntry
                {
                    ActorId = 27, SkillId = 98123, SkillSlot = 2, SkillLevel = 1, Sequence = 1,
                    RuntimeId = 1, RuntimeGeneration = 1, RootContextId = 500, SourceContextId = 500,
                    Policy = SkillCastPolicy.Default, Context = state,
                    DefinitionRevision = new string('A', 64),
                    Run = PipelineRollbackStateCodec.Serialize(new PipelineRunRollbackState())
                }
            }
        });

    [Fact]
    public void Aim_scope_survives_substate_input_changes_rollback_and_same_frame_reentry()
    {
        var definition = Graph();
        using var parameters = new MobaCharacterHfsmParameterService();
        parameters.RegisterActorProfile(27, definition);
        using var runtime = new MobaCharacterHfsmRuntime(27, definition, 0, Fixed64.Zero, parameters: parameters);
        Set(parameters, 81, 1);
        runtime.Tick(1, Fixed64.One, true, false, false, 0, 0, new Vec3(10,0,0), new Vec3(1,0,0));
        Assert.True(runtime.TryGetAimSnapshot("aim-cycle", out var pos, out var dir));
        var firstIdentity = runtime.CaptureSnapshot().AimScopeInstanceId;
        Set(parameters, 81, 2);
        runtime.Tick(2, Fixed64.FromInt32(2), true, false, false, 0, 0, new Vec3(-10,0,0), new Vec3(-1,0,0));
        Assert.True(runtime.TryGetAimSnapshot("aim-cycle", out var retained, out var direction));
        Assert.Equal(pos, retained); Assert.Equal(dir, direction);
        var checkpoint = runtime.CaptureSnapshot();
        using var restored = new MobaCharacterHfsmRuntime(27, definition, 0, Fixed64.Zero, parameters: parameters);
        restored.RestoreSnapshot(checkpoint);
        Assert.True(restored.TryGetAimSnapshot("aim-cycle", out retained, out direction));
        Assert.Equal(pos, retained); Assert.Equal(dir, direction);
        Set(parameters, 81, 0); restored.ReevaluateCurrentFrame();
        Assert.False(restored.TryGetAimSnapshot("aim-cycle", out _, out _));
        Set(parameters, 81, 1); restored.SyncAim(new Vec3(-8,0,0), new Vec3(-1,0,0)); restored.ReevaluateCurrentFrame();
        Assert.True(restored.TryGetAimSnapshot("aim-cycle", out retained, out direction));
        Assert.Equal(new Vec3(-1,0,0), direction);
        Assert.NotEqual(firstIdentity, restored.CaptureSnapshot().AimScopeInstanceId);
        restored.Tick(3, Fixed64.FromInt32(3), true, true, false, 0, 0);
        Assert.False(restored.TryGetAimSnapshot("aim-cycle", out _, out _));
    }

    [Fact]
    public void Declaration_not_identity_enables_conditions_and_invalid_binding_is_rejected()
    {
        var definition = Graph();
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey(definition, "mode.enabled"));
        definition.DefinitionId = "entirely.different.identity";
        var binding = MobaCharacterBindings.For(definition); binding.DefinitionId = definition.DefinitionId;
        MobaCharacterBindings.Attach(definition, binding);
        Assert.True(MobaCharacterHfsmProfile.IsSupportedConditionKey(definition, "mode.enabled"));
        var plain = MobaCharacterHfsmProfile.CreateGenericDefinition();
        Assert.False(MobaCharacterHfsmProfile.IsSupportedConditionKey(plain, "mode.enabled"));
        binding.Casts.Add(new MobaCastBinding {SkillId = 1, AimSource = "StateAimSnapshot", AimScope = "missing"});
        Assert.Throws<InvalidOperationException>(() => binding.Validate(definition));
    }

    [Theory]
    [InlineData(-30f, true)]
    [InlineData(30f, true)]
    [InlineData(0f, true)]
    [InlineData(-30.01f, false)]
    [InlineData(30.01f, false)]
    [InlineData(180f, false)]
    public void State_cone_uses_current_horizontal_input_including_boundary_and_keeps_the_axis(float degrees, bool accepted)
    {
        var graph = Graph();
        var binding = MobaCharacterBindings.For(graph);
        binding.Casts[0].AimSource = "InputWithinStateCone";
        binding.Casts[0].AimConeHalfAngleDegrees = 30;
        binding.Validate(graph);
        var cast = binding.Casts[0];
        var radians = degrees * MathF.PI / 180;
        var input = new Vec3(MathF.Sin(radians), 7, MathF.Cos(radians));
        var axis = Vec3.Forward;
        Assert.Equal(accepted, cast.TryConstrainInputAim(input, axis, out var actual));
        Assert.Equal(Vec3.Forward, axis);
        if (accepted)
        {
            Assert.Equal(0, actual.Y);
            Assert.Equal(input.X, actual.X, .00001f);
            Assert.Equal(input.Z, actual.Z, .00001f);
        }
        Assert.False(cast.TryConstrainInputAim(Vec3.Zero, axis, out _));
        Assert.False(cast.TryConstrainInputAim(Vec3.Up, axis, out _));
        Assert.False(cast.TryConstrainInputAim(new Vec3(float.NaN, 0, 1), axis, out _));
        Assert.False(cast.TryConstrainInputAim(input, Vec3.Zero, out _));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(90.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void State_cone_rejects_invalid_angle_or_missing_scope_before_cast(float angle)
    {
        var graph = Graph();
        var binding = MobaCharacterBindings.For(graph);
        var cast = binding.Casts[0];
        cast.AimSource = "InputWithinStateCone";
        cast.AimConeHalfAngleDegrees = angle;
        Assert.Throws<InvalidOperationException>(() => binding.Validate(graph));
        cast.AimConeHalfAngleDegrees = 30;
        cast.AimScope = "not-active";
        Assert.Throws<InvalidOperationException>(() => binding.Validate(graph));
        cast.AimScope = "aim-cycle";
        binding.Validate(graph);
        cast.AimSource = "Input";
        Assert.Throws<InvalidOperationException>(() => binding.Validate(graph));
    }

    private static StateMachineDefinition Graph()
    {
        var definition = new StateMachineDefinition {DefinitionId = "synthetic.character", RootMachineId = "mode"};
        foreach (var id in new[] {81,82}) definition.Parameters.Add(new StateMachineParameterDefinition {
            Id=id, Name="Parameter"+id, SupportedOperations = new List<int>{(int)ModifierOp.Override}});
        var machine = new MachineDefinition {Id="mode", InitialStateId="rest"};
        foreach(var id in new[]{"rest","prepare","fire"}) machine.States.Add(new StateDefinition {Id=id});
        machine.Transitions.Add(new TransitionDefinition {Id="enter", FromStateId="rest", ToStateId="prepare", ConditionKey="mode.enabled"});
        machine.Transitions.Add(new TransitionDefinition {Id="fire", FromStateId="prepare", ToStateId="fire", ConditionKey="parameter.81.eq.2"});
        foreach(var state in new[]{"prepare","fire"}) machine.Transitions.Add(new TransitionDefinition {
            Id="leave-"+state, FromStateId=state, ToStateId="rest", ConditionKey="parameter.81.eq.0"});
        definition.Machines.Add(machine);
        MobaCharacterBindings.Attach(definition, new MobaCharacterBindings {
            DefinitionId=definition.DefinitionId,
            Conditions=new List<MobaConditionBinding>{new() {Key="mode.enabled", Expression=new MobaConditionExpression {
                All=new List<MobaConditionExpression>{new(){Fact="alive"},new(){ParameterId=81,Value=1}}}}},
            States=new List<MobaStateBinding>{
                new(){Path="mode/prepare", AimScope="aim-cycle", CaptureAim=true},
                new(){Path="mode/fire", AimScope="aim-cycle"}},
            Casts=new List<MobaCastBinding>{new(){SkillId=98123, AimSource="StateAimSnapshot", AimScope="aim-cycle"}}
        });
        return definition;
    }
    private static void Set(MobaCharacterHfsmParameterService parameters,int id,float value)
    { var modifier=ModifierData.Override(ModifierKey.FromPacked((uint)id),value); parameters.Set(27, 100, id, in modifier); }
    private sealed class Services : IWorldResolver
    {
        private readonly MobaCharacterHfsmParameterService _parameters;
        public Services(MobaCharacterHfsmParameterService parameters) => _parameters=parameters;
        public object Resolve(Type type) => type==typeof(MobaCharacterHfsmParameterService)?_parameters:throw new InvalidOperationException();
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object value) {value=type==typeof(MobaCharacterHfsmParameterService)?_parameters:null;return value!=null;}
        public bool TryResolve<T>(out T value) {var found=TryResolve(typeof(T),out var service);value=found?(T)service:default;return found;}
    }
}

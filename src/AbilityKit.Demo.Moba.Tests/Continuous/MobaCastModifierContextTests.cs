using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Continuous;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Continuous;

public sealed class MobaCastModifierContextTests
{
    [Theory]
    [InlineData(0d, 160f, 70f)]
    [InlineData(.5d, 200f, 55f)]
    [InlineData(1d, 240f, 40f)]
    public void One_active_sample_drives_ranked_damage_with_AP_and_actual_movement_slow(
        double sample, float expectedDamage, float expectedSpeed)
    {
        using var world = new World();
        var cast = world.CreateCast();
        world.Write(cast, "cast.sample", sample);
        var domains = new NumericVarDomainRegistry();
        domains.Register(new MobaSkillRuntimeNumericVarDomain());
        var boards = new MobaSkillRuntimeBlackboardResolver(cast.Blackboard, 0, 10, 101);
        var exec = new ExecCtx<IWorldResolver>(world, null, null, null, boards, null, null, domains, null, default, default);
        var args = new Dictionary<string, ActionArgValue>
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.SkillLevelParameter, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("damage", "magnitude_parameter"),
            ["magnitude_coefficient"] = ActionArgValue.Of(NumericValueRef.Expr("1 + skill_runtime.cast.sample * 0.5"), "magnitude_coefficient"),
            ["magnitude_secondary_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.Attribute, "magnitude_secondary_type"),
            ["magnitude_secondary_attribute"] = ActionArgValue.OfConst((int)BattleAttributeType.MAGIC_ATTACK, "magnitude_secondary_attribute"),
            ["magnitude_secondary_coefficient"] = ActionArgValue.Of(NumericValueRef.Expr("0.8 * (1 + skill_runtime.cast.sample * 0.5)"), "magnitude_secondary_coefficient"),
        };
        Assert.True(GiveDamageSchema.Instance.TryValidateArgs(args.ToArray(), out var error), error);
        var damageArgs = GiveDamageSchema.Instance.ParseArgs(args, exec);
        var magnitude = damageArgs.Magnitude;
        var execution = new MobaCombatExecutionContext(null, default, default, default, cast.Handle, 0);
        Assert.True(MobaEffectMagnitudeResolver.TryEvaluate(in magnitude, in execution, in exec,
            20, 20, 10, default, out var damage, out _, out var failure), failure);
        Assert.Equal(expectedDamage, damage, 4);

        var slow = Spec("skill_runtime:cast.sample", -.3f, baseValue: 1f, target: BattleAttributeType.MOVE_SPEED);
        var buff = world.CreateBuff(cast, slow, 10, 101);
        Assert.True(world.Manager.TryActivate(buff));
        Assert.Equal(expectedSpeed, world.Attributes[10].GetValue(MobaAttributeIds.MOVE_SPEED), 4);
        world.Casts.MarkPipelineEnded(cast.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        Assert.True(world.Manager.TryPause(buff));
        Assert.True(world.Manager.TryResume(buff));
        Assert.Equal(expectedSpeed, world.Attributes[10].GetValue(MobaAttributeIds.MOVE_SPEED), 4);
    }

    [Fact]
    public void Trigger_writes_are_isolated_and_buff_capture_survives_cast_release_and_rollback()
    {
        using var world = new World();
        var first = world.CreateCast();
        var second = world.CreateCast();
        world.Write(first, "cast.sample", .5);
        world.Write(second, "cast.sample", 1);
        var castSnapshot = world.Casts.CaptureRollbackSnapshot();
        world.Write(first, "cast.sample", .9);
        world.Casts.RestoreRollbackSnapshot(castSnapshot);
        var spec = Spec("skill_runtime:cast.sample", coefficient: -.3f);
        var firstBuff = world.CreateBuff(first, spec, 10, 101);
        var secondBuff = world.CreateBuff(second, spec, 11, 102);
        Assert.True(world.Manager.TryActivate(firstBuff));
        Assert.True(world.Manager.TryActivate(secondBuff));
        Assert.Equal(85f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP), 4);
        Assert.Equal(70f, world.Attributes[11].GetValue(MobaAttributeIds.MAX_HP), 4);
        var rollback = new MobaContinuousModifierCaptureRollbackProvider(world.Manager);
        var saved = rollback.ExportState(new FrameIndex(1));
        var before = firstBuff.StableModifierCaptures.Single().Value.Value;
        world.Casts.MarkPipelineEnded(first.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        world.Casts.MarkPipelineEnded(second.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        Assert.False(world.Casts.TryGet(first.Handle, out _));
        var query = new MobaContinuousModifierQueryService();
        query.OnInit(world);
        var entry = Assert.Single(query.GetActiveModifiers(10));
        var explanation = Assert.Single(query.ExplainActiveModifiers(10));
        Assert.True(explanation.HasCurrentValue);
        Assert.Equal(-.15f, explanation.CurrentValue, 4);
        Assert.Equal(-.15f, explanation.DeclaredMagnitude.CalculatedValue, 4);
        Assert.Equal(-.15f, explanation.ProjectedMagnitude.CalculatedValue, 4);
        Assert.Equal(-.15f, query.ResolveMagnitude(entry), 4);
#pragma warning disable CS0618
        Assert.Equal(85f, query.EvaluateNumeric(10, spec.TargetKind, spec.TargetId, 100f), 4);
#pragma warning restore CS0618
        Assert.True(world.Manager.TryPause(firstBuff));
        Assert.Equal(100f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP));
        Assert.True(world.Manager.TryResume(firstBuff));
        Assert.Equal(85f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP), 4);
        firstBuff.ClearModifierCaptures();
        secondBuff.ClearModifierCaptures();
        rollback.ImportState(new FrameIndex(1), saved);
        world.Manager.Reproject(firstBuff);
        Assert.Equal(before, firstBuff.StableModifierCaptures.Single().Value.Value);
        Assert.Equal(85f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP), 4);
        Assert.True(world.Manager.TryEnd(firstBuff));
        Assert.Empty(firstBuff.StableModifierCaptures);
        Assert.Equal(100f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP));
    }

    [Theory]
    [InlineData("target", 10, 11)]
    [InlineData("child", 101, 102)]
    public void Non_buff_continuous_reads_the_exact_target_or_child_scope(string scope, int firstOwner, int secondOwner)
    {
        using var world = new World();
        var cast = world.CreateCast();
        world.Write(cast, scope + ".sample", .2, firstOwner);
        world.Write(cast, scope + ".sample", .4, secondOwner);
        var spec = Spec("skill_runtime:" + scope + ".sample", coefficient: -1f);
        var first = new Process(spec, cast.Handle, 10, 101);
        var second = new Process(spec, cast.Handle, 11, 102);
        Assert.True(world.Manager.TryActivate(first));
        Assert.True(world.Manager.TryActivate(second));
        Assert.Equal(80f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP), 4);
        Assert.Equal(60f, world.Attributes[11].GetValue(MobaAttributeIds.MAX_HP), 4);
    }

    [Fact]
    public void Missing_second_input_and_stale_generation_leave_no_capture_or_attribute_contribution()
    {
        using var world = new World();
        var cast = world.CreateCast();
        world.Write(cast, "cast.sample", .2);
        var process = new Process(Spec("skill_runtime:cast.sample"), cast.Handle, 10, 101);
        var projector = new MobaAttributeContinuousModifierProjector();
        projector.OnInit(world);
        var specs = new IMobaContinuousModifierSpec[] { Spec("skill_runtime:cast.sample"), Spec("skill_runtime:cast.missing", definition: 2) };
        Assert.Throws<InvalidOperationException>(() => projector.Apply(process, (IMobaContinuousProjectionConfig)process.Config, specs));
        Assert.Empty(process.StableModifierCaptures);
        var query = new MobaContinuousModifierQueryService();
        query.OnInit(world);
        var entry = new MobaContinuousModifierEntry(process, (IMobaContinuousProjectionConfig)process.Config, specs[0], 1, 0);
        Assert.True(query.TryExplainModifier(entry, out var explanation));
        Assert.False(explanation.HasCurrentValue);
        Assert.False(explanation.HasCapturedValue);
        Assert.False(explanation.DeclaredMagnitude.HasCalculatedValue);
        Assert.Throws<InvalidOperationException>(() => query.ResolveMagnitude(entry));
        var stale = new Process(Spec("skill_runtime:cast.sample"),
            new MobaSkillCastRuntimeHandle(cast.RuntimeId, cast.Generation + 1, cast.RootContextId), 10, 101);
        Assert.Throws<InvalidOperationException>(() => projector.Apply(stale, (IMobaContinuousProjectionConfig)stale.Config, new[] { specs[0] }));
        Assert.Empty(stale.StableModifierCaptures);
        var wrongRoot = new Process(Spec("skill_runtime:cast.sample"),
            new MobaSkillCastRuntimeHandle(cast.RuntimeId, cast.Generation, cast.RootContextId + 1), 10, 101);
        Assert.Throws<InvalidOperationException>(() => projector.Apply(wrongRoot,
            (IMobaContinuousProjectionConfig)wrongRoot.Config, new[] { specs[0] }));
        Assert.Empty(wrongRoot.StableModifierCaptures);
        Assert.Equal(100f, world.Attributes[10].GetValue(MobaAttributeIds.MAX_HP));
    }

    [Theory]
    [InlineData("skill_runtime:effect.sample", 1)]
    [InlineData("skill_runtime:cast.", 1)]
    [InlineData("skill_runtime:cast.sample", 0)]
    public void Invalid_binding_or_realtime_policy_is_rejected(string key, int policy)
    {
        Assert.Throws<InvalidOperationException>(() => MobaContinuousModifierContractValidator.Validate(Spec(key, policy: policy)));
    }

    private static ContinuousModifierMO Spec(string key, float coefficient = 1f, int definition = 1, int policy = 1,
        float baseValue = 0f, BattleAttributeType target = BattleAttributeType.MAX_HP) => new(new ContinuousModifierDTO
    {
        DefinitionId = definition, TargetKind = MobaContinuousModifierTargetKind.Attribute,
        TargetId = (int)target, Op = (int)ModifierOp.PercentAdd,
        MagnitudeSourceType = (int)MagnitudeSourceType.ContextFloat, MagnitudeContextKey = key,
        MagnitudeCoefficient = coefficient, MagnitudeBaseValue = baseValue, EvaluationPolicy = policy,
    });

    private sealed class World : IWorldResolver, IDisposable
    {
        private readonly Contexts _contexts = new();
        private readonly ActorIdIndex _index;
        private readonly MobaActorRegistry _registry = new();
        private readonly MobaEntityManager _entities = new(null);
        private readonly MobaActorLookupService _lookup;
        private readonly MobaContinuousModifierCaptureStore _captures = new();
        private int _nextRoot = 7000;
        public MobaSkillCastRuntimeService Casts { get; } = new();
        public MobaContinuousManager Manager { get; } = new();
        public Dictionary<int, AttributeContext> Attributes { get; } = new();
        public World()
        {
            _index = new ActorIdIndex(_contexts);
            _lookup = new MobaActorLookupService(_index, _registry, _entities, _contexts);
            foreach (var id in new[] { 10, 11, 20 })
            {
                var actor = _contexts.actor.CreateEntity();
                actor.AddActorId(id);
                var attributes = new AttributeContext();
                attributes.SetBase(MobaAttributeIds.MAX_HP, 100f);
                attributes.SetBase(MobaAttributeIds.MOVE_SPEED, 100f);
                attributes.SetBase(MobaAttributeIds.MAGIC_ATTACK, 100f);
                actor.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MAX_HP), attributes);
                Attributes[id] = attributes;
            }
            Manager.OnInit(this);
        }
        public MobaSkillCastRuntime CreateCast() => Casts.Create(new MobaSkillCastRuntimeCreateRequest(
            701, 1, 1, 1, 20, 10, Vec3.Zero, Vec3.Forward, ++_nextRoot,
            levelParameters: new MobaSkillLevelParameterSnapshot(new[] { new MobaSkillLevelParameterValue("damage", 80f) })));
        public BuffContinuousRuntime CreateBuff(MobaSkillCastRuntime cast, ContinuousModifierMO spec, int target, int source)
        {
            var dto = new ContinuousModifierDTO
            {
                DefinitionId = spec.DefinitionId, TargetKind = spec.TargetKind, TargetId = spec.TargetId, Op = spec.Op,
                MagnitudeSourceType = (int)MagnitudeSourceType.ContextFloat, MagnitudeContextKey = spec.Magnitude.ContextFloatKey,
                MagnitudeCoefficient = spec.Magnitude.Coefficient, EvaluationPolicy = spec.EvaluationPolicy,
                MagnitudeBaseValue = spec.Magnitude.BaseValue,
            };
            var buff = new BuffContinuousRuntime(new BuffMO(new BuffDTO { Id = source, DurationMs = 4000, Modifiers = new[] { dto } }), 20, target, 4, null);
            buff.BindRuntime(new BuffRuntime { BuffId = source, SourceId = 20, StackCount = 1,
                SkillRuntimeHandle = cast.Handle, Origin = new MobaGameplayOrigin(20, target, MobaExecutionKind.SkillCast,
                    701, source, source, cast.RootContextId, source, cast.Handle) });
            return buff;
        }
        public void Write(MobaSkillCastRuntime cast, string path, double value, int scopeOwner = 0)
        {
            var scope = path.Split('.')[0];
            var boards = new MobaSkillRuntimeBlackboardResolver(cast.Blackboard, scopeOwner, scopeOwner, scopeOwner);
            var target = new BlackboardWriteTarget(BlackboardIdMapper.BoardId("skill_runtime." + scope),
                BlackboardIdMapper.KeyId("skill_runtime." + path), BlackboardKeyType.Float, scope);
            var actions = new ActionRegistry();
            new SetNumericBlackboardPlanActionModule().Register(actions, this);
            Assert.True(actions.TryGet<NamedAction0<object, object, IWorldResolver>>(SetNumericBlackboardSchema.Instance.ActionId, out var action, out _));
            var ctx = new ExecCtx<IWorldResolver>(this, null, null, actions, boards, null, null, null, null, default, default);
            action(new object(), new SetNumericBlackboardArgs(in target, value), ctx);
        }
        public bool TryResolve(Type type, out object instance)
        {
            instance = type == typeof(MobaActorLookupService) ? _lookup :
                type == typeof(MobaSkillCastRuntimeService) ? Casts :
                type == typeof(MobaContinuousModifierCaptureStore) ? _captures :
                type == typeof(MobaContinuousManager) || type == typeof(IContinuousManager) ? Manager : null;
            return instance != null;
        }
        public bool TryResolve<T>(out T instance) { var found = TryResolve(typeof(T), out var value); instance = found ? (T)value : default; return found; }
        public object Resolve(Type type) => TryResolve(type, out var value) ? value : throw new InvalidOperationException(type.Name);
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public void Dispose() { Manager.Dispose(); _contexts.actor.DestroyAllEntities(); _index.Dispose(); _registry.Dispose(); _entities.Dispose(); }
    }

    private sealed class Process : MobaContinuousRuntimeBase, IMobaContinuousExecutionContextProvider
    {
        private readonly ConfigData _config;
        private readonly MobaCombatContextSource _source;
        public Process(ContinuousModifierMO spec, MobaSkillCastRuntimeHandle handle, int target, int child)
        {
            _config = new ConfigData(spec, target, child);
            _source = new MobaCombatContextSource(EffectContextKind.ContinuousPeriodic, MobaExecutionKind.EffectExecution,
                20, target, child, handle.RootContextId, child, 701, 0, 0, handle, "Test", 701, true);
        }
        public override IContinuousConfig Config => _config;
        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context) { context = MobaCombatContextBuilder.FromSource(this, _source); return true; }
        public bool TryGetContextSource(out MobaContextSourceView source) { TryGetCombatExecutionContext(out var context); return context.TryGetContextSource(out source); }
    }
    private sealed class ConfigData : MobaContinuousConfigBase
    {
        private readonly int _owner, _source;
        public ConfigData(ContinuousModifierMO spec, int owner, int source) : base(0, null, new[] { spec }) { _owner = owner; _source = source; }
        public override string Id => "cast-context-test";
        public override long OwnerId => _owner;
        public override int OwnerActorId => _owner;
        public override int SourceActorId => 20;
        public override int TargetActorId => _owner;
        public override int ModifierSourceId => _source;
        public override GameplayTagSource TagSource => GameplayTagSource.System;
    }
}

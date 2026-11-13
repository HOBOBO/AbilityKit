using AbilityKit.Ability.Config;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaCastContextBattleTests
{
    private const int TriggerId = 99190151;
    private const int BuffId = 99190152;

    [Theory]
    [InlineData(0f, 160f, 70f)]
    [InlineData(.5f, 200f, 55f)]
    [InlineData(1f, 240f, 40f)]
    public void Area_payload_sample_commits_real_damage_and_preserves_slow_through_refresh_and_dispel(
        float sample, float damage, float speed)
    {
        using var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();
        var setup = new LiveSimSetupActionExecutor(battle);
        var caster = Spawn("caster", 1, 100);
        var target = Spawn("target", 2, 102);
        setup.SetScenarioActorAttribute(caster, "MAGIC_ATTACK", 100);
        setup.SetScenarioActorAttribute(target, "MOVE_SPEED", 100);
        setup.Tick(2);
        var services = battle.RuntimeServices!;
        var configs = services.Resolve<MobaConfigDatabase>();
        var buffs = Assert.IsType<IntKeyConfigTable<BuffMO>>(configs.GetTable<BuffMO>());
        buffs.Add(BuffId, new BuffMO(new BuffDTO
        {
            Id = BuffId, DurationMs = 4000, MaxStacks = 1,
            Modifiers = new[] { new ContinuousModifierDTO
            {
                DefinitionId = BuffId, TargetKind = MobaContinuousModifierTargetKind.Attribute,
                TargetId = (int)BattleAttributeType.MOVE_SPEED, Op = (int)ModifierOp.PercentAdd,
                MagnitudeSourceType = (int)MagnitudeSourceType.ContextFloat,
                MagnitudeContextKey = "skill_runtime:cast.residence_ratio",
                MagnitudeBaseValue = 1f, MagnitudeCoefficient = -.3f, EvaluationPolicy = 1
            } }
        }));
        RegisterPlan(services.Resolve<TriggerPlanJsonDatabase>());
        var root = services.Resolve<MobaExecutionContextRegistry>().Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, TriggerId, caster, target));
        var casts = services.Resolve<MobaSkillCastRuntimeService>();
        var cast = casts.Create(new MobaSkillCastRuntimeCreateRequest(
            TriggerId, 1, 1, 1, caster, target, new Vec3(100, 0, 102), Vec3.Forward, root.ContextId,
            levelParameters: new MobaSkillLevelParameterSnapshot(new[] { new MobaSkillLevelParameterValue("damage", 80f) })));
        var payload = new AreaEventArgs
        {
            EventId = "area.terminal", OwnerActorId = caster, TargetActorId = target,
            TemplateId = TriggerId, AreaId = 1, Center = new Vec3(100, 0, 102),
            SourceContextId = root.ContextId, RootContextId = root.ContextId, OwnerContextId = root.ContextId,
            SkillRuntimeHandle = cast.Handle, HasResidenceSample = true, ResidenceRatio = sample
        };
        var hp = setup.GetActorHp(target);
        var request = MobaTriggerExecutionRequest<AreaEventArgs>.Create(TriggerId, payload, "cast-context-battle-test");
        services.Resolve<MobaTriggerExecutionGateway>().ExecuteDirectTrigger(in request);
        Assert.InRange(hp - setup.GetActorHp(target), damage - .01f, damage + .01f);
        var lookup = services.Resolve<MobaActorLookupService>();
        Assert.True(lookup.TryGetActorEntity(target, out var actor));
        Assert.Equal(speed, new MobaAttrs(actor).MoveSpeed, 3);
        Assert.Equal(1, setup.CountActorBuffs(target, BuffId));

        // A later input write and ordinary refresh cannot replace the first capture.
        var boardTarget = new BlackboardWriteTarget(BlackboardIdMapper.BoardId("skill_runtime.cast"),
            BlackboardIdMapper.KeyId("skill_runtime.cast.residence_ratio"), BlackboardKeyType.Double, "");
        var board = new MobaSkillRuntimeBlackboardResolver(cast.Blackboard, 0, target, root.ContextId);
        Assert.True(board.TryResolve(boardTarget.BoardId, out var writable));
        writable.SetDouble(boardTarget.KeyId, 99d);
        casts.MarkPipelineEnded(cast.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        // The production Buff context is a retained child; the input Pipeline has ended.
        Assert.True(casts.TryGet(cast.Handle, out var pending));
        Assert.True(pending.PipelineEnded);
        setup.TickMilliseconds(500);
        Assert.Equal(speed, new MobaAttrs(actor).MoveSpeed, 3);
        Assert.True(services.Resolve<MobaBuffService>().ApplyBuffImmediate(target, BuffId, caster, 4000));
        Assert.Equal(speed, new MobaAttrs(actor).MoveSpeed, 3);
        Assert.True(services.Resolve<MobaBuffService>().RemoveBuffImmediate(target, BuffId, caster, MobaExecutionEndReason.Dispelled));
        Assert.Equal(100f, new MobaAttrs(actor).MoveSpeed, 3);
        Assert.Equal(0, setup.CountActorBuffs(target, BuffId));
        Assert.False(casts.TryGet(cast.Handle, out _));

        int Spawn(string alias, int team, float z)
        {
            setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, heroId = 1001, attributeTemplateId = 1001,
                playerId = "cast_context_" + alias, teamId = team,
                position = new MobaAcceptanceVector3Expectation { x = 100, z = z }
            });
            Assert.True(setup.TryGetActorId(alias, out var id));
            setup.DisableActorBrain(id);
            setup.SetScenarioActorAttribute(id, "MAGIC_DEFENSE", 0);
            setup.SetScenarioActorAttribute(id, "PER_SECOND_BLOOD_R", 0);
            return id;
        }
    }

    private static void RegisterPlan(TriggerPlanJsonDatabase plans)
    {
        var destination = new BlackboardWriteTarget(BlackboardIdMapper.BoardId("skill_runtime.cast"),
            BlackboardIdMapper.KeyId("skill_runtime.cast.residence_ratio"), BlackboardKeyType.Double, "");
        var write = NamedCall(SetNumericBlackboardSchema.Instance.ActionId, new()
        {
            ["target"] = ActionArgValue.OfBlackboardTarget(destination, "target"),
            ["value"] = ActionArgValue.Of(NumericValueRef.PayloadField(MobaBattlePayloadFields.FieldId("area_residence_ratio")), "value")
        });
        var damage = NamedCall(GiveDamageSchema.Instance.ActionId, new()
        {
            ["damage_type"] = ActionArgValue.OfConst((int)DamageType.Magic, "damage_type"),
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.SkillLevelParameter, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("damage", "magnitude_parameter"),
            ["magnitude_coefficient"] = ActionArgValue.Of(NumericValueRef.Expr("1 + skill_runtime.cast.residence_ratio * 0.5"), "magnitude_coefficient"),
            ["magnitude_secondary_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.Attribute, "magnitude_secondary_type"),
            ["magnitude_secondary_attribute"] = ActionArgValue.OfConst((int)BattleAttributeType.MAGIC_ATTACK, "magnitude_secondary_attribute"),
            ["magnitude_secondary_coefficient"] = ActionArgValue.Of(NumericValueRef.Expr("0.8 * (1 + skill_runtime.cast.residence_ratio * 0.5)"), "magnitude_secondary_coefficient")
        });
        var buff = NamedCall(AddBuffSchema.Instance.ActionId, new()
        {
            ["buff_id"] = ActionArgValue.OfConst(BuffId, "buff_id")
        });
        var calls = new[] { write, damage, buff };
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: TriggerId, actions: calls);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(TriggerId, string.Empty, 0, TriggerPlanScope.Global,
            in plan, TriggerPlanExecutableDsl.Sequence(calls.Select(c => TriggerPlanExecutableDsl.Action(c)).ToArray())));
    }

    // The published MOBA schema actions have delegate arity zero, irrespective of business field count.
    private static ActionCallPlan NamedCall(ActionId id, Dictionary<string, ActionArgValue> args) =>
        new(id, 0, default, default, args, EActionScheduleMode.Immediate, 0, -1, true, EActionExecutionPolicy.Immediate);
}

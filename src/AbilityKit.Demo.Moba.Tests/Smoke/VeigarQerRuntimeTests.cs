using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>Real configured casts, collision/trigger processing and health/control results.</summary>
public sealed class VeigarQerRuntimeTests
{
    [Theory]
    [InlineData(0, 8000)]
    [InlineData(49, 8000)]
    [InlineData(50, 7200)]
    [InlineData(99, 7200)]
    [InlineData(100, 6400)]
    [InlineData(449, 1600)]
    [InlineData(450, 800)]
    [InlineData(500, 800)]
    public void W_freezes_approved_stack_step_cooldown_at_commit(int stacks, int cooldown)
    {
        using var battle = new Battle();
        battle.AddGrowth(stacks);
        var record = new MobaParameterRecord(MobaParameterStage.CastCommit, slot: 2,
            baseSkillId: 30040301, effectiveSkillId: 30040301);
        Assert.Equal(cooldown / 8000f, battle.Services.Resolve<MobaSkillParamModifierService>()
            .Skill.ResolveCooldownCommitFactor(battle.Caster, in record), 4);
        battle.Cast(2, new Vec3(100, 0, 106), Vec3.Forward, 0);
        Assert.Equal(cooldown / 8000f, battle.Services.Resolve<MobaSkillParamModifierService>()
            .Skill.ResolveCooldownCommitFactor(battle.Caster, in record), 4);
        battle.Until(() => battle.Count(MobaExecutionKind.AreaSpawn, 42040301) > 0);
        Assert.Equal(cooldown / 8000f, battle.Services.Resolve<MobaSkillParamModifierService>()
            .Skill.ResolveCooldownCommitFactor(battle.Caster, in record), 4);
        var skill = battle.ActiveSkill(2);
        Assert.Equal(cooldown, skill.CooldownDurationMs);
        var end = skill.CooldownEndTimeMs;
        battle.AddGrowth(50);
        battle.Setup.Tick(1);
        Assert.Equal(end, skill.CooldownEndTimeMs);
        Assert.Equal(cooldown, skill.CooldownDurationMs);
        var q = battle.ActiveSkill(1);
        battle.Setup.TickMilliseconds(300);
        battle.Cast(1, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Setup.Tick(2);
        Assert.Equal(6000, q.CooldownDurationMs);
    }

    [Fact]
    public void W_source_removal_keeps_running_cooldown_and_next_cast_returns_to_base()
    {
        using var battle = new Battle();
        battle.AddGrowth(50);
        battle.Cast(2, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Until(() => battle.Count(MobaExecutionKind.AreaSpawn, 42040301) > 0);
        var skill = battle.ActiveSkill(2);
        Assert.Equal(7200, skill.CooldownDurationMs);
        var end = skill.CooldownEndTimeMs;
        Assert.True(battle.Services.Resolve<MobaBuffService>().RemoveBuffImmediate(
            battle.Caster, 30040000, battle.Caster, MobaExecutionEndReason.Cancelled));
        battle.Setup.TickMilliseconds(7000);
        Assert.False(battle.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
            battle.Caster, 2, new Vec3(100, 0, 106), Vec3.Forward, 0).Success);
        Assert.Equal(end, skill.CooldownEndTimeMs);
        Assert.Equal(7200, skill.CooldownDurationMs);
        Assert.True(battle.Services.Resolve<MobaSkillStateSnapshotService>().TryGetSnapshot(
            new AbilityKit.Ability.FrameSync.FrameIndex(999999), out var snapshot));
        var hud = AbilityKit.Protocol.Moba.StateSync.MobaSkillStateSnapshotCodec.Deserialize(snapshot.Payload)
            .Single(entry => entry.ActorId == battle.Caster && entry.Slot == 2);
        Assert.Equal(7200, hud.CooldownTotalMs);
        Assert.InRange(hud.CooldownRemainingMs, 1, 200);
        Assert.Equal(end, hud.CooldownEndTimeMs);
        battle.Setup.TickMilliseconds(300);
        battle.Cast(2, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Until(() => battle.Count(MobaExecutionKind.AreaSpawn, 42040301) > 1);
        Assert.Equal(8000, skill.CooldownDurationMs);
    }

    [Fact]
    public void W_reads_new_stack_threshold_after_prepare_and_reserved_restore_then_commits_once()
    {
        using var battle = new Battle();
        battle.AddGrowth(49);
        var prepare = new SkillCastPreparationService(battle.Services,
            battle.Services.Resolve<AbilityKit.Triggering.Eventing.IEventBus>(),
            battle.Services.Resolve<AbilityKit.Ability.Share.ECS.IUnitResolver>(),
            battle.Services.Resolve<MobaActorLookupService>(), battle.Services.Resolve<IMobaSkillPipelineLibrary>());
        var point = new Vec3(100, 0, 106);
        var direction = Vec3.Forward;
        var input = new SkillCastPreparationInput(battle.Caster, 30040301, 2, in point, in direction, true, 0);
        var prepared = prepare.Prepare(in input);
        Assert.True(prepared.Success, prepared.FailReason);
        var context = new SkillPipelineContext();
        var request = prepared.Request;
        context.Initialize(new object(), in request, prepared.Context);
        var economy = battle.Services.Resolve<MobaSkillEconomyService>();
        var runtimes = battle.Services.Resolve<MobaSkillCastRuntimeService>();
        var specification = new SkillEconomyPhaseDTO { StartSkillCooldown = true,
            UseResolvedSkillCooldown = true, ChargeCost = 0, RequireExplicitCommit = true };
        Assert.True(economy.TryReserve(context, specification, out var error), error);
        var reserved = economy.CaptureRollbackSnapshot();
        var castState = runtimes.CaptureRollbackSnapshot();
        battle.AddGrowth(1);
        var handle = context.RuntimeHandle;
        Assert.True(economy.Commit(in handle));
        var end = battle.ActiveSkill(2).CooldownEndTimeMs;
        Assert.Equal(7200, battle.ActiveSkill(2).CooldownDurationMs);
        Assert.True(economy.Commit(in handle));
        Assert.Equal(end, battle.ActiveSkill(2).CooldownEndTimeMs);
        Assert.True(runtimes.TryResolveCooldownRefund(in handle, battle.Caster, 30040301, 2, .4, out var refund));
        Assert.Equal(2880, refund);
        runtimes.RestoreRollbackSnapshot(in castState);
        economy.RestoreRollbackSnapshot(in reserved);
        Assert.True(economy.Commit(in handle));
        Assert.Equal(7200, battle.ActiveSkill(2).CooldownDurationMs);
        Assert.Equal(end, battle.ActiveSkill(2).CooldownEndTimeMs);
    }

    [Fact]
    public void Basic_attack_deals_damage_on_missile_hit_and_can_repeat_before_full_animation_ends()
    {
        using var battle = new Battle();
        var target = battle.Spawn("basic_target", 1001, 2, 100, 104);
        battle.Setup.Tick(2);
        var hp = battle.Setup.GetActorHp(target);
        battle.Cast(5, new Vec3(100, 0, 104), Vec3.Forward, target);
        battle.Until(() => battle.Count(MobaExecutionKind.ProjectileLaunch, 32040011) == 1);
        Assert.Equal(hp, battle.Setup.GetActorHp(target));
        Assert.Equal(0, battle.Count(MobaExecutionKind.DamageApply, 30040101));
        battle.Setup.TickMilliseconds(900);
        Assert.True(battle.Setup.GetActorHp(target) < hp);
        Assert.Equal(1, battle.Count(MobaExecutionKind.DamageApply, 30040101));
        battle.Cast(5, new Vec3(100, 0, 104), Vec3.Forward, target);
        battle.Setup.TickMilliseconds(500);
        Assert.Equal(2, battle.Count(MobaExecutionKind.DamageApply, 30040101));
        Assert.Equal(0, battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>().ActiveCount);
        Assert.Equal(0, battle.Setup.CountActorBuffs(battle.Caster, 30040000));
    }

    [Fact]
    public void Q_hits_two_targets_leaves_third_unharmed_and_empty_cast_grants_no_growth()
    {
        using var battle = new Battle();
        var first = battle.Spawn("first", 1001, 2, 100, 102);
        var second = battle.Spawn("second", 1001, 2, 100, 104);
        var third = battle.Spawn("third", 1001, 2, 100, 105.7f);
        battle.Setup.SetScenarioActorAttribute(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Setup.Tick(2);
        var before = new[] { first, second, third }.Select(battle.Setup.GetActorHp).ToArray();
        battle.Cast(1, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Setup.TickMilliseconds(1800);
        Assert.InRange(before[0] - battle.Setup.GetActorHp(first), 129.99f, 130.01f);
        // Passive AP growth is sampled by each hit through the existing magnitude contract.
        Assert.InRange(before[1] - battle.Setup.GetActorHp(second), 130.49f, 130.51f);
        Assert.Equal(before[2], battle.Setup.GetActorHp(third));
        Assert.Equal(2, battle.Count(MobaExecutionKind.DamageApply, 30040201));
        Assert.Equal(2, battle.GrowthStacks());

        using var empty = new Battle();
        empty.Cast(1, new Vec3(100, 0, 106), Vec3.Forward, 0);
        empty.Setup.TickMilliseconds(1800);
        Assert.Equal(0, empty.Count(MobaExecutionKind.DamageApply, 30040201));
        Assert.Equal(0, empty.Setup.CountActorBuffs(empty.Caster, 30040000));
    }

    [Theory]
    [InlineData(1, 0, 1f, 5000)]
    [InlineData(1, 100, .5f, 5000)]
    [InlineData(1, 100, 1f/3f, 6000)]
    [InlineData(1, 100, .2f, 5000)]
    [InlineData(1, 100, .5f, 10000)]
    [InlineData(2, 200, .5f, 10000)]
    [InlineData(3, 1000, .5f, 20000)]
    public void R_multiplies_the_whole_rank_damage_by_missing_health_ratio(int rank, float ap, float hpRatio, float maxHp)
    {
        using var battle = new Battle();
        battle.ActiveSkill(4).Level = rank;
        var target = battle.Spawn("target", 3007, 2, 100, 106);
        battle.Setup.SetScenarioActorAttribute(target, "MAX_HP", maxHp);
        battle.Setup.SetScenarioActorAttribute(battle.Caster, "MAGIC_ATTACK", ap);
        var hp = battle.Setup.GetActorMaxHp(target) * hpRatio;
        battle.Setup.SetScenarioActorAttribute(target, "hp", hp);
        battle.Setup.Tick(2);
        battle.Cast(4, new Vec3(100, 0, 106), Vec3.Forward, target);
        battle.Setup.TickMilliseconds(2200);
        var damage = (new[] {175,250,325}[rank-1] + new[] {.65f,.7f,.75f}[rank-1] * ap)
            * (1 + Math.Min(1, 1.5f * (1 - hpRatio)));
        Assert.InRange(hp - battle.Setup.GetActorHp(target), damage - .02f, damage + .02f);
        Assert.Equal(1, battle.Count(MobaExecutionKind.DamageApply, 30040501));
        Assert.Equal(new[] {120000,90000,60000}[rank-1],battle.ActiveSkill(4).CooldownDurationMs);
    }

    [Fact]
    public void R_samples_health_at_impact_and_does_not_redirect_to_a_bystander()
    {
        using var battle = new Battle();
        var target = battle.Spawn("target", 1001, 2, 100, 106);
        var bystander = battle.Spawn("bystander", 1001, 2, 100, 103);
        battle.Setup.SetScenarioActorAttribute(battle.Caster, "MAGIC_ATTACK", 100);
        var max = battle.Setup.GetActorMaxHp(target);
        battle.Setup.SetScenarioActorAttribute(target, "hp", max - 1000);
        battle.Setup.Tick(2);
        battle.Cast(4, new Vec3(100, 0, 106), Vec3.Forward, target);
        battle.Until(() => battle.Count(MobaExecutionKind.ProjectileLaunch, 32040501) == 1);
        Assert.Equal(0, battle.Count(MobaExecutionKind.DamageApply, 30040501));
        battle.Setup.SetScenarioActorAttribute(target, "hp", max);
        battle.Setup.TickMilliseconds(1800);
        Assert.InRange(max - battle.Setup.GetActorHp(target), 239.99f, 240.01f);
        Assert.Equal(battle.Setup.GetActorMaxHp(bystander), battle.Setup.GetActorHp(bystander));
        Assert.Equal(1, battle.Count(MobaExecutionKind.DamageApply, 30040501));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void E_swept_crossing_stuns_once_and_leaving_does_not_cancel_buff(bool inward)
    {
        using var battle = new Battle();
        var crossings = new List<AreaEventArgs>();
        using var subscription = battle.Services.Resolve<IEventBus>().Subscribe(
            new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.cross")),
            payload => crossings.Add(payload));
        var target = battle.Spawn("target", 1001, 2, inward ? 105 : 100, 106);
        battle.Setup.Tick(2);
        battle.Cast(3, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Until(() => battle.Count(MobaExecutionKind.EffectExecution, 30040401) > 0);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        battle.Setup.TickMilliseconds(550);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        battle.Move(target, inward ? 100 : 105, 106);
        battle.Setup.Tick(2);
        Assert.Equal(1, battle.Setup.CountActorBuffs(target, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.BuffApply, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.EffectExecution, 30040413));
        var crossing = Assert.Single(crossings.Where(c => c.TargetActorId == target));
        Assert.Equal(inward ? -1 : 1, crossing.CrossingDirection);
        var accessor = new SkillPipelineContextObjectPayloadAccessor(null!);
        object boxed = crossing;
        Assert.True(accessor.TryGet(in boxed, MobaBattlePayloadFields.FieldId("crossing_direction"), out int direction));
        Assert.Equal(crossing.CrossingDirection, direction);
        battle.AssertStunned(target, true);
        battle.Move(target, 100, 111);
        battle.Setup.TickMilliseconds(350);
        Assert.Equal(1, battle.Setup.CountActorBuffs(target, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.BuffApply, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.EffectExecution, 30040413));
        battle.Setup.TickMilliseconds(1300);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        battle.AssertStunned(target, false);
        // Re-cross a different edge after stun expires: the same cage's claim remains.
        battle.Move(target, 100, 106);
        battle.Setup.Tick(2);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.BuffApply, 32040401));
        Assert.Equal(1, battle.Count(MobaExecutionKind.EffectExecution, 30040413));
    }

    [Fact]
    public void E_expiration_does_not_cancel_a_recent_stun()
    {
        using var battle = new Battle();
        // Use a target without Lian Po's independently timed control immunity.
        var target = battle.Spawn("late", 3007, 2, 105, 106);
        battle.Setup.Tick(2);
        battle.Cast(3, new Vec3(100, 0, 106), Vec3.Forward, 0);
        battle.Until(() => battle.Count(MobaExecutionKind.EffectExecution, 30040401) > 0);
        // Existing Area DurationMs is total lifetime, including the arming delay.
        battle.Setup.TickMilliseconds(3100);
        var active = new List<MobaAreaRuntimeInfo>();
        var areas = battle.Services.Resolve<MobaAreaRuntimeService>();
        Assert.True(areas.TryGetAreas(active, battle.Caster, 42040401));
        battle.Move(target, 100, 106);
        battle.Setup.Tick(2);
        Assert.Equal(1, battle.Setup.CountActorBuffs(target, 32040401));
        battle.Setup.TickMilliseconds(450);
        Assert.False(areas.TryGetAreas(active, battle.Caster, 42040401));
        Assert.Equal(1, battle.Setup.CountActorBuffs(target, 32040401));
        battle.AssertStunned(target, true);
        battle.Setup.TickMilliseconds(1200);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        battle.AssertStunned(target, false);
    }

    [Fact]
    public void R_tracks_its_target_through_the_authored_arena_obstacle()
    {
        using var battle = new Battle();
        battle.Move(battle.Caster, 0, 0);
        var target = battle.Spawn("behind_wall", 1001, 2, 6, 0);
        battle.Setup.Tick(2);
        var hp = battle.Setup.GetActorHp(target);
        battle.Cast(4, new Vec3(6, 0, 0), new Vec3(1, 0, 0), target);
        battle.Setup.TickMilliseconds(2200);
        Assert.True(battle.Count(MobaExecutionKind.ProjectileLaunch, 32040501) > 0);
        Assert.True(battle.Setup.GetActorHp(target) < hp);
        Assert.Equal(1, battle.Count(MobaExecutionKind.DamageApply, 30040501));
        Assert.Equal(0, battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>().ActiveCount);
    }

    [Fact]
    public void R_lost_target_exits_without_hitting_another_actor()
    {
        using var battle = new Battle();
        var target = battle.Spawn("target", 1001, 2, 100, 106);
        var other = battle.Spawn("other", 1001, 2, 102, 105);
        battle.Setup.Tick(2);
        battle.Cast(4, new Vec3(100, 0, 106), Vec3.Forward, target);
        battle.Until(() => battle.Count(MobaExecutionKind.ProjectileLaunch, 32040501) == 1);
        battle.Setup.SetScenarioActorAttribute(target, "hp", 0);
        battle.Setup.TickMilliseconds(1800);
        Assert.Equal(0, battle.Count(MobaExecutionKind.DamageApply, 30040501));
        Assert.Equal(battle.Setup.GetActorMaxHp(other), battle.Setup.GetActorHp(other));
    }

    [Theory]
    [InlineData(1,1500)]
    [InlineData(2,1750)]
    [InlineData(3,2000)]
    [InlineData(4,2250)]
    [InlineData(5,2500)]
    public void E_stun_uses_original_cast_rank_even_after_rank_changes_and_restore(int rank, int duration)
    {
        using var battle = new Battle();
        battle.ActiveSkill(3).Level = rank;
        var target = battle.Spawn("rank_target",3007,2,105,106);
        battle.Setup.Tick(2);
        battle.Cast(3,new Vec3(100,0,106),Vec3.Forward,0);
        battle.Until(() => battle.Count(MobaExecutionKind.AreaSpawn,42040401)>0);
        var runtimes = battle.Services.Resolve<MobaSkillCastRuntimeService>();
        var snapshot = runtimes.CaptureRollbackSnapshot();
        battle.ActiveSkill(3).Level = rank == 5 ? 1 : 5;
        runtimes.RestoreRollbackSnapshot(in snapshot);
        battle.Setup.TickMilliseconds(550);
        battle.Move(target,100,106);
        battle.Setup.Tick(2);
        Assert.Equal(1,battle.Setup.CountActorBuffs(target,32040401));
        Assert.Equal(1,battle.GrowthStacks());
        battle.Setup.TickMilliseconds(duration-150);
        Assert.Equal(1,battle.Setup.CountActorBuffs(target,32040401));
        battle.Setup.TickMilliseconds(250);
        Assert.Equal(0,battle.Setup.CountActorBuffs(target,32040401));
        Assert.Equal(1,battle.GrowthStacks());
    }

    [Theory]
    [InlineData(1,1,7)]
    [InlineData(2,1,6)]
    [InlineData(4,1,6)]
    [InlineData(5,1,5)]
    [InlineData(1,2,1)]
    [InlineData(2,2,0)]
    [InlineData(5,2,0)]
    public void Growth_rewards_require_enemy_hero_or_direct_Q_unit_kill(int slot,int subtype,int expected)
    {
        using var battle = new Battle();
        var target=battle.Spawn("reward",3007,2,100,104,subtype);
        battle.Setup.SetScenarioActorAttribute(target,"hp",1);
        battle.Setup.Tick(2);
        battle.Cast(slot,new Vec3(100,0,104),Vec3.Forward,target);
        battle.Setup.TickMilliseconds(2300);
        Assert.Equal(0,battle.Setup.GetActorHp(target));
        Assert.Equal(expected,battle.GrowthStacks());
    }

    [Theory]
    [InlineData(2,42040301,9.5f)]
    [InlineData(3,42040402,7.25f)]
    public void Ground_cast_clamps_original_point_and_area_to_exact_resolved_range(int slot,int template,float range)
    {
        using var battle = new Battle();
        battle.Move(battle.Caster,103,101);
        battle.Setup.Tick(2);
        battle.Cast(slot,new Vec3(133,0,141),new Vec3(.6f,0,.8f),0);
        battle.Until(() => battle.Count(MobaExecutionKind.AreaSpawn,template)>0);
        var areas=new List<MobaAreaRuntimeInfo>();
        Assert.True(battle.Services.Resolve<MobaAreaRuntimeService>().TryGetAreas(areas,battle.Caster,template));
        var area=Assert.Single(areas);
        Assert.InRange(area.Center.X,103+range*.6f-.001f,103+range*.6f+.001f);
        Assert.InRange(area.Center.Z,101+range*.8f-.001f,101+range*.8f+.001f);
        var runtimes=battle.Services.Resolve<MobaSkillCastRuntimeService>();
        var state=runtimes.CaptureRollbackSnapshot();
        runtimes.RestoreRollbackSnapshot(in state);
        Assert.True(runtimes.TryGet(in area.SkillRuntimeHandle,out var cast));
        Assert.Equal(area.Center,cast.AimPos);
    }

    [Theory]
    [InlineData(0,0,4,0,0)]
    [InlineData(3,4,5,3,4)]
    [InlineData(30,40,5,3,4)]
    [InlineData(-30,40,5,-3,4)]
    [InlineData(30,-40,5,3,-4)]
    [InlineData(3,4,0,0,0)]
    public void Point_range_contract_preserves_ground_height_and_handles_boundary_directions(float x,float z,float range,float expectedX,float expectedZ)
    {
        var origin=new Vec3(10,3,20);
        var point=new Vec3(10+x,7,20+z);
        Assert.True(SkillCastPreparationService.TryClampGroundPoint(in origin,in point,range,out var result));
        Assert.Equal(7,result.Y);
        Assert.InRange(result.X,10+expectedX-.0001f,10+expectedX+.0001f);
        Assert.InRange(result.Z,20+expectedZ-.0001f,20+expectedZ+.0001f);
    }

    [Theory]
    [InlineData(float.NaN,0,5)]
    [InlineData(0,float.PositiveInfinity,5)]
    [InlineData(0,0,float.NaN)]
    [InlineData(0,0,float.PositiveInfinity)]
    [InlineData(0,0,-1)]
    public void Point_range_contract_rejects_invalid_input(float x,float y,float range)
    {
        var origin=Vec3.Zero;
        var point=new Vec3(x,y,0);
        Assert.False(SkillCastPreparationService.TryClampGroundPoint(in origin,in point,range,out _));
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper _bootstrapper;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        public AbilityKit.Ability.World.DI.IWorldResolver Services => _bootstrapper.RuntimeServices!;

        public Battle()
        {
            _bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault(),
                additionalModules: new[] { new MobaTraceAdapterModule() });
            _bootstrapper.Initialize();
            _bootstrapper.Start();
            for (var i = 0; i < 8 && _bootstrapper.Context.EcsWorld == null; i++) _bootstrapper.Tick();
            _bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) _bootstrapper.Tick();
            Setup = new LiveSimSetupActionExecutor(_bootstrapper);
            Caster = Spawn("caster", 3004, 1, 100, 100);
        }

        public int Spawn(string alias, int hero, int team, float x, float z, int subtype = 1)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation {
                action = "spawn_actor", alias = alias, heroId = subtype == 1 ? hero : 0, attributeTemplateId = hero,
                playerId = "veigar_qer_" + alias, teamId = team, unitSubType = subtype,
                position = new MobaAcceptanceVector3Expectation { x = x, z = z },
            });
            Assert.True(Setup.TryGetActorId(alias, out var id));
            Setup.DisableActorBrain(id);
            Setup.SetScenarioActorAttribute(id, "MAGIC_DEFENSE", 0);
            Setup.SetScenarioActorAttribute(id, "PER_SECOND_BLOOD_R", 0);
            return id;
        }

        public void Move(int id, float x, float z) => Setup.MoveScenarioActor(id, new MobaAcceptanceVector3Expectation { x = x, z = z });

        public void Cast(int slot, Vec3 point, Vec3 direction, int target)
        {
            var cast = _bootstrapper.RuntimeServices!.Resolve<SkillCastCoordinator>()
                .TryCastBySlot(Caster, slot, point, direction, target);
            Assert.True(cast.Success, cast.FailReason);
        }

        public int Count(MobaExecutionKind kind, int config) => _bootstrapper.RuntimeServices!
            .Resolve<MobaTraceRegistry>().GetNodesByKind((int)kind).Count(n => n.Metadata?.ConfigId == config);

        public int GrowthStacks()
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(Caster, out var actor));
            return actor.hasBuffs ? actor.buffs.Active.SingleOrDefault(b => b.BuffId == 30040000)?.StackCount ?? 0 : 0;
        }

        public void AddGrowth(int count)
        {
            var buffs = Services.Resolve<MobaBuffService>();
            for (var i = 0; i < count; i++)
                Assert.True(buffs.ApplyBuffImmediate(Caster, 30040000, Caster, 0));
        }

        public AbilityKit.Demo.Moba.Components.ActiveSkillRuntime ActiveSkill(int slot)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(Caster, out var actor));
            return actor.skillLoadout.ActiveSkills[slot - 1];
        }

        public void AssertStunned(int target, bool expected)
        {
            var gates = Services.Resolve<IMobaTagBehaviorGateQueryService>();
            Assert.True(gates.TryIsBlocked(target, moba_luban.MobaTagBehaviorGate.Move, out var movementBlocked));
            Assert.True(gates.TryIsBlocked(target, moba_luban.MobaTagBehaviorGate.Cast, out var castBlocked));
            Assert.Equal(expected, movementBlocked);
            Assert.Equal(expected, castBlocked);
        }

        public void Until(Func<bool> condition)
        {
            for (var i = 0; i < 80 && !condition(); i++) Setup.Tick(1);
            Assert.True(condition(), "Expected real runtime event was not produced.");
        }

        public void Dispose() => _bootstrapper.Dispose();
    }
}

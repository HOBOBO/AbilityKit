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
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>Real configured casts, collision/trigger processing and health/control results.</summary>
public sealed class VeigarQerRuntimeTests
{
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
        Assert.InRange(before[0] - battle.Setup.GetActorHp(first), 179.99f, 180.01f);
        // Passive AP growth is sampled by each hit through the existing magnitude contract.
        Assert.InRange(before[1] - battle.Setup.GetActorHp(second), 180.99f, 181.01f);
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
    [InlineData(100, 0, 200)]
    [InlineData(100, 200, 300)]
    [InlineData(100, 1000, 500)]
    [InlineData(1000, 1000, 600)]
    public void R_applies_authored_missing_health_bonus_and_caps(float ap, float missingHp, float damage)
    {
        using var battle = new Battle();
        var target = battle.Spawn("target", 1001, 2, 100, 106);
        battle.Setup.SetScenarioActorAttribute(battle.Caster, "MAGIC_ATTACK", ap);
        var hp = battle.Setup.GetActorMaxHp(target) - missingHp;
        battle.Setup.SetScenarioActorAttribute(target, "hp", hp);
        battle.Setup.Tick(2);
        battle.Cast(4, new Vec3(100, 0, 106), Vec3.Forward, target);
        battle.Setup.TickMilliseconds(2200);
        Assert.InRange(hp - battle.Setup.GetActorHp(target), damage - .01f, damage + .01f);
        Assert.Equal(1, battle.Count(MobaExecutionKind.DamageApply, 30040501));
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
        Assert.InRange(max - battle.Setup.GetActorHp(target), 199.99f, 200.01f);
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
        battle.Setup.TickMilliseconds(800);
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
        var target = battle.Spawn("late", 1001, 2, 105, 106);
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
        battle.Setup.TickMilliseconds(700);
        Assert.Equal(0, battle.Setup.CountActorBuffs(target, 32040401));
        battle.AssertStunned(target, false);
    }

    [Fact]
    public void R_static_world_rule_blocks_at_the_authored_arena_obstacle()
    {
        using var battle = new Battle();
        battle.Move(battle.Caster, 0, 0);
        var target = battle.Spawn("behind_wall", 1001, 2, 6, 0);
        battle.Setup.Tick(2);
        var hp = battle.Setup.GetActorHp(target);
        battle.Cast(4, new Vec3(6, 0, 0), new Vec3(1, 0, 0), target);
        battle.Setup.TickMilliseconds(2200);
        Assert.True(battle.Count(MobaExecutionKind.ProjectileLaunch, 32040501) > 0);
        Assert.Equal(hp, battle.Setup.GetActorHp(target));
        Assert.Equal(0, battle.Count(MobaExecutionKind.DamageApply, 30040501));
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

        public int Spawn(string alias, int hero, int team, float x, float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation {
                action = "spawn_actor", alias = alias, heroId = hero, attributeTemplateId = hero,
                playerId = "veigar_qer_" + alias, teamId = team,
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
            return actor.buffs.Active.Single(b => b.BuffId == 30040000).StackCount;
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

using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Deterministic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Ability.FrameSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>Uses the published hero, flows, triggers and real battle simulation.</summary>
public sealed class GragasRuntimeTests
{
    [Theory]
    [InlineData("flight")]
    [InlineData("deployed")]
    [InlineData("detonating")]
    public void Q_projectile_and_hfsm_checkpoints_restore_each_phase_without_replaying_gameplay(string phase)
    {
        using var b = new Battle();
        var enemy = b.Spawn("target", 1001, 2, 100, 106);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Until(() => b.Barrels().Count == 1);
        var actor = Assert.Single(b.Barrels());
        if (phase != "flight")
        {
            b.Until(() => b.Areas(42050201).Count == 1);
            b.Setup.TickMilliseconds(700);
        }
        if (phase == "detonating")
        {
            b.Cast(1, Vec3.Zero);
            b.Until(() => b.ProjectilePhase(actor) == "detonating");
            b.Setup.TickMilliseconds(100);
        }
        Assert.Equal(phase, b.ProjectilePhase(actor));
        var hp = b.Setup.GetActorHp(enemy);
        b.RoundtripProjectilePhase(actor);
        Assert.Equal(actor, Assert.Single(b.Barrels()));
        Assert.Equal(phase, b.ProjectilePhase(actor));
        Assert.Equal(hp, b.Setup.GetActorHp(enemy));
        Assert.Equal(phase == "deployed" ? 1 : 0, b.Areas(42050201).Count);
        b.Setup.TickMilliseconds(6000);
        Assert.Empty(b.Barrels());
        Assert.Empty(b.Areas(42050201));
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
        Assert.Equal(1, b.Count(MobaExecutionKind.EffectExecution, 30050211));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Q_keeps_same_actor_at_actual_center_then_plays_half_second_tail(bool manual)
    {
        using var b = new Battle();
        b.Spawn("target", 1001, 2, 100, 106);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Until(() => b.Barrels().Count == 1);
        var original = Assert.Single(b.Barrels());
        b.Until(() => b.Areas(42050201).Count == 1);
        Assert.Equal(original, Assert.Single(b.Barrels()));
        var projectiles = b.Services.Resolve<IProjectileService>();
        var links = b.Services.Resolve<MobaProjectileLinkService>();
        Assert.True(links.TryGetProjectileId(original, out var id));
        Assert.True(projectiles.TryGetRuntimeState(id, out var deployed));
        Assert.True(deployed.HasArrived);
        Assert.InRange(deployed.Position.Z, 105.99f, 106.01f);
        b.Setup.TickMilliseconds(700);
        Assert.Equal("deployed", b.ProjectilePhase(original));
        if (manual) b.Cast(1, new Vec3(110, 0, 110));
        b.Until(() => b.Count(MobaExecutionKind.DamageApply, 30050201) == 1);
        b.Until(() => b.ProjectilePhase(original) == "detonating");
        Assert.Equal(original, Assert.Single(b.Barrels()));
        b.Setup.TickMilliseconds(300);
        Assert.Equal(original, Assert.Single(b.Barrels()));
        b.Setup.TickMilliseconds(300);
        Assert.True(b.Barrels().Count == 0, b.Dump());
        Assert.Empty(b.Areas(42050201));
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
    }
    [Fact]
    public void Q_automatically_explodes_once_with_mature_damage_and_sampled_slow()
    {
        using var b = new Battle();
        var target = b.Spawn("target", 1001, 2, 100, 106);
        b.Set(b.Caster, "MAGIC_ATTACK", 100);
        b.Set(target, "MOVE_SPEED", 100);
        b.Setup.Tick(2);
        var hp = b.Setup.GetActorHp(target);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Until(() => b.Areas(42050201).Count == 1);
        Assert.Equal(hp, b.Setup.GetActorHp(target));
        Assert.Equal(2, b.Param(4));
        b.Setup.TickMilliseconds(4200);
        Assert.InRange(hp - b.Setup.GetActorHp(target), 239.98f, 240.02f);
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
        Assert.InRange(b.Speed(target), 39.98f, 40.02f);
        Assert.Empty(b.Areas(42050201));
        Assert.Equal(0, b.Param(4));
        b.Setup.TickMilliseconds(2200);
        Assert.Equal(100f, b.Speed(target), 3);
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
    }

    [Fact]
    public void Q_manual_recast_is_free_and_keeps_original_cooldown_and_passive_icd()
    {
        using var b = new Battle();
        var target = b.Spawn("target", 1001, 2, 100, 106);
        b.Set(b.Caster, "hp", 300);
        b.Setup.Tick(2);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Until(() => b.Areas(42050201).Count == 1);
        b.Setup.TickMilliseconds(700); // Full Q animation must end before the next input.
        var mana = b.Setup.GetActorMana(b.Caster);
        var hp = b.Setup.GetActorHp(b.Caster);
        Assert.InRange(hp, 335.19f, 335.21f);
        b.Cast(1, new Vec3(100, 0, 108));
        b.Setup.Tick(2);
        Assert.Equal(mana, b.Setup.GetActorMana(b.Caster));
        Assert.Equal(hp, b.Setup.GetActorHp(b.Caster));
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
        Assert.Empty(b.Areas(42050201));
        b.Setup.TickMilliseconds(4500);
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
        Assert.False(b.TryCast(b.Caster, 1, new Vec3(100, 0, 106), 0).Success);
        Assert.True(b.Setup.GetActorHp(target) < b.Setup.GetActorMaxHp(target));
    }

    [Fact]
    public void W_grants_after_drinking_and_consumes_one_source_for_per_target_max_hp_splash()
    {
        using var b = new Battle();
        var target = b.Spawn("target", 1001, 2, 100, 101);
        var splash = b.Spawn("splash", 1001, 2, 101, 101);
        b.Set(b.Caster, "MAGIC_ATTACK", 100);
        b.Set(target, "MAX_HP", 2000);
        b.Set(target, "hp", 2000);
        b.Set(splash, "MAX_HP", 3000);
        b.Set(splash, "hp", 3000);
        b.Setup.Tick(2);
        b.Cast(2, new Vec3(100, 0, 101));
        b.Setup.TickMilliseconds(500);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050391));
        b.Setup.TickMilliseconds(750);
        Assert.Equal(1, b.Setup.CountActorBuffs(b.Caster, 30050391));
        Assert.Equal(1, b.Param(5));
        b.Cast(5, new Vec3(100, 0, 101), target);
        b.Setup.TickMilliseconds(1900);
        Assert.True(2000 - b.Setup.GetActorHp(target) > 293.98f, b.Dump());
        Assert.InRange(2000 - b.Setup.GetActorHp(target), 293.98f, 294.02f); // 64 physical + 230 magic.
        Assert.InRange(3000 - b.Setup.GetActorHp(splash), 299.98f, 300.02f);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050391));
        Assert.Equal(0, b.Param(5));
        Assert.Equal(2, b.Count(MobaExecutionKind.DamageApply, 30050701));
    }

    [Theory]
    [InlineData(0f, 90f)]
    [InlineData(100f, 86f)]
    public void W_reduction_reads_defender_ap_at_each_hit_and_expires(float ap, float expected)
    {
        using var b = new Battle();
        var attacker = b.Spawn("attacker", 3005, 2, 100, 101);
        b.Set(attacker, "PHYSICS_ATTACK", 100);
        b.Set(b.Caster, "MAGIC_ATTACK", 0);
        b.Setup.Tick(2);
        b.Cast(2, new Vec3(100, 0, 101));
        b.Setup.Tick(2);
        Assert.Equal(1, b.Setup.CountActorBuffs(b.Caster, 30050381));
        b.Set(b.Caster, "MAGIC_ATTACK", ap); // AP changes after drinking began.
        var hp = b.Setup.GetActorHp(b.Caster);
        Assert.True(b.TryCast(attacker, 5, new Vec3(100, 0, 100), b.Caster).Success);
        b.Setup.TickMilliseconds(500);
        Assert.InRange(hp - b.Setup.GetActorHp(b.Caster), expected - .02f, expected + .02f);
        b.Setup.TickMilliseconds(2300);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050381));
        hp = b.Setup.GetActorHp(b.Caster);
        Assert.True(b.TryCast(attacker, 5, new Vec3(100, 0, 100), b.Caster).Success);
        b.Setup.TickMilliseconds(500);
        Assert.InRange(hp - b.Setup.GetActorHp(b.Caster), 99.98f, 100.02f);
    }

    [Fact]
    public void E_first_enemy_collision_damages_stuns_and_stops_before_second_target()
    {
        using var b = new Battle();
        var first = b.Spawn("first", 1001, 2, 100, 103);
        var ally = b.Spawn("ally", 1001, 1, 100, 101.5f);
        var second = b.Spawn("second", 1001, 2, 100, 106);
        b.Set(b.Caster, "MAGIC_ATTACK", 100);
        b.Setup.Tick(2);
        var hp = b.Setup.GetActorHp(first);
        b.CastDirection(3, Vec3.Forward);
        var originalCooldownEnd = b.CooldownEnd(3);
        b.Until(() => b.Count(MobaExecutionKind.DamageApply, 30050401) > 0);
        Assert.Equal(5600, originalCooldownEnd - b.CooldownEnd(3));
        Assert.InRange(hp - b.Setup.GetActorHp(first), 139.98f, 140.02f);
        Assert.Equal(1, b.Setup.CountActorBuffs(first, 30050490));
        Assert.Equal(b.Setup.GetActorMaxHp(second), b.Setup.GetActorHp(second));
        Assert.Equal(b.Setup.GetActorMaxHp(ally), b.Setup.GetActorHp(ally));
        Assert.Equal(0, b.Setup.CountActorBuffs(ally, 30050490));
        Assert.InRange(b.Setup.GetActorPosition(b.Caster).Z, 101.8f, 102.6f);
        Assert.True(b.IsStunned(first));
        b.Setup.TickMilliseconds(300);
        Assert.InRange(b.Setup.GetActorPosition(first).Z, 103.8f, 104.1f);
        Assert.True(b.IsStunned(first));
        b.Setup.TickMilliseconds(1400);
        Assert.Equal(0, b.Setup.CountActorBuffs(first, 30050490));
        Assert.False(b.IsStunned(first));
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050401));
    }

    [Fact]
    public void R_explodes_at_aim_point_and_pushes_targets_radially_away()
    {
        using var b = new Battle();
        var left = b.Spawn("left", 1001, 2, 98, 106);
        var right = b.Spawn("right", 1001, 2, 102, 106);
        var center = b.Spawn("center", 1001, 2, 100, 106);
        b.Set(b.Caster, "MAGIC_ATTACK", 100);
        b.Setup.Tick(2);
        var hp = b.Setup.GetActorHp(left);
        b.Cast(4, new Vec3(100, 0, 106));
        b.Setup.TickMilliseconds(1800);
        Assert.True(hp - b.Setup.GetActorHp(left) > 279.98f, b.Dump());
        Assert.InRange(hp - b.Setup.GetActorHp(left), 279.98f, 280.02f);
        Assert.InRange(hp - b.Setup.GetActorHp(right), 279.98f, 280.02f);
        Assert.True(b.Setup.GetActorPosition(left).X < 97f);
        Assert.True(b.Setup.GetActorPosition(right).X > 103f);
        Assert.True(b.Setup.GetActorPosition(center).Z > 107f, "Coincident center must use the frozen aim direction.");
        Assert.Equal(3, b.Count(MobaExecutionKind.DamageApply, 30050501));
    }

    [Fact]
    public void Q_flight_rejects_recast_then_barrel_survives_owner_movement_and_another_skill()
    {
        using var b = new Battle();
        var target = b.Spawn("target", 1001, 2, 100, 106);
        b.Setup.Tick(2);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Setup.TickMilliseconds(300);
        Assert.Equal(1, b.Param(4));
        var mana = b.Setup.GetActorMana(b.Caster);
        Assert.False(b.TryCast(b.Caster, 1, new Vec3(100, 0, 108), 0).Success);
        Assert.Equal(mana, b.Setup.GetActorMana(b.Caster));
        b.Until(() => b.Areas(42050201).Count == 1);
        b.Setup.TickMilliseconds(800);
        b.Setup.MoveScenarioActor(b.Caster, new MobaAcceptanceVector3Expectation { x = 110, z = 100 });
        b.Cast(2, new Vec3(110, 0, 100));
        b.Setup.TickMilliseconds(1300);
        Assert.Equal(2, b.Param(4));
        Assert.Equal(1, b.Param(5));
        Assert.Single(b.Areas(42050201));
        b.Setup.TickMilliseconds(3000);
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
        Assert.True(b.Setup.GetActorHp(target) < b.Setup.GetActorMaxHp(target));
        Assert.Equal(0, b.Param(4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Q_owner_death_cleans_flight_or_deployed_barrel_without_explosion(bool deployed)
    {
        using var b = new Battle();
        var enemy = b.Spawn("target", 1001, 2, 100, 106);
        b.Setup.Tick(2);
        b.Cast(1, new Vec3(100, 0, 106));
        if (deployed) b.Until(() => b.Areas(42050201).Count == 1);
        else b.Setup.TickMilliseconds(300);
        var lethal = new AttackInfo { AttackerActorId = enemy, TargetActorId = b.Caster, DamageType = DamageType.True,
            ReasonKind = DamageReasonKind.Skill, ReasonParam = 1 };
        lethal.BaseDamage.FixedBaseValue = Fixed64.FromInt32(10000);
        Assert.NotNull(b.Services.Resolve<MobaCombatEffectService>().DealDamage(lethal));
        b.Setup.Tick(2);
        Assert.Empty(b.Areas(42050201));
        Assert.True(b.Barrels().Count == 0, b.Dump());
        Assert.Equal(0, b.Param(4));
        b.Setup.TickMilliseconds(5500);
        Assert.Empty(b.Areas(42050201));
        Assert.Equal(0, b.Param(4));
        Assert.Equal(0, b.Count(MobaExecutionKind.DamageApply, 30050201));
    }

    [Fact]
    public void Q_owner_death_during_tail_removes_bucket_without_another_explosion()
    {
        using var b = new Battle();
        var enemy = b.Spawn("target", 1001, 2, 100, 106);
        b.Cast(1, new Vec3(100, 0, 106));
        b.Until(() => b.Areas(42050201).Count == 1);
        b.Setup.TickMilliseconds(700);
        b.Cast(1, Vec3.Zero);
        b.Until(() => b.Count(MobaExecutionKind.DamageApply, 30050201) == 1);
        Assert.Single(b.Barrels());
        var lethal = new AttackInfo { AttackerActorId = enemy, TargetActorId = b.Caster, DamageType = DamageType.True,
            ReasonKind = DamageReasonKind.Skill, ReasonParam = 1 };
        lethal.BaseDamage.FixedBaseValue = Fixed64.FromInt32(10000);
        b.Services.Resolve<MobaCombatEffectService>().DealDamage(lethal);
        b.Setup.Tick(2);
        Assert.True(b.Barrels().Count == 0, b.Dump());
        b.Setup.TickMilliseconds(1000);
        Assert.Equal(1, b.Count(MobaExecutionKind.DamageApply, 30050201));
    }

    [Fact]
    public void W_cancel_before_drink_completes_does_not_grant_empowerment()
    {
        using var b = new Battle();
        b.Cast(2, Vec3.Zero);
        b.Setup.TickMilliseconds(500);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().CancelBySlot(b.Caster, 2));
        b.Setup.TickMilliseconds(1200);
        Assert.Equal(0, b.Param(5));
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050391));
        Assert.Equal(1, b.Setup.CountActorBuffs(b.Caster, 30050381));
        b.Setup.TickMilliseconds(1000);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050381));
    }

    [Fact]
    public void W_shield_absorbed_attack_still_consumes_empowerment_and_splashes()
    {
        using var b = new Battle();
        var target = b.Spawn("target", 1001, 2, 100, 101);
        var splash = b.Spawn("splash", 1001, 2, 101, 101);
        b.Setup.Tick(2);
        b.Services.Resolve<MobaShieldService>().AddShield(target, new ShieldLayer
        {
            ShieldId = 900001, SourceActorId = target, OwnerActorId = target, TargetActorId = target,
            CurrentValue = Fixed64.FromInt32(5000), MaxValue = Fixed64.FromInt32(5000), InitialValue = Fixed64.FromInt32(5000),
            AbsorbRatio = Fixed64.One, DamageTypeMask = 7, ExpireFrame = int.MaxValue,
            StackingPolicy = ShieldStackingPolicy.Independent, ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
        });
        b.Cast(2, Vec3.Zero);
        b.Setup.TickMilliseconds(1300);
        b.Cast(5, new Vec3(100, 0, 101), target);
        b.Setup.TickMilliseconds(1800);
        Assert.Equal(b.Setup.GetActorMaxHp(target), b.Setup.GetActorHp(target));
        Assert.True(b.Setup.GetActorHp(splash) < b.Setup.GetActorMaxHp(splash));
        Assert.Equal(0, b.Param(5));
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 30050391));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void E_direction_only_empty_cast_moves_six_units_and_keeps_full_cooldown(float dx, float dz)
    {
        using var b = new Battle();
        b.CastDirection(3, new Vec3(dx, 0f, dz));
        var original = b.CooldownEnd(3);
        b.Setup.TickMilliseconds(1600);
        Assert.Equal(original, b.CooldownEnd(3));
        Assert.Equal(0, b.Count(MobaExecutionKind.DamageApply, 30050401));
        Assert.InRange(b.Setup.GetActorPosition(b.Caster).Z, 100f + 6f * dz - .05f, 100f + 6f * dz + .05f);
        Assert.InRange(b.Setup.GetActorPosition(b.Caster).X, 100f + 6f * dx - .05f, 100f + 6f * dx + .05f);
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper bootstrap;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        public AbilityKit.Ability.World.DI.IWorldResolver Services => bootstrap.RuntimeServices!;
        public Battle()
        {
            bootstrap = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault(), additionalModules: new[] { new MobaTraceAdapterModule() });
            bootstrap.Initialize(); bootstrap.Start();
            for (var i = 0; i < 8 && bootstrap.Context.EcsWorld == null; i++) bootstrap.Tick();
            bootstrap.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrap.Tick();
            Setup = new LiveSimSetupActionExecutor(bootstrap);
            Caster = Spawn("caster", 3005, 1, 100, 100);
            Setup.Tick(2);
        }
        public int Spawn(string alias, int hero, int team, float x, float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation { action = "spawn_actor", alias = alias,
                heroId = hero, attributeTemplateId = hero, playerId = "gragas_" + alias, teamId = team,
                position = new MobaAcceptanceVector3Expectation { x = x, z = z } });
            Assert.True(Setup.TryGetActorId(alias, out var id));
            Setup.DisableActorBrain(id);
            Set(id, "MAGIC_DEFENSE", 0); Set(id, "PHYSICS_DEFENSE", 0);
            Set(id, "PER_SECOND_BLOOD_R", 0); Set(id, "PER_SECOND_MANA_R", 0);
            return id;
        }
        public void Set(int id, string key, float value) => Setup.SetScenarioActorAttribute(id, key, value);
        public void Cast(int slot, Vec3 point, int target = 0)
        {
            var result = TryCast(Caster, slot, point, target);
            Assert.True(result.Success, result.FailReason + "\n" + Dump());
        }
        public MobaSkillCastResult TryCast(int actor, int slot, Vec3 point, int target) => Services.Resolve<SkillCastCoordinator>()
            .TryCastBySlot(actor, slot, point, Vec3.Forward, target);
        public void CastDirection(int slot, Vec3 direction)
        {
            var result = Services.Resolve<SkillCastCoordinator>().TryCastBySlot(Caster, slot, Vec3.Zero, direction, 0);
            Assert.True(result.Success, result.FailReason + "\n" + Dump());
        }
        public List<MobaAreaRuntimeInfo> Areas(int template)
        {
            var list = new List<MobaAreaRuntimeInfo>();
            Services.Resolve<MobaAreaRuntimeService>().TryGetAreas(list, Caster, template);
            return list;
        }
        public List<int> Barrels()
        {
            var links = new List<MobaProjectileLinkDiagnostics>();
            Services.Resolve<MobaProjectileLinkService>().CopyDiagnosticsTo(links);
            return links.Where(l => l.Source.ProjectileConfigId == 32050201 && l.Source.SourceActorId == Caster)
                .Select(l => l.ActorId).ToList();
        }
        public string ProjectilePhase(int actor) => Actor(actor).actorStateMachine.Runtime.State.ActiveStatePath.TrimStart('/');
        public void RoundtripProjectilePhase(int actorId)
        {
            var frame = Services.Resolve<IFrameTime>().Frame;
            var projectiles = Services.Resolve<IProjectileService>();
            var runtime = Actor(actorId).actorStateMachine.Runtime;
            var source = runtime.CaptureSnapshot();
            var checkpoint = projectiles.ExportRollback(frame);
            var casts = Services.Resolve<MobaSkillCastRuntimeService>();
            var castState = casts.CaptureRollbackSnapshot();
            // Exercise the public module restore paths while the existing actor,
            // source context and Area bindings still belong to the same world.
            // Structural world rollback has separate lifecycle/roster preflight.
            runtime.Tick(new FrameIndex(frame.Value + 1), .1f);
            projectiles.Tick(frame.Value + 1, .1f);
            projectiles.ImportRollback(frame, checkpoint);
            casts.RestoreRollbackSnapshot(castState);
            runtime.RestoreSnapshot(source);
            Assert.Equal(source.State.ActiveStatePath, runtime.State.ActiveStatePath);
            Assert.Equal(source.State.EnteredFrame, runtime.State.EnteredFrame);
            Assert.Equal(source.State.DurationSeconds, runtime.State.DurationSeconds);
            Assert.Equal(checkpoint, projectiles.ExportRollback(frame));
        }
        public int Count(MobaExecutionKind kind, int config) => Services.Resolve<MobaTraceRegistry>()
            .GetNodesByKind((int)kind).Count(n => n.Metadata?.ConfigId == config);
        public float Speed(int actor) => new MobaAttrs(Actor(actor)).MoveSpeed;
        public bool IsStunned(int actor)
        {
            var gates = Services.Resolve<IMobaTagBehaviorGateQueryService>();
            Assert.True(gates.TryIsBlocked(actor, moba_luban.MobaTagBehaviorGate.Move, out var move));
            Assert.True(gates.TryIsBlocked(actor, moba_luban.MobaTagBehaviorGate.Cast, out var cast));
            Assert.Equal(move, cast);
            return move;
        }
        private ActorEntity Actor(int id)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id, out var actor));
            return actor;
        }
        public float Param(int id) => Actor(Caster).characterHfsm.Runtime.ResolveParameter(id);
        public long CooldownEnd(int slot) => Actor(Caster).skillLoadout.ActiveSkills[slot - 1].CooldownEndTimeMs;
        public void Until(Func<bool> condition)
        {
            for (var i = 0; i < 240 && !condition(); i++) Setup.Tick(1);
            Assert.True(condition(), "Expected configured runtime result was not produced. " + Dump());
        }
        public string Dump() => "Barrels: " + string.Join(",", Barrels().Select(id => {
            var links = Services.Resolve<MobaProjectileLinkService>(); links.TryGetProjectileId(id, out var p);
            return id + " active=" + Services.Resolve<IProjectileService>().TryGetRuntimeState(p, out _) + " phase=" + ProjectilePhase(id);
        })) + "\n" + string.Join("\n", Services.Resolve<IMobaBattleDiagnosticsService>().GetWarningsSnapshot().Select(w => w.Message)) + "\n" + string.Join("\n", Enum.GetValues<MobaExecutionKind>().SelectMany(k => Services.Resolve<MobaTraceRegistry>()
            .GetNodesByKind((int)k)).Where(n => n.Metadata?.SourceActorId == Caster).Select(n => n.Metadata?.ToDisplayString()));
        public void Dispose() => bootstrap.Dispose();
    }
}

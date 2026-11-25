using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 使用已发布七英雄配置和正式施法入口验证数值；预期值是项目基线，
/// 不将配置正确或测试通过表述为完整复刻及实战平衡已经完成。
/// </summary>
public sealed class SevenHeroNumericBaselineTests
{
    [Theory]
    [InlineData(1, false, 149)]
    [InlineData(1, true, 149)]
    [InlineData(3, false, 300)]
    [InlineData(3, true, 300)]
    public void Composite_hit_commits_one_damage_transaction_with_shield_and_lethal_targets(int slot, bool lethal, float expected)
    {
        using var battle = new Battle(3001);
        battle.Set(battle.Caster, "physics_attack", 100);
        battle.Set(battle.Caster, "magic_attack", 100);
        if (lethal) battle.Set(battle.Target, "hp", 50);
        var shields = battle.Services.Resolve<MobaShieldService>();
        shields.AddShield(battle.Target, new ShieldLayer
        {
            ShieldId = 900001, SourceActorId = battle.Caster, OwnerActorId = battle.Caster,
            TargetActorId = battle.Target, CurrentValue = Fixed64.FromInt32(25),
            MaxValue = Fixed64.FromInt32(25), InitialValue = Fixed64.FromInt32(25),
            AbsorbRatio = Fixed64.One, DamageTypeMask = (int)(DamageType.Physical | DamageType.Magic),
            ExpireFrame = int.MaxValue, RemoveWhenDepleted = true,
            StackingPolicy = ShieldStackingPolicy.Independent, ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest
        });
        var results = new List<DamageResult>();
        using var subscription = battle.Services.Resolve<IEventBus>().Subscribe(
            new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)),
            result => { if (result.TargetActorId == battle.Target) results.Add(result); });
        var before = battle.Setup.GetActorHp(battle.Target);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(6500);
        var result = Assert.Single(results);
        Assert.Equal(25, result.ShieldAbsorb, .05f);
        Assert.Equal(lethal, result.TargetBecameDead);
        Assert.Equal(lethal ? 0 : before - expected + 25, result.TargetHp, .1f);
        if (!lethal) AssertAuthoritativeProjectionAndImportBoundary(battle, battle.Target, result.TargetHp, 10000);
    }

    [Fact]
    public void Activation_rank_source_rejects_missing_ambiguous_and_revoked_origins_and_roundtrips_commit_marker()
    {
        using var battle = new Battle(3001, 6);
        var actor = battle.Actor(battle.Caster);
        Assert.False(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out _, out _));
        actor.skillLoadout.ActiveSkills[3].Level = 2;
        var entry = battle.Skills.TryCastBySlot(battle.Caster, 4, default, Vec3.Forward);
        Assert.True(entry.Success, entry.FailReason);
        battle.Setup.TickMilliseconds(1500);
        Assert.True(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out var rank, out var root));
        Assert.Equal(2, rank);
        Assert.Equal(entry.RuntimeHandle.RootContextId, root);
        var mode = Assert.Single(actor.buffs.Active.Where(buff => buff.BuffId == 32010404));
        actor.buffs.Active.Add(mode);
        Assert.False(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out _, out _));
        actor.buffs.Active.Remove(mode);

        var request = new SkillCastRequest(30010501, 5, battle.Caster, battle.Target, Vec3.Zero, Vec3.Forward,
            battle.Services, null, null, null);
        var trigger = new SkillCastContext { SkillLevel = 2, LevelSourceRootContextId = root };
        var context = new SkillPipelineContext();
        context.Initialize(new object(), request, trigger);
        Assert.True(context.CanCommitPreparedParameters());
        var checkpoint = context.CaptureRollbackState();
        var restored = new SkillPipelineContext();
        restored.Initialize(new object(), request, trigger);
        restored.RestoreRollbackState(checkpoint);
        Assert.Equal(root, Assert.IsType<long>(restored.SharedData["cast.level.source.root"]));
        actor.skillLoadout.ActiveSkills[3].Level = 3;
        Assert.True(restored.CanCommitPreparedParameters());
        var remaining = mode.Remaining;
        mode.Remaining = 0;
        Assert.False(restored.CanCommitPreparedParameters());
        mode.Remaining = remaining;
        Assert.True(restored.CanCommitPreparedParameters());
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_summon_birth_override_rejects_before_allocating_an_actor(float invalid)
    {
        using var battle = new Battle(3003);
        var actors = battle.Services.Resolve<MobaActorRegistry>();
        var before = actors.Entries.Select(pair => pair.Key).Order().ToArray();
        var summon = battle.Services.Resolve<MobaSummonService>();
        var attributes = new[] { new KeyValuePair<BattleAttributeType, float>(BattleAttributeType.HP, invalid) };
        Assert.False(summon.TrySummon(default(MobaSummonRequestTicket), battle.Caster, 30030502, Vec3.Zero,
            Vec3.Forward, default(SummonSourceContext), out var id, 14000, attributes));
        Assert.Equal(0, id);
        Assert.Equal(before, actors.Entries.Select(pair => pair.Key).Order().ToArray());
    }

    [Theory]
    [InlineData(3001, 655, 107, 61, 4, 300, 50)]
    [InlineData(3002, 610, 105, 59, 3, 260, 50)]
    [InlineData(3003, 560, 96, 50, 3, 418, 25)]
    [InlineData(3004, 580, 108, 52, 3, 490, 26)]
    [InlineData(3005, 640, 115, 64, 4, 400, 47)]
    [InlineData(3006, 620, 110, 46, 3, 467, 31)]
    [InlineData(3007, 580, 99, 54, 3, 440, 24)]
    public void Published_attributes_apply_the_linear_baseline_at_levels_one_nine_and_eighteen(
        int hero, int hp, int hpGrowth, int ad, int adGrowth, int mana, int manaGrowth)
    {
        using var battle = new Battle(hero);
        var configs = battle.Services.Resolve<MobaConfigDatabase>();
        Assert.True(configs.TryGetAttributeTemplate(hero, out var template));
        foreach (var level in new[] { 1, 9, 18 })
        {
            var prepared = new MobaActorAttributeInitializer().Prepare(template, level);
            Assert.Equal(hp + hpGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.MAX_HP));
            Assert.Equal(hp + hpGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.HP));
            Assert.Equal(mana + manaGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.MAX_MANA));
            Assert.Equal(ad + adGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.PHYSICS_ATTACK));
            Assert.Equal(0, prepared.Group.GetValue(MobaAttributeIds.MAGIC_ATTACK));
            Assert.InRange(prepared.Group.GetValue(MobaAttributeIds.MOVE_SPEED), 3.2f, 3.5f);
        }
    }

    [Theory]
    [InlineData(3001, 5)]
    [InlineData(3002, 5)]
    [InlineData(3003, 5)]
    [InlineData(3004, 5)]
    [InlineData(3005, 5)]
    [InlineData(3006, 5)]
    [InlineData(3007, 5)]
    public void Basic_attacks_do_one_total_ad_without_demonstration_flat_damage(int hero, int slot)
    {
        using var battle = new Battle(hero, hero == 3005 ? 1.5f : 3);
        battle.Set(battle.Caster, "physics_attack", 100);
        battle.Set(battle.Caster, "magic_attack", 275);
        var before = battle.Setup.GetActorHp(battle.Target);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, default, Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2500);
        Assert.Equal(100f, before - battle.Setup.GetActorHp(battle.Target), .05f);
    }

    [Theory]
    [InlineData(3001, 1, 1, 40, 7000)]
    [InlineData(3001, 1, 5, 60, 5000)]
    [InlineData(3001, 2, 1, 50, 12000)]
    [InlineData(3001, 2, 5, 70, 12000)]
    [InlineData(3001, 3, 1, 30, 2000)]
    [InlineData(3001, 3, 5, 30, 2000)]
    [InlineData(3001, 4, 1, 100, 120000)]
    [InlineData(3001, 4, 3, 100, 90000)]
    [InlineData(3002, 3, 1, 90, 24000)]
    [InlineData(3002, 3, 5, 90, 10000)]
    [InlineData(3002, 4, 1, 100, 85000)]
    [InlineData(3002, 4, 3, 100, 45000)]
    [InlineData(3004, 3, 1, 70, 20000)]
    [InlineData(3004, 3, 5, 90, 14000)]
    public void Actual_commit_consumes_rank_mana_once_and_starts_rank_cooldown(
        int hero, int slot, int rank, float mana, int cooldownMs)
    {
        using var battle = new Battle(hero);
        var runtime = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1];
        battle.Set(battle.Caster, "MANA_REGEN_PER_SECOND", 0);
        runtime.Level = rank;
        var before = battle.Setup.GetActorMana(battle.Caster);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2000);
        Assert.Equal(mana, before - battle.Setup.GetActorMana(battle.Caster), .05f);
        Assert.Equal(cooldownMs, runtime.CooldownDurationMs);
        // 既有烬 E Flow 使用固定 18 秒逐个充能，和等级表的 2 秒施法间隔分开断言。
        if (runtime.MaxCharges > 1) Assert.Equal(18000, runtime.ChargeRecoveryMs);
    }

    [Theory]
    [InlineData(3001, 1, 1, 0, 0, 45)]
    [InlineData(3001, 1, 3, 100, 0, 154)]
    [InlineData(3001, 1, 5, 200, 0, 293)]
    [InlineData(3001, 1, 1, 100, 100, 149)]
    [InlineData(3001, 1, 3, 100, 200, 274)]
    [InlineData(3001, 1, 5, 200, 300, 473)]
    [InlineData(3001, 2, 1, 0, 0, 60)]
    [InlineData(3001, 2, 3, 100, 0, 180)]
    [InlineData(3001, 2, 5, 200, 0, 300)]
    [InlineData(3001, 3, 1, 0, 0, 80)]
    [InlineData(3001, 3, 3, 100, 0, 310)]
    [InlineData(3001, 3, 5, 200, 0, 540)]
    [InlineData(3001, 3, 1, 100, 100, 300)]
    [InlineData(3001, 3, 3, 100, 200, 510)]
    [InlineData(3001, 3, 5, 200, 300, 840)]
    [InlineData(3002, 3, 1, 0, 0, 70)]
    [InlineData(3002, 3, 3, 0, 100, 270)]
    [InlineData(3002, 3, 5, 0, 200, 470)]
    public void Published_hits_use_rank_base_and_the_configured_attribute(
        int hero, int slot, int rank, float ad, float ap, float expected)
    {
        using var battle = new Battle(hero);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].Level = rank;
        battle.Set(battle.Caster, "physics_attack", ad);
        battle.Set(battle.Caster, "magic_attack", ap);
        var before = battle.Setup.GetActorHp(battle.Target);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(6500);
        Assert.Equal(expected, before - battle.Setup.GetActorHp(battle.Target), .1f);
    }

    [Theory]
    [InlineData(3003, 3, 1, 0, 100)]
    [InlineData(3003, 3, 3, 100, 190)]
    [InlineData(3003, 3, 5, 200, 280)]
    [InlineData(3007, 2, 1, 0, 40)]
    [InlineData(3007, 2, 3, 100, 110)]
    [InlineData(3007, 2, 5, 200, 180)]
    public void Shields_use_rank_base_plus_ap_on_the_first_application(
        int hero, int slot, int rank, float ap, float expected)
    {
        using var battle = new Battle(hero);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].Level = rank;
        battle.Set(battle.Caster, "magic_attack", ap);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 8), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(600);
        Assert.Equal(expected, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .05f);
    }

    [Theory]
    [InlineData(940, 995)]
    [InlineData(990, 1000)]
    public void Gragas_passive_heals_max_hp_fraction_and_clamps_overhealing(float before, float expected)
    {
        using var battle = new Battle(3005);
        battle.Set(battle.Caster, "max_hp", 1000);
        battle.Set(battle.Caster, "hp", before);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 2, default, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2000);
        Assert.Equal(expected, battle.Setup.GetActorHp(battle.Caster), .05f);
    }

    [Theory]
    [InlineData(1, 1200, 50)]
    [InlineData(2, 1400, 60)]
    [InlineData(3, 1600, 70)]
    public void Tibbers_birth_uses_original_cast_rank_and_preserves_owner_ap(int rank, float hp, float ad)
    {
        using var battle = new Battle(3003, 8);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[3].Level = rank;
        battle.Set(battle.Caster, "magic_attack", 123);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 4, new Vec3(0, 0, 3), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[3].Level = rank == 3 ? 1 : 3;
        battle.Setup.TickMilliseconds(1500);
        var registry = battle.Services.Resolve<MobaActorRegistry>();
        var tibbers = Assert.Single(registry.Entries.Where(p => p.Value.hasSummonMeta && p.Value.summonMeta.SummonId == 30030502)).Value;
        Assert.Equal(hp, tibbers.GetMobaAttrs().MaxHp);
        Assert.Equal(hp, tibbers.GetMobaAttrs().Hp);
        Assert.Equal(ad, tibbers.GetMobaAttrs().PhysicsAttack);
        Assert.Equal(123, tibbers.GetMobaAttrs().MagicAttack);
        Assert.Equal(hp, tibbers.resourceContainer.Value.Map[AbilityKit.Demo.Moba.Components.ResourceType.Hp].Current.ToSingle());
        AssertAuthoritativeProjectionAndImportBoundary(battle, tibbers.actorId.Value, hp, hp, importSupported: false);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[3].Level = 2;
        Assert.Equal(hp, tibbers.GetMobaAttrs().MaxHp);
        battle.Setup.TickMilliseconds(14000);
        Assert.Equal(0, battle.Services.Resolve<MobaSummonService>().GetAliveCount(battle.Caster, 30030502));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Jhin_child_shots_use_activation_rank_despite_different_input_rank_and_mid_mode_upgrade(int rank)
    {
        using var battle = new Battle(3001, 6);
        var actor = battle.Actor(battle.Caster);
        actor.skillLoadout.ActiveSkills[3].Level = rank;
        actor.skillLoadout.ActiveSkills[4].Level = 1;
        battle.Set(battle.Caster, "physics_attack", 100);
        battle.Set(battle.Caster, "magic_attack", 0);
        var hits = new List<DamageResult>();
        using var subscription = battle.Services.Resolve<IEventBus>().Subscribe(
            new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)),
            result => { if (result.TargetActorId == battle.Target) hits.Add(result); });
        var entry = battle.Skills.TryCastBySlot(battle.Caster, 4, default, Vec3.Forward);
        Assert.True(entry.Success, entry.FailReason);
        battle.Setup.TickMilliseconds(1500);
        actor.skillLoadout.ActiveSkills[3].Level = rank == 3 ? 1 : 3;
        var runtimes = battle.Services.Resolve<MobaSkillCastRuntimeService>();
        for (var shot = 0; shot < 4; shot++)
        {
            var hp = battle.Setup.GetActorHp(battle.Target);
            var cast = battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, battle.Target);
            Assert.True(cast.Success, cast.FailReason);
            var handle = cast.RuntimeHandle;
            Assert.True(runtimes.TryGet(handle, out var runtime));
            Assert.Equal(rank, runtime.SkillLevel);
            Assert.True(runtime.LevelParameters.TryGetValue(shot == 3 ? "finalDamage" : "damage", out var damage));
            Assert.Equal((shot == 3 ? 2 : 1) * (50 + 75 * (rank - 1)), damage);
            battle.Setup.TickMilliseconds(1600);
            Assert.Equal(shot + 1, hits.Count);
            var expected = (damage + (shot == 3 ? 50 : 25)) * (1 + 3 * (1 - hp / 10000));
            Assert.Equal(expected, hits[shot].Value, .1f);
            AssertAuthoritativeProjectionAndImportBoundary(battle, battle.Target, hits[shot].TargetHp, 10000);
        }
        battle.Setup.TickMilliseconds(2000);
        Assert.False(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out _, out _));
        actor.skillLoadout.ActiveSkills[3].CooldownEndTimeMs = 0;
        var next = battle.Skills.TryCastBySlot(battle.Caster, 4, default, Vec3.Forward);
        Assert.True(next.Success, next.FailReason);
        battle.Setup.TickMilliseconds(1500);
        Assert.True(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out var nextRank, out var nextRoot));
        Assert.Equal(rank == 3 ? 1 : 3, nextRank);
        Assert.Equal(next.RuntimeHandle.RootContextId, nextRoot);
        Assert.NotEqual(entry.RuntimeHandle.RootContextId, nextRoot);
        var lethal = new AttackInfo
        {
            AttackerActorId = battle.Target, TargetActorId = battle.Caster, DamageType = DamageType.True,
            CritType = CritType.None, ReasonKind = DamageReasonKind.Skill
        };
        lethal.BaseDamage.BaseValue = 50000;
        battle.Services.Resolve<DamagePipelineService>().Execute(lethal);
        battle.Setup.TickMilliseconds(100);
        Assert.False(SkillCastPreparationService.TryResolveLevelSource(battle.Services, actor, 30010501, 5, out _, out _));
    }

    // Exercise the actual authority projection and state baseline import into an
    // independent replica. The current state schema carries HP/MaxHP, pose and
    // team; it does not carry combat runtimes, summon AD or cast rank snapshots.
    private static void AssertAuthoritativeProjectionAndImportBoundary(Battle battle, int actorId, float hp, float maxHp,
        bool importSupported = true)
    {
        using var producer = new MobaActorProjectionProducer(battle.Services.Resolve<MobaActorRegistry>());
        var projection = producer.ExtractFull(actorId);
        Assert.Equal(hp, projection.Hp, .1f);
        Assert.Equal(maxHp, projection.HpMax, .1f);

        var context = new ActorContext();
        var replica = context.CreateEntity();
        replica.AddActorId(actorId);
        replica.AddTransform(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One));
        var attributes = new AttributeContext();
        attributes.SetBase(MobaAttributeIds.MAX_HP, 20);
        attributes.SetBase(MobaAttributeIds.HP, 7);
        replica.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MAX_HP), attributes);
        replica.AddResourceContainer(new ResourceContainer
        {
            Map = new() { [ResourceType.Hp] = new ResourceState { Current = Fixed64.FromInt32(7) } }
        }, true);
        var registry = new MobaActorRegistry();
        registry.Register(actorId, replica);
        var importer = new MobaLogicWorldStateImporter(registry, null!, null!);
        var yaw = MathF.Atan2(2f * (projection.RotW * projection.RotY + projection.RotX * projection.RotZ),
            1f - 2f * (projection.RotY * projection.RotY + projection.RotZ * projection.RotZ));
        var snapshot = new MobaActorStateImport(actorId, projection.PosX, projection.PosY, projection.PosZ,
            yaw, projection.Hp, projection.HpMax, projection.TeamId, projection.Kind, projection.Code, projection.OwnerNetId);
        var result = importer.ImportScopedFullSnapshot([snapshot], 1, [actorId]);
        if (!importSupported)
        {
            // Summons currently project as Kind=4 (summon), which this state
            // importer rejects. Record that boundary without inventing a new
            // summon reconstruction or snapshot protocol in a numeric change.
            Assert.Equal(4, projection.Kind);
            Assert.Equal(1, result.Failed);
            Assert.Equal(0, result.Updated);
            Assert.Equal(7, replica.GetMobaAttrs().Hp);
            Assert.Equal(20, replica.GetMobaAttrs().MaxHp);
            context.DestroyAllEntities();
            return;
        }
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Spawned);
        Assert.Equal(hp, replica.GetMobaAttrs().Hp, .1f);
        Assert.Equal(maxHp, replica.GetMobaAttrs().MaxHp, .1f);
        Assert.Equal(hp, replica.resourceContainer.Value.Map[ResourceType.Hp].Current.ToSingle(), .1f);
        Assert.Equal(new Vec3(projection.PosX, projection.PosY, projection.PosZ), replica.transform.Value.Position);
        context.DestroyAllEntities();
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper _bootstrapper;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        public int Target { get; }
        public IWorldResolver Services => _bootstrapper.RuntimeServices!;
        public SkillCastCoordinator Skills => Services.Resolve<SkillCastCoordinator>();

        public Battle(int hero, float targetZ = 3)
        {
            var config = BattleStartConfig.CreateDefault();
            foreach (var player in config.Players) player.EnableBrainOnSpawn = false;
            _bootstrapper = new ConsoleBattleBootstrapper(config);
            _bootstrapper.Initialize();
            _bootstrapper.Start();
            for (var i = 0; i < 8 && _bootstrapper.Context.EcsWorld == null; i++) _bootstrapper.Tick();
            _bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) _bootstrapper.Tick();
            Setup = new LiveSimSetupActionExecutor(_bootstrapper);
            Caster = Spawn("caster", hero, 1, 0);
            Target = Spawn("target", 3007, 2, targetZ);
            Set(Target, "max_hp", 10000);
            Set(Target, "hp", 10000);
            Set(Target, "physics_defense", 0);
            Set(Target, "magic_defense", 0);
            Set(Caster, "max_mana", 1000);
            Set(Caster, "mana", 1000);
            Setup.Tick(2);
        }

        private int Spawn(string alias, int hero, int team, float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, playerId = "numeric_" + alias,
                heroId = hero, attributeTemplateId = hero, teamId = team,
                position = new MobaAcceptanceVector3Expectation { z = z }
            });
            Assert.True(Setup.TryGetActorId(alias, out var actor));
            Setup.DisableActorBrain(actor);
            return actor;
        }

        public ActorEntity Actor(int id)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id, out var actor));
            return actor;
        }

        public void Set(int actor, string name, float value) => Setup.SetScenarioActorAttribute(actor, name, value);
        public void Dispose() => _bootstrapper.Dispose();
    }
}

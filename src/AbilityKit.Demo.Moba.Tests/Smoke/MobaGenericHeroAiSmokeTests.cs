using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaGenericHeroAiSmokeTests
{
    private const int DajiFirstSkillId = 10020101;

    [Fact]
    public void Default_enemy_slots_keep_their_brain_profile_but_do_not_auto_start_it()
    {
        var battleTemplate = BattleStartConfig.CreateDefault();
        var enemies = battleTemplate.Players.Where(player => player.PlayerId.StartsWith("ai_", StringComparison.Ordinal));

        Assert.All(enemies, player =>
        {
            Assert.Equal(100, player.BrainId);
            Assert.False(player.EnableBrainOnSpawn);
        });
    }

    [Fact]
    public void Configured_brain_can_start_disabled_and_be_enabled_later()
    {
        var battleTemplate = BattleStartConfig.CreateDefault();
        foreach (var player in battleTemplate.Players)
        {
            player.BrainId = player.PlayerId == "ai_2" ? 100 : 0;
            player.EnableBrainOnSpawn = false;
        }

        var bootstrapper = new ConsoleBattleBootstrapper(battleTemplate);
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
            bootstrapper.SetupBattle();
            for (var i = 0; i < 3; i++) bootstrapper.Tick();

            var services = bootstrapper.RuntimeServices;
            Assert.NotNull(services);
            Assert.True(services.TryResolve<MobaPlayerActorMapService>(out var playerActors) && playerActors != null);
            Assert.True(services.TryResolve<MobaActorLookupService>(out var actors) && actors != null);
            Assert.True(services.TryResolve<MobaBrainService>(out var brains) && brains != null);
            Assert.True(playerActors.TryGetActorId(new PlayerId("ai_2"), out var actorId));
            Assert.True(actors.TryGetActorEntity(actorId, out var actor));

            Assert.False(actor.hasActorBrain);
            Assert.True(brains.ActivateBrain(actor, 100, MobaBrainSourceKinds.BattleTemplate, sourceId: 4));
            Assert.True(actor.hasActorBrain);
            Assert.Equal(100, actor.actorBrain.BrainId);
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }

    [Fact]
    public void Generic_hero_ai_chases_casts_respects_cooldown_and_can_be_disabled()
    {
        var battleTemplate = BattleStartConfig.CreateDefault();
        foreach (var player in battleTemplate.Players)
        {
            if (player.PlayerId == "player_2") player.BrainId = 100;
            if (player.PlayerId == "ai_2") player.BrainId = 0;
        }

        var bootstrapper = new ConsoleBattleBootstrapper(battleTemplate);
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrapper.Tick();

            var services = bootstrapper.RuntimeServices;
            Assert.NotNull(services);
            Assert.True(services.TryResolve<MobaActorRegistry>(out var registry) && registry != null);
            Assert.True(services.TryResolve<MobaBrainService>(out var brains) && brains != null);
            Assert.True(services.TryResolve<MobaConfigDatabase>(out var config) && config != null);
            Assert.True(config.TryGetCharacter(1002, out var dajiConfig) && dajiConfig != null);

            global::ActorEntity caster = null;
            global::ActorEntity enemy = null;
            foreach (var entry in registry.Entries)
            {
                var actor = entry.Value;
                if (actor == null || !actor.hasTeam || !actor.hasSkillLoadout) continue;
                var skills = actor.skillLoadout.ActiveSkills;
                if (skills == null || skills.Length == 0 || skills[0] == null
                    || skills[0].SkillId != DajiFirstSkillId)
                    continue;

                if (caster == null)
                {
                    caster = actor;
                    continue;
                }

                if (actor.team.Value != caster.team.Value)
                {
                    enemy = actor;
                    break;
                }
            }

            Assert.NotNull(caster);
            Assert.NotNull(enemy);
            Assert.True(caster.hasActorBrain);
            Assert.Equal(100, caster.actorBrain.BrainId);
            Assert.Equal(MobaBrainSourceKinds.BattleTemplate, caster.actorBrain.SourceKind);
            Assert.False(enemy.hasActorBrain);

            foreach (var entry in registry.Entries)
            {
                var other = entry.Value;
                if (other == null || ReferenceEquals(other, enemy) || !other.hasTeam
                    || other.team.Value == caster.team.Value || !other.hasTransform) continue;
                if (other.hasActorBrain) brains.DeactivateBrain(other);
                var position = other.transform.Value.Position;
                other.ReplaceTransform(new Transform3(
                    new Vec3(position.X + 1000f, position.Y, position.Z + 1000f),
                    other.transform.Value.Rotation,
                    other.transform.Value.Scale));
            }

            var casterStart = caster.transform.Value;
            var farEnemyPosition = new Vec3(casterStart.Position.X, casterStart.Position.Y, casterStart.Position.Z + 30f);
            enemy.ReplaceTransform(new Transform3(farEnemyPosition, enemy.transform.Value.Rotation, enemy.transform.Value.Scale));

            Assert.True(caster.actorBrain.BehaviorInstanceId > 0);
            Assert.True(brains.TryGetBehavior(caster.actorBrain.BehaviorInstanceId, out var behavior));
            Assert.Equal("MobaBTree", behavior.Decision.DecisionType);

            for (var i = 0; i < 30; i++) bootstrapper.Tick();
            var chasePosition = caster.transform.Value.Position;
            Assert.True((chasePosition - casterStart.Position).Magnitude > 0.25f,
                $"AI did not chase through the locomotion input path. state={behavior.Decision.CurrentState} " +
                $"move=({caster.moveInput.Dx},{caster.moveInput.Dz}) movement={behavior.Output.Movement.HasValue}");
            Assert.True(caster.hasMoveInput);
            var btreeDecision = Assert.IsType<MobaBTreeDecision>(behavior.Decision);
            Assert.True(btreeDecision.Blackboard.GetValue<bool>(MobaBTreeKeys.SkillValid));
            Assert.Equal(DajiFirstSkillId,
                btreeDecision.Blackboard.GetValue<int>(MobaBTreeKeys.SkillId));
            Assert.True(btreeDecision.Blackboard.GetValue<int>(MobaBTreeKeys.TargetId) > 0);

            var nearEnemyPosition = new Vec3(chasePosition.X, chasePosition.Y, chasePosition.Z + 5f);
            enemy.ReplaceTransform(new Transform3(nearEnemyPosition, enemy.transform.Value.Rotation, enemy.transform.Value.Scale));
            var firstSkill = caster.skillLoadout.ActiveSkills[0];
            firstSkill.CooldownDurationMs = 0;
            firstSkill.CooldownEndTimeMs = 0L;

            for (var i = 0; i < 10 && firstSkill.CooldownEndTimeMs <= 0L; i++) bootstrapper.Tick();
            Assert.True(firstSkill.CooldownEndTimeMs > 0L, "AI did not cast the first ready skill in range.");
            var firstCooldownEnd = firstSkill.CooldownEndTimeMs;

            for (var i = 0; i < 30; i++) bootstrapper.Tick();
            Assert.Equal(firstCooldownEnd, firstSkill.CooldownEndTimeMs);

            Assert.True(brains.DeactivateBrain(caster));
            Assert.False(caster.hasActorBrain);
            Assert.Equal(0f, caster.moveInput.Dx, 3);
            Assert.Equal(0f, caster.moveInput.Dz, 3);
            var stoppedPosition = caster.transform.Value.Position;
            for (var i = 0; i < 10; i++) bootstrapper.Tick();
            Assert.True((caster.transform.Value.Position - stoppedPosition).Magnitude < 0.01f,
                "Actor kept moving after AI was disabled.");
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }
}

using System.Linq;
using AbilityKit.Ability.Behavior;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Attributes;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSummonMovementHandoffTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_cleanup_detaches_only_its_source_when_brain_or_actor_is_removed(bool despawn)
    {
        var start = BattleStartConfig.CreateDefault();
        foreach (var player in start.Players) player.EnableBrainOnSpawn = false;
        using var battle = new ConsoleBattleBootstrapper(start);
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();

        var services = battle.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var owner = actors.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform).Value;
        foreach (var pair in actors.Entries)
            if (pair.Value.hasTransform) Place(pair.Value, new Vec3(40f + pair.Key, 0f, 40f));
        Place(owner, new Vec3(4f, 0f, -10f));
        var spawn = new Vec3(-10f, 0f, -10f);
        Assert.True(services.Resolve<MobaSummonService>().TrySummon(owner.actorId.Value, 30030502,
            in spawn, Vec3.Right, default, out var id));
        for (var i = 0; i < 8; i++) battle.Tick();
        Assert.True(actors.TryGet(id, out var summon));
        var pipeline = summon.motion.Pipeline;
        var path = Assert.Single(pipeline.CaptureSourceOrder().OfType<PathFollowerMotionSource>());
        Assert.True(path.IsActive);
        var otherSources = pipeline.CaptureSourceOrder().Where(source => !ReferenceEquals(source, path)).ToArray();
        var stoppedAt = summon.transform.Value.Position;

        if (despawn)
        {
            Assert.True(services.Resolve<MobaSummonService>().TryDespawn(id, SummonDespawnReason.ManualRemove));
            for (var i = 0; i < 8 && actors.TryGet(id, out _); i++) battle.Tick();
            Assert.False(actors.TryGet(id, out _));
        }
        else
        {
            Assert.True(services.Resolve<MobaBrainService>().DeactivateBrain(summon));
        }
        battle.Tick();

        Assert.False(pipeline.ContainsSource(path));
        if (!despawn)
        {
            Assert.Same(pipeline, summon.motion.Pipeline);
            Assert.Equal(otherSources, pipeline.CaptureSourceOrder());
            Assert.Equal(stoppedAt, summon.transform.Value.Position);
        }
    }

    [Theory]
    [InlineData(.4f)]
    [InlineData(1.9f)]
    public void Moving_owner_does_not_pull_tibbers_out_of_combat_or_reverse_its_clear_follow_path(float enemyDistance)
    {
        var start = BattleStartConfig.CreateDefault();
        foreach (var player in start.Players) player.EnableBrainOnSpawn = false;
        using var battle = new ConsoleBattleBootstrapper(start);
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var owner = registry.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform).Value;
        var enemy = registry.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform &&
            p.Value.team.Value != owner.team.Value).Value;
        foreach (var pair in registry.Entries)
            if (pair.Value.hasTransform) Place(pair.Value, new Vec3(40f + pair.Key, 0f, 40f));
        Place(owner, new Vec3(4f, 0f, -10f));
        var spawn = new Vec3(-10f, 0f, -10f);
        Assert.True(services.Resolve<MobaSummonService>().TrySummon(owner.actorId.Value, 30030502,
            in spawn, Vec3.Right, default, out var id));
        Assert.True(registry.TryGet(id, out var summon));
        battle.Tick();
        Assert.True(services.Resolve<MobaBrainService>().TryGetBehavior(summon.actorBrain.BehaviorInstanceId, out var behavior));
        var trace = new System.Text.StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            // Continuous forward travel with a shallow bend, crossing navigation cells/repath thresholds.
            Place(owner, new Vec3(4f + i * .08f, 0f, -10f + i * .01f));
            var before = summon.transform.Value.Position;
            battle.Tick();
            var after = summon.transform.Value.Position;
            var intent = MobaBrainIntentReader.Read(behavior);
            trace.AppendLine($"{i}: {before} -> {after}, forward={summon.transform.Value.Forward}, target={intent.MoveTarget}, kind={intent.MovementKind}");
            Assert.True(after.X >= before.X - .001f, trace.ToString());
            if ((after - before).Magnitude > .001f)
                Assert.True(summon.transform.Value.Forward.X > .8f, trace.ToString());
        }

        var combatPosition = summon.transform.Value.Position;
        var enemyPosition = combatPosition + new Vec3(0f, 0f, enemyDistance);
        Place(enemy, enemyPosition);
        var enemyAttrs = new MobaAttrs(enemy);
        enemyAttrs.SetBase(BattleAttributeType.MAX_HP, 10000f);
        enemyAttrs.SetBase(BattleAttributeType.PER_SECOND_BLOOD_R, 0f);
        enemyAttrs.Hp = 10000f;
        var hpBefore = new MobaAttrs(enemy).Hp;
        for (var i = 0; i < 70; i++)
        {
            Place(owner, new Vec3(20f + i * .08f, 0f, -10f));
            battle.Tick();
            var intent = MobaBrainIntentReader.Read(behavior);
            Assert.Equal(MobaActorMovementIntentKind.Hold, intent.MovementKind);
            Assert.True((summon.transform.Value.Position - combatPosition).Magnitude < .05f,
                $"Combat was pulled toward the moving owner at {i}: {summon.transform.Value.Position}");
        }
        Assert.True(new MobaAttrs(enemy).Hp < hpBefore, "Combat must cause actual attack damage.");
        Place(enemy, combatPosition + new Vec3(0f, 0f, 10f));
        var chased = false;
        trace.Clear();
        for (var i = 0; i < 45; i++)
        {
            battle.Tick();
            var chase = MobaBrainIntentReader.Read(behavior);
            trace.AppendLine($"{i}: pos={summon.transform.Value.Position}, kind={chase.MovementKind}, target={chase.MoveTarget}, enemy={enemy.transform.Value.Position}, hp={new MobaAttrs(enemy).Hp}, state={behavior.Decision.CurrentState}");
            if (chase.MovementKind == MobaActorMovementIntentKind.TargetPosition)
            {
                Assert.Equal(enemy.transform.Value.Position, chase.MoveTarget);
                chased = true;
            }
        }
        Assert.True(chased && summon.transform.Value.Position.Z > combatPosition.Z + .1f, trace.ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30030502)]
    public void Follow_path_yields_to_combat_and_resumes_only_after_enemy_leaves(int summonId)
    {
        var start = BattleStartConfig.CreateDefault();
        foreach (var player in start.Players) player.EnableBrainOnSpawn = false;
        using var battle = new ConsoleBattleBootstrapper(start);
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();

        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        var brains = services.Resolve<MobaBrainService>();
        var owner = registry.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform).Value;
        var enemy = registry.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform &&
            p.Value.team.Value != owner.team.Value).Value;
        foreach (var pair in registry.Entries)
            if (pair.Value.hasTransform) Place(pair.Value, new Vec3(40f + pair.Key, 0f, 40f));
        Place(owner, new Vec3(4f, 0f, -10f));
        var spawn = new Vec3(-10f, 0f, -10f);
        Assert.True(summons.TrySummon(owner.actorId.Value, summonId, in spawn, Vec3.Forward,
            default, out var id));
        Assert.True(registry.TryGet(id, out var summon));
        for (var i = 0; i < 8; i++) battle.Tick();
        Assert.True(brains.TryGetBehavior(summon.actorBrain.BehaviorInstanceId, out var behavior));
        var following = MobaBrainIntentReader.Read(behavior);
        Assert.Equal(MobaActorMovementIntentKind.TargetPosition, following.MovementKind);
        Assert.Equal(owner.transform.Value.Position, following.MoveTarget);
        Assert.True((summon.transform.Value.Position - spawn).Magnitude > .1f);
        for (var i = 0; i < 35; i++)
        {
            var before = summon.transform.Value.Position;
            battle.Tick();
            Assert.True(summon.transform.Value.Position.X >= before.X - .001f,
                $"Follow path folded backwards during repath: {before} -> {summon.transform.Value.Position}");
        }

        // The enemy appears across the old follow direction. Combat/hold must cancel the old path,
        // including the cooldown ticks between casts, even if the owner keeps moving away.
        var combatPosition = summon.transform.Value.Position;
        Place(enemy, combatPosition + new Vec3(0f, 0f, .4f));
        for (var i = 0; i < 40; i++)
        {
            Place(owner, new Vec3(20f + i * .05f, 0f, -10f));
            battle.Tick();
            var intent = MobaBrainIntentReader.Read(behavior);
            Assert.Equal(MobaActorMovementIntentKind.Hold, intent.MovementKind);
            Assert.True((summon.transform.Value.Position - combatPosition).Magnitude < .05f,
                $"Old follow path pulled actor during combat at tick {i}: {summon.transform.Value.Position}");
        }
        if (summonId == 30030502)
            Assert.True(summon.transform.Value.Forward.Z > .99f,
                $"The formal target-facing attack must turn to the enemy: {summon.transform.Value.Forward}");

        Place(enemy, new Vec3(40f, 0f, 40f));
        for (var i = 0; i < 8; i++) battle.Tick();
        Assert.Equal(MobaActorMovementIntentKind.TargetPosition, MobaBrainIntentReader.Read(behavior).MovementKind);
        Assert.True((summon.transform.Value.Position - combatPosition).Magnitude > .1f);

        // An inactive controller cannot leave its last navigation source driving the actor.
        var stoppedAt = summon.transform.Value.Position;
        var brain = summon.actorBrain;
        behavior.Interrupt("movement handoff regression");
        summon.ReplaceActorBrain(0, brain.OwnerActorId, brain.SourceKind, brain.SourceId, brain.BehaviorInstanceId);
        battle.Tick();
        Assert.True((summon.transform.Value.Position - stoppedAt).Magnitude < .001f,
            "A stopped brain left its previous follow path active.");
    }

    [Fact]
    public void Warden_keeps_a_valid_target_through_boundary_and_distance_order_changes()
    {
        var start = BattleStartConfig.CreateDefault();
        foreach (var player in start.Players) player.EnableBrainOnSpawn = false;
        using var battle = new ConsoleBattleBootstrapper(start);
        battle.Initialize();
        battle.Start();
        for (var tick = 0; tick < 8 && battle.Context.EcsWorld == null; tick++) battle.Tick();
        battle.SetupBattle();
        for (var tick = 0; tick < 10; tick++) battle.Tick();

        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var owner = registry.Entries.First(pair => pair.Value.hasTeam && pair.Value.hasTransform).Value;
        var enemies = registry.Entries.Where(pair => pair.Value.hasTeam && pair.Value.hasTransform &&
            pair.Value.team.Value != owner.team.Value).Select(pair => pair.Value).Take(2).ToArray();
        foreach (var pair in registry.Entries)
            if (pair.Value.hasTransform) Place(pair.Value, new Vec3(40f + pair.Key, 0f, 40f));
        var spawn = new Vec3(-10f, 0f, -10f);
        Place(owner, new Vec3(4f, 0f, -10f));
        Assert.True(services.Resolve<MobaSummonService>().TrySummon(owner.actorId.Value, 30030502,
            in spawn, Vec3.Right, default, out var id));
        Assert.True(registry.TryGet(id, out var summon));
        battle.Tick();
        Assert.True(services.Resolve<MobaBrainService>().TryGetBehavior(summon.actorBrain.BehaviorInstanceId, out var behavior));

        void AssertMovingTo(global::ActorEntity target)
        {
            var intent = MobaBrainIntentReader.Read(behavior);
            Assert.Equal(MobaActorMovementIntentKind.TargetPosition, intent.MovementKind);
            Assert.Equal(target.transform.Value.Position, intent.MoveTarget);
        }

        Place(enemies[0], summon.transform.Value.Position - Vec3.Right * 12.1f);
        battle.Tick();
        AssertMovingTo(owner);

        for (var tick = 0; tick < 20; tick++)
        {
            var before = summon.transform.Value;
            Place(enemies[0], before.Position - Vec3.Right * (tick % 2 == 0 ? 11.9f : 12.1f));
            battle.Tick();
            AssertMovingTo(enemies[0]);
            if (tick > 0) Assert.True(Vec3.Dot(before.Forward, summon.transform.Value.Forward) > 0f);
        }

        for (var tick = 0; tick < 20; tick++)
        {
            var before = summon.transform.Value;
            Place(enemies[0], before.Position - Vec3.Right * (tick % 2 == 0 ? 4.2f : 3.8f));
            Place(enemies[1], before.Position + Vec3.Right * (tick % 2 == 0 ? 3.8f : 4.2f));
            battle.Tick();
            AssertMovingTo(enemies[0]);
            Assert.True(Vec3.Dot(before.Forward, summon.transform.Value.Forward) > 0f);
        }

        var snapshots = Assert.IsAssignableFrom<IBehaviorSnapshotValidator>(behavior.Decision);
        var retainedFirst = snapshots.CaptureSnapshot();
        Place(enemies[0], summon.transform.Value.Position - Vec3.Right * 14.1f);
        battle.Tick();
        AssertMovingTo(enemies[1]);

        snapshots.ValidateSnapshot(retainedFirst);
        snapshots.RestoreSnapshot(retainedFirst);
        Place(enemies[0], summon.transform.Value.Position - Vec3.Right * 12.1f);
        Place(enemies[1], summon.transform.Value.Position + Vec3.Right * 4f);
        battle.Tick();
        AssertMovingTo(enemies[0]);

        new MobaAttrs(enemies[0]).Hp = 0;
        battle.Tick();
        AssertMovingTo(enemies[1]);
        Place(enemies[0], new Vec3(60f, 0f, 60f));
        Place(enemies[1], summon.transform.Value.Position + Vec3.Right * 14.1f);
        battle.Tick();
        AssertMovingTo(owner);
    }

    private static void Place(global::ActorEntity actor, Vec3 position) => actor.ReplaceTransform(
        new Transform3(position, actor.transform.Value.Rotation, actor.transform.Value.Scale));
}

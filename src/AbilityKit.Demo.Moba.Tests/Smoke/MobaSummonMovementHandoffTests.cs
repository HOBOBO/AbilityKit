using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSummonMovementHandoffTests
{
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

    private static void Place(global::ActorEntity actor, Vec3 position) => actor.ReplaceTransform(
        new Transform3(position, actor.transform.Value.Rotation, actor.transform.Value.Scale));
}

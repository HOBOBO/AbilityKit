using System;
using AbilityKit.Ability.Host.Builder;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Game.Battle;
using AbilityKit.Demo.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Runtime;

public sealed class ShooterWorldModuleTests
{
    [Fact]
    public void ConfigureRegistersRuntimePortWithSveltoEntityManager()
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();

        Assert.True(container.TryResolve<IShooterEntityManager>(out var entities));
        Assert.True(container.TryResolve<ShooterBattleState>(out var state));
        Assert.True(container.TryResolve<IShooterBattleRules>(out var rules));
        Assert.True(container.TryResolve<IShooterBattleSimulation>(out var simulation));
        Assert.True(container.TryResolve<IShooterSveltoWorld>(out var shooterSveltoWorld));
        Assert.True(container.TryResolve<IShooterBattleRuntimePort>(out var runtime));
        Assert.True(container.TryResolve<ISveltoWorldContext>(out var svelto));
        Assert.IsType<ShooterBattleRules>(rules);
        Assert.IsType<ShooterBattleSimulation>(simulation);
        Assert.IsType<ShooterSveltoWorld>(shooterSveltoWorld);
        Assert.IsType<ShooterEntityManager>(entities);
        Assert.Same(svelto, shooterSveltoWorld.Context);
        Assert.Same(entities, state.Entities);

        var start = new ShooterStartGamePayload(
            "world-module",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(entities.HasPlayer(1));
        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(1, player.PlayerId);
        Assert.Equal(1, svelto.EntitiesDB.Count<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players));
        Assert.Same(entities, container.Resolve<IShooterEntityManager>());
        Assert.Same(state, container.Resolve<ShooterBattleState>());
        Assert.Same(shooterSveltoWorld, container.Resolve<IShooterSveltoWorld>());
    }

    [Fact]
    public void ConfigureKeepsExplicitBattleRulesOverride()
    {
        var customRules = new ShooterBattleRules(
            playerSpeed: 9f,
            bulletSpeed: 21f,
            bulletLifeFrames: 7,
            hitRadius: 1.5f,
            hitDamage: 3);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(customRules)
            .AddModule(new ShooterWorldModule())
            .Build();

        Assert.Same(customRules, container.Resolve<IShooterBattleRules>());
    }

    [Fact]
    public void RuntimeUsesInjectedBattleRulesForMovementAndProjectile()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 30f,
            bulletSpeed: 60f,
            bulletLifeFrames: 3,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "custom-rules",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) }));
        Assert.True(runtime.Tick(0.5f));

        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(15f, player.X);
        Assert.True(entities.TryGetProjectile(1, out var projectile));
        Assert.Equal(60f, projectile.VelocityX);
        Assert.Equal(2, projectile.RemainingFrames);

        var snapshot = runtime.GetSnapshot();
        var fireEvent = Assert.Single(snapshot.Events);
        Assert.Equal((int)ShooterEventType.Fire, fireEvent.EventType);
        Assert.Equal(1, fireEvent.SourcePlayerId);
        Assert.Equal(0, fireEvent.TargetPlayerId);
        Assert.Equal(projectile.BulletId, fireEvent.BulletId);
    }

    [Fact]
    public void RuntimeSpawnsPrimaryProjectileForEveryConsecutiveFireCommand()
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "primary-fire-rapid",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        const int fireCommandCount = 6;
        for (var frame = 0; frame < fireCommandCount; frame++)
        {
            Assert.Equal(1, runtime.SubmitInput(
                runtime.CurrentFrame,
                new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) }));
            Assert.True(runtime.Tick(0f));
            var fireEvent = Assert.Single(runtime.GetSnapshot().Events);
            Assert.Equal((int)ShooterEventType.Fire, fireEvent.EventType);
        }

        Assert.Equal(fireCommandCount, entities.ProjectileCount);
    }

    [Theory]
    [InlineData(ShooterPlayerAttackSlots.Spread, 3)]
    [InlineData(ShooterPlayerAttackSlots.Twin, 2)]
    public void RuntimeSpawnsSpecialProjectilesForEveryConsecutiveFireCommand(
        int attackSlot,
        int projectilesPerCommand)
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "special-fire-rapid",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        const int fireCommandCount = 4;
        for (var frame = 0; frame < fireCommandCount; frame++)
        {
            Assert.Equal(1, runtime.SubmitInput(
                runtime.CurrentFrame,
                new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, attackSlot) }));
            Assert.True(runtime.Tick(0f));
            var events = runtime.GetSnapshot().Events;
            Assert.Equal(projectilesPerCommand, events.Length);
            Assert.All(events, static evt => Assert.Equal((int)ShooterEventType.Fire, evt.EventType));
        }

        var expectedProjectileCount = fireCommandCount * projectilesPerCommand;
        Assert.Equal(expectedProjectileCount, entities.ProjectileCount);
    }

    [Fact]
    public void RuntimeSpawnsSpreadProjectilesForSecondaryAttackSlot()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 30f,
            bulletLifeFrames: 9,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "spread-projectiles",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Spread) }));
        Assert.True(runtime.Tick(0f));

        Assert.True(entities.TryGetProjectile(1, out var center));
        Assert.True(entities.TryGetProjectile(2, out var upper));
        Assert.True(entities.TryGetProjectile(3, out var lower));
        Assert.Equal(30f, center.VelocityX, 5);
        Assert.Equal(0f, center.VelocityY, 5);
        Assert.True(center.ExplosionRadius > 0f);
        Assert.Equal(rules.HitDamage, center.ExplosionDamage);
        Assert.True(upper.VelocityY > 0f);
        Assert.True(lower.VelocityY < 0f);
        Assert.Equal(center.ExplosionRadius, upper.ExplosionRadius);
        Assert.Equal(center.ExplosionRadius, lower.ExplosionRadius);
        Assert.Equal(rules.HitDamage, upper.ExplosionDamage);
        Assert.Equal(rules.HitDamage, lower.ExplosionDamage);
        Assert.Equal(3, runtime.GetSnapshot().Events.Length);
    }

    [Fact]
    public void RuntimeExplodesSpreadCenterProjectileOnEnemyHit()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 30f,
            bulletLifeFrames: 9,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var state = container.Resolve<ShooterBattleState>();
        var start = new ShooterStartGamePayload(
            "spread-explosion",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Spread) }));
        Assert.True(runtime.Tick(0f));

        entities.AddEnemy(
            100,
            new ShooterSveltoTransformComponent { X = 1.65f, Y = 0f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            101,
            new ShooterSveltoTransformComponent { X = 1.65f, Y = 1.1f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            102,
            new ShooterSveltoTransformComponent { X = 1.65f, Y = -1.1f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            103,
            new ShooterSveltoTransformComponent { X = 1.65f, Y = 3.0f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });

        Assert.True(runtime.Tick(1f / 30f));

        Assert.False(entities.TryGetProjectile(1, out _));
        AssertEnemyNotAlive(entities, 100);
        AssertEnemyNotAlive(entities, 101);
        AssertEnemyNotAlive(entities, 102);
        Assert.True(entities.TryGetEnemy(103, out _, out var outsideHealth));
        Assert.Equal(1, outsideHealth.Alive);
        Assert.Equal(3, state.DefeatedEnemies);
        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(3, player.Score);
    }

    [Fact]
    public void RuntimeSpawnsTwinProjectilesForThirdAttackSlot()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 30f,
            bulletLifeFrames: 9,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "twin-projectiles",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Twin) }));
        Assert.True(runtime.Tick(0f));

        Assert.True(entities.TryGetProjectile(1, out var left));
        Assert.True(entities.TryGetProjectile(2, out var right));
        Assert.Equal(left.VelocityX, right.VelocityX, 5);
        Assert.Equal(left.VelocityY, right.VelocityY, 5);
        Assert.True(left.Y < 0f);
        Assert.True(right.Y > 0f);
        Assert.Equal(2, runtime.GetSnapshot().Events.Length);
    }

    [Fact]
    public void RuntimeCanFireTowardBackSideAimDirection()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 30f,
            bulletLifeFrames: 9,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "back-side-aim",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 0f, -1f, true) }));
        Assert.True(runtime.Tick(0f));

        Assert.True(entities.TryGetProjectile(1, out var projectile));
        Assert.Equal(0f, projectile.VelocityX, 5);
        Assert.True(projectile.VelocityY < 0f);
        Assert.True(projectile.Y < 0f);
    }

    [Fact]
    public void RuntimeKeepsTwinProjectilesAliveAcrossMultiplePenetrations()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 30f,
            bulletLifeFrames: 9,
            hitRadius: 0.45f,
            hitDamage: 1);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "penetrating-twin-projectile",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Twin) }));
        Assert.True(runtime.Tick(0f));

        entities.AddEnemy(
            100,
            new ShooterSveltoTransformComponent { X = 1.65f, Y = -0.28f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            101,
            new ShooterSveltoTransformComponent { X = 3.7f, Y = -0.28f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            102,
            new ShooterSveltoTransformComponent { X = 5.75f, Y = -0.28f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            103,
            new ShooterSveltoTransformComponent { X = 7.8f, Y = -0.28f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        entities.AddEnemy(
            104,
            new ShooterSveltoTransformComponent { X = 9.85f, Y = -0.28f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });

        Assert.True(entities.TryGetProjectile(1, out var projectile));
        Assert.Equal(5, projectile.PenetrationRemaining);

        for (var enemyId = 100; enemyId <= 104; enemyId++)
        {
            Assert.True(runtime.Tick(1f / 30f));
            Assert.True(entities.TryGetProjectile(1, out projectile));
            Assert.Equal(104 - enemyId, projectile.PenetrationRemaining);
            AssertEnemyNotAlive(entities, enemyId);
        }
    }

    [Fact]
    public void RuntimeConstrainsCircularArenaMovementFireAndWaveSpawns()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 20f,
            bulletSpeed: 30f,
            bulletLifeFrames: 3,
            hitRadius: 0.45f,
            hitDamage: 1);
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 60,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 4,
            new[]
            {
                new ShooterSveltoGameplayWaveConfig(
                    waveId: 1,
                    startFrame: 1,
                    spawnFrameInterval: 1,
                    enemyCount: 1,
                    enemyHp: 2,
                    spawnRadius: 10f)
            });
        var arena = ShooterArenaGameplayOptions.CreateCircular(2f);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(arena)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "circular-arena",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 5f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(2f, player.X, 5);
        Assert.Equal(0f, player.Y, 5);

        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) }));
        Assert.True(runtime.Tick(1f));

        Assert.True(entities.TryGetPlayer(1, out player));
        Assert.Equal(2f, player.X, 5);
        Assert.Equal(0f, player.Y, 5);
        Assert.False(entities.TryGetProjectile(1, out _));
        Assert.Equal(1, entities.EnemyCount);

        foreach (var enemyId in entities.EnemyIds)
        {
            Assert.True(entities.TryGetEnemy(enemyId, out var transform, out _));
            Assert.True(transform.X * transform.X + transform.Y * transform.Y <= arena.RadiusSquared + 0.0001f);
        }
    }

    [Fact]
    public void RuntimeEmitsNamedHitEventWhenProjectileHitsPlayer()
    {
        var rules = new ShooterBattleRules(
            playerSpeed: 0f,
            bulletSpeed: 0f,
            bulletLifeFrames: 3,
            hitRadius: 0.45f,
            hitDamage: 2);
        var container = new WorldContainerBuilder()
            .RegisterInstance<IShooterBattleRules>(rules)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "hit-event",
            30,
            2,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 0.6f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) }));
        Assert.True(runtime.Tick(0f));

        Assert.False(entities.TryGetProjectile(1, out _));
        Assert.True(entities.TryGetPlayer(2, out var target));
        Assert.Equal(ShooterGameplay.DefaultPlayerHp - 2, target.Hp);

        var snapshot = runtime.GetSnapshot();
        Assert.Equal(2, snapshot.Events.Length);
        Assert.Equal((int)ShooterEventType.Fire, snapshot.Events[0].EventType);
        Assert.Equal((int)ShooterEventType.Hit, snapshot.Events[1].EventType);
        Assert.Equal(1, snapshot.Events[1].SourcePlayerId);
        Assert.Equal(2, snapshot.Events[1].TargetPlayerId);
        Assert.Equal(1, snapshot.Events[1].BulletId);
        Assert.Equal(2, snapshot.Events[1].Value);
    }

    [Fact]
    public void RuntimeExposesVictoryMatchResultAndEventWhenObjectiveCompletes()
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var statusProvider = Assert.IsAssignableFrom<IBattleRuntimeStatusProvider>(runtime);
        var state = container.Resolve<ShooterBattleState>();
        var start = new ShooterStartGamePayload(
            "victory-result",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        var readyStatus = statusProvider.BattleStatus;
        Assert.Equal(BattleRuntimeState.Ready, readyStatus.State);
        Assert.True(readyStatus.Has(
            BattleRuntimeCapability.GameStart |
            BattleRuntimeCapability.Input |
            BattleRuntimeCapability.Simulation |
            BattleRuntimeCapability.SnapshotOutput |
            BattleRuntimeCapability.SnapshotInput |
            BattleRuntimeCapability.StateReadModel |
            BattleRuntimeCapability.StateHash |
            BattleRuntimeCapability.BotControl));

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(ShooterBattleMatchState.Running, runtime.MatchState);
        Assert.Equal(BattleRuntimeState.Running, statusProvider.BattleStatus.State);
        Assert.True(statusProvider.BattleStatus.Meets(BattleReadinessRequirement.BattleLoop));

        state.VictoryTargetDefeats = 1;
        state.DefeatedEnemies = 1;

        Assert.False(runtime.Tick(1f / 30f));
        Assert.Equal(ShooterBattleMatchState.Victory, runtime.MatchState);
        Assert.False(runtime.IsStarted);
        Assert.Equal(BattleRuntimeState.Completed, statusProvider.BattleStatus.State);
        Assert.False(statusProvider.BattleStatus.Meets(BattleReadinessRequirement.BattleLoop));

        var result = runtime.MatchResult;
        Assert.True(result.IsFinal);
        Assert.True(result.IsVictory);
        Assert.Equal(ShooterBattleMatchState.Victory, result.MatchState);
        Assert.Equal(runtime.CurrentFrame, result.CompletedFrame);
        Assert.Equal(1, result.DefeatedEnemies);
        Assert.Equal(1, result.VictoryTargetDefeats);

        var snapshot = runtime.GetSnapshot();
        var matchEvent = Assert.Single(snapshot.Events);
        Assert.Equal((int)ShooterEventType.MatchVictory, matchEvent.EventType);
        Assert.Equal(1, matchEvent.Value);
    }

    [Fact]
    public void RuntimeExposesDefeatMatchResultAndEventWhenAllPlayersAreDefeated()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "defeat-result",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.TryGetPlayer(1, out var player));
        player.Hp = 0;
        player.Alive = false;
        runtime.SetPlayer(in player);

        Assert.False(runtime.Tick(1f / 30f));
        Assert.Equal(ShooterBattleMatchState.Defeat, runtime.MatchState);
        Assert.False(runtime.IsStarted);

        var result = runtime.MatchResult;
        Assert.True(result.IsFinal);
        Assert.False(result.IsVictory);
        Assert.Equal(ShooterBattleMatchState.Defeat, result.MatchState);
        Assert.Equal(runtime.CurrentFrame, result.CompletedFrame);

        var snapshot = runtime.GetSnapshot();
        var matchEvent = Assert.Single(snapshot.Events);
        Assert.Equal((int)ShooterEventType.MatchDefeat, matchEvent.EventType);
        Assert.Equal(0, matchEvent.Value);
    }

    [Fact]
    public void RuntimeExposesTimedMatchResultAndEventWhenCountdownEnds()
    {
        var timedFlow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 3,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 1,
            Array.Empty<ShooterSveltoGameplayWaveConfig>());
        var runtime = new ShooterBattleRuntimePort(
            ShooterEntityLimitOptions.Default,
            new ShooterEnemyWaveOptions(enabled: true, timedFlow));
        var start = new ShooterStartGamePayload(
            "timed-result",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(ShooterBattleMatchState.Running, runtime.MatchState);
        Assert.Equal(3, runtime.MatchResult.TimeLimitFrames);
        Assert.Equal(3, runtime.MatchResult.RemainingTimeFrames);

        Assert.True(runtime.Tick(1f / 30f));
        Assert.Equal(2, runtime.MatchResult.RemainingTimeFrames);
        var runningSnapshot = runtime.GetSnapshot();
        Assert.Equal((int)ShooterBattleMatchState.Running, runningSnapshot.MatchState);
        Assert.Equal(3, runningSnapshot.TimeLimitFrames);
        Assert.Equal(2, runningSnapshot.RemainingTimeFrames);

        Assert.True(runtime.Tick(1f / 30f));
        Assert.False(runtime.Tick(1f / 30f));
        Assert.Equal(ShooterBattleMatchState.Ended, runtime.MatchState);
        Assert.False(runtime.IsStarted);

        var result = runtime.MatchResult;
        Assert.True(result.IsFinal);
        Assert.False(result.IsVictory);
        Assert.True(result.IsTimeLimited);
        Assert.True(result.IsTimeExpired);
        Assert.Equal(ShooterBattleMatchState.Ended, result.MatchState);
        Assert.Equal(runtime.CurrentFrame, result.CompletedFrame);
        Assert.Equal(3, result.TimeLimitFrames);
        Assert.Equal(0, result.RemainingTimeFrames);

        var snapshot = runtime.GetSnapshot();
        Assert.Equal((int)ShooterBattleMatchState.Ended, snapshot.MatchState);
        Assert.Equal(3, snapshot.TimeLimitFrames);
        Assert.Equal(0, snapshot.RemainingTimeFrames);
        var matchEvent = Assert.Single(snapshot.Events);
        Assert.Equal((int)ShooterEventType.MatchEnded, matchEvent.EventType);
        Assert.Equal(0, matchEvent.Value);
    }

    [Fact]
    public void ShooterAutoModuleUsesShooterStartupDomainOnly()
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();

        Assert.True(container.TryResolve<IShooterEntityManager>(out _));
        Assert.False(container.TryResolve<AbilityKit.Demo.Moba.Tests.ForeignWorldService>(out _));
    }

    [Fact]
    public void RuntimeWritesSveltoEntitiesIncrementally()
    {
        var container = new WorldContainerBuilder()
            .RegisterInstance(ShooterEnemyWaveOptions.EnabledOption)
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var svelto = container.Resolve<ISveltoWorldContext>();
        var start = new ShooterStartGamePayload(
            "incremental-sync",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(svelto.EntitiesDB.TryQueryMappedEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players, out var players));
        Assert.Equal(0f, players.Entity(1u).X);

        Assert.Equal(1, runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) }));
        Assert.True(runtime.Tick(1f / 30f));

        Assert.Equal(1, svelto.EntitiesDB.Count<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players));
        Assert.Equal(1, svelto.EntitiesDB.Count<ShooterSveltoProjectileComponent>(ShooterSveltoGroups.Projectiles));
        players = svelto.EntitiesDB.QueryMappedEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players);
        var updatedPlayer = players.Entity(1u);
        Assert.True(updatedPlayer.X > 0f);
        Assert.True(svelto.EntitiesDB.Exists<ShooterSveltoProjectileComponent>(1u, ShooterSveltoGroups.Projectiles));

        var packedSnapshot = runtime.ExportPackedSnapshot(77ul, isFullSnapshot: true);
        Assert.True(packedSnapshot.EntityCount >= 3);
        var enemyLifecycleChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
        var enemyTransformChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Enemy);
        var enemyHealthChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(enemyLifecycleChunk);
        Assert.NotNull(enemyTransformChunk);
        Assert.NotNull(enemyHealthChunk);
        Assert.True(enemyLifecycleChunk.Value.Count > 0);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyTransformChunk.Value.Count);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyHealthChunk.Value.Count);

        var emptySnapshot = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            77ul,
            runtime.CurrentFrame,
            runtime.CurrentFrame,
            ShooterPackedSnapshotFlags.Full,
            0u,
            0,
            Array.Empty<byte>(),
            Array.Empty<ShooterPackedComponentChunk>());

        Assert.True(runtime.ImportPackedSnapshot(in emptySnapshot));
        Assert.Equal(0, svelto.EntitiesDB.Count<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players));
        Assert.Equal(0, svelto.EntitiesDB.Count<ShooterSveltoProjectileComponent>(ShooterSveltoGroups.Projectiles));
    }

    [Fact]
    public void RuntimeSpawnsWaveEnemiesAndEnemiesAttackPlayers()
    {
        var waves = new ShooterSveltoGameplayWaveConfig[8];
        for (var i = 0; i < waves.Length; i++)
        {
            waves[i] = new ShooterSveltoGameplayWaveConfig(i + 1, 0, 1, 1, 3, 1f);
        }

        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: waves.Length,
            waves,
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 1,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var svelto = container.Resolve<ISveltoWorldContext>();
        var start = new ShooterStartGamePayload(
            "wave-enemies",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));

        Assert.Equal(waves.Length, svelto.EntitiesDB.Count<ShooterSveltoHealthComponent>(ShooterSveltoGroups.GameplayTargets));
        Assert.True(AnyPlayerDamaged(entities));
        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(
            ShooterGameplay.DefaultPlayerHp - ShooterEnemyWaveOptions.DefaultMaxEnemyAttackersPerPlayer,
            player.Hp);

        var snapshot = runtime.GetSnapshot();
        Assert.Contains(snapshot.Events, static evt => evt.EventType == (int)ShooterEventType.Hit && evt.SourcePlayerId < 0);

        var packedSnapshot = runtime.ExportPackedSnapshot(77ul, isFullSnapshot: true);
        var enemyLifecycleChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
        var enemyTransformChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Enemy);
        var enemyHealthChunk = FindPackedChunk(packedSnapshot, ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(enemyLifecycleChunk);
        Assert.NotNull(enemyTransformChunk);
        Assert.NotNull(enemyHealthChunk);
        Assert.True(enemyLifecycleChunk.Value.Count > 0);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyTransformChunk.Value.Count);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyHealthChunk.Value.Count);
    }

    [Fact]
    public void RuntimeDoesNotApplyEnemyDamageOutsideAttackRange()
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 1,
            new[] { new ShooterSveltoGameplayWaveConfig(1, 0, 1, 1, 3, 4f) },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 1,
            enemyAttackDamage: 5,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "enemy-attack-range",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));

        Assert.Equal(1, entities.EnemyCount);
        Assert.True(entities.TryGetPlayer(1, out var player));
        Assert.Equal(ShooterGameplay.DefaultPlayerHp, player.Hp);
        Assert.DoesNotContain(runtime.GetSnapshot().Events, static evt =>
            evt.EventType == (int)ShooterEventType.Hit && evt.SourcePlayerId < 0);
    }

    [Fact]
    public void RuntimeMovesWaveEnemiesTowardNearestLivePlayer()
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 1,
            new[]
            {
                new ShooterSveltoGameplayWaveConfig(
                    waveId: 1,
                    startFrame: 1,
                    spawnFrameInterval: 1,
                    enemyCount: 1,
                    enemyHp: 3,
                    spawnRadius: 4f)
            },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 120,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(ShooterArenaGameplayOptions.CreateCircular(8f))
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "wave-enemy-movement",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));
        Assert.Equal(1, entities.EnemyCount);

        var enemyId = 0;
        foreach (var candidate in entities.EnemyIds)
        {
            enemyId = candidate;
            break;
        }

        Assert.True(entities.TryGetEnemy(enemyId, out var before, out _));
        var beforeDistanceSquared = before.X * before.X + before.Y * before.Y;

        Assert.True(runtime.Tick(1f / 30f));

        Assert.True(entities.TryGetEnemy(enemyId, out var after, out _));
        var afterDistanceSquared = after.X * after.X + after.Y * after.Y;
        Assert.True(afterDistanceSquared < beforeDistanceSquared);
        Assert.NotEqual(before.X, after.X);
        Assert.NotEqual(before.Y, after.Y);
    }

    [Fact]
    public void RuntimeMovesFarSparseWaveEnemiesTowardLivePlayer()
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 1,
            new[]
            {
                new ShooterSveltoGameplayWaveConfig(
                    waveId: 1,
                    startFrame: 1,
                    spawnFrameInterval: 1,
                    enemyCount: 1,
                    enemyHp: 3,
                    spawnRadius: 1000f)
            },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 120,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(ShooterArenaGameplayOptions.CreateCircular(2000f))
            .AddModule(new ShooterWorldModule())
            .Build();

        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "far-sparse-wave-enemy-movement",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));
        Assert.Equal(1, entities.EnemyCount);

        var enemyId = 0;
        foreach (var candidate in entities.EnemyIds)
        {
            enemyId = candidate;
            break;
        }

        Assert.True(entities.TryGetEnemy(enemyId, out var before, out _));
        var beforeDistanceSquared = before.X * before.X + before.Y * before.Y;

        Assert.True(runtime.Tick(1f));

        Assert.True(entities.TryGetEnemy(enemyId, out var after, out _));
        var afterDistanceSquared = after.X * after.X + after.Y * after.Y;
        Assert.True(afterDistanceSquared < beforeDistanceSquared);
    }

    [Fact]
    public void RuntimeMovesEnemyTowardNearestOfTwoPlayers()
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 1,
            new[] { new ShooterSveltoGameplayWaveConfig(1, 100, 1, 1, 3, 4f) },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 120,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(new ShooterRvoOptions(ShooterRvoExecutionMode.Disabled))
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "two-player-nearest-target",
            30,
            1,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -10f, 0f),
                new ShooterStartPlayer(2, "P2", 10f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));
        var transform = new ShooterSveltoTransformComponent { X = 8f, Y = 0f, DirectionX = 1f };
        var health = new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 };
        entities.AddEnemy(9001, in transform, in health);

        Assert.True(runtime.Tick(1f / 30f));
        Assert.True(entities.TryGetEnemy(9001, out var moved, out _));
        Assert.True(moved.X > transform.X);
        Assert.True(MathF.Abs(10f - moved.X) < MathF.Abs(10f - transform.X));
    }

    [Theory]
    [InlineData(ShooterRvoExecutionMode.Managed, true)]
    [InlineData(ShooterRvoExecutionMode.Disabled, false)]
    public void RuntimeRvoSeparatesExactlyOverlappingEnemiesAndSupportsDisabledFallback(
        ShooterRvoExecutionMode mode,
        bool expectSeparation)
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 2,
            new[] { new ShooterSveltoGameplayWaveConfig(1, 100, 1, 1, 3, 4f) },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 120,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var container = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(new ShooterRvoOptions(mode, maxAcceleration: 100f))
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "overlapping-enemy-rvo",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));
        var transform = new ShooterSveltoTransformComponent { X = 4f, Y = 0f, DirectionX = -1f };
        var health = new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 };
        entities.BeginStructuralChanges();
        try
        {
            entities.AddEnemy(9001, in transform, in health);
            entities.AddEnemy(9002, in transform, in health);
        }
        finally
        {
            entities.EndStructuralChanges();
        }

        Assert.True(runtime.Tick(1f / 30f));
        Assert.True(entities.TryGetEnemy(9001, out var first, out _));
        Assert.True(entities.TryGetEnemy(9002, out var second, out _));
        Assert.True(float.IsFinite(first.X) && float.IsFinite(first.Y));
        Assert.True(float.IsFinite(second.X) && float.IsFinite(second.Y));
        Assert.Equal(expectSeparation, first.X != second.X || first.Y != second.Y);

        if (!expectSeparation)
        {
            Assert.True(first.X < transform.X);
            Assert.True(second.X < transform.X);
            return;
        }

        for (var i = 0; i < 30; i++)
        {
            Assert.True(runtime.Tick(1f / 30f));
        }

        Assert.True(entities.TryGetEnemy(9001, out first, out _));
        Assert.True(entities.TryGetEnemy(9002, out second, out _));
        var centerX = (first.X + second.X) * 0.5f;
        Assert.True(
            centerX < transform.X,
            $"Expected the separated pair center to advance toward the player, but reached center X {centerX} from {transform.X}.");
    }

    [Fact]
    public void RuntimeRvoCrowdSimulationIsDeterministicAcrossIndependentWorlds()
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 180,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 4,
            new[] { new ShooterSveltoGameplayWaveConfig(1, 200, 1, 1, 3, 4f) },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 180,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var firstContainer = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(new ShooterRvoOptions(ShooterRvoExecutionMode.Managed))
            .AddModule(new ShooterWorldModule())
            .Build();
        var secondContainer = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(new ShooterRvoOptions(ShooterRvoExecutionMode.Managed))
            .AddModule(new ShooterWorldModule())
            .Build();
        var firstRuntime = firstContainer.Resolve<IShooterBattleRuntimePort>();
        var secondRuntime = secondContainer.Resolve<IShooterBattleRuntimePort>();
        var firstEntities = firstContainer.Resolve<IShooterEntityManager>();
        var secondEntities = secondContainer.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "deterministic-rvo-crowd",
            30,
            12345,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(firstRuntime.StartGame(in start));
        Assert.True(secondRuntime.StartGame(in start));
        Assert.True(firstRuntime.Tick(0f));
        Assert.True(secondRuntime.Tick(0f));
        var transform = new ShooterSveltoTransformComponent { X = 4f, Y = 0f, DirectionX = -1f };
        var health = new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 };
        for (var enemyId = 9001; enemyId <= 9004; enemyId++)
        {
            firstEntities.AddEnemy(enemyId, in transform, in health);
            secondEntities.AddEnemy(enemyId, in transform, in health);
        }

        for (var frame = 0; frame < 60; frame++)
        {
            Assert.True(firstRuntime.Tick(1f / 30f));
            Assert.True(secondRuntime.Tick(1f / 30f));
        }

        Assert.Equal(firstRuntime.ComputeStateHash(), secondRuntime.ComputeStateHash());
        Assert.True(firstEntities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
            ShooterSveltoGroups.GameplayTargets,
            out var firstNavigation));
        Assert.True(secondEntities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
            ShooterSveltoGroups.GameplayTargets,
            out var secondNavigation));
        for (var enemyId = 9001; enemyId <= 9004; enemyId++)
        {
            Assert.True(firstEntities.TryGetEnemy(enemyId, out var firstTransform, out _));
            Assert.True(secondEntities.TryGetEnemy(enemyId, out var secondTransform, out _));
            Assert.Equal(firstTransform.X, secondTransform.X);
            Assert.Equal(firstTransform.Y, secondTransform.Y);
            Assert.Equal(firstNavigation.Entity((uint)enemyId).VelocityX, secondNavigation.Entity((uint)enemyId).VelocityX);
            Assert.Equal(firstNavigation.Entity((uint)enemyId).VelocityY, secondNavigation.Entity((uint)enemyId).VelocityY);
        }
    }

    [Fact]
    public void BlueprintRegistrationCreatedWorldResolvesShooterSveltoServices()
    {
        var registry = new WorldTypeRegistry();
        ShooterWorldBlueprintsRegistration.RegisterAll(registry);
        var manager = new WorldManager(new RegistryWorldFactory(registry));

        var world = manager.Create(new WorldCreateOptions(new WorldId("shooter-world-1"), ShooterGameplay.WorldType));

        Assert.Equal(ShooterGameplay.WorldType, world.WorldType);
        Assert.True(world.Services.TryResolve<IShooterBattleRuntimePort>(out var runtime));
        Assert.True(world.Services.TryResolve<IShooterEntityManager>(out var entities));
        Assert.True(world.Services.TryResolve<ISveltoWorldContext>(out var svelto));
        Assert.True(world.Services.TryResolve<IShooterSveltoWorld>(out var shooterSveltoWorld));
        Assert.Same(svelto, shooterSveltoWorld.Context);
        Assert.Same(entities, world.Services.Resolve<ShooterBattleState>().Entities);

        var start = new ShooterStartGamePayload(
            "blueprint-world",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(entities.HasPlayer(1));
        Assert.Equal(1, svelto.EntitiesDB.Count<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players));

        manager.DisposeAll();
    }

    [Fact]
    public void ShooterWorldHostCreatesAndDrivesBattleWorldRuntime()
    {
        var host = new ShooterWorldHost();
        var world = host.CreateBattleWorld("host-world-1");

        Assert.Equal(ShooterGameplay.WorldType, world.WorldType);
        Assert.True(host.TryGetBattleWorld("host-world-1", out var resolvedWorld));
        Assert.Same(world, resolvedWorld);
        Assert.True(world.Services.TryResolve<IShooterBattleRuntimePort>(out var runtime));
        Assert.True(world.Services.TryResolve<ISveltoWorldContext>(out var svelto));

        var start = new ShooterStartGamePayload(
            "host-world",
            30,
            1,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.Equal(0, runtime.CurrentFrame);

        host.Tick(1f / 30f);

        Assert.Equal(1, runtime.CurrentFrame);
        Assert.Equal(1, svelto.EntitiesDB.Count<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players));
        Assert.True(host.DestroyBattleWorld("host-world-1"));
        Assert.False(host.TryGetBattleWorld("host-world-1", out _));
    }

    private static ShooterPackedComponentChunk? FindPackedChunk(in ShooterPackedSnapshotPayload snapshot, int componentKind, int entityKind)
    {
        for (int i = 0; i < snapshot.ComponentChunks.Length; i++)
        {
            var chunk = snapshot.ComponentChunks[i];
            if (chunk.ComponentKind == componentKind && chunk.EntityKind == entityKind)
            {
                return chunk;
            }
        }

        return null;
    }

    private static void AssertEnemyNotAlive(IShooterEntityManager entities, int enemyId)
    {
        if (entities.TryGetEnemy(enemyId, out _, out var health))
        {
            Assert.Equal(0, health.Alive);
        }
    }

    private static bool AnyPlayerDamaged(IShooterEntityManager entities)
    {
        foreach (var playerId in entities.PlayerIds)
        {
            if (entities.TryGetPlayer(playerId, out var player) && player.Hp < ShooterGameplay.DefaultPlayerHp)
            {
                return true;
            }
        }

        return false;
    }
}

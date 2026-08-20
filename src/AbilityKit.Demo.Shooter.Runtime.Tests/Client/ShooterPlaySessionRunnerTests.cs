using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.LagCompensation;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

public sealed class ShooterPlaySessionRunnerTests
{
    [Fact]
    public void PlayModeScenarioSeparatesActiveDensityFromCampaignLength()
    {
        var scenario = ShooterPlayModeSessionOptions.CreatePlayModeScenario(
            ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget);
        var flow = scenario.BattleFlow;
        var totalEnemies = 0;
        var initialEnemies = 0;
        var firstReinforcementFrame = int.MaxValue;
        for (var i = 0; i < flow.Waves.Length; i++)
        {
            var wave = flow.Waves[i];
            totalEnemies += wave.EnemyCount;
            if (wave.StartFrame == 0)
            {
                initialEnemies += wave.EnemyCount;
            }
            else
            {
                firstReinforcementFrame = Math.Min(firstReinforcementFrame, wave.StartFrame);
            }
        }

        Assert.Equal(ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget, flow.MaxActiveEnemies);
        Assert.Equal(ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget, initialEnemies);
        Assert.Equal(totalEnemies, flow.VictoryTargetDefeats);
        Assert.True(totalEnemies >= flow.MaxActiveEnemies * 5);
        Assert.True(firstReinforcementFrame >= ShooterAcceptanceLab.DefaultTickRate * 60);
        Assert.True(flow.DurationFrames >= ShooterAcceptanceLab.DefaultTickRate * 600);
        Assert.Equal(ShooterAcceptanceLab.DefaultTickRate * 2, flow.EnemyAttackIntervalFrames);
        Assert.Equal(36f, scenario.ArenaRadius);
        Assert.All(flow.Waves, wave => Assert.True(
            wave.SpawnRadius >= 36f,
            $"Expected doubled PlayMode spawn radius, got {wave.SpawnRadius}."));
    }

    [Theory]
    [InlineData(ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget)]
    [InlineData(ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget)]
    public void PlayModeScenarioPreservesSupportedDensityProfilesAcrossCampaign(int enemyBudget)
    {
        var flow = ShooterPlayModeSessionOptions.CreatePlayModeScenario(enemyBudget).BattleFlow;
        var totalEnemies = 0;
        var initialEnemies = 0;
        for (var i = 0; i < flow.Waves.Length; i++)
        {
            totalEnemies += flow.Waves[i].EnemyCount;
            if (flow.Waves[i].StartFrame == 0)
            {
                initialEnemies += flow.Waves[i].EnemyCount;
            }
        }

        Assert.Equal(enemyBudget, flow.MaxActiveEnemies);
        Assert.Equal(enemyBudget, initialEnemies);
        Assert.Equal(enemyBudget * 5, totalEnemies);
        Assert.Equal(totalEnemies, flow.VictoryTargetDefeats);
        Assert.True(flow.Waves.Length > initialEnemies / 64);
    }

    [Theory]
    [InlineData(int.MaxValue, ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget)]
    [InlineData(0, 1)]
    [InlineData(-100, 1)]
    public void PlayModeScenarioClampsUnsupportedDensityWithoutOverflow(int requestedBudget, int expectedBudget)
    {
        var flow = ShooterPlayModeSessionOptions.CreatePlayModeScenario(requestedBudget).BattleFlow;
        var totalEnemies = 0;
        for (var i = 0; i < flow.Waves.Length; i++)
        {
            totalEnemies += flow.Waves[i].EnemyCount;
        }

        Assert.Equal(expectedBudget, flow.MaxActiveEnemies);
        Assert.Equal(expectedBudget * 5, totalEnemies);
        Assert.Equal(totalEnemies, flow.VictoryTargetDefeats);
    }

    [Fact]
    public void FiveHundredMsLatencyDoesNotPullControlledPlayerBackAfterStopping()
    {
        const int tickRate = 30;
        const int controlledPlayerId = 1;
        var moveRightTicks = tickRate;
        var observeTicks = tickRate;
        var inputs = new List<ShooterHostFrameInput>(moveRightTicks + observeTicks);
        for (var i = 0; i < moveRightTicks; i++)
        {
            inputs.Add(new ShooterHostFrameInput(1f, 0f, 1f, 0f, false));
        }

        for (var i = 0; i < observeTicks; i++)
        {
            inputs.Add(new ShooterHostFrameInput(0f, 0f, 1f, 0f, false));
        }

        var input = new ScriptedInputSource(inputs.ToArray());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 33,
            controlledPlayerId,
            enableAuthoritativeWorld: true,
            latencyMs: 500,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "500ms pull-back smoke"));

        var key = new ShooterViewEntityKey(ShooterViewEntityKind.Player, controlledPlayerId);
        float? previousObservedX = null;
        float? stoppedBaselineX = null;
        var maxPullBackAfterStop = 0f;
        var maxDriftAfterStop = 0f;
        for (var tick = 0; tick < moveRightTicks + observeTicks; tick++)
        {
            runner.Tick(1f / tickRate);
            Assert.NotEmpty(view.Frames);
            Assert.True(TryGetTransformX(view.Frames[^1].ClientBatch, key, out var x));

            if (tick == moveRightTicks)
            {
                stoppedBaselineX = x;
            }

            if (tick >= moveRightTicks && previousObservedX.HasValue)
            {
                maxPullBackAfterStop = Math.Max(maxPullBackAfterStop, previousObservedX.Value - x);
                if (stoppedBaselineX.HasValue)
                {
                    maxDriftAfterStop = Math.Max(maxDriftAfterStop, Math.Abs(x - stoppedBaselineX.Value));
                }
            }

            previousObservedX = x;
        }

        Assert.True(maxPullBackAfterStop <= 0.001f, $"Controlled player was pulled back by {maxPullBackAfterStop:0.0000} units after input stopped under 500ms latency.");
        Assert.True(maxDriftAfterStop <= 0.001f, $"Controlled player drifted by {maxDriftAfterStop:0.0000} units after input stopped under 500ms latency.");
    }

    [Fact]
    public void RepeatedMovementUnderFiveHundredMsLatencyKeepsControlledPlayerReasonablyAlignedAfterDrain()
    {
        const int tickRate = 30;
        const int controlledPlayerId = 1;
        var inputs = new List<ShooterHostFrameInput>();
        AddRepeatedMovement(inputs, repeats: 3, activeTicks: 10, restTicks: 5);
        AddIdle(inputs, tickRate * 2);

        var input = new ScriptedInputSource(inputs.ToArray());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 41,
            controlledPlayerId,
            enableAuthoritativeWorld: true,
            latencyMs: 500,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "500ms repeated movement alignment smoke"));

        for (var tick = 0; tick < inputs.Count; tick++)
        {
            runner.Tick(1f / tickRate);
        }

        var comparison = runner.Session!.CompareWorlds();
        var controlled = Assert.Single(comparison.Divergences, d => d.PlayerId == controlledPlayerId);
        Assert.True(controlled.Distance <= 0.05d, $"Controlled player diverged from authority by {controlled.Distance:0.0000} after repeated movement and latency drain. client=({controlled.ClientX:0.0000},{controlled.ClientY:0.0000}) authority=({controlled.AuthorityX:0.0000},{controlled.AuthorityY:0.0000})");
    }

    [Fact]
    public void StartAlignsRuntimeOptionsWithSelectedGameplayScenario()
    {
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        var options = ShooterPlayModeSessionOptions.FromTemplate(
            ShooterAcceptanceCatalog.GetSyncTemplate("predict-rollback-authority"),
            ShooterSveltoGameplayScenarioCatalog.WaveSurvival);

        runner.Start(options);

        Assert.Equal(ShooterSveltoGameplayScenarioCatalog.WaveSurvival.Id, runner.Options.GameplayScenario.Id);
        Assert.Equal(30, runner.Options.TickRate);
        Assert.Equal(ShooterSveltoGameplayScenarioCatalog.WaveSurvival.ShooterCount, runner.Options.PlayerCount);
    }

    [Fact]
    public void PlayInputMappingMapsSpecialFireKeysToAttackSlots()
    {
        var primary = ShooterPlayInputMapping.CreateFrameInput(0f, 0f, primaryFire: true, spreadFire: false, twinFire: false);
        var spread = ShooterPlayInputMapping.CreateFrameInput(0f, 0f, primaryFire: false, spreadFire: true, twinFire: false);
        var twin = ShooterPlayInputMapping.CreateFrameInput(0f, 0f, primaryFire: false, spreadFire: false, twinFire: true);
        var combined = ShooterPlayInputMapping.CreateFrameInput(2f, -2f, primaryFire: true, spreadFire: true, twinFire: true);

        Assert.True(primary.Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Primary, primary.AttackSlot);
        Assert.True(spread.Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Spread, spread.AttackSlot);
        Assert.True(twin.Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Twin, twin.AttackSlot);
        Assert.True(combined.Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Twin, combined.AttackSlot);
        Assert.Equal(1f, combined.MoveX);
        Assert.Equal(-1f, combined.MoveY);
    }

    [Fact]
    public void GameplayScenarioWorldHostEnablesWaveEnemyMovement()
    {
        using var world = ShooterBattleWorldSession.Create(
            "play-mode-scenario-world-host-tests",
            ShooterGameplayScenarioWorldHostFactory.Create(CreateCloseEnemyHitScenario(30)));
        var runtime = world.Runtime;
        var start = new ShooterStartGamePayload(
            "play-mode-scenario-world-host-tests",
            30,
            123,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(0f));
        var spawned = runtime.GetSnapshot();
        var enemy = Assert.Single(spawned.Enemies);
        var beforeDistanceSquared = enemy.X * enemy.X + enemy.Y * enemy.Y;

        Assert.True(runtime.Tick(1f / 30f));

        var movedEnemy = Assert.Single(runtime.GetSnapshot().Enemies, candidate => candidate.EnemyId == enemy.EnemyId);
        var afterDistanceSquared = movedEnemy.X * movedEnemy.X + movedEnemy.Y * movedEnemy.Y;
        Assert.True(afterDistanceSquared < beforeDistanceSquared);
    }

    [Fact]
    public void LocalMenuDefaultSessionProjectsWaveEnemiesAndFireBullets()
    {
        var tickRate = ShooterPlayModeSessionOptions.Default.TickRate;
        var inputs = new List<ShooterHostFrameInput>();
        AddInput(inputs, tickRate, 0f, 0f, 0f, 1f, true);
        AddIdle(inputs, tickRate * 2);

        var input = new ScriptedInputSource(inputs.ToArray());
        var view = new AggregatingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(ShooterPlayModeSessionOptions.Default);

        for (var tick = 0; tick < inputs.Count; tick++)
        {
            runner.Tick(1f / runner.Options.TickRate);
        }

        Assert.True(view.MaxPlayerCount > 0, "Expected the controlled player to be visible in the projected menu view.");
        Assert.True(view.MaxEnemyCount > 0, "Expected wave enemies to be visible in the projected menu view.");
        Assert.True(view.MaxBulletCount > 0 || view.TotalExplicitEntityRemovals > 0 || view.TotalDeadEntityRemovals > 0, "Expected fired bullets to enter or be removed from the projected menu view.");
    }

    [Fact]
    public void MassBattlePlayModeHidesEnemiesUntilTheyEnterConfiguredAoi()
    {
        var template = ShooterAcceptanceCatalog.GetSyncTemplate(ShooterSyncTemplateIds.MassBattleLodAoi);
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new AggregatingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(ShooterPlayModeSessionOptions.FromTemplate(in template));

        runner.Tick(1f / runner.Options.TickRate);

        Assert.Equal(template.SendPolicy.SnapshotIntervalFrames, runner.PresentationSnapshotIntervalTicks);
        Assert.True(runner.Session!.Runtime.GetSnapshot().Enemies.Length > 0);
        Assert.Equal(0, view.ProjectedEnemyCount);

        var ticksUntilEnemyEnters = runner.Options.TickRate * 9;
        for (var tick = 1; tick < ticksUntilEnemyEnters; tick++)
        {
            runner.Tick(1f / runner.Options.TickRate);
        }

        Assert.True(view.ProjectedEnemyCount > 0, "Expected inward-moving enemies to appear after entering the configured AOI radius.");
    }

    [Fact]
    public void LocalMenuDefaultSessionDoesNotDefeatIdlePlayersAfterTwelveSeconds()
    {
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new AggregatingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(ShooterPlayModeSessionOptions.Default);

        var totalTicks = runner.Options.TickRate * 12;
        for (var tick = 0; tick < totalTicks; tick++)
        {
            runner.Tick(1f / runner.Options.TickRate);
        }

        Assert.Equal(totalTicks, runner.StepCount);
        Assert.Equal(totalTicks, view.RenderCount);
        Assert.Equal(ShooterBattleMatchState.Running, runner.Session!.Runtime.MatchState);
        Assert.True(runner.Session.Runtime.IsStarted);
        Assert.InRange(view.MaxEnemyCount, 128, ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget);
    }

    [Fact]
    public void ExplicitHighDensityPlayModeScenarioCanDemonstrateThousandsOfEnemies()
    {
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new AggregatingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        var options = ShooterPlayModeSessionOptions.Default.WithGameplayScenario(
            ShooterPlayModeSessionOptions.CreatePlayModeScenario(ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget));
        runner.Start(options);

        var totalTicks = runner.Options.TickRate * 3;
        var maxAuthoritativeEnemyCount = 0;
        for (var tick = 0; tick < totalTicks; tick++)
        {
            runner.Tick(1f / runner.Options.TickRate);
            maxAuthoritativeEnemyCount = Math.Max(
                maxAuthoritativeEnemyCount,
                runner.Session!.Runtime.GetSnapshot().Enemies.Length);
        }

        Assert.Equal(totalTicks, runner.StepCount);
        Assert.Equal(totalTicks, view.RenderCount);
        Assert.Equal(ShooterBattleMatchState.Running, runner.Session!.Runtime.MatchState);
        Assert.True(runner.Session.Runtime.IsStarted);
        Assert.True(
            maxAuthoritativeEnemyCount >= 2048,
            $"Expected the explicit high-density PlayMode scenario to simulate thousands of active enemies, but max was {maxAuthoritativeEnemyCount}.");
        Assert.InRange(
            view.MaxEnemyCount,
            1,
            options.GameplayScenario.BattleFlow.MaxActiveEnemies);
    }

    [Fact]
    public void HighDensityPlayModeUsesBoundedPureStateDeltasAfterInitialBaseline()
    {
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        var options = ShooterPlayModeSessionOptions.Default.WithGameplayScenario(
            ShooterPlayModeSessionOptions.CreatePlayModeScenario(ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget));
        runner.Start(options);

        var publishInterval = runner.PresentationSnapshotIntervalTicks;
        var publishedBatches = new List<ShooterSnapshotViewBatch>();
        var observedPublishCount = runner.PresentationPublishCount;
        for (var tick = 0; tick < publishInterval * 2; tick++)
        {
            runner.Tick(1f / runner.Options.TickRate);
            if (runner.PresentationPublishCount == observedPublishCount)
            {
                continue;
            }

            observedPublishCount = runner.PresentationPublishCount;
            publishedBatches.Add(view.Frames[^1].ClientBatch);
        }

        Assert.Equal(3, publishedBatches.Count);
        Assert.Equal(ShooterViewSnapshotKind.Full, publishedBatches[0].SnapshotKind);
        Assert.Equal(ShooterViewSnapshotKind.Delta, publishedBatches[1].SnapshotKind);
        Assert.Equal(ShooterViewSnapshotKind.Delta, publishedBatches[2].SnapshotKind);
        Assert.True(
            publishedBatches[1].EntityChanges.Count <= ShooterPureStateSyncSettings.Default.ActiveSyncBudget,
            $"Expected high-density pure-state presentation deltas to stay within the active budget, but got {publishedBatches[1].EntityChanges.Count} entities.");
        Assert.True(
            publishedBatches[2].EntityChanges.Count <= ShooterPureStateSyncSettings.Default.ActiveSyncBudget,
            $"Expected high-density pure-state presentation deltas to stay within the active budget, but got {publishedBatches[2].EntityChanges.Count} entities.");
    }

    [Fact]
    public void CatchUpFramePublishesOnlyFinalPresentationSnapshot()
    {
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(ShooterPlayModeSessionOptions.Default);

        runner.Tick(2.1f / runner.Options.TickRate);
        var session = runner.Session!;

        Assert.Equal(2, runner.StepCount);
        Assert.Equal(1, runner.RenderCount);
        Assert.Equal(1, runner.PresentationPublishCount);
        Assert.Equal(session.Runtime.CurrentFrame, session.Presentation.ViewModel.Current.Frame);
    }

    [Fact]
    public void MediumDensityPlayModeProjectionRemovesExpiredBulletsAndDefeatedEnemiesAfterLongRun()
    {
        var tickRate = ShooterPlayModeSessionOptions.Default.TickRate;
        var input = new MutableInputSource(new ShooterHostFrameInput(0f, 0f, 1f, 0f, false));
        var view = new AggregatingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        var options = ShooterPlayModeSessionOptions.Default.WithGameplayScenario(
            ShooterPlayModeSessionOptions.CreatePlayModeScenario(ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget));
        runner.Start(options);

        var totalTicks = tickRate * 12;
        for (var tick = 0; tick < totalTicks; tick++)
        {
            var snapshot = runner.Session!.Runtime.GetSnapshot();
            input.Current = TryCreateAimAtNearestEnemy(in snapshot, runner.Options.ControlledPlayerId, out var aimX, out var aimY)
                ? new ShooterHostFrameInput(0f, 0f, aimX, aimY, true)
                : new ShooterHostFrameInput(0f, 0f, 1f, 0f, false);

            runner.Tick(1f / runner.Options.TickRate);
        }

        var finalSnapshot = runner.Session!.Runtime.GetSnapshot();
        Assert.Equal(totalTicks, runner.StepCount);
        Assert.Equal(totalTicks, view.RenderCount);
        Assert.Equal(ShooterBattleMatchState.Running, runner.Session.Runtime.MatchState);
        var observedEnemyActivity = view.MaxEnemyCount + view.TotalExplicitEntityRemovals + view.TotalDeadEntityRemovals;
        Assert.True(
            observedEnemyActivity >= ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget,
            $"Expected the 2K PlayMode scenario to spawn or remove at least {ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget} enemies, but max={view.MaxEnemyCount}, explicitRemovals={view.TotalExplicitEntityRemovals}, deadRemovals={view.TotalDeadEntityRemovals}.");
        Assert.True(view.TotalExplicitEntityRemovals > 0 || view.TotalDeadEntityRemovals > 0, "Expected the local-authoritative projection to remove bullets or defeated enemies during the long run.");
        Assert.True(view.ProjectedBulletCount <= finalSnapshot.Bullets.Length + tickRate, $"Projected bullets appear to be retained after runtime removal. projected={view.ProjectedBulletCount} runtime={finalSnapshot.Bullets.Length}");
        Assert.True(view.ProjectedEnemyCount <= finalSnapshot.Enemies.Length + tickRate, $"Projected enemies appear to be retained after runtime removal. projected={view.ProjectedEnemyCount} runtime={finalSnapshot.Enemies.Length}");
        Assert.True(finalSnapshot.Bullets.Length <= tickRate * 4, $"Runtime projectile count grew beyond the expected lifetime window. bullets={finalSnapshot.Bullets.Length}");
    }

    [Fact]
    public void LocalMenuDefaultSessionContinuesAfterEnemyHit()
    {
        var tickRate = ShooterPlayModeSessionOptions.Default.TickRate;
        var totalTicks = tickRate * 2;
        var options = new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 1,
            randomSeed: 3901,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: false,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "close enemy hit regression",
            syncTemplateId: null,
            gameplayScenario: CreateCloseEnemyHitScenario(tickRate));
        var input = new MutableInputSource(new ShooterHostFrameInput(0f, 0f, 0f, 1f, false));
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(options);

        var enemyHitFrame = -1;
        var runtimeEnemyHitFrame = -1;
        ShooterEventSnapshot? firstPresentationHit = null;
        for (var tick = 0; tick < totalTicks; tick++)
        {
            var snapshot = runner.Session!.Runtime.GetSnapshot();
            input.Current = TryCreateAimAtNearestEnemy(in snapshot, runner.Options.ControlledPlayerId, out var aimX, out var aimY)
                ? new ShooterHostFrameInput(0f, 0f, aimX, aimY, true)
                : new ShooterHostFrameInput(0f, 0f, 0f, 1f, false);
 
            runner.Tick(1f / runner.Options.TickRate);
            var postTickSnapshot = runner.Session!.Runtime.GetSnapshot();
            if (runtimeEnemyHitFrame < 0 && ContainsEnemyHitEvent(in postTickSnapshot))
            {
                runtimeEnemyHitFrame = postTickSnapshot.Frame;
            }
 
            if (enemyHitFrame < 0 && TryGetEnemyHitEvent(view.Frames[^1].ClientBatch, out var hit))
            {
                enemyHitFrame = view.Frames[^1].ClientBatch.Frame;
                firstPresentationHit = hit;
            }

        }
 
        Assert.True(runtimeEnemyHitFrame > 0, "Expected the PlayMode runtime to produce an enemy hit while dynamically aiming at the nearest close enemy.");
        Assert.True(enemyHitFrame > 0, "Expected the PlayMode presentation to project the enemy hit event.");
        Assert.Equal(totalTicks, runner.StepCount);
        Assert.Equal(totalTicks, view.Frames.Count);
        Assert.Contains(view.Frames, frame => frame.ClientBatch.Frame > enemyHitFrame);
        var finalRuntimeSnapshot = runner.Session!.Runtime.GetSnapshot();
        Assert.True(
            ProjectionContainsEnemyAfterFrame(view.Frames, enemyHitFrame),
            $"Expected an enemy projection after hit frame {enemyHitFrame}; finalRuntimeEnemies={finalRuntimeSnapshot.Enemies.Length}, matchState={runner.Session.Runtime.MatchState}, finalRuntimeFrame={finalRuntimeSnapshot.Frame}, finalViewFrame={view.Frames[^1].ClientBatch.Frame}.");

        var firstHit = Assert.IsType<ShooterEventSnapshot>(firstPresentationHit);
        var projection = new ShooterSnapshotViewProjection();
        for (var i = 0; i < view.Frames.Count; i++)
        {
            var batch = view.Frames[i].ClientBatch;
            projection.Apply(in batch);
        }

        Assert.Equal(ShooterViewBatchSource.LocalAuthoritative, view.Frames[^1].ClientBatch.Source);
        Assert.False(projection.Store.ContainsEntity(new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, firstHit.BulletId)));
        Assert.False(projection.Store.ContainsEntity(new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, -firstHit.TargetPlayerId)));
    }

    [Fact]
    public void ViewSinkAppliesFirstZeroSequenceBatchAndLaterBulletRemoval()
    {
        var sink = new AggregatingViewSink();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var initial = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 0ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.LocalAuthoritative,
            new[]
            {
                new ShooterViewEntityChange(player, ownerEntityId: 0, alive: true),
                new ShooterViewEntityChange(bullet, ownerEntityId: 1, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(player, 0f, 0f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(bullet, 1f, 0f, 1f, 0f, 10f, 0f)
            },
            new[] { new ShooterViewHealthComponentChange(player, 100) },
            Array.Empty<ShooterViewScoreComponentChange>(),
            new[] { new ShooterViewProjectileLifetimeComponentChange(bullet, 30) },
            Array.Empty<ShooterEventSnapshot>());
        var removal = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 2,
            sequence: 1ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.LocalAuthoritative,
            Array.Empty<ShooterViewEntityChange>(),
            new[] { bullet },
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        var initialFrame = CreatePresentationFrame(initial);
        sink.Render(in initialFrame);

        Assert.Equal(1, sink.ProjectedPlayerCount);
        Assert.Equal(1, sink.ProjectedBulletCount);

        var removalFrame = CreatePresentationFrame(removal);
        sink.Render(in removalFrame);

        Assert.Equal(1, sink.ProjectedPlayerCount);
        Assert.Equal(0, sink.ProjectedBulletCount);
        Assert.Equal(1, sink.TotalExplicitEntityRemovals);
    }

    [Fact]
    public void AuthoritativePlayModePresentationUsesAuthoritativeSnapshotSource()
    {
        const int tickRate = 30;
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 3902,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: true,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "authority projection source regression"));

        runner.Tick(1f / tickRate);

        var frame = Assert.Single(view.Frames);
        Assert.True(frame.HasAuthorityBatch);
        Assert.Equal(ShooterViewBatchSource.LocalAuthoritative, frame.AuthorityBatch.Source);
    }

    [Fact]
    public void TickAdvancesLocalTimeAnchorAndProjectsItToDiagnostics()
    {
        const int tickRate = 20;
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 42,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: true,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "local anchor smoke"));

        var actualTickRate = runner.Options.TickRate;
        runner.Tick(1f / actualTickRate);
        runner.Tick(1f / actualTickRate);

        var frame = Assert.Single(view.Frames, f => f.LocalTimeAnchor.LocalFrame == 1);
        Assert.Equal(2, runner.StepCount);
        Assert.Equal(1, runner.LastLocalTimeAnchor.LocalFrame);
        Assert.Equal(1L, runner.LastLocalTimeAnchor.TimelineTicks);
        Assert.Equal(1d / actualTickRate, runner.LastLocalTimeAnchor.ElapsedSeconds, precision: 6);

        var diagnostics = ShooterHostDiagnosticsProjector.ProjectFromFrame(in frame, previousTotalEvents: 0);
        Assert.Equal(frame.LocalTimeAnchor, diagnostics.LocalTimeAnchor);
    }

    [Fact]
    public void FireUnderFiveHundredMsLatencySpawnsPredictedAndAuthoritativeBulletsAtSameOrigin()
    {
        const int tickRate = 30;
        const int controlledPlayerId = 1;
        var inputs = new List<ShooterHostFrameInput>();
        AddRepeatedMovement(inputs, repeats: 2, activeTicks: 10, restTicks: 5);
        var fireFrameIndex = inputs.Count;
        inputs.Add(new ShooterHostFrameInput(0f, 0f, 1f, 0f, true));
        AddIdle(inputs, tickRate * 2);

        var input = new ScriptedInputSource(inputs.ToArray());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 43,
            controlledPlayerId,
            enableAuthoritativeWorld: true,
            latencyMs: 500,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "500ms fire origin smoke"));

        ShooterEventSnapshot? predictedFire = null;
        ShooterEventSnapshot? authoritativeFire = null;
        ShooterSveltoPlayerComponent? predictedFirePlayer = null;
        ShooterSveltoPlayerComponent? authoritativeFirePlayer = null;
        for (var tick = 0; tick < inputs.Count; tick++)
        {
            runner.Tick(1f / tickRate);
            if (tick == fireFrameIndex)
            {
                predictedFirePlayer = TryGetPlayer(runner.Session!.Runtime.GetSnapshot(), controlledPlayerId, out var localPlayer)
                    ? localPlayer
                    : null;
                authoritativeFirePlayer = runner.Session!.AuthoritativeWorld != null && TryGetPlayer(runner.Session.AuthoritativeWorld.GetSnapshot(), controlledPlayerId, out var authorityPlayer)
                    ? authorityPlayer
                    : null;
            }

            predictedFire ??= TryGetFireEvent(runner.Session!.Runtime.GetSnapshot(), controlledPlayerId, out var localFire)
                ? localFire
                : null;
            authoritativeFire ??= runner.Session!.AuthoritativeWorld != null && TryGetFireEvent(runner.Session.AuthoritativeWorld.GetSnapshot(), controlledPlayerId, out var authorityFire)
                ? authorityFire
                : null;
        }

        var predictedFirePlayerValue = Assert.NotNull(predictedFirePlayer);
        var authoritativeFirePlayerValue = Assert.NotNull(authoritativeFirePlayer);
        var predicted = Assert.IsType<ShooterEventSnapshot>(predictedFire);
        var authoritative = Assert.IsType<ShooterEventSnapshot>(authoritativeFire);
        var dx = predicted.X - authoritative.X;
        var dy = predicted.Y - authoritative.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var playerDx = predictedFirePlayerValue.X - authoritativeFirePlayerValue.X;
        var playerDy = predictedFirePlayerValue.Y - authoritativeFirePlayerValue.Y;
        var playerDistance = Math.Sqrt(playerDx * playerDx + playerDy * playerDy);
        Assert.True(distance <= 0.05d, $"Predicted and authoritative bullet origins diverged by {distance:0.0000}. predicted=({predicted.X:0.0000},{predicted.Y:0.0000}) authority=({authoritative.X:0.0000},{authoritative.Y:0.0000}); fire-frame player predicted=({predictedFirePlayerValue.X:0.0000},{predictedFirePlayerValue.Y:0.0000}) authority=({authoritativeFirePlayerValue.X:0.0000},{authoritativeFirePlayerValue.Y:0.0000}) playerDistance={playerDistance:0.0000}");
    }

    [Fact]
    public void LagCompensationEvaluationIsProjectedToHostDiagnostics()
    {
        const int tickRate = 30;
        var input = new ScriptedInputSource(Array.Empty<ShooterHostFrameInput>());
        var view = new RecordingViewSink();
        using var runner = new ShooterPlaySessionRunner(input, view);
        runner.Start(new ShooterPlayModeSessionOptions(
            NetworkSyncModel.PredictRollback,
            tickRate,
            playerCount: 2,
            randomSeed: 44,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: true,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "lag compensation diagnostics smoke"));
        runner.Tick(1f / tickRate);
        var shot = new ShooterLagCompensationShot(
            shooterPlayerId: 1,
            originX: 0f,
            originY: 0f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 10f,
            rewindFrame: 1,
            serverReceiveFrame: 1);

        var accepted = runner.Session!.TryEvaluateLagCompensationShot(in shot, out var evaluation);
        runner.Tick(1f / tickRate);

        Assert.True(accepted);
        Assert.Equal(LagCompensationResultReason.Hit, evaluation.Reason);
        var frame = Assert.Single(view.Frames, f => f.LagCompensationEvaluation.HasValue);
        var frameEvaluation = Assert.IsType<ShooterLagCompensationEvaluation>(frame.LagCompensationEvaluation);
        Assert.Equal(evaluation, frameEvaluation);
        var diagnostics = ShooterHostDiagnosticsProjector.ProjectFromFrame(in frame, previousTotalEvents: 0);
        Assert.Equal(evaluation, diagnostics.LagCompensationEvaluation);
    }

    [Fact]
    public void PureStateRecoveryDiagnosticsAreProjectedFromHostFrame()
    {
        var frame = new ShooterHostPresentationFrame(
            ShooterSnapshotViewBatch.Empty,
            ShooterSnapshotViewBatch.Empty,
            false,
            controlledPlayerId: 1,
            worldScale: 1f,
            carrierNetworkStats: null,
            lastCarrierSnapshotApplyResult: ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded,
            lastCarrierTimeAnchor: default,
            localTimeAnchor: default,
            lagCompensationTelemetry: null,
            lagCompensationEvaluation: null,
            remoteLatencyCompensationDiagnostics: default,
            crossLayerDiagnostics: new ShooterCrossLayerDiagnostics(
                frameworkPacketCount: 3,
                frameworkDispatchedSnapshotCount: 2,
                frameworkPackedSnapshotCount: 1,
                frameworkPureStateSnapshotCount: 1,
                lastFrameworkFrame: 18,
                lastFrameworkPayloadOpCode: 12002,
                lastFrameworkWorldId: "shooter:7001",
                hasSnapshotApplyResult: true,
                snapshotApplyResult: ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded,
                hasRemoteLatencyResult: false,
                remoteInputDelayFrames: 0,
                remoteAuthoritativeFrameGap: 0,
                needsPureStateBaselineResync: true,
                lastPureStateAppliedFrame: 12,
                lastPureStateResyncFrame: 18),
            pureStateSyncDiagnostics: new ShooterPureStateSyncDiagnostics(
                ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync,
                sourceFrame: 18,
                sourceSnapshotKind: ShooterPureStateSnapshotKinds.Delta,
                sourceEntityCount: 2,
                sourceVisibilityHintCount: 1,
                sourceBaselineFrame: 12,
                sourceBaselineHash: 0x1234u,
                sourceStateHash: 0x5678u,
                sourceServerTick: 99L,
                appliedFrame: 12,
                appliedStateHash: 0x1234u,
                needsFullBaselineResync: true,
                lastResyncReason: ShooterPureStateResyncReason.BaselineMismatch,
                lastResyncFrame: 18,
                lastResyncStateHash: 0x5678u,
                lastIgnoredFrame: -1),
            needsPureStateBaselineResync: true,
            lastPureStateResyncReason: ShooterPureStateResyncReason.BaselineMismatch,
            lastPureStateAppliedFrame: 12,
            lastPureStateAppliedStateHash: 0x1234u,
            lastPureStateResyncFrame: 18,
            lastPureStateResyncStateHash: 0x5678u);

        var diagnostics = ShooterHostDiagnosticsProjector.ProjectFromFrame(in frame, previousTotalEvents: 0);

        Assert.True(diagnostics.NeedsPureStateBaselineResync);
        Assert.Equal(3, diagnostics.CrossLayerDiagnostics.FrameworkPacketCount);
        Assert.Equal(2, diagnostics.CrossLayerDiagnostics.FrameworkDispatchedSnapshotCount);
        Assert.Equal(1, diagnostics.CrossLayerDiagnostics.FrameworkPackedSnapshotCount);
        Assert.Equal(1, diagnostics.CrossLayerDiagnostics.FrameworkPureStateSnapshotCount);
        Assert.Equal(18, diagnostics.CrossLayerDiagnostics.LastFrameworkFrame);
        Assert.Equal(12002, diagnostics.CrossLayerDiagnostics.LastFrameworkPayloadOpCode);
        Assert.Equal("shooter:7001", diagnostics.CrossLayerDiagnostics.LastFrameworkWorldId);
        Assert.True(diagnostics.CrossLayerDiagnostics.HasSnapshotApplyResult);
        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, diagnostics.CrossLayerDiagnostics.SnapshotApplyResult);
        Assert.True(diagnostics.CrossLayerDiagnostics.NeedsPureStateBaselineResync);
        Assert.Equal(12, diagnostics.CrossLayerDiagnostics.LastPureStateAppliedFrame);
        Assert.Equal(18, diagnostics.CrossLayerDiagnostics.LastPureStateResyncFrame);
        Assert.Equal(ShooterPureStateResyncReason.BaselineMismatch, diagnostics.LastPureStateResyncReason);
        Assert.Equal(12, diagnostics.LastPureStateAppliedFrame);
        Assert.Equal(0x1234u, diagnostics.LastPureStateAppliedStateHash);
        Assert.Equal(18, diagnostics.LastPureStateResyncFrame);
        Assert.Equal(0x5678u, diagnostics.LastPureStateResyncStateHash);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, diagnostics.PureStateSyncDiagnostics.LastApplyResult);
        Assert.Equal(18, diagnostics.PureStateSyncDiagnostics.SourceFrame);
        Assert.Equal(ShooterPureStateSnapshotKinds.Delta, diagnostics.PureStateSyncDiagnostics.SourceSnapshotKind);
        Assert.Equal(2, diagnostics.PureStateSyncDiagnostics.SourceEntityCount);
        Assert.Equal(1, diagnostics.PureStateSyncDiagnostics.SourceVisibilityHintCount);
        Assert.Equal(12, diagnostics.PureStateSyncDiagnostics.SourceBaselineFrame);
        Assert.Equal(0x1234u, diagnostics.PureStateSyncDiagnostics.SourceBaselineHash);
        Assert.Equal(0x5678u, diagnostics.PureStateSyncDiagnostics.SourceStateHash);
        Assert.Equal(99L, diagnostics.PureStateSyncDiagnostics.SourceServerTick);
        Assert.Equal(12, diagnostics.PureStateSyncDiagnostics.AppliedFrame);
        Assert.Equal(0x1234u, diagnostics.PureStateSyncDiagnostics.AppliedStateHash);
        Assert.True(diagnostics.PureStateSyncDiagnostics.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.BaselineMismatch, diagnostics.PureStateSyncDiagnostics.LastResyncReason);
        Assert.Equal(18, diagnostics.PureStateSyncDiagnostics.LastResyncFrame);
        Assert.Equal(0x5678u, diagnostics.PureStateSyncDiagnostics.LastResyncStateHash);
        Assert.True(diagnostics.PureStateSyncDiagnostics.HasSourceSnapshot);
        Assert.False(diagnostics.PureStateSyncDiagnostics.AppliedPresentation);
    }

    [Fact]
    public void RemoteLatencyCompensationDiagnosticsAreProjectedFromHostFrame()
    {
        var remoteInput = new ShooterClientGatewayInputSubmitResult(
            new ShooterClientInputSubmitResult(1, 10, default),
            new ShooterGatewayBattleInputResult(
                success: false,
                acceptedFrame: 12,
                message: "Input frame is too far ahead.",
                currentFrame: 8,
                status: "RejectedTooFarFuture",
                shouldResync: true,
                serverTicks: 987654321L));
        var remoteDiagnostics = ShooterRemoteLatencyCompensationDiagnostics.FromGatewayInput(
            in remoteInput,
            hasPendingInput: true,
            hasQueuedInput: false,
            submittedCount: 5,
            queuedCount: 2,
            replacedCount: 1,
            completedCount: 4,
            failedCount: 0,
            resyncRequestedCount: 1);
        var frame = new ShooterHostPresentationFrame(
            ShooterSnapshotViewBatch.Empty,
            ShooterSnapshotViewBatch.Empty,
            false,
            controlledPlayerId: 1,
            worldScale: 1f,
            carrierNetworkStats: null,
            lastCarrierSnapshotApplyResult: ShooterSnapshotApplyResult.Ignored,
            lastCarrierTimeAnchor: default,
            localTimeAnchor: default,
            lagCompensationTelemetry: null,
            lagCompensationEvaluation: null,
            remoteLatencyCompensationDiagnostics: remoteDiagnostics,
            crossLayerDiagnostics: ShooterCrossLayerDiagnostics.From(
                default,
                ShooterSnapshotApplyResult.Ignored,
                remoteDiagnostics,
                needsPureStateBaselineResync: false,
                lastPureStateAppliedFrame: 0,
                lastPureStateResyncFrame: 0),
            pureStateSyncDiagnostics: default,
            needsPureStateBaselineResync: false,
            lastPureStateResyncReason: ShooterPureStateResyncReason.None,
            lastPureStateAppliedFrame: 0,
            lastPureStateAppliedStateHash: 0,
            lastPureStateResyncFrame: 0,
            lastPureStateResyncStateHash: 0);

        var diagnostics = ShooterHostDiagnosticsProjector.ProjectFromFrame(in frame, previousTotalEvents: 0);

        Assert.True(diagnostics.RemoteLatencyCompensationDiagnostics.HasResult);
        Assert.True(diagnostics.CrossLayerDiagnostics.HasSnapshotApplyResult);
        Assert.True(diagnostics.CrossLayerDiagnostics.HasRemoteLatencyResult);
        Assert.Equal(ShooterSnapshotApplyResult.Ignored, diagnostics.CrossLayerDiagnostics.SnapshotApplyResult);
        Assert.Equal(2, diagnostics.CrossLayerDiagnostics.RemoteInputDelayFrames);
        Assert.Equal(-2, diagnostics.CrossLayerDiagnostics.RemoteAuthoritativeFrameGap);
        Assert.Equal(10, diagnostics.RemoteLatencyCompensationDiagnostics.RequestedFrame);
        Assert.Equal(12, diagnostics.RemoteLatencyCompensationDiagnostics.AcceptedFrame);
        Assert.Equal(8, diagnostics.RemoteLatencyCompensationDiagnostics.AuthoritativeFrame);
        Assert.Equal(2, diagnostics.RemoteLatencyCompensationDiagnostics.InputDelayFrames);
        Assert.Equal(-2, diagnostics.RemoteLatencyCompensationDiagnostics.AuthoritativeFrameGap);
        Assert.True(diagnostics.RemoteLatencyCompensationDiagnostics.ShouldResync);
        Assert.Equal("RejectedTooFarFuture", diagnostics.RemoteLatencyCompensationDiagnostics.Status);
        Assert.Equal(987654321L, diagnostics.RemoteLatencyCompensationDiagnostics.ServerTicks);
        Assert.True(diagnostics.RemoteLatencyCompensationDiagnostics.HasPendingInput);
        Assert.False(diagnostics.RemoteLatencyCompensationDiagnostics.HasQueuedInput);
        Assert.Equal(5, diagnostics.RemoteLatencyCompensationDiagnostics.SubmittedCount);
        Assert.Equal(1, diagnostics.RemoteLatencyCompensationDiagnostics.ResyncRequestedCount);
    }

    private static bool TryGetTransformX(in ShooterSnapshotViewBatch batch, ShooterViewEntityKey key, out float x)
    {
        var transforms = batch.TransformChanges;
        for (var i = 0; i < transforms.Count; i++)
        {
            var transform = transforms[i];
            if (transform.Key.Equals(key))
            {
                x = transform.X;
                return true;
            }
        }

        x = 0f;
        return false;
    }

    private static int MaxEntities(IReadOnlyList<ShooterHostPresentationFrame> frames, ShooterViewEntityKind kind)
    {
        var max = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            max = Math.Max(max, CountEntities(frames[i].ClientBatch, kind));
        }

        return max;
    }

    private static int CountEntities(in ShooterSnapshotViewBatch batch, ShooterViewEntityKind kind)
    {
        var count = 0;
        var entityChanges = batch.EntityChanges;
        for (var i = 0; i < entityChanges.Count; i++)
        {
            var entity = entityChanges[i];
            if (entity.Alive && entity.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static bool ProjectionContainsEnemyAfterFrame(IReadOnlyList<ShooterHostPresentationFrame> frames, int minFrame)
    {
        var projection = new ShooterSnapshotViewProjection();
        var lastBatchKey = default(ViewBatchKey);
        var hasAppliedBatch = false;
        for (var i = 0; i < frames.Count; i++)
        {
            var batch = frames[i].ClientBatch;
            var batchKey = ViewBatchKey.From(in batch);
            if (!hasAppliedBatch || !batchKey.Equals(lastBatchKey))
            {
                projection.Apply(in batch);
                lastBatchKey = batchKey;
                hasAppliedBatch = true;
            }

            if (batch.Frame > minFrame && projection.Store.EnemyCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static ShooterSveltoGameplayScenarioConfig CreateCloseEnemyHitScenario(int tickRate)
    {
        return new ShooterSveltoGameplayScenarioConfig(
            "close-enemy-hit-regression",
            "Close Enemy Hit Regression",
            "Spawns a short-range one-health enemy so PlayMode can verify post-hit frame continuity.",
            shooterCount: 1,
            targetCount: 1,
            tickCount: tickRate * 2,
            tickDeltaTime: 1f / tickRate,
            arenaRadius: 8f,
            ShooterSveltoGameplayScenarioCatalog.DefaultLoadout,
            new ShooterSveltoGameplayBattleFlowConfig(
                durationFrames: tickRate * 2,
                victoryTargetDefeats: 2,
                maxActiveEnemies: 2,
                new[]
                {
                    new ShooterSveltoGameplayWaveConfig(
                        waveId: 1,
                        startFrame: 0,
                        spawnFrameInterval: 1,
                        enemyCount: 1,
                        enemyHp: 1,
                        spawnRadius: 2f),
                    new ShooterSveltoGameplayWaveConfig(
                        waveId: 2,
                        startFrame: tickRate,
                        spawnFrameInterval: 1,
                        enemyCount: 1,
                        enemyHp: tickRate * 4,
                        spawnRadius: 2f)
                },
                enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
                enemyAttackIntervalFrames: tickRate,
                enemyAttackDamage: 1,
                enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
                enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
                enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees));
    }

    private static bool ContainsEnemyHitEvent(in ShooterSnapshotViewBatch batch)
    {
        return TryGetEnemyHitEvent(in batch, out _);
    }

    private static bool TryGetEnemyHitEvent(in ShooterSnapshotViewBatch batch, out ShooterEventSnapshot hit)
    {
        var events = batch.Events;
        for (var i = 0; i < events.Count; i++)
        {
            if (IsEnemyHitEvent(events[i]))
            {
                hit = events[i];
                return true;
            }
        }

        hit = default;
        return false;
    }

    private static bool ContainsEnemyHitEvent(in ShooterStateSnapshotPayload snapshot)
    {
        var events = snapshot.Events;
        for (var i = 0; i < events.Length; i++)
        {
            if (IsEnemyHitEvent(events[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnemyHitEvent(in ShooterEventSnapshot candidate)
    {
        return candidate.EventType == (int)ShooterEventType.Hit && candidate.SourcePlayerId > 0 && candidate.TargetPlayerId < 0 && candidate.BulletId != 0;
    }

    private static bool TryCreateAimAtNearestEnemy(in ShooterStateSnapshotPayload snapshot, int playerId, out float aimX, out float aimY)
    {
        aimX = 0f;
        aimY = 1f;
        ShooterPlayerSnapshot? player = null;
        var players = snapshot.Players;
        for (var i = 0; i < players.Length; i++)
        {
            if (players[i].PlayerId == playerId)
            {
                player = players[i];
                break;
            }
        }

        if (!player.HasValue)
        {
            return false;
        }

        var bestDistanceSquared = float.MaxValue;
        ShooterEnemySnapshot? nearestEnemy = null;
        var enemies = snapshot.Enemies;
        for (var i = 0; i < enemies.Length; i++)
        {
            if (!enemies[i].Alive)
            {
                continue;
            }

            var dx = enemies[i].X - player.Value.X;
            var dy = enemies[i].Y - player.Value.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestEnemy = enemies[i];
            }
        }

        if (!nearestEnemy.HasValue || bestDistanceSquared <= 0.000001f)
        {
            return false;
        }

        var target = nearestEnemy.Value;
        var targetX = target.X - player.Value.X;
        var targetY = target.Y - player.Value.Y;
        var invLength = 1f / MathF.Sqrt(targetX * targetX + targetY * targetY);
        aimX = targetX * invLength;
        aimY = targetY * invLength;
        return true;
    }

    private static bool TryGetFireEvent(in ShooterStateSnapshotPayload snapshot, int sourcePlayerId, out ShooterEventSnapshot fire)
    {
        var events = snapshot.Events;
        for (var i = 0; i < events.Length; i++)
        {
            var candidate = events[i];
            if (candidate.EventType == (int)ShooterEventType.Fire && candidate.SourcePlayerId == sourcePlayerId)
            {
                fire = candidate;
                return true;
            }
        }

        fire = default;
        return false;
    }

    private static bool TryGetPlayer(in ShooterStateSnapshotPayload snapshot, int playerId, out ShooterSveltoPlayerComponent player)
    {
        var players = snapshot.Players;
        for (var i = 0; i < players.Length; i++)
        {
            var candidate = players[i];
            if (candidate.PlayerId != playerId)
            {
                continue;
            }

            player = new ShooterSveltoPlayerComponent
            {
                PlayerId = candidate.PlayerId,
                X = candidate.X,
                Y = candidate.Y,
                Hp = candidate.Hp,
                Score = candidate.Score,
                Alive = candidate.Alive
            };
            return true;
        }

        player = default;
        return false;
    }

    private static void AddRepeatedMovement(List<ShooterHostFrameInput> inputs, int repeats, int activeTicks, int restTicks)
    {
        for (var repeat = 0; repeat < repeats; repeat++)
        {
            AddInput(inputs, activeTicks, repeat % 2 == 0 ? 1f : -1f, 0f, 1f, 0f, false);
            AddIdle(inputs, restTicks);
        }
    }

    private static void AddIdle(List<ShooterHostFrameInput> inputs, int ticks)
    {
        AddInput(inputs, ticks, 0f, 0f, 1f, 0f, false);
    }

    private static void AddInput(List<ShooterHostFrameInput> inputs, int ticks, float moveX, float moveY, float aimX, float aimY, bool fire)
    {
        for (var i = 0; i < ticks; i++)
        {
            inputs.Add(new ShooterHostFrameInput(moveX, moveY, aimX, aimY, fire));
        }
    }

    private static ShooterHostPresentationFrame CreatePresentationFrame(in ShooterSnapshotViewBatch batch)
    {
        return new ShooterHostPresentationFrame(
            batch,
            ShooterSnapshotViewBatch.Empty,
            false,
            controlledPlayerId: 1,
            worldScale: 1f,
            carrierNetworkStats: null,
            lastCarrierSnapshotApplyResult: ShooterSnapshotApplyResult.Ignored,
            lastCarrierTimeAnchor: default,
            localTimeAnchor: default,
            lagCompensationTelemetry: null,
            lagCompensationEvaluation: null,
            remoteLatencyCompensationDiagnostics: default,
            crossLayerDiagnostics: default,
            pureStateSyncDiagnostics: default,
            needsPureStateBaselineResync: false,
            lastPureStateResyncReason: ShooterPureStateResyncReason.None,
            lastPureStateAppliedFrame: 0,
            lastPureStateAppliedStateHash: 0,
            lastPureStateResyncFrame: 0,
            lastPureStateResyncStateHash: 0);
    }

    private sealed class ScriptedInputSource : IShooterPlayInputSource
    {
        private readonly ShooterHostFrameInput[] _inputs;
        private int _index;

        public ScriptedInputSource(ShooterHostFrameInput[] inputs)
        {
            _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        }

        public ShooterPlayFrameInput ReadInput(int controlledPlayerId)
        {
            if (_index >= _inputs.Length)
            {
                return new ShooterPlayFrameInput(0f, 0f, 1f, 0f, false);
            }

            return new ShooterPlayFrameInput(_inputs[_index++]);
        }
    }

    private sealed class MutableInputSource : IShooterPlayInputSource
    {
        public MutableInputSource(ShooterHostFrameInput current)
        {
            Current = current;
        }

        public ShooterHostFrameInput Current { get; set; }

        public ShooterPlayFrameInput ReadInput(int controlledPlayerId)
        {
            return new ShooterPlayFrameInput(Current);
        }
    }

    private sealed class AggregatingViewSink : IShooterPlayViewSink
    {
        private readonly ShooterSnapshotViewProjection _projection = new();
        private ViewBatchKey _lastClientBatchKey;
        private bool _hasAppliedClientBatch;

        public int RenderCount { get; private set; }

        public int MaxPlayerCount { get; private set; }

        public int MaxEnemyCount { get; private set; }

        public int MaxBulletCount { get; private set; }

        public int ProjectedPlayerCount => _projection.Store.PlayerCount;

        public int ProjectedBulletCount => _projection.Store.BulletCount;

        public int ProjectedEnemyCount => _projection.Store.EnemyCount;

        public int TotalFireEvents { get; private set; }

        public int TotalExplicitEntityRemovals { get; private set; }

        public int TotalDeadEntityRemovals { get; private set; }

        public void Render(in ShooterHostPresentationFrame frame)
        {
            RenderCount++;
            var clientBatch = frame.ClientBatch;
            var batchKey = ViewBatchKey.From(in clientBatch);
            if (_hasAppliedClientBatch && batchKey.Equals(_lastClientBatchKey))
            {
                UpdateMaxProjectedCounts();
                return;
            }

            var result = _projection.Apply(in clientBatch);
            _lastClientBatchKey = batchKey;
            _hasAppliedClientBatch = true;
            UpdateMaxProjectedCounts();
            TotalFireEvents += CountEvents(clientBatch, ShooterEventType.Fire);
            TotalExplicitEntityRemovals += result.ExplicitEntityRemovals;
            TotalDeadEntityRemovals += result.DeadEntityRemovals;
        }

        public void Clear()
        {
            RenderCount = 0;
            MaxPlayerCount = 0;
            MaxEnemyCount = 0;
            MaxBulletCount = 0;
            TotalFireEvents = 0;
            TotalExplicitEntityRemovals = 0;
            TotalDeadEntityRemovals = 0;
            _lastClientBatchKey = default;
            _hasAppliedClientBatch = false;
            _projection.Clear();
        }

        private void UpdateMaxProjectedCounts()
        {
            MaxPlayerCount = Math.Max(MaxPlayerCount, _projection.Store.PlayerCount);
            MaxEnemyCount = Math.Max(MaxEnemyCount, _projection.Store.EnemyCount);
            MaxBulletCount = Math.Max(MaxBulletCount, _projection.Store.BulletCount);
        }

        private static int CountEvents(in ShooterSnapshotViewBatch batch, ShooterEventType eventType)
        {
            var count = 0;
            var events = batch.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].EventType == (int)eventType)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private readonly struct ViewBatchKey : IEquatable<ViewBatchKey>
    {
        private ViewBatchKey(ulong sequence, int frame, ShooterViewBatchSource source, ShooterViewSnapshotKind snapshotKind)
        {
            Sequence = sequence;
            Frame = frame;
            Source = source;
            SnapshotKind = snapshotKind;
        }

        private ulong Sequence { get; }

        private int Frame { get; }

        private ShooterViewBatchSource Source { get; }

        private ShooterViewSnapshotKind SnapshotKind { get; }

        public static ViewBatchKey From(in ShooterSnapshotViewBatch batch)
        {
            return new ViewBatchKey(batch.Sequence, batch.Frame, batch.Source, batch.SnapshotKind);
        }

        public bool Equals(ViewBatchKey other)
        {
            return Sequence == other.Sequence &&
                Frame == other.Frame &&
                Source == other.Source &&
                SnapshotKind == other.SnapshotKind;
        }

        public override bool Equals(object? obj)
        {
            return obj is ViewBatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Sequence, Frame, Source, SnapshotKind);
        }
    }

    private sealed class RecordingViewSink : IShooterPlayViewSink
    {
        private readonly List<ShooterHostPresentationFrame> _frames = new();

        public IReadOnlyList<ShooterHostPresentationFrame> Frames => _frames;

        public void Render(in ShooterHostPresentationFrame frame)
        {
            _frames.Add(frame);
        }

        public void Clear()
        {
            _frames.Clear();
        }
    }

    [Fact]
    public void RecordingViewSinkCollectsFramesAndCanBeCleared()
    {
        var sink = new RecordingViewSink();
        var frame = new ShooterHostPresentationFrame(
            ShooterSnapshotViewBatch.Empty,
            ShooterSnapshotViewBatch.Empty,
            false,
            controlledPlayerId: 7,
            worldScale: 1.25f,
            carrierNetworkStats: null,
            lastCarrierSnapshotApplyResult: ShooterSnapshotApplyResult.Ignored,
            lastCarrierTimeAnchor: default,
            localTimeAnchor: default,
            lagCompensationTelemetry: null,
            lagCompensationEvaluation: null,
            remoteLatencyCompensationDiagnostics: default,
            crossLayerDiagnostics: default,
            pureStateSyncDiagnostics: default,
            needsPureStateBaselineResync: false,
            lastPureStateResyncReason: ShooterPureStateResyncReason.None,
            lastPureStateAppliedFrame: 0,
            lastPureStateAppliedStateHash: 0,
            lastPureStateResyncFrame: 0,
            lastPureStateResyncStateHash: 0);

        sink.Render(in frame);

        var recordedFrame = Assert.Single(sink.Frames);
        Assert.Equal(7, recordedFrame.ControlledPlayerId);
        Assert.Equal(1.25f, recordedFrame.WorldScale);

        sink.Clear();

        Assert.Empty(sink.Frames);
    }
}

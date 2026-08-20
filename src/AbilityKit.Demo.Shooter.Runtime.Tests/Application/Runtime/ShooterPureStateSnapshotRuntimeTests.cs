using System.Linq;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterPureStateSnapshotRuntimeTests
{
    [Fact]
    public void PureStateWorldCacheReusesSameFrameScanAndInvalidatesOnMutation()
    {
        var runtime = CreateTransientRuntime();

        runtime.ExportPureStateSnapshotTransient(70ul, isFullBaseline: true);
        runtime.ExportPureStateSnapshotTransient(70ul, isFullBaseline: false);
        var cached = runtime.PureStateWorldCacheDiagnostics;

        Assert.Equal(1, cached.RebuildCount);
        Assert.Equal(1, cached.HitCount);
        Assert.True(cached.CachedEntityCount > 0);

        Assert.True(runtime.TryGetPlayer(1, out var player));
        player.X += 1f;
        runtime.SetPlayer(in player);
        runtime.ExportPureStateSnapshotTransient(70ul, isFullBaseline: false);

        Assert.Equal(2, runtime.PureStateWorldCacheDiagnostics.RebuildCount);
    }

    [Fact]
    public void PureStateTransientSnapshotReusesCapacityBuffers()
    {
        var runtime = CreateTransientRuntime();

        var first = runtime.ExportPureStateSnapshotTransient(75ul, isFullBaseline: true);
        var second = runtime.ExportPureStateSnapshotTransient(75ul, isFullBaseline: true);

        Assert.Same(first.Entities, second.Entities);
        Assert.Same(first.VisibilityHints, second.VisibilityHints);
        Assert.Equal(first.EffectiveEntityCount, second.EffectiveEntityCount);
        Assert.Equal(first.EffectiveVisibilityHintCount, second.EffectiveVisibilityHintCount);
    }

    [Fact]
    public void PureStateOwnedSnapshotDoesNotAliasTransientBuffers()
    {
        var runtime = CreateTransientRuntime();

        var transient = runtime.ExportPureStateSnapshotTransient(76ul, isFullBaseline: true);
        var owned = runtime.ExportPureStateSnapshot(76ul, isFullBaseline: true);

        Assert.NotSame(transient.Entities, owned.Entities);
        Assert.NotSame(transient.VisibilityHints, owned.VisibilityHints);
        Assert.Equal(owned.Entities, transient.Entities.AsSpan(0, transient.EffectiveEntityCount).ToArray());
        Assert.Equal(owned.VisibilityHints, transient.VisibilityHints.AsSpan(0, transient.EffectiveVisibilityHintCount).ToArray());
    }

    [Fact]
    public void PureStateTransientSnapshotCanBeSerializedBeforeBufferReuse()
    {
        var runtime = CreateTransientRuntime();
        var transient = runtime.ExportPureStateSnapshotTransient(77ul, isFullBaseline: true);

        var bytes = ShooterPureStateSyncCodec.Serialize(in transient);
        runtime.ExportPureStateSnapshotTransient(78ul, isFullBaseline: true);
        var decoded = ShooterPureStateSyncCodec.Deserialize(bytes);

        Assert.Equal(77ul, decoded.WorldId);
        Assert.Equal(transient.Frame, decoded.Frame);
        Assert.Equal(transient.StateHash, decoded.StateHash);
        Assert.Equal(transient.Entities.AsSpan(0, transient.EffectiveEntityCount).ToArray(), decoded.Entities);
        Assert.Equal(transient.VisibilityHints.AsSpan(0, transient.EffectiveVisibilityHintCount).ToArray(), decoded.VisibilityHints);
    }

    [Fact]
    public void PureStateSnapshotExportsPlayersAndProjectilesFromRuntime()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-smoke",
            30,
            7001,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));

        var payload = runtime.ExportPureStateSnapshot(77ul, isFullBaseline: true);
        var player = Assert.Single(payload.Entities, e => e.EntityKind == ShooterPackedEntityKinds.Player && e.EntityId == 1);
        var projectile = Assert.Single(payload.Entities, e => e.EntityKind == ShooterPackedEntityKinds.Projectile);

        Assert.Equal(ShooterPureStateSyncCodec.CurrentVersion, payload.Version);
        Assert.Equal(77ul, payload.WorldId);
        Assert.Equal(runtime.CurrentFrame, payload.Frame);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, payload.SnapshotKind);
        Assert.Equal(payload.Frame, payload.BaselineFrame);
        Assert.Equal(runtime.ComputeStateHash(), payload.BaselineHash);
        Assert.Equal(runtime.ComputeStateHash(), payload.StateHash);
        Assert.Equal(ShooterPureStateEntityLayers.KeyInteraction, player.EntityLayer);
        Assert.Equal(ShooterPureStateDeltaKinds.Spawn, player.DeltaKind);
        Assert.Equal(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible, player.Flags);
        Assert.Equal(ShooterPureStateEntityLayers.Combat, projectile.EntityLayer);
        Assert.Equal(ShooterPureStateDeltaKinds.Spawn, projectile.DeltaKind);
        Assert.Equal(1, projectile.OwnerId);
        Assert.Equal(payload.Entities.Length, payload.VisibilityHints.Length);
    }

    [Fact]
    public void PureStateDeltaSnapshotCarriesBaselineIdentityAndUpdateDeltas()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-delta",
            30,
            7002,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -1f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));
        var baseline = runtime.ExportPureStateSnapshot(88ul, isFullBaseline: true);

        runtime.SubmitInput(1, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false) });
        Assert.True(runtime.Tick(1f / 30f));

        var delta = runtime.ExportPureStateSnapshot(
            88ul,
            isFullBaseline: false,
            settings: ShooterPureStateSyncSettings.Default,
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);

        Assert.Equal(ShooterPureStateSnapshotKinds.Delta, delta.SnapshotKind);
        Assert.Equal(baseline.Frame, delta.BaselineFrame);
        Assert.Equal(baseline.StateHash, delta.BaselineHash);
        Assert.All(delta.Entities, entity => Assert.Equal(ShooterPureStateDeltaKinds.Update, entity.DeltaKind));
        Assert.Equal(runtime.CurrentFrame, delta.ServerTick);
        Assert.Equal(runtime.ComputeStateHash(), delta.StateHash);
    }

    [Fact]
    public void PureStateDeltaSnapshotRespectsActiveSyncBudgetAndKeepsPlayersFirst()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-budget",
            30,
            7003,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 10,
            activeSyncBudget: 2,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 10,
            interpolationDelayFrames: 3);

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        var baseline = runtime.ExportPureStateSnapshot(89ul, isFullBaseline: true, settings: settings);

        runtime.SubmitInput(1, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        var delta = runtime.ExportPureStateSnapshot(
            89ul,
            isFullBaseline: false,
            settings: settings,
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);

        Assert.Equal(2, delta.Entities.Length);
        Assert.All(delta.Entities, entity => Assert.Equal(ShooterPackedEntityKinds.Player, entity.EntityKind));
        Assert.Contains(delta.Entities, entity => entity.EntityId == 1);
        Assert.Contains(delta.Entities, entity => entity.EntityId == 2);
        Assert.Equal(delta.Entities.Length, delta.VisibilityHints.Length);
    }

    [Fact]
    public void PureStateDeltaEmitsProjectileDespawnOutsideActiveSyncBudget()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-despawn-budget",
            30,
            7010,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 10,
            activeSyncBudget: 1,
            baselineIntervalFrames: 300,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 300,
            interpolationDelayFrames: 1);

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));

        var baseline = runtime.ExportPureStateSnapshotTransient(95ul, isFullBaseline: true, settings: settings);
        var projectile = Assert.Single(
            baseline.Entities.Take(baseline.EffectiveEntityCount),
            entity => entity.EntityKind == ShooterPackedEntityKinds.Projectile);

        for (var i = 0; i < 300 && runtime.GetSnapshot().Bullets.Length > 0; i++)
        {
            Assert.True(runtime.Tick(1f / 30f));
        }

        Assert.Empty(runtime.GetSnapshot().Bullets);
        var delta = runtime.ExportPureStateSnapshotTransient(
            95ul,
            isFullBaseline: false,
            settings: settings,
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);
        var effectiveEntities = delta.Entities.Take(delta.EffectiveEntityCount).ToArray();
        var despawn = Assert.Single(
            effectiveEntities,
            entity => entity.EntityKind == ShooterPackedEntityKinds.Projectile &&
                entity.EntityId == projectile.EntityId &&
                entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn);

        Assert.Equal(0, despawn.Flags & ShooterPureStateEntityFlags.Alive);
        Assert.Equal(settings.ActiveSyncBudget + 1, effectiveEntities.Length);
        Assert.Equal(settings.ActiveSyncBudget, delta.EffectiveVisibilityHintCount);
    }

    [Fact]
    public void PureStateDeltaSnapshotMarksLowFrequencyFrames()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-low-frequency",
            30,
            7004,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 10,
            activeSyncBudget: 10,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 2,
            interpolationDelayFrames: 3);

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        var baseline = runtime.ExportPureStateSnapshot(90ul, isFullBaseline: true, settings: settings);

        runtime.SubmitInput(1, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        var lowFrequency = runtime.ExportPureStateSnapshot(
            90ul,
            isFullBaseline: false,
            settings: settings,
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);

        Assert.Equal(ShooterPureStateSnapshotKinds.LowFrequency, lowFrequency.SnapshotKind);
        var projectiles = lowFrequency.Entities.Where(entity => entity.EntityKind == ShooterPackedEntityKinds.Projectile).ToArray();
        Assert.NotEmpty(projectiles);
        Assert.All(projectiles, projectile => Assert.True((projectile.Flags & ShooterPureStateEntityFlags.LowFrequency) != 0));
        Assert.Contains(lowFrequency.VisibilityHints, hint => hint.EntityKind == ShooterPackedEntityKinds.Projectile && (hint.Flags & ShooterPureStateEntityFlags.LowFrequency) != 0);
    }

    [Fact]
    public void PureStateSnapshotBudgetProfilesKeepPayloadAndVisibilityHintsAligned()
    {
        var profiles = new[]
        {
            (Name: "small", PlayerCount: 2, ActiveSyncBudget: 4, ExpectedEntities: 4),
            (Name: "medium", PlayerCount: 4, ActiveSyncBudget: 8, ExpectedEntities: 8),
            (Name: "mass", PlayerCount: 8, ActiveSyncBudget: 16, ExpectedEntities: 16)
        };

        foreach (var profile in profiles)
        {
            var runtime = new ShooterBattleRuntimePort();
            var players = Enumerable.Range(1, profile.PlayerCount)
                .Select(index => new ShooterStartPlayer(index, $"P{index}", index * 2f, 0f))
                .ToArray();
            var settings = new ShooterPureStateSyncSettings(
                maxEntityCount: ShooterEntityLimitOptions.DefaultMaxEntityCount,
                activeSyncBudget: profile.ActiveSyncBudget,
                baselineIntervalFrames: 60,
                deltaIntervalFrames: 1,
                lowFrequencyIntervalFrames: 10,
                interpolationDelayFrames: 3);
            var start = new ShooterStartGamePayload(
                $"pure-state-{profile.Name}-budget",
                30,
                7100 + profile.PlayerCount,
                players);

            Assert.True(runtime.StartGame(in start));
            var commands = players
                .Select(player => new ShooterPlayerCommand(player.PlayerId, 0f, 0f, 0f, 1f, true))
                .ToArray();
            runtime.SubmitInput(runtime.CurrentFrame, commands);
            Assert.True(runtime.Tick(1f / 30f));

            var payload = runtime.ExportPureStateSnapshot(100ul + (ulong)profile.PlayerCount, isFullBaseline: false, settings: settings);

            Assert.Equal(profile.ExpectedEntities, payload.Entities.Length);
            Assert.Equal(payload.Entities.Length, payload.VisibilityHints.Length);
            Assert.Equal(profile.PlayerCount, payload.Entities.Count(entity => entity.EntityKind == ShooterPackedEntityKinds.Player));
            Assert.Equal(profile.PlayerCount, payload.Entities.Count(entity => entity.EntityKind == ShooterPackedEntityKinds.Projectile));
            Assert.All(payload.VisibilityHints, hint => Assert.True(hint.Priority > 0));
        }
    }

    [Fact]
    public void PureStateSnapshotAppliesInterestScopeBeforeBudgetCut()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi",
            30,
            7005,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 40f, 0f),
                new ShooterStartPlayer(3, "P3", 2f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 20,
            activeSyncBudget: 20,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 10,
            interpolationDelayFrames: 3);
        var scope = new ShooterPureStateInterestScope(
            observerPlayerId: 1,
            centerX: 0f,
            centerY: 0f,
            radius: 8f,
            maxEntities: 2);

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(2, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));

        var payload = runtime.ExportPureStateSnapshot(91ul, isFullBaseline: false, settings: settings, interestScope: scope);

        Assert.Equal(2, payload.Entities.Length);
        Assert.Equal(1, payload.Entities[0].EntityId);
        var observer = Assert.Single(payload.Entities, entity => entity.EntityId == 1);
        Assert.True((observer.Flags & ShooterPureStateEntityFlags.PredictedLocal) != 0);
        var observerHint = Assert.Single(payload.VisibilityHints, hint => hint.EntityId == 1);
        Assert.True((observerHint.Flags & ShooterPureStateEntityFlags.PredictedLocal) != 0);
        Assert.Contains(payload.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 3);
        Assert.DoesNotContain(
            payload.Entities,
            entity => entity.EntityId != 1 && (entity.Flags & ShooterPureStateEntityFlags.PredictedLocal) != 0);
        Assert.DoesNotContain(payload.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 2);
        Assert.DoesNotContain(payload.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Projectile);
        Assert.Equal(payload.Entities.Length, payload.VisibilityHints.Length);
        Assert.All(payload.VisibilityHints, hint => Assert.True(hint.Priority > 0));
    }

    [Fact]
    public void PureStateSnapshotAoiLifecycleUsesVisibleAndBoundaryRadii()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-lifecycle",
            30,
            7006,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 10,
            activeSyncBudget: 10,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 10,
            interpolationDelayFrames: 3);
        var aoi = new AoiInterestSet();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));

        var enterScope = new ShooterPureStateInterestScope(1, 0f, 0f, visibleRadius: 5f, boundaryRadius: 8f, maxEntities: 10);
        var enter = runtime.ExportPureStateSnapshot(92ul, isFullBaseline: false, settings: settings, interestScope: enterScope, aoiInterestSet: aoi);
        Assert.Contains(enter.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Spawn);

        Assert.True(runtime.TryGetPlayer(2, out var changedTarget));
        changedTarget.Score++;
        runtime.SetPlayer(in changedTarget);
        Assert.True(runtime.Tick(1f / 30f));
        var stayScope = new ShooterPureStateInterestScope(1, 0f, 0f, visibleRadius: 3f, boundaryRadius: 8f, maxEntities: 10);
        var stay = runtime.ExportPureStateSnapshot(92ul, isFullBaseline: false, settings: settings, interestScope: stayScope, aoiInterestSet: aoi);
        Assert.Contains(stay.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Update);

        var leaveScope = new ShooterPureStateInterestScope(1, 0f, 0f, visibleRadius: 3f, boundaryRadius: 3f, maxEntities: 1);
        var leave = runtime.ExportPureStateSnapshot(92ul, isFullBaseline: false, settings: settings, interestScope: leaveScope, aoiInterestSet: aoi);
        var despawn = Assert.Single(leave.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn);
        Assert.Equal(ShooterPureStateEntityLayers.KeyInteraction, despawn.EntityLayer);
        Assert.Equal(2, despawn.OwnerId);
        Assert.DoesNotContain(leave.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 1);
        Assert.Empty(leave.VisibilityHints);

        var reenter = runtime.ExportPureStateSnapshot(92ul, isFullBaseline: false, settings: settings, interestScope: enterScope, aoiInterestSet: aoi);
        Assert.Contains(reenter.Entities, entity => entity.EntityKind == ShooterPackedEntityKinds.Player && entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Spawn);
    }

    [Fact]
    public void PureStateSnapshotAoiBudgetRotatesFirstSpawnsWithoutStarvation()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-budget-rotation",
            30,
            7007,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 1f, 0f),
                new ShooterStartPlayer(3, "P3", 2f, 0f),
                new ShooterStartPlayer(4, "P4", 3f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 10,
            activeSyncBudget: 1,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 10,
            interpolationDelayFrames: 3);
        var scope = new ShooterPureStateInterestScope(1, 0f, 0f, visibleRadius: 8f, boundaryRadius: 10f, maxEntities: 1);
        var aoi = new AoiInterestSet();
        var firstReplications = new HashSet<int>();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));

        for (var i = 0; i < 4; i++)
        {
            var payload = runtime.ExportPureStateSnapshot(93ul, isFullBaseline: false, settings: settings, interestScope: scope, aoiInterestSet: aoi);
            var entity = Assert.Single(payload.Entities);
            Assert.Equal(ShooterPureStateDeltaKinds.Spawn, entity.DeltaKind);
            Assert.True(firstReplications.Add(entity.EntityId));
        }

        Assert.Equal(new[] { 1, 2, 3, 4 }, firstReplications.Order().ToArray());
        var next = runtime.ExportPureStateSnapshot(93ul, isFullBaseline: false, settings: settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Empty(next.Entities);
    }

    [Fact]
    public void PureStateSnapshotAoiAppliesNearMidFarUpdateIntervals()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-distance-lod",
            30,
            7011,
            new[]
            {
                new ShooterStartPlayer(1, "Observer", 0f, 0f),
                new ShooterStartPlayer(2, "Near", 2f, 0f),
                new ShooterStartPlayer(3, "Mid", 5f, 0f),
                new ShooterStartPlayer(4, "Far", 8f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(20, 20, 450, 1, 90, 3, 10, 30, 90);
        var scope = new ShooterPureStateInterestScope(1, 0f, 0f, 9f, 12f, 20);
        var aoi = new AoiInterestSet();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));
        var initial = runtime.ExportPureStateSnapshot(96ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.All(initial.Entities, entity => Assert.Equal(ShooterPureStateDeltaKinds.Spawn, entity.DeltaKind));

        var updateFrames = new Dictionary<int, List<int>>
        {
            [2] = new List<int>(),
            [3] = new List<int>(),
            [4] = new List<int>()
        };
        while (runtime.CurrentFrame < 91)
        {
            foreach (var playerId in updateFrames.Keys)
            {
                Assert.True(runtime.TryGetPlayer(playerId, out var player));
                player.X += 0.01f;
                runtime.SetPlayer(in player);
            }
            Assert.True(runtime.Tick(1f / 30f));
            var payload = runtime.ExportPureStateSnapshot(96ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
            foreach (var entity in payload.Entities.Where(entity => updateFrames.ContainsKey(entity.EntityId)))
            {
                Assert.Equal(ShooterPureStateDeltaKinds.Update, entity.DeltaKind);
                Assert.True((entity.Flags & ShooterPureStateEntityFlags.LowFrequency) != 0);
                var hint = Assert.Single(payload.VisibilityHints, value => value.EntityId == entity.EntityId);
                Assert.True((hint.Flags & ShooterPureStateEntityFlags.LowFrequency) != 0);
                updateFrames[entity.EntityId].Add(payload.Frame);
            }
        }

        Assert.Equal(Enumerable.Range(1, 9).Select(index => 1 + index * 10), updateFrames[2]);
        Assert.Equal(new[] { 31, 61, 91 }, updateFrames[3]);
        Assert.Equal(new[] { 91 }, updateFrames[4]);
    }

    [Fact]
    public void PureStateSnapshotAoiSuppressesUnchangedUpdatesUntilPeriodicRefresh()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-unchanged-suppression",
            30,
            7013,
            new[]
            {
                new ShooterStartPlayer(1, "Observer", 0f, 0f),
                new ShooterStartPlayer(2, "Static", 2f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(10, 10, 20, 1, 20, 3, 1, 1, 1);
        var scope = new ShooterPureStateInterestScope(1, 0f, 0f, 5f, 8f, 10);
        var aoi = new AoiInterestSet();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));
        var initial = runtime.ExportPureStateSnapshot(98ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Equal(2, initial.EffectiveEntityCount);

        while (runtime.CurrentFrame < 20)
        {
            Assert.True(runtime.Tick(1f / 30f));
            var suppressed = runtime.ExportPureStateSnapshot(98ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
            Assert.Equal(0, suppressed.EffectiveEntityCount);
        }

        Assert.True(runtime.Tick(1f / 30f));
        var refresh = runtime.ExportPureStateSnapshot(98ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Equal(21, refresh.Frame);
        Assert.Equal(2, refresh.EffectiveEntityCount);
        Assert.All(refresh.Entities.Take(refresh.EffectiveEntityCount), entity => Assert.Equal(ShooterPureStateDeltaKinds.Update, entity.DeltaKind));
    }

    [Fact]
    public void PureStateSnapshotAoiLifecycleBypassesDistanceLodThrottle()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-lod-lifecycle",
            30,
            7012,
            new[]
            {
                new ShooterStartPlayer(1, "Observer", 0f, 0f),
                new ShooterStartPlayer(2, "Target", 20f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(10, 10, 450, 1, 90, 3, 90, 90, 90);
        var scope = new ShooterPureStateInterestScope(1, 0f, 0f, 5f, 8f, 10);
        var aoi = new AoiInterestSet();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));
        runtime.ExportPureStateSnapshot(97ul, false, settings, interestScope: scope, aoiInterestSet: aoi);

        Assert.True(runtime.TryGetPlayer(2, out var target));
        target.X = 4f;
        runtime.SetPlayer(in target);
        var enter = runtime.ExportPureStateSnapshot(97ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Contains(enter.Entities, entity => entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Spawn);

        var throttledStay = runtime.ExportPureStateSnapshot(97ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.DoesNotContain(throttledStay.Entities, entity => entity.EntityId == 2);

        target.X = 9f;
        runtime.SetPlayer(in target);
        var leave = runtime.ExportPureStateSnapshot(97ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Contains(leave.Entities, entity => entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn);

        target.X = 4f;
        runtime.SetPlayer(in target);
        var reenter = runtime.ExportPureStateSnapshot(97ul, false, settings, interestScope: scope, aoiInterestSet: aoi);
        Assert.Contains(reenter.Entities, entity => entity.EntityId == 2 && entity.DeltaKind == ShooterPureStateDeltaKinds.Spawn);
    }

    [Fact]
    public void PureStateSnapshotAoiDoesNotDespawnEntitiesThatWereNeverReplicated()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-aoi-unsent-leave",
            30,
            7008,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 1f, 0f),
                new ShooterStartPlayer(3, "P3", 2f, 0f)
            });
        var settings = new ShooterPureStateSyncSettings(10, 1, 60, 1, 10, 3);
        var visibleScope = new ShooterPureStateInterestScope(1, 0f, 0f, 8f, 10f, 1);
        var outsideScope = new ShooterPureStateInterestScope(1, 100f, 100f, 1f, 1f, 1);
        var aoi = new AoiInterestSet();

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));

        var first = runtime.ExportPureStateSnapshot(94ul, false, settings, interestScope: visibleScope, aoiInterestSet: aoi);
        var replicated = Assert.Single(first.Entities);
        Assert.Equal(ShooterPureStateDeltaKinds.Spawn, replicated.DeltaKind);

        var leave = runtime.ExportPureStateSnapshot(94ul, false, settings, interestScope: outsideScope, aoiInterestSet: aoi);
        var despawn = Assert.Single(leave.Entities);
        Assert.Equal(replicated.EntityId, despawn.EntityId);
        Assert.Equal(ShooterPureStateDeltaKinds.Despawn, despawn.DeltaKind);
    }

    private static ShooterBattleRuntimePort CreateTransientRuntime()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-transient",
            30,
            7099,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        return runtime;
    }
}

using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Presentation;

[Collection(ShooterSnapshotViewProjectionTestCollection.Name)]
public sealed class ShooterSnapshotViewProjectionTests
{
    [Fact]
    public void DenseStoreSwapBackKeepsMovedEntityAndTransformAligned()
    {
        var store = new ShooterViewEntityStore();
        var first = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 1);
        var second = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2);
        store.UpsertEntity(new ShooterViewEntityChange(first, 0, alive: true));
        store.UpsertEntity(new ShooterViewEntityChange(second, 0, alive: true));
        store.UpsertTransform(new ShooterViewTransformComponentChange(first, 1f, 2f, 1f, 0f, 0f, 0f));
        store.UpsertTransform(new ShooterViewTransformComponentChange(second, 3f, 4f, 0f, 1f, 0f, 0f));

        Assert.True(store.RemoveEntity(first));

        Assert.Equal(1, store.DenseCount);
        Assert.True(store.TryGetDenseEntityAndTransform(0, out var entity, out var transform));
        Assert.Equal(second, entity.Key);
        Assert.Equal(3f, transform.X);
        Assert.Equal(4f, transform.Y);
    }

    [Fact]
    public void FullSnapshotProjectsEntitiesAndSeparatedComponentsIntoStore()
    {
        var projection = new ShooterSnapshotViewProjection();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var batch = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 12,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[]
            {
                new ShooterViewEntityChange(player, ownerEntityId: 0, alive: true),
                new ShooterViewEntityChange(bullet, ownerEntityId: 1, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(player, 1f, 2f, 1f, 0f, 0.5f, 0.25f),
                new ShooterViewTransformComponentChange(bullet, 3f, 4f, 0f, 1f, 8f, 9f)
            },
            new[] { new ShooterViewHealthComponentChange(player, 80) },
            new[] { new ShooterViewScoreComponentChange(player, 7) },
            new[] { new ShooterViewProjectileLifetimeComponentChange(bullet, 19) },
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in batch);

        Assert.Equal(2, projection.Store.EntityCount);
        Assert.Equal(1, projection.Store.PlayerCount);
        Assert.Equal(1, projection.Store.BulletCount);
        Assert.True(projection.Store.TryGetTransform(player, out var playerTransform));
        Assert.Equal(1f, playerTransform.X);
        Assert.Equal(2f, playerTransform.Y);
        Assert.True(projection.Store.TryGetHealth(player, out var health));
        Assert.Equal(80, health.Hp);
        Assert.True(projection.Store.TryGetScore(player, out var score));
        Assert.Equal(7, score.Score);
        Assert.True(projection.Store.TryGetProjectileLifetime(bullet, out var lifetime));
        Assert.Equal(19, lifetime.RemainingFrames);
    }

    [Fact]
    public void LocalAuthoritativePlayerComponentDeltaRecoversMissingPlayerEntity()
    {
        var projection = new ShooterSnapshotViewProjection();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var batch = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 12,
            sequence: 9ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.LocalAuthoritative,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(player, 1.5f, -2.5f, 0f, 1f, 0.25f, -0.5f),
                new ShooterViewTransformComponentChange(bullet, 3f, 4f, 0f, 1f, 8f, 9f)
            },
            new[] { new ShooterViewHealthComponentChange(player, 90) },
            new[] { new ShooterViewScoreComponentChange(player, 11) },
            new[] { new ShooterViewProjectileLifetimeComponentChange(bullet, 18) },
            Array.Empty<ShooterEventSnapshot>());

        var result = projection.Apply(in batch);

        Assert.Equal(1, result.AddedEntities);
        Assert.Equal(1, result.FinalPlayerCount);
        Assert.Equal(1, projection.Store.EntityCount);
        Assert.Equal(1, projection.Store.PlayerCount);
        Assert.Equal(0, projection.Store.BulletCount);
        Assert.True(projection.Store.ContainsEntity(player));
        Assert.True(projection.Store.TryGetTransform(player, out var transform));
        Assert.Equal(1.5f, transform.X);
        Assert.Equal(-2.5f, transform.Y);
        Assert.True(projection.Store.TryGetHealth(player, out var health));
        Assert.Equal(90, health.Hp);
        Assert.True(projection.Store.TryGetScore(player, out var score));
        Assert.Equal(11, score.Score);
        Assert.False(projection.Store.ContainsEntity(bullet));
        Assert.False(projection.Store.TryGetTransform(bullet, out _));
        Assert.False(projection.Store.TryGetProjectileLifetime(bullet, out _));
    }

    [Fact]
    public void PackedEnemySnapshotProjectsEnemyTransformAndHealthIntoStore()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 88ul,
            frame: 6,
            serverTick: 6L,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.EntityLifecycle,
                    ShooterPackedEntityKinds.Enemy,
                    count: 1,
                    entityIds: new[] { 10001 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: Array.Empty<int>(),
                    flags: new[] { ShooterPackedEntityFlags.Alive },
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>()),
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.Transform,
                    ShooterPackedEntityKinds.Enemy,
                    count: 1,
                    entityIds: new[] { 10001 },
                    valueX: new[] { 7f },
                    valueY: new[] { -3f },
                    valueZ: new[] { -1f },
                    valueW: new[] { 0f },
                    intValues: Array.Empty<int>(),
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: new[] { 0, 0 }),
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.Health,
                    ShooterPackedEntityKinds.Enemy,
                    count: 1,
                    entityIds: new[] { 10001 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: new[] { 2 },
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>())
            });

        var batch = mapper.Map(in packed);
        projection.Apply(in batch);

        var enemy = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 10001);
        Assert.Equal(1, projection.Store.EnemyCount);
        Assert.True(projection.Store.TryGetTransform(enemy, out var transform));
        Assert.Equal(7f, transform.X);
        Assert.Equal(-3f, transform.Y);
        Assert.True(projection.Store.TryGetHealth(enemy, out var health));
        Assert.Equal(2, health.Hp);
    }

    [Fact]
    public void GatewayActorSnapshotKeepsControlledPlayerTransform()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var gatewaySnapshot = new ShooterGatewaySnapshot(
            worldId: 77ul,
            frame: 2,
            timestamp: 2d,
            serverTicks: 2L,
            isFullSnapshot: true,
            actors: new[] { new ShooterGatewayActorSnapshot(actorId: 1, x: 2.5f, y: -1.5f, rotation: 0f, velocityX: 0.25f, velocityY: -0.5f, hp: 100f, hpMax: 100f, teamId: 1) });

        var batch = mapper.Map(in gatewaySnapshot, controlledPlayerId: 1);
        projection.Apply(in batch);

        var controlledPlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        Assert.True(projection.Store.ContainsEntity(controlledPlayer));
        Assert.True(projection.Store.TryGetTransform(controlledPlayer, out var transform));
        Assert.Equal(2.5f, transform.X);
        Assert.Equal(-1.5f, transform.Y);
    }

    [Fact]
    public void PackedSnapshotKeepsControlledPlayerTransform()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 88ul,
            frame: 6,
            serverTick: 6L,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.EntityLifecycle,
                    ShooterPackedEntityKinds.Player,
                    count: 1,
                    entityIds: new[] { 1 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: Array.Empty<int>(),
                    flags: new[] { ShooterPackedEntityFlags.Alive },
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>()),
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.Transform,
                    ShooterPackedEntityKinds.Player,
                    count: 1,
                    entityIds: new[] { 1 },
                    valueX: new[] { 2.5f },
                    valueY: new[] { -1.5f },
                    valueZ: new[] { 0f },
                    valueW: new[] { 1f },
                    intValues: Array.Empty<int>(),
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: new[] { 2500, -5000 }),
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.Health,
                    ShooterPackedEntityKinds.Player,
                    count: 1,
                    entityIds: new[] { 1 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: new[] { 100 },
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>())
            });
        var gatewaySnapshot = new ShooterGatewaySnapshot(
            worldId: 88ul,
            frame: 6,
            timestamp: 2d,
            serverTicks: 6L,
            isFullSnapshot: true,
            actors: Array.Empty<ShooterGatewayActorSnapshot>(),
            payloadOpCode: ShooterOpCodes.Snapshot.PackedState,
            packedSnapshot: packed);

        var batch = mapper.Map(in gatewaySnapshot, controlledPlayerId: 1);
        projection.Apply(in batch);

        var controlledPlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        Assert.True(projection.Store.ContainsEntity(controlledPlayer));
        Assert.True(projection.Store.TryGetTransform(controlledPlayer, out var transform));
        Assert.Equal(2.5f, transform.X);
        Assert.Equal(-1.5f, transform.Y);
    }

    [Fact]
    public void PureStateSnapshotKeepsControlledPlayerTransform()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var pureState = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            worldId: 99ul,
            frame: 12,
            serverTick: 12L,
            snapshotKind: ShooterPureStateSnapshotKinds.FullBaseline,
            baselineFrame: 12,
            stateHash: 111u,
            baselineHash: 111u,
            settings: ShooterPureStateSyncSettings.Default,
            entities: new[]
            {
                new ShooterPureStateEntityDelta(
                    1,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Spawn,
                    ownerId: 0,
                    quantizedX: 2500,
                    quantizedY: -1500,
                    quantizedVelocityX: 0,
                    quantizedVelocityY: 0,
                    quantizedFacingX: 250,
                    quantizedFacingY: -500,
                    hp: 100,
                    score: 3,
                    remainingFrames: 0,
                    flags: ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible)
            },
            visibilityHints: Array.Empty<ShooterPureStateVisibilityHint>());

        var batch = mapper.Map(in pureState, controlledPlayerId: 1);
        projection.Apply(in batch);

        var controlledPlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        Assert.True(projection.Store.ContainsEntity(controlledPlayer));
        Assert.True(projection.Store.TryGetTransform(controlledPlayer, out var transform));
        Assert.Equal(2.5f, transform.X);
        Assert.Equal(-1.5f, transform.Y);
    }

    [Fact]
    public void PureStateDespawnRemovesPlayerWithoutRecoveringItFromResidualComponents()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var spawn = CreatePureStateSnapshot(
            frame: 10,
            settings: ShooterPureStateSyncSettings.Default,
            entities: new[]
            {
                new ShooterPureStateEntityDelta(
                    2,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    ShooterPureStateDeltaKinds.Spawn,
                    ownerId: 2,
                    quantizedX: 2500,
                    quantizedY: -1500,
                    quantizedVelocityX: 0,
                    quantizedVelocityY: 0,
                    quantizedFacingX: 250,
                    quantizedFacingY: -500,
                    hp: 100,
                    score: 3,
                    remainingFrames: 0,
                    flags: ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible)
            });
        var despawn = CreatePureStateSnapshot(
            frame: 11,
            settings: ShooterPureStateSyncSettings.Default,
            entities: new[]
            {
                new ShooterPureStateEntityDelta(
                    2,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    ShooterPureStateDeltaKinds.Despawn,
                    ownerId: 2,
                    quantizedX: 2500,
                    quantizedY: -1500,
                    quantizedVelocityX: 0,
                    quantizedVelocityY: 0,
                    quantizedFacingX: 250,
                    quantizedFacingY: -500,
                    hp: 100,
                    score: 3,
                    remainingFrames: 0,
                    flags: 0)
            },
            snapshotKind: ShooterPureStateSnapshotKinds.Delta);

        var spawnBatch = mapper.Map(in spawn);
        projection.Apply(in spawnBatch);
        var despawnBatch = mapper.Map(in despawn);
        var result = projection.Apply(in despawnBatch);

        Assert.Single(despawnBatch.RemovedEntities);
        Assert.Contains(player, despawnBatch.RemovedEntities);
        Assert.Empty(despawnBatch.EntityChanges);
        Assert.Empty(despawnBatch.TransformChanges);
        Assert.Empty(despawnBatch.HealthChanges);
        Assert.Empty(despawnBatch.ScoreChanges);
        Assert.Equal(1, result.ExplicitEntityRemovals);
        Assert.False(projection.Store.ContainsEntity(player));
        Assert.False(projection.Store.TryGetTransform(player, out _));
        Assert.False(projection.Store.TryGetHealth(player, out _));
        Assert.False(projection.Store.TryGetScore(player, out _));
    }

    [Fact]
    public void PureStateSettingsConfigurePresentationInterpolationDelay()
    {
        var presentation = new ShooterPresentationFacade();
        var snapshot = CreatePureStateSnapshot(
            frame: 1,
            settings: new ShooterPureStateSyncSettings(100, 50, 60, 2, 10, 7),
            entities: Array.Empty<ShooterPureStateEntityDelta>());

        presentation.ApplyPureStateSnapshot(in snapshot);

        Assert.Equal(7f, presentation.Snapshots.InterpolationDelayFrames);
    }

    [Fact]
    public void PureStatePredictedLocalSkipsControlledAuthoritativeTransform()
    {
        var mapper = new ShooterSnapshotViewModelMapper();
        var projection = new ShooterSnapshotViewProjection();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var predicted = new ShooterSnapshotViewBatch(
            worldId: 99ul,
            frame: 11,
            sequence: 11ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.LocalPrediction,
            new[] { new ShooterViewEntityChange(player, ownerEntityId: 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            new[] { new ShooterViewTransformComponentChange(player, 9f, 4f, 1f, 0f, 0f, 0f) },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var snapshot = CreatePureStateSnapshot(
            frame: 12,
            settings: ShooterPureStateSyncSettings.Default,
            entities: new[]
            {
                new ShooterPureStateEntityDelta(
                    1,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Spawn,
                    ownerId: 0,
                    quantizedX: 2500,
                    quantizedY: -1500,
                    quantizedVelocityX: 0,
                    quantizedVelocityY: 0,
                    quantizedFacingX: 250,
                    quantizedFacingY: -500,
                    hp: 100,
                    score: 3,
                    remainingFrames: 0,
                    flags: ShooterPureStateEntityFlags.Alive |
                        ShooterPureStateEntityFlags.Visible |
                        ShooterPureStateEntityFlags.PredictedLocal)
            });

        projection.Apply(in predicted);
        var batch = mapper.Map(in snapshot, controlledPlayerId: 1);
        projection.Apply(in batch);

        Assert.Single(batch.EntityChanges);
        Assert.Single(batch.HealthChanges);
        Assert.Single(batch.ScoreChanges);
        Assert.Empty(batch.TransformChanges);
        Assert.True(projection.Store.TryGetTransform(player, out var transform));
        Assert.Equal(9f, transform.X);
        Assert.Equal(4f, transform.Y);
        Assert.True(projection.Store.TryGetHealth(player, out var health));
        Assert.Equal(100, health.Hp);
    }

    [Fact]
    public void PureStateLowFrequencyFlagFlowsToTransformChange()
    {
        var mapper = new ShooterSnapshotViewModelMapper();
        var snapshot = CreatePureStateSnapshot(
            frame: 12,
            settings: ShooterPureStateSyncSettings.Default,
            entities: new[]
            {
                new ShooterPureStateEntityDelta(
                    2001,
                    ShooterPackedEntityKinds.Enemy,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Spawn,
                    ownerId: 0,
                    quantizedX: 2500,
                    quantizedY: -1500,
                    quantizedVelocityX: 0,
                    quantizedVelocityY: 0,
                    quantizedFacingX: 0,
                    quantizedFacingY: 0,
                    hp: 100,
                    score: 0,
                    remainingFrames: 0,
                    flags: ShooterPureStateEntityFlags.Alive |
                        ShooterPureStateEntityFlags.Visible |
                        ShooterPureStateEntityFlags.LowFrequency)
            });

        var batch = mapper.Map(in snapshot);

        Assert.Single(batch.TransformChanges);
        Assert.True(batch.TransformChanges[0].IsLowFrequency);
    }

    [Fact]
    public void PureStateVisibilityChangeRemovesHiddenEntityFromProjection()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var key = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2001);
        var visible = CreatePureStateVisibilitySnapshot(
            frame: 1,
            deltaKind: ShooterPureStateDeltaKinds.Spawn,
            flags: ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible);
        var hidden = CreatePureStateVisibilitySnapshot(
            frame: 2,
            deltaKind: ShooterPureStateDeltaKinds.VisibilityChange,
            flags: ShooterPureStateEntityFlags.Alive);

        var visibleBatch = mapper.Map(in visible);
        projection.Apply(in visibleBatch);
        var hiddenBatch = mapper.Map(in hidden);
        var result = projection.Apply(in hiddenBatch);

        Assert.True(visibleBatch.EntityChanges[0].Alive);
        Assert.False(hiddenBatch.EntityChanges[0].Alive);
        Assert.Equal(1, result.DeadEntityRemovals);
        Assert.False(projection.Store.ContainsEntity(key));
    }

    [Fact]
    public void DeltaSnapshotUpdatesComponentsWithoutRemovingMissingEntities()
    {
        var projection = new ShooterSnapshotViewProjection();
        var playerOne = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var playerTwo = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var full = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[]
            {
                new ShooterViewEntityChange(playerOne, 0, alive: true),
                new ShooterViewEntityChange(playerTwo, 0, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(playerOne, 0f, 0f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(playerTwo, 10f, 0f, 1f, 0f, 0f, 0f)
            },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var delta = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 2,
            sequence: 2ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.LocalPrediction,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            new[] { new ShooterViewTransformComponentChange(playerOne, 2f, 3f, 0f, 1f, 4f, 5f) },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in full);
        projection.Apply(in delta);

        Assert.Equal(2, projection.Store.PlayerCount);
        Assert.True(projection.Store.TryGetTransform(playerOne, out var playerOneTransform));
        Assert.Equal(2f, playerOneTransform.X);
        Assert.Equal(3f, playerOneTransform.Y);
        Assert.True(projection.Store.TryGetTransform(playerTwo, out var playerTwoTransform));
        Assert.Equal(10f, playerTwoTransform.X);
    }

    [Fact]
    public void PackedDeltaSnapshotDoesNotRemoveMissingEntities()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var playerOne = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var playerTwo = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var full = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[]
            {
                new ShooterViewEntityChange(playerOne, 0, alive: true),
                new ShooterViewEntityChange(playerTwo, 0, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(playerOne, 0f, 0f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(playerTwo, 10f, 0f, 1f, 0f, 0f, 0f)
            },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var packedDelta = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 77ul,
            frame: 2,
            serverTick: 2L,
            snapshotFlags: ShooterPackedSnapshotFlags.Delta,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.Transform,
                    ShooterPackedEntityKinds.Player,
                    count: 1,
                    entityIds: new[] { 1 },
                    valueX: new[] { 2f },
                    valueY: new[] { 3f },
                    valueZ: new[] { 0f },
                    valueW: new[] { 1f },
                    intValues: Array.Empty<int>(),
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: new[] { 40000, 50000 })
            });
        var gatewaySnapshot = new ShooterGatewaySnapshot(
            worldId: 77ul,
            frame: 2,
            timestamp: 2d,
            serverTicks: 2L,
            isFullSnapshot: false,
            actors: Array.Empty<ShooterGatewayActorSnapshot>(),
            payloadOpCode: ShooterOpCodes.Snapshot.PackedStateDelta,
            packedSnapshot: packedDelta);

        var delta = mapper.Map(in gatewaySnapshot);
        projection.Apply(in full);
        projection.Apply(in delta);

        Assert.Equal(ShooterViewSnapshotKind.Delta, delta.SnapshotKind);
        Assert.False(delta.ShouldReplaceMissingEntities);
        Assert.Equal(2, projection.Store.PlayerCount);
        Assert.True(projection.Store.TryGetTransform(playerOne, out var playerOneTransform));
        Assert.Equal(2f, playerOneTransform.X);
        Assert.Equal(3f, playerOneTransform.Y);
        Assert.True(projection.Store.TryGetTransform(playerTwo, out var playerTwoTransform));
        Assert.Equal(10f, playerTwoTransform.X);
    }

    [Fact]
    public void PackedDeltaSnapshotRemovesProjectileWhenLifecycleMarksDespawned()
    {
        var projection = new ShooterSnapshotViewProjection();
        var mapper = new ShooterSnapshotViewModelMapper();
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var full = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[] { new ShooterViewEntityChange(bullet, 1, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            new[] { new ShooterViewTransformComponentChange(bullet, 3f, 4f, 0f, 1f, 8f, 9f) },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            new[] { new ShooterViewProjectileLifetimeComponentChange(bullet, 12) },
            Array.Empty<ShooterEventSnapshot>());
        var packedDelta = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 77ul,
            frame: 2,
            serverTick: 2L,
            snapshotFlags: ShooterPackedSnapshotFlags.Delta,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.EntityLifecycle,
                    ShooterPackedEntityKinds.Projectile,
                    count: 1,
                    entityIds: new[] { 100 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: Array.Empty<int>(),
                    flags: new[] { (byte)(ShooterPackedEntityFlags.Projectile | ShooterPackedEntityFlags.Despawned) },
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>())
            });
        var gatewaySnapshot = new ShooterGatewaySnapshot(
            worldId: 77ul,
            frame: 2,
            timestamp: 2d,
            serverTicks: 2L,
            isFullSnapshot: false,
            actors: Array.Empty<ShooterGatewayActorSnapshot>(),
            payloadOpCode: ShooterOpCodes.Snapshot.PackedStateDelta,
            packedSnapshot: packedDelta);

        var delta = mapper.Map(in gatewaySnapshot);
        projection.Apply(in full);
        var result = projection.Apply(in delta);

        Assert.Equal(ShooterViewSnapshotKind.Delta, delta.SnapshotKind);
        Assert.Equal(1, result.RemovedEntities);
        Assert.Equal(1, result.ExplicitEntityRemovals);
        Assert.False(projection.Store.ContainsEntity(bullet));
        Assert.False(projection.Store.Transforms.ContainsKey(bullet));
        Assert.False(projection.Store.ProjectileLifetimes.ContainsKey(bullet));
    }

    [Fact]
    public void FullSnapshotRemovesEntitiesMissingFromAuthoritativeBatch()
    {
        var projection = new ShooterSnapshotViewProjection();
        var playerOne = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var playerTwo = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var first = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[]
            {
                new ShooterViewEntityChange(playerOne, 0, alive: true),
                new ShooterViewEntityChange(playerTwo, 0, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(playerOne, 0f, 0f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(playerTwo, 10f, 0f, 1f, 0f, 0f, 0f)
            },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var correction = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 2,
            sequence: 2ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[] { new ShooterViewEntityChange(playerOne, 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            new[] { new ShooterViewTransformComponentChange(playerOne, 4f, 5f, 0f, 1f, 0f, 0f) },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in first);
        projection.Apply(in correction);

        Assert.True(projection.Store.ContainsEntity(playerOne));
        Assert.False(projection.Store.ContainsEntity(playerTwo));
        Assert.False(projection.Store.Transforms.ContainsKey(playerTwo));
        Assert.Equal(1, projection.Store.PlayerCount);
    }

    [Fact]
    public void EmptyAuthoritativeFullSnapshotClearsLargeProjection()
    {
        const int entityCount = 2048;
        var projection = new ShooterSnapshotViewProjection();
        var entities = new ShooterViewEntityChange[entityCount];
        var transforms = new ShooterViewTransformComponentChange[entityCount];
        for (var i = 0; i < entityCount; i++)
        {
            var key = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, i + 1);
            entities[i] = new ShooterViewEntityChange(key, 0, alive: true);
            transforms[i] = new ShooterViewTransformComponentChange(key, i, 0f, 1f, 0f, 0f, 0f);
        }

        var full = new ShooterSnapshotViewBatch(
            77ul,
            1,
            1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            entities,
            Array.Empty<ShooterViewEntityKey>(),
            transforms,
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var empty = new ShooterSnapshotViewBatch(
            77ul,
            2,
            2ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in full);
        var result = projection.Apply(in empty);

        Assert.Equal(entityCount, result.MissingEntityRemovals);
        Assert.Equal(0, result.FinalEntityCount);
        Assert.Empty(projection.Store.Entities);
        Assert.Empty(projection.Store.Transforms);
    }

    [Fact]
    public void ClearBatchRemovesAllProjectedEntities()
    {
        var projection = new ShooterSnapshotViewProjection();
        var full = CreateBatch(frame: 1, sequence: 1ul);

        projection.Apply(in full);
        var result = projection.Apply(ShooterSnapshotViewBatch.Empty);

        Assert.True(ShooterSnapshotViewBatch.Empty.ShouldReplaceMissingEntities);
        Assert.Equal(1, result.MissingEntityRemovals);
        Assert.Equal(0, projection.Store.EntityCount);
    }

    [Fact]
    public void FullBatchSynchronizesPresentationEntitySet()
    {
        var projection = new ShooterSnapshotViewProjection();
        var stalePlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var deadPlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var newPlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 3);
        var staleBullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var newBullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 101);
        var first = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.JoinOrReconnect,
            new[]
            {
                new ShooterViewEntityChange(stalePlayer, 0, alive: true),
                new ShooterViewEntityChange(deadPlayer, 0, alive: true),
                new ShooterViewEntityChange(staleBullet, 1, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(stalePlayer, 1f, 1f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(deadPlayer, 2f, 2f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(staleBullet, 3f, 3f, 0f, 1f, 6f, 7f)
            },
            new[] { new ShooterViewHealthComponentChange(deadPlayer, 25) },
            Array.Empty<ShooterViewScoreComponentChange>(),
            new[] { new ShooterViewProjectileLifetimeComponentChange(staleBullet, 8) },
            Array.Empty<ShooterEventSnapshot>());
        var batchSync = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 8,
            sequence: 2ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.JoinOrReconnect,
            new[]
            {
                new ShooterViewEntityChange(deadPlayer, 0, alive: false),
                new ShooterViewEntityChange(newPlayer, 0, alive: true),
                new ShooterViewEntityChange(newBullet, 3, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            new[]
            {
                new ShooterViewTransformComponentChange(deadPlayer, 20f, 20f, 1f, 0f, 0f, 0f),
                new ShooterViewTransformComponentChange(newPlayer, 4f, 5f, 0f, 1f, 0.5f, 0.25f),
                new ShooterViewTransformComponentChange(newBullet, 6f, 7f, 0f, 1f, 8f, 9f)
            },
            new[]
            {
                new ShooterViewHealthComponentChange(deadPlayer, 0),
                new ShooterViewHealthComponentChange(newPlayer, 100)
            },
            new[] { new ShooterViewScoreComponentChange(newPlayer, 3) },
            new[] { new ShooterViewProjectileLifetimeComponentChange(newBullet, 12) },
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in first);
        var result = projection.Apply(in batchSync);
 
        Assert.Equal(2, result.AddedEntities);
        Assert.Equal(0, result.UpdatedEntities);
        Assert.Equal(3, result.RemovedEntities);
        Assert.Equal(3, result.MissingEntityRemovals);
        Assert.Equal(0, result.ExplicitEntityRemovals);
        Assert.Equal(0, result.DeadEntityRemovals);
        Assert.Equal(5, result.ComponentUpdates);
        Assert.Equal(2, result.FinalEntityCount);
        Assert.Equal(ShooterViewBatchSource.JoinOrReconnect, result.Source);
        Assert.False(projection.Store.ContainsEntity(stalePlayer));
        Assert.False(projection.Store.ContainsEntity(deadPlayer));
        Assert.False(projection.Store.Transforms.ContainsKey(stalePlayer));
        Assert.False(projection.Store.Transforms.ContainsKey(deadPlayer));
        Assert.False(projection.Store.Health.ContainsKey(deadPlayer));
        Assert.False(projection.Store.ContainsEntity(staleBullet));
        Assert.False(projection.Store.ProjectileLifetimes.ContainsKey(staleBullet));
        Assert.True(projection.Store.ContainsEntity(newPlayer));
        Assert.True(projection.Store.ContainsEntity(newBullet));
        Assert.True(projection.Store.TryGetTransform(newPlayer, out var newPlayerTransform));
        Assert.Equal(4f, newPlayerTransform.X);
        Assert.True(projection.Store.TryGetHealth(newPlayer, out var newPlayerHealth));
        Assert.Equal(100, newPlayerHealth.Hp);
        Assert.True(projection.Store.TryGetProjectileLifetime(newBullet, out var newBulletLifetime));
        Assert.Equal(12, newBulletLifetime.RemainingFrames);
        Assert.Equal(2, projection.Store.EntityCount);
        Assert.Equal(1, projection.Store.PlayerCount);
        Assert.Equal(1, projection.Store.BulletCount);
    }

    [Fact]
    public void LocalPredictionFullSnapshotDoesNotRemoveMissingEntities()
    {
        var projection = new ShooterSnapshotViewProjection();
        var playerOne = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var playerTwo = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var authoritative = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[]
            {
                new ShooterViewEntityChange(playerOne, 0, alive: true),
                new ShooterViewEntityChange(playerTwo, 0, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var localPrediction = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 2,
            sequence: 2ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.LocalPrediction,
            new[] { new ShooterViewEntityChange(playerOne, 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in authoritative);
        projection.Apply(in localPrediction);

        Assert.True(projection.Store.ContainsEntity(playerOne));
        Assert.True(projection.Store.ContainsEntity(playerTwo));
        Assert.Equal(2, projection.Store.PlayerCount);
    }

    [Fact]
    public void LocalAuthoritativeFullSnapshotRemovesMissingEntities()
    {
        var projection = new ShooterSnapshotViewProjection();
        var playerOne = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var enemy = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 200);
        var first = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.LocalAuthoritative,
            new[]
            {
                new ShooterViewEntityChange(playerOne, 0, alive: true),
                new ShooterViewEntityChange(bullet, 1, alive: true),
                new ShooterViewEntityChange(enemy, 0, alive: true)
            },
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var second = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 2,
            sequence: 2ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.LocalAuthoritative,
            new[] { new ShooterViewEntityChange(playerOne, 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projection.Apply(in first);
        var result = projection.Apply(in second);

        Assert.True(second.ShouldReplaceMissingEntities);
        Assert.Equal(2, result.MissingEntityRemovals);
        Assert.True(projection.Store.ContainsEntity(playerOne));
        Assert.False(projection.Store.ContainsEntity(bullet));
        Assert.False(projection.Store.ContainsEntity(enemy));
    }
 
    [Fact]
    public void LocalAuthoritativeMapperDeltaRemovesEntitiesMissingFromNextSnapshot()
    {
        var mapper = new ShooterSnapshotViewModelMapper();
        var projection = new ShooterSnapshotViewProjection();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var enemy = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 200);
        var firstSnapshot = new ShooterStateSnapshotPayload(
            10,
            new[] { new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, 100, 0, alive: true) },
            new[] { new ShooterBulletSnapshot(100, 1, 1f, 0f, 1f, 0f, remainingFrames: 30) },
            Array.Empty<ShooterEventSnapshot>(),
            matchState: 0,
            timeLimitFrames: 0,
            remainingTimeFrames: 0,
            enemies: new[] { new ShooterEnemySnapshot(200, 2f, 0f, -1f, 0f, 20, 20, alive: true) });
        var secondSnapshot = new ShooterStateSnapshotPayload(
            11,
            new[] { new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, 100, 0, alive: true) },
            Array.Empty<ShooterBulletSnapshot>(),
            Array.Empty<ShooterEventSnapshot>(),
            matchState: 0,
            timeLimitFrames: 0,
            remainingTimeFrames: 0,
            enemies: Array.Empty<ShooterEnemySnapshot>());

        var first = mapper.Map(in firstSnapshot, ShooterViewBatchSource.LocalAuthoritative);
        projection.Apply(in first);
        var second = mapper.Map(in secondSnapshot, ShooterViewBatchSource.LocalAuthoritative);
        var result = projection.Apply(in second);

        Assert.Equal(ShooterViewSnapshotKind.Delta, second.SnapshotKind);
        Assert.False(second.ShouldReplaceMissingEntities);
        Assert.Equal(2, second.RemovedEntityCount);
        Assert.Contains(bullet, second.RemovedEntities);
        Assert.Contains(enemy, second.RemovedEntities);
        Assert.Equal(2, result.ExplicitEntityRemovals);
        Assert.True(projection.Store.ContainsEntity(player));
        Assert.False(projection.Store.ContainsEntity(bullet));
        Assert.False(projection.Store.ContainsEntity(enemy));
    }

    [Fact]
    public void LocalAuthoritativeMapperDeltaOmitsUnchangedEntityAndStaticComponents()
    {
        var mapper = new ShooterSnapshotViewModelMapper();
        var projection = new ShooterSnapshotViewProjection();
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var bullet = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, 100);
        var enemy = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 200);
        var firstSnapshot = new ShooterStateSnapshotPayload(
            10,
            new[] { new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, 100, 3, alive: true) },
            new[] { new ShooterBulletSnapshot(100, 1, 1f, 0f, 1f, 0f, remainingFrames: 30) },
            Array.Empty<ShooterEventSnapshot>(),
            matchState: 0,
            timeLimitFrames: 0,
            remainingTimeFrames: 0,
            enemies: new[] { new ShooterEnemySnapshot(200, 2f, 0f, -1f, 0f, 20, 20, alive: true) });
        var secondSnapshot = new ShooterStateSnapshotPayload(
            11,
            new[] { new ShooterPlayerSnapshot(1, 0.1f, 0f, 1f, 0f, 100, 3, alive: true) },
            new[] { new ShooterBulletSnapshot(100, 1, 1.2f, 0f, 1f, 0f, remainingFrames: 29) },
            Array.Empty<ShooterEventSnapshot>(),
            matchState: 0,
            timeLimitFrames: 0,
            remainingTimeFrames: 0,
            enemies: new[] { new ShooterEnemySnapshot(200, 2.1f, 0f, -1f, 0f, 20, 20, alive: true) });

        var first = mapper.Map(in firstSnapshot, ShooterViewBatchSource.LocalAuthoritative);
        projection.Apply(in first);
        var second = mapper.Map(in secondSnapshot, ShooterViewBatchSource.LocalAuthoritative);
        var result = projection.Apply(in second);

        Assert.Equal(ShooterViewSnapshotKind.Delta, second.SnapshotKind);
        Assert.Empty(second.EntityChanges);
        Assert.Empty(second.HealthChanges);
        Assert.Empty(second.ScoreChanges);
        Assert.Equal(3, second.TransformChanges.Count);
        Assert.Single(second.ProjectileLifetimeChanges);
        Assert.Equal(29, second.ProjectileLifetimeChanges[0].RemainingFrames);
        Assert.True(projection.Store.ContainsEntity(player));
        Assert.True(projection.Store.ContainsEntity(bullet));
        Assert.True(projection.Store.ContainsEntity(enemy));
        Assert.Equal(4, result.ComponentUpdates);
    }

    [Fact]
    public void ProjectedSinkPublishesStoreAfterApplyingBatch()
    {
        var recordingSink = new RecordingProjectedViewSink();
        var projectedSink = new ShooterProjectedSnapshotViewSink(recordingSink);
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var batch = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 1,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[] { new ShooterViewEntityChange(player, 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        projectedSink.ApplySnapshot(in batch);
 
        Assert.Equal(1, recordingSink.ApplyCount);
        Assert.Equal(1, recordingSink.LastPlayerCount);
        Assert.Equal(1, recordingSink.LastFrame);
        Assert.Equal(1, recordingSink.LastAddedEntities);
        Assert.Equal(1, projectedSink.LastApplyResult.AddedEntities);
    }

    [Fact]
    public void SnapshotStreamKeepsBoundedRecentBatchesAndSamplesByFrame()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 2);
        var first = CreateBatch(frame: 1, sequence: 1ul);
        var second = CreateBatch(frame: 2, sequence: 2ul);
        var third = CreateBatch(frame: 3, sequence: 3ul);

        stream.Publish(in first);
        stream.Publish(in second);
        stream.Publish(in third);

        Assert.Equal(2, stream.BufferedSnapshotCount);
        Assert.True(stream.TrySample(playbackFrame: 1.5f, out var sampledOldest));
        Assert.Equal(2, sampledOldest.Frame);
        Assert.True(stream.TrySample(playbackFrame: 99f, out var sampledLatest));
        Assert.Equal(3, sampledLatest.Frame);
    }

    [Fact]
    public void SnapshotStreamAdvancesDelayedPlaybackWithoutRepeatingSameSequence()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4)
        {
            PlaybackFramesPerSecond = 10f,
            InterpolationDelayFrames = 1f
        };
        var first = CreateBatch(frame: 10, sequence: 10ul);
        var second = CreateBatch(frame: 11, sequence: 11ul);
        var third = CreateBatch(frame: 12, sequence: 12ul);

        stream.Publish(in first);
        stream.Publish(in second);
        stream.Publish(in third);

        Assert.True(stream.TryAdvancePlayback(0f, out var initial));
        Assert.Equal(11, initial.Frame);
        Assert.False(stream.TryAdvancePlayback(0f, out _));
        Assert.True(stream.TryAdvancePlayback(0.1f, out var advanced));
        Assert.Equal(12, advanced.Frame);
    }

    [Fact]
    public void SnapshotStreamPrebuffersConfiguredDelayBeforeContinuousPlayback()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 8)
        {
            PlaybackFramesPerSecond = 30f,
            InterpolationDelayFrames = 6f
        };
        var first = CreateBatch(frame: 0, sequence: 1ul, x: 0f);
        var second = CreateBatch(frame: 3, sequence: 2ul, x: 3f);
        var third = CreateBatch(frame: 6, sequence: 3ul, x: 6f);

        stream.Publish(in first);
        Assert.True(stream.TryAdvancePlayback(0f, out var initial));
        Assert.Equal(0f, initial.SampleFrame);
        Assert.False(stream.IsPlaybackStarved);

        stream.Publish(in second);
        Assert.False(stream.TryAdvancePlayback(1f / 30f, out _));
        Assert.Equal(0f, stream.PlaybackFrame);
        Assert.False(stream.IsPlaybackStarved);

        stream.Publish(in third);
        Assert.True(stream.TryAdvancePlayback(1f / 30f, out var playing));
        Assert.Equal(1f, playing.SampleFrame, 3);
        Assert.Equal(5f, stream.AvailablePlaybackLeadFrames, 3);
    }

    [Fact]
    public void SnapshotStreamConvergesDelayWithoutMovingPlaybackBackwards()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 8)
        {
            PlaybackFramesPerSecond = 30f,
            InterpolationDelayFrames = 6f,
            DelayConvergenceRate = 0.125f
        };
        var first = CreateBatch(frame: 0, sequence: 1ul, x: 0f);
        var latest = CreateBatch(frame: 30, sequence: 2ul, x: 30f);
        stream.Publish(in first);
        stream.Publish(in latest);
        Assert.True(stream.TryAdvancePlayback(0f, out _));
        Assert.Equal(24f, stream.PlaybackFrame, 3);

        Assert.True(stream.TrySetTargetInterpolationDelayFrames(9f));
        var previous = stream.PlaybackFrame;
        for (var i = 0; i < 24; i++)
        {
            stream.TryAdvancePlayback(1f / 30f, out _);
            Assert.True(stream.PlaybackFrame >= previous);
            previous = stream.PlaybackFrame;
        }

        Assert.Equal(9f, stream.InterpolationDelayFrames, 3);
        Assert.Equal(30f, stream.PlaybackFrame, 3);
        Assert.True(stream.IsPlaybackStarved);
    }

    [Fact]
    public void SnapshotStreamAdvancesBatchesWithSameSequenceButDifferentFrames()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4)
        {
            PlaybackFramesPerSecond = 10f,
            InterpolationDelayFrames = 1f
        };
        var first = CreateBatch(frame: 10, sequence: 0ul);
        var second = CreateBatch(frame: 11, sequence: 0ul);
        var third = CreateBatch(frame: 12, sequence: 0ul);

        stream.Publish(in first);
        stream.Publish(in second);
        stream.Publish(in third);

        Assert.True(stream.TryAdvancePlayback(0f, out var initial));
        Assert.Equal(11, initial.Frame);
        Assert.False(stream.TryAdvancePlayback(0f, out _));
        Assert.True(stream.TryAdvancePlayback(0.1f, out var advanced));
        Assert.Equal(12, advanced.Frame);
    }

    [Fact]
    public void SnapshotStreamInterpolatesBatchesWithSameSequenceButDifferentFrames()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var first = CreateBatch(frame: 10, sequence: 0ul, x: 0f, hp: 100, score: 1);
        var second = CreateBatch(frame: 20, sequence: 0ul, x: 10f, hp: 50, score: 9);

        stream.Publish(in first);
        stream.Publish(in second);

        Assert.True(stream.TrySample(playbackFrame: 15f, out var sampled, out var isContinuousSample));
        Assert.True(isContinuousSample);
        Assert.Equal(10, sampled.Frame);
        Assert.Single(sampled.TransformChanges);
        Assert.Equal(5f, sampled.TransformChanges[0].X);
    }

    [Fact]
    public void SnapshotStreamInterpolatesTransformAndStepsDiscreteComponentsByDefaultPolicy()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var first = CreateBatch(frame: 10, sequence: 10ul, x: 0f, hp: 100, score: 1);
        var second = CreateBatch(frame: 20, sequence: 20ul, x: 10f, hp: 50, score: 9);

        stream.Publish(in first);
        stream.Publish(in second);

        Assert.True(stream.TrySample(playbackFrame: 15f, out var sampled, out var isContinuousSample));
        Assert.True(isContinuousSample);
        Assert.Single(sampled.TransformChanges);
        Assert.Equal(5f, sampled.TransformChanges[0].X);
        Assert.Single(sampled.HealthChanges);
        Assert.Equal(100, sampled.HealthChanges[0].Hp);
        Assert.Single(sampled.ScoreChanges);
        Assert.Equal(1, sampled.ScoreChanges[0].Score);
    }

    [Fact]
    public void SnapshotStreamInterpolatesSparseLowFrequencyTrackAcrossIntermediateBatch()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var target = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2001);
        var baseline = CreateSparseTransformBatch(
            frame: 0,
            sequence: 1ul,
            ShooterViewSnapshotKind.Full,
            target,
            x: 0f,
            deliveryHints: SnapshotDeliveryHints.None);
        var intermediate = CreateSparseTransformBatch(
            frame: 5,
            sequence: 2ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: null,
            deliveryHints: SnapshotDeliveryHints.None);
        var update = CreateSparseTransformBatch(
            frame: 10,
            sequence: 3ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: 10f,
            deliveryHints: SnapshotDeliveryHints.SparseUpdate);

        stream.Publish(in baseline);
        stream.Publish(in intermediate);
        stream.Publish(in update);

        Assert.True(stream.TrySample(5f, out var sampled, out var isContinuousSample));
        Assert.True(isContinuousSample);
        Assert.Single(sampled.TransformChanges);
        Assert.Equal(target, sampled.TransformChanges[0].Key);
        Assert.Equal(5f, sampled.TransformChanges[0].X);
        Assert.True(sampled.TransformChanges[0].IsLowFrequency);
    }

    [Fact]
    public void SnapshotStreamHoldsLatestLowFrequencyTransformWithoutFutureSample()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var target = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2001);
        var update = CreateSparseTransformBatch(
            frame: 10,
            sequence: 1ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: 10f,
            deliveryHints: SnapshotDeliveryHints.SparseUpdate);
        var later = CreateSparseTransformBatch(
            frame: 15,
            sequence: 2ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: null,
            deliveryHints: SnapshotDeliveryHints.None);

        stream.Publish(in update);
        stream.Publish(in later);

        Assert.True(stream.TrySample(15f, out var sampled, out var isContinuousSample));
        Assert.True(isContinuousSample);
        Assert.Single(sampled.TransformChanges);
        Assert.Equal(target, sampled.TransformChanges[0].Key);
        Assert.Equal(10f, sampled.TransformChanges[0].X);
    }

    [Fact]
    public void SnapshotStreamExtrapolatesSparseVelocityWithBoundedHorizon()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4)
        {
            PlaybackFramesPerSecond = 30f,
            MaxTransformExtrapolationFrames = 6f
        };
        var target = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2001);
        var moving = new ShooterViewTransformComponentChange(
            target,
            10f,
            0f,
            1f,
            0f,
            30f,
            0f,
            SnapshotDeliveryHints.SparseUpdate);
        var update = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 10,
            sequence: 1ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.AuthoritativeCorrection,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            new[] { moving },
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var later = CreateSparseTransformBatch(
            frame: 30,
            sequence: 2ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: null,
            deliveryHints: SnapshotDeliveryHints.None);

        stream.Publish(in update);
        stream.Publish(in later);

        Assert.True(stream.TrySample(13f, out var projected));
        Assert.True(stream.TrySample(30f, out var clamped));
        Assert.Equal(13f, projected.TransformChanges[0].X, 3);
        Assert.Equal(16f, clamped.TransformChanges[0].X, 3);
    }

    [Fact]
    public void SnapshotStreamStopsHoldingLowFrequencyTransformAfterDespawn()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var target = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2001);
        var update = CreateSparseTransformBatch(
            frame: 10,
            sequence: 1ul,
            ShooterViewSnapshotKind.Delta,
            target,
            x: 10f,
            deliveryHints: SnapshotDeliveryHints.SparseUpdate);
        var despawn = new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame: 15,
            sequence: 2ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[] { new ShooterViewEntityChange(target, ownerEntityId: 0, alive: false) },
            new[] { target },
            Array.Empty<ShooterViewTransformComponentChange>(),
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());

        stream.Publish(in update);
        stream.Publish(in despawn);

        Assert.True(stream.TrySample(15f, out var sampled, out var isContinuousSample));
        Assert.False(isContinuousSample);
        Assert.Empty(sampled.TransformChanges);
    }

    [Fact]
    public void SnapshotStreamCanDisableTransformInterpolationThroughPolicy()
    {
        var policy = new ShooterSnapshotSamplingPolicy(new ShooterSnapshotSamplingPolicyOptions
        {
            TransformMode = ShooterSnapshotComponentSamplingMode.Step
        });
        var stream = new ShooterSnapshotStream(bufferCapacity: 4, policy);
        var first = CreateBatch(frame: 10, sequence: 10ul, x: 0f);
        var second = CreateBatch(frame: 20, sequence: 20ul, x: 10f);

        stream.Publish(in first);
        stream.Publish(in second);

        Assert.True(stream.TrySample(playbackFrame: 15f, out var sampled, out var isContinuousSample));
        Assert.False(isContinuousSample);
        Assert.Equal(0f, sampled.TransformChanges[0].X);
    }

    [Fact]
    public void SnapshotStreamRetainedSamplesAreNotOverwrittenByLaterSamples()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4);
        var first = CreateBatch(frame: 10, sequence: 10ul, x: 0f);
        var second = CreateBatch(frame: 20, sequence: 20ul, x: 10f);
        stream.Publish(in first);
        stream.Publish(in second);

        Assert.True(stream.TrySample(playbackFrame: 12.5f, out var retained));
        Assert.True(stream.TrySample(playbackFrame: 17.5f, out var later));

        Assert.Equal(2.5f, retained.TransformChanges[0].X);
        Assert.Equal(7.5f, later.TransformChanges[0].X);
    }

    [Fact]
    public void SnapshotStreamTransientPlaybackReusesLargeTransformBufferAfterWarmup()
    {
        const int entityCount = 2048;
        var stream = new ShooterSnapshotStream(bufferCapacity: 4)
        {
            PlaybackFramesPerSecond = 30f,
            InterpolationDelayFrames = 99_999f
        };
        var first = CreateLargeTransformBatch(frame: 0, sequence: 1ul, entityCount, xOffset: 0f);
        var second = CreateLargeTransformBatch(frame: 100_000, sequence: 2ul, entityCount, xOffset: 10f);
        stream.Publish(in first);
        stream.Publish(in second);

        for (var i = 0; i < 16; i++)
        {
            Assert.True(stream.TryAdvancePlaybackTransient(1f / 60f, out _));
        }

        var checksum = 0f;
        var allSamplesValid = true;
        var sampleFramesAdvance = true;
        var lastSampleFrame = float.MinValue;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            if (!stream.TryAdvancePlaybackTransient(1f / 60f, out var sampled) || sampled.TransformChanges.Count != entityCount)
            {
                allSamplesValid = false;
                continue;
            }

            sampleFramesAdvance &= sampled.SampleFrame > lastSampleFrame;
            lastSampleFrame = sampled.SampleFrame;
            checksum += sampled.TransformChanges[0].X;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allSamplesValid);
        Assert.True(sampleFramesAdvance);
        Assert.True(checksum > 0f);
        Assert.True(allocatedBytes < 1_024L, $"Transient interpolation allocated {allocatedBytes} bytes after warmup.");
    }

    [Fact]
    public void LargeStateMappingKeepsSteadyStateAllocationBoundedAfterWarmup()
    {
        const int entityCount = 2048;
        var enemies = new ShooterEnemySnapshot[entityCount];
        for (var i = 0; i < enemies.Length; i++)
        {
            enemies[i] = new ShooterEnemySnapshot(i + 1, i, 0f, 1f, 0f, 10, 10, alive: true);
        }

        var snapshot = new ShooterStateSnapshotPayload(
            1,
            Array.Empty<ShooterPlayerSnapshot>(),
            Array.Empty<ShooterBulletSnapshot>(),
            Array.Empty<ShooterEventSnapshot>(),
            0,
            0,
            0,
            enemies);
        var mapper = new ShooterSnapshotViewModelMapper();
        var warmup = mapper.Map(in snapshot);
        warmup.ReleasePooledResources();

        var countsAreValid = true;
        long mapAllocated = 0;
        long releaseAllocated = 0;
        for (var i = 0; i < 32; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var batch = mapper.Map(in snapshot);
            mapAllocated += GC.GetAllocatedBytesForCurrentThread() - before;
            countsAreValid &= batch.EntityChangeCount == entityCount;
            countsAreValid &= batch.TransformChanges.Count == entityCount;
            before = GC.GetAllocatedBytesForCurrentThread();
            batch.ReleasePooledResources();
            releaseAllocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var allocated = mapAllocated + releaseAllocated;
        Assert.True(countsAreValid);
        Assert.True(allocated < 256_000L, $"Large mapping allocated {allocated} bytes after warmup (map={mapAllocated}, release={releaseAllocated}).");
    }

    [Fact]
    public void LargeRepeatedEntityUpdatesDoNotGrowProjectionCapacity()
    {
        const int entityCount = 2048;
        var entityChanges = new ShooterViewEntityChange[entityCount];
        var transformChanges = new ShooterViewTransformComponentChange[entityCount];
        for (var i = 0; i < entityCount; i++)
        {
            var key = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, i + 1);
            entityChanges[i] = new ShooterViewEntityChange(key, 0, alive: true);
            transformChanges[i] = new ShooterViewTransformComponentChange(key, i, 0f, 1f, 0f, 0f, 0f);
        }

        var batch = new ShooterSnapshotViewBatch(
            1,
            1,
            1,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.AuthoritativeCorrection,
            entityChanges,
            Array.Empty<ShooterViewEntityKey>(),
            transformChanges,
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
        var projection = new ShooterSnapshotViewProjection();
        projection.Apply(in batch);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 32; i++)
        {
            projection.Apply(in batch);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(entityCount, projection.Store.EntityCount);
        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void SnapshotViewBinderConsumesBufferedSnapshotsOnInterpolationTick()
    {
        var stream = new ShooterSnapshotStream(bufferCapacity: 4)
        {
            PlaybackFramesPerSecond = 30f,
            InterpolationDelayFrames = 0f
        };
        var presentation = new ShooterPresentationFacade(
            new ShooterGatewaySnapshotDecoder(),
            new ShooterSnapshotViewAdapter(),
            stream);
        var sink = new RecordingSnapshotViewSink();
        using var binder = new ShooterSnapshotViewBinder(presentation, sink);
        var batch = CreateBatch(frame: 5, sequence: 5ul);

        presentation.Snapshots.Publish(in batch);

        Assert.Equal(0, sink.ApplyCount);
        Assert.True(binder.HasBufferedSnapshots);

        binder.TickInterpolation(0f);

        Assert.Equal(1, sink.ApplyCount);
        Assert.Equal(5, sink.LastFrame);
    }

    [Fact]
    public void SnapshotViewBinderCanBypassInterpolationForImmediateSync()
    {
        var presentation = new ShooterPresentationFacade();
        var sink = new RecordingSnapshotViewSink();
        using var binder = new ShooterSnapshotViewBinder(presentation, sink)
        {
            InterpolationEnabled = false
        };
        var batch = CreateBatch(frame: 6, sequence: 6ul);

        presentation.Snapshots.Publish(in batch);

        Assert.Equal(1, sink.ApplyCount);
        Assert.Equal(6, sink.LastFrame);
    }

    [Fact]
    public void DotsSnapshotViewBinderProjectsBatchAndExposesBatchSyncDiagnostics()
    {
        var presentation = new ShooterPresentationFacade();
        var sink = new RecordingSnapshotViewSink();
        using var binder = new ShooterDotsSnapshotViewBinder(presentation, sink);
        var batch = CreateBatch(frame: 7, sequence: 7ul, x: 3f, hp: 80, score: 5);

        binder.Sync(in batch);

        Assert.Equal(1, sink.ApplyCount);
        Assert.Equal(7, sink.LastFrame);
        Assert.Equal(1, binder.AppliedBatchCount);
        Assert.Equal(1, binder.AppliedEntityChangeCount);
        Assert.Equal(3, binder.AppliedComponentChangeCount);
        Assert.Equal(1, binder.LastApplyResult.AddedEntities);
        Assert.Equal(3, binder.LastApplyResult.ComponentUpdates);
        Assert.Equal(1, binder.Store.PlayerCount);
        Assert.True(binder.Store.TryGetHealth(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1), out var health));
        Assert.Equal(80, health.Hp);
    }

    [Fact]
    public void DotsSnapshotViewBinderCanBypassInterpolationForImmediateProjectedSync()
    {
        var presentation = new ShooterPresentationFacade();
        var sink = new RecordingSnapshotViewSink();
        using var binder = new ShooterDotsSnapshotViewBinder(presentation, sink)
        {
            InterpolationEnabled = false
        };
        var batch = CreateBatch(frame: 8, sequence: 8ul, x: 4f, hp: 70, score: null);

        presentation.Snapshots.Publish(in batch);

        Assert.Equal(1, sink.ApplyCount);
        Assert.Equal(8, sink.LastFrame);
        Assert.Equal(1, binder.AppliedBatchCount);
        Assert.Equal(1, binder.Store.PlayerCount);
        Assert.Equal(8, binder.LastApplyResult.Frame);
    }
 
    private static ShooterSnapshotViewBatch CreateBatch(int frame, ulong sequence)
    {
        return CreateBatch(frame, sequence, frame, hp: null, score: null);
    }

    private static ShooterSnapshotViewBatch CreateBatch(int frame, ulong sequence, float x, int? hp = null, int? score = null)
    {
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        return new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame,
            sequence,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            new[] { new ShooterViewEntityChange(player, 0, alive: true) },
            Array.Empty<ShooterViewEntityKey>(),
            new[] { new ShooterViewTransformComponentChange(player, x, 0f, 1f, 0f, 0f, 0f) },
            hp.HasValue ? new[] { new ShooterViewHealthComponentChange(player, hp.Value) } : Array.Empty<ShooterViewHealthComponentChange>(),
            score.HasValue ? new[] { new ShooterViewScoreComponentChange(player, score.Value) } : Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
    }

    private static ShooterSnapshotViewBatch CreateLargeTransformBatch(int frame, ulong sequence, int entityCount, float xOffset)
    {
        var transforms = new ShooterViewTransformComponentChange[entityCount];
        for (var i = 0; i < entityCount; i++)
        {
            var key = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, i + 1);
            transforms[i] = new ShooterViewTransformComponentChange(key, xOffset + i, i, 1f, 0f, 0f, 0f);
        }

        return new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame,
            sequence,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.AuthoritativeCorrection,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            transforms,
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
    }

    private static ShooterSnapshotViewBatch CreateSparseTransformBatch(
        int frame,
        ulong sequence,
        ShooterViewSnapshotKind snapshotKind,
        ShooterViewEntityKey key,
        float? x,
        SnapshotDeliveryHints deliveryHints)
    {
        var transforms = x.HasValue
            ? new[] { new ShooterViewTransformComponentChange(key, x.Value, 0f, 1f, 0f, 0f, 0f, deliveryHints) }
            : Array.Empty<ShooterViewTransformComponentChange>();
        return new ShooterSnapshotViewBatch(
            worldId: 77ul,
            frame,
            sequence,
            snapshotKind,
            ShooterViewBatchSource.AuthoritativeCorrection,
            Array.Empty<ShooterViewEntityChange>(),
            Array.Empty<ShooterViewEntityKey>(),
            transforms,
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
    }

    private static ShooterPureStateSnapshotPayload CreatePureStateSnapshot(
        int frame,
        ShooterPureStateSyncSettings settings,
        ShooterPureStateEntityDelta[] entities,
        int snapshotKind = ShooterPureStateSnapshotKinds.FullBaseline)
    {
        return new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            worldId: 99ul,
            frame,
            serverTick: frame,
            snapshotKind,
            baselineFrame: frame,
            baselineHash: 111u,
            stateHash: 111u,
            settings,
            entities,
            visibilityHints: Array.Empty<ShooterPureStateVisibilityHint>());
    }

    private static ShooterPureStateSnapshotPayload CreatePureStateVisibilitySnapshot(int frame, int deltaKind, byte flags)
    {
        return new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            77ul,
            frame,
            frame,
            frame == 1 ? ShooterPureStateSnapshotKinds.FullBaseline : ShooterPureStateSnapshotKinds.Delta,
            1,
            0u,
            0u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    2001,
                    ShooterPackedEntityKinds.Enemy,
                    ShooterPureStateEntityLayers.Combat,
                    deltaKind,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    10,
                    0,
                    0,
                    flags)
            },
            Array.Empty<ShooterPureStateVisibilityHint>());
    }

    private sealed class RecordingSnapshotViewSink : IShooterSnapshotViewSink
    {
        public int ApplyCount { get; private set; }

        public int LastFrame { get; private set; }

        public void ApplySnapshot(in ShooterSnapshotViewBatch batch)
        {
            ApplyCount++;
            LastFrame = batch.Frame;
        }

        public void Clear()
        {
            LastFrame = 0;
        }
    }

    private sealed class RecordingProjectedViewSink : IShooterProjectedViewSink
    {
        public int ApplyCount { get; private set; }

        public int LastPlayerCount { get; private set; }

        public int LastFrame { get; private set; }

        public int LastAddedEntities { get; private set; }
 
        public void ApplyViewState(
            ShooterViewEntityStore store,
            in ShooterSnapshotViewBatch sourceBatch,
            in ShooterViewProjectionApplyResult applyResult)
        {
            ApplyCount++;
            LastPlayerCount = store.PlayerCount;
            LastFrame = sourceBatch.Frame;
            LastAddedEntities = applyResult.AddedEntities;
        }

        public void Clear()
        {
            LastPlayerCount = 0;
            LastFrame = 0;
            LastAddedEntities = 0;
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ShooterSnapshotViewProjectionTestCollection
{
    public const string Name = "Shooter snapshot view projection";
}

using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterPackedSnapshotRuntimeTests
{
    [Theory]
    [InlineData(0x3F0570A4, 5213)]
    [InlineData(0x3FFFEDFB, 19995)]
    [InlineData(0x405445A1, 33167)]
    [InlineData(unchecked((int)0xBF0570A4), -5213)]
    public void SnapshotQuantizationDoesNotDependOnSinglePrecisionIntermediateRounding(
        int floatBits,
        int expected)
    {
        var value = BitConverter.Int32BitsToSingle(floatBits);

        Assert.Equal(expected, ShooterRuntimeSnapshotUtility.Quantize(value));
    }

    [Fact]
    public void PackedSnapshotRoundTripRestoresHashAndEntities()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "smoke",
            30,
            1234,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });

        Assert.True(source.StartGame(in start));
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));

        var sourceSnapshot = source.ExportPackedSnapshot(42ul, isFullSnapshot: true, authorityOverride: true);
        var sourceHash = source.ComputeStateHash();
        var bytes = source.ExportPackedSnapshotBytes(42ul, isFullSnapshot: true, authorityOverride: true);

        Assert.NotEmpty(bytes);
        Assert.Equal(sourceHash, sourceSnapshot.StateHash);
        Assert.Equal(3, sourceSnapshot.EntityCount);
        Assert.True((sourceSnapshot.SnapshotFlags & ShooterPackedSnapshotFlags.AuthorityOverride) != 0);

        var target = new ShooterBattleRuntimePort();
        Assert.True(target.ImportPackedSnapshotBytes(bytes));

        var restoredSnapshot = target.ExportPackedSnapshot(42ul, isFullSnapshot: true, authorityOverride: true);
        Assert.Equal(source.CurrentFrame, target.CurrentFrame);
        Assert.Equal(sourceHash, target.ComputeStateHash());
        Assert.Equal(sourceSnapshot.EntityCount, restoredSnapshot.EntityCount);
        Assert.Equal(sourceSnapshot.Frame, restoredSnapshot.Frame);
    }

    [Fact]
    public void PackedSnapshotRoundTripRestoresExplosiveProjectileMetadata()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "explosive-packed-roundtrip",
            30,
            1235,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(source.StartGame(in start));
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Spread) });
        Assert.True(source.Tick(0f));
        var sourcePacked = source.ExportPackedSnapshot(42ul, isFullSnapshot: true, authorityOverride: true);
        var sourceLifetime = FindPackedChunk(sourcePacked, ShooterPackedComponentKinds.ProjectileLifetime, ShooterPackedEntityKinds.Projectile);
        Assert.NotNull(sourceLifetime);
        Assert.True(sourceLifetime.Value.ValueX[0] > 0f);
        Assert.Equal(1f, sourceLifetime.Value.ValueY[0]);

        var bytes = source.ExportPackedSnapshotBytes(42ul, isFullSnapshot: true, authorityOverride: true);
        var target = new ShooterBattleRuntimePort();

        Assert.True(target.ImportPackedSnapshotBytes(bytes));
        var restoredPacked = target.ExportPackedSnapshot(42ul, isFullSnapshot: true, authorityOverride: true);
        var restoredLifetime = FindPackedChunk(restoredPacked, ShooterPackedComponentKinds.ProjectileLifetime, ShooterPackedEntityKinds.Projectile);
        Assert.NotNull(restoredLifetime);
        Assert.Equal(sourceLifetime.Value.ValueX[0], restoredLifetime.Value.ValueX[0], 5);
        Assert.Equal(sourceLifetime.Value.ValueY[0], restoredLifetime.Value.ValueY[0]);
        Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());
    }

    [Fact]
    public void RoomWireSnapshotPushPreservesPackedShooterPayload()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "wire-smoke",
            30,
            5678,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -1f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));

        var packed = runtime.ExportPackedSnapshot(9001ul, isFullSnapshot: true, authorityOverride: true);
        var packedBytes = ShooterPackedSnapshotCodec.Serialize(in packed);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 123.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = packedBytes
        };

        var wireBytes = WireRoomGatewayBinary.Serialize(in wire);
        var restoredWire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(wireBytes);

        Assert.Equal(wire.WorldId, restoredWire.WorldId);
        Assert.Equal(wire.Frame, restoredWire.Frame);
        Assert.True(restoredWire.IsFullSnapshot);
        Assert.Equal(ShooterOpCodes.Snapshot.PackedState, restoredWire.PayloadOpCode);
        Assert.NotNull(restoredWire.Payload);
        Assert.Equal(packedBytes, restoredWire.Payload);

        var restoredPacked = ShooterPackedSnapshotCodec.Deserialize(restoredWire.Payload!);
        Assert.Equal(packed.WorldId, restoredPacked.WorldId);
        Assert.Equal(packed.Frame, restoredPacked.Frame);
        Assert.Equal(packed.EntityCount, restoredPacked.EntityCount);
        Assert.Equal(packed.StateHash, restoredPacked.StateHash);
    }

    [Fact]
    public void DeltaSnapshotPreservesExistingEntitiesAndUpdatesComponents()
    {
        // Full import establishes the baseline world.
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "delta-import",
            30,
            4200,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 5f, 0f)
            });
        Assert.True(source.StartGame(in start));

        // Advance a few frames so entities move.
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false) });
        Assert.True(source.Tick(1f / 30f));
        var fullSnapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        Assert.True((fullSnapshot.SnapshotFlags & ShooterPackedSnapshotFlags.Full) != 0);

        var target = new ShooterBattleRuntimePort();
        Assert.True(target.ImportPackedSnapshot(in fullSnapshot));
        var initialCount = target.ExportPackedSnapshot(99ul).EntityCount;

        // Advance the source further, then export a delta snapshot (not full).
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 1f, 0f, 1f, false) });
        Assert.True(source.Tick(1f / 30f));
        var deltaSnapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: false, authorityOverride: true);
        Assert.True((deltaSnapshot.SnapshotFlags & ShooterPackedSnapshotFlags.Delta) != 0);

        // Delta import must not reset the world.
        Assert.True(target.ImportPackedSnapshot(in deltaSnapshot));

        // Entity count must be preserved (delta doesn't remove unmentioned entities).
        var restored = target.ExportPackedSnapshot(99ul);
        Assert.Equal(initialCount, restored.EntityCount);
        Assert.Equal(deltaSnapshot.Frame, target.CurrentFrame);
    }

    [Fact]
    public void DeltaSnapshotImportRemovesProjectileWhenLifecycleMarksDespawned()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "delta-despawn-import",
            30,
            4250,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });
        Assert.True(source.StartGame(in start));
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));
        var fullSnapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var projectileLifecycle = FindPackedChunk(fullSnapshot, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Projectile);
        Assert.NotNull(projectileLifecycle);
        Assert.Equal(1, projectileLifecycle.Value.Count);
        var projectileId = projectileLifecycle.Value.EntityIds[0];

        var target = new ShooterBattleRuntimePort();
        Assert.True(target.ImportPackedSnapshot(in fullSnapshot));
        Assert.Equal(fullSnapshot.EntityCount, target.ExportPackedSnapshot(99ul).EntityCount);

        var despawnDelta = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 99ul,
            frame: fullSnapshot.Frame + 1,
            serverTick: fullSnapshot.ServerTick + 1,
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
                    entityIds: new[] { projectileId },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: Array.Empty<int>(),
                    flags: new[] { (byte)(ShooterPackedEntityFlags.Projectile | ShooterPackedEntityFlags.Despawned) },
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>())
            });

        Assert.True(target.ImportPackedSnapshot(in despawnDelta));

        var restored = target.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var restoredProjectileLifecycle = FindPackedChunk(restored, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Projectile);
        Assert.True(!restoredProjectileLifecycle.HasValue || restoredProjectileLifecycle.Value.Count == 0);
        Assert.Equal(fullSnapshot.EntityCount - 1, restored.EntityCount);
        Assert.Equal(despawnDelta.Frame, target.CurrentFrame);
    }

    [Fact]
    public void DeltaSnapshotExportMarksRemovedEnemyAsDespawned()
    {
        var container = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var entities = container.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "delta-enemy-despawn-export",
            30,
            4260,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(runtime.StartGame(in start));
        entities.AddEnemy(
            100,
            new ShooterSveltoTransformComponent { X = 2f, Y = 0f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });

        var fullSnapshot = runtime.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var fullEnemyLifecycle = FindPackedChunk(fullSnapshot, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(fullEnemyLifecycle);
        Assert.Contains(100, fullEnemyLifecycle.Value.EntityIds);

        entities.RemoveEnemy(100);
        var deltaSnapshot = runtime.ExportPackedSnapshot(99ul, isFullSnapshot: false, authorityOverride: true);
        var enemyLifecycle = FindPackedChunk(deltaSnapshot, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);

        Assert.NotNull(enemyLifecycle);
        Assert.Equal(1, enemyLifecycle.Value.Count);
        Assert.Equal(100, enemyLifecycle.Value.EntityIds[0]);
        Assert.True((enemyLifecycle.Value.Flags[0] & ShooterPackedEntityFlags.Despawned) != 0);
    }

    [Fact]
    public void DeltaSnapshotImportRemovesEnemyWhenLifecycleMarksDespawned()
    {
        var sourceContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var source = sourceContainer.Resolve<IShooterBattleRuntimePort>();
        var sourceEntities = sourceContainer.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "delta-enemy-despawn-import",
            30,
            4270,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });
        Assert.True(source.StartGame(in start));
        sourceEntities.AddEnemy(
            100,
            new ShooterSveltoTransformComponent { X = 2f, Y = 0f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 1, Max = 1, Alive = 1 });
        var fullSnapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);

        var targetContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var target = targetContainer.Resolve<IShooterBattleRuntimePort>();
        var targetEntities = targetContainer.Resolve<IShooterEntityManager>();
        Assert.True(target.ImportPackedSnapshot(in fullSnapshot));
        Assert.True(targetEntities.TryGetEnemy(100, out _, out _));

        var despawnDelta = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 99ul,
            frame: fullSnapshot.Frame + 1,
            serverTick: fullSnapshot.ServerTick + 1,
            snapshotFlags: ShooterPackedSnapshotFlags.Delta,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.EntityLifecycle,
                    ShooterPackedEntityKinds.Enemy,
                    count: 1,
                    entityIds: new[] { 100 },
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: Array.Empty<int>(),
                    flags: new[] { (byte)(ShooterPackedEntityFlags.Enemy | ShooterPackedEntityFlags.Despawned) },
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>())
            });

        Assert.True(target.ImportPackedSnapshot(in despawnDelta));

        var restored = target.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var restoredEnemyLifecycle = FindPackedChunk(restored, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
        Assert.False(targetEntities.TryGetEnemy(100, out _, out _));
        Assert.True(!restoredEnemyLifecycle.HasValue || restoredEnemyLifecycle.Value.Count == 0);
        Assert.Equal(fullSnapshot.EntityCount - 1, restored.EntityCount);
        Assert.Equal(despawnDelta.Frame, target.CurrentFrame);
    }

    [Fact]
    public void PackedSnapshotImportPreservesFinalMatchMetadataWhenPlayersExist()
    {
        var target = new ShooterBattleRuntimePort();
        var finalSnapshot = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 99ul,
            frame: 12,
            serverTick: 12,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[]
            {
                new ShooterPackedComponentChunk(
                    ShooterPackedComponentKinds.RuntimeMetadata,
                    entityKind: 0,
                    count: 1,
                    entityIds: Array.Empty<int>(),
                    valueX: Array.Empty<float>(),
                    valueY: Array.Empty<float>(),
                    valueZ: Array.Empty<float>(),
                    valueW: Array.Empty<float>(),
                    intValues: new[] { (int)ShooterBattleMatchState.Victory, 12, 3, 3, 180, 168 },
                    flags: Array.Empty<byte>(),
                    ownerIds: Array.Empty<int>(),
                    aux: Array.Empty<int>()),
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
                    flags: new[] { (byte)(ShooterPackedEntityFlags.Player | ShooterPackedEntityFlags.Alive) },
                    ownerIds: new[] { 1 },
                    aux: Array.Empty<int>())
            });

        Assert.True(target.ImportPackedSnapshot(in finalSnapshot));

        Assert.Equal(ShooterBattleMatchState.Victory, target.MatchState);
        Assert.False(target.IsStarted);
        Assert.Equal(12, target.CurrentFrame);
    }

    [Fact]
    public void PackedSnapshotRoundTripRestoresEnemyNavigationVelocityAndLegacyPayloadDefaultsToZero()
    {
        var sourceContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var source = sourceContainer.Resolve<IShooterBattleRuntimePort>();
        var sourceEntities = sourceContainer.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "enemy-navigation-packed-roundtrip",
            30,
            4280,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(source.StartGame(in start));
        sourceEntities.AddEnemy(
            100,
            new ShooterSveltoTransformComponent { X = 2f, Y = 1f, DirectionX = -1f, DirectionY = 0f },
            new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 });
        Assert.True(sourceEntities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
            ShooterSveltoGroups.GameplayTargets,
            out var sourceNavigation));
        sourceNavigation.Entity(100u) = new ShooterSveltoNavigationComponent
        {
            VelocityX = -0.75f,
            VelocityY = 0.25f
        };

        var snapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var targetContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var target = targetContainer.Resolve<IShooterBattleRuntimePort>();
        var targetEntities = targetContainer.Resolve<IShooterEntityManager>();

        Assert.True(target.ImportPackedSnapshot(in snapshot));
        Assert.True(targetEntities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
            ShooterSveltoGroups.GameplayTargets,
            out var targetNavigation));
        var restored = targetNavigation.Entity(100u);
        Assert.Equal(-0.75f, restored.VelocityX, 4);
        Assert.Equal(0.25f, restored.VelocityY, 4);
        Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());

        var legacySnapshot = WithoutEnemyNavigation(in snapshot);
        var legacyContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var legacyTarget = legacyContainer.Resolve<IShooterBattleRuntimePort>();
        var legacyEntities = legacyContainer.Resolve<IShooterEntityManager>();
        Assert.True(legacyTarget.ImportPackedSnapshot(in legacySnapshot));
        Assert.True(legacyEntities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
            ShooterSveltoGroups.GameplayTargets,
            out var legacyNavigation));
        var legacyRestored = legacyNavigation.Entity(100u);
        Assert.Equal(0f, legacyRestored.VelocityX);
        Assert.Equal(0f, legacyRestored.VelocityY);
    }

    [Fact]
    public void FullSnapshotImportKeepsTwoEnemyComponentLayoutsAlignedAcrossTick()
    {
        var sourceContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var source = sourceContainer.Resolve<IShooterBattleRuntimePort>();
        var sourceEntities = sourceContainer.Resolve<IShooterEntityManager>();
        var start = new ShooterStartGamePayload(
            "two-enemy-layout-import",
            30,
            4290,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -1f, 0f),
                new ShooterStartPlayer(2, "P2", 1f, 0f)
            });

        Assert.True(source.StartGame(in start));
        sourceEntities.BeginStructuralChanges();
        try
        {
            sourceEntities.AddEnemy(
                100,
                new ShooterSveltoTransformComponent { X = 3f, Y = 1f, DirectionX = -1f },
                new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 });
            sourceEntities.AddEnemy(
                101,
                new ShooterSveltoTransformComponent { X = -3f, Y = -1f, DirectionX = 1f },
                new ShooterSveltoHealthComponent { Current = 4, Max = 4, Alive = 1 });
        }
        finally
        {
            sourceEntities.EndStructuralChanges();
        }

        Assert.Equal(2, sourceEntities.EnemyCount);
        AssertEnemyComponentCounts(sourceEntities, 2);

        var snapshot = source.ExportPackedSnapshot(99ul, isFullSnapshot: true, authorityOverride: true);
        var targetContainer = new WorldContainerBuilder()
            .AddModule(new ShooterWorldModule())
            .Build();
        var target = targetContainer.Resolve<IShooterBattleRuntimePort>();
        var targetEntities = targetContainer.Resolve<IShooterEntityManager>();

        Assert.True(target.ImportPackedSnapshot(in snapshot));
        AssertEnemyComponentCounts(targetEntities, 2);
        Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());

        for (var frame = 0; frame < 30; frame++)
        {
            Assert.True(source.Tick(1f / 30f));
            var delta = source.ExportPackedSnapshot(99ul, isFullSnapshot: false, authorityOverride: true);
            Assert.True(target.ImportPackedSnapshot(in delta));
            AssertEnemyComponentCounts(targetEntities, targetEntities.EnemyCount);
            Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());
        }
    }

    [Fact]
    public void RepeatedFullAuthoritySnapshotsRoundTripDuringTwoPlayerCombat()
    {
        var start = new ShooterStartGamePayload(
            "full-authority-two-player-combat",
            30,
            3901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f)
            });
        var source = new ShooterBattleRuntimePort();
        var target = new ShooterBattleRuntimePort();

        Assert.True(source.StartGame(in start));
        Assert.True(target.StartGame(in start));

        for (var frame = 0; frame < 300; frame++)
        {
            var fire = frame == 75 || frame == 81 || frame == 194 || frame == 217 || frame == 223 || frame == 260 || frame == 272;
            Assert.Equal(2, source.SubmitInput(frame, new[]
            {
                new ShooterPlayerCommand(1, frame < 145 ? 1f : 0f, 0f, 1f, 0f, fire),
                new ShooterPlayerCommand(2, frame < 145 ? -1f : 0f, 0f, -1f, 0f, fire)
            }));
            Assert.True(source.Tick(1f / 30f));

            var snapshot = source.ExportPackedSnapshot(3901ul, isFullSnapshot: true, authorityOverride: true);
            var payload = ShooterPackedSnapshotCodec.Serialize(in snapshot);
            var decoded = ShooterPackedSnapshotCodec.Deserialize(payload);
            Assert.True(target.ImportPackedSnapshot(in decoded));
            Assert.True(
                decoded.StateHash == target.ComputeStateHash(),
                $"Full authority snapshot hash mismatch at frame {decoded.Frame}; entities={decoded.EntityCount}, expected=0x{decoded.StateHash:X8}, actual=0x{target.ComputeStateHash():X8}.");
        }
    }

    [Fact]
    public void DeltaSnapshotImportPreservesEntityCountAfterMultipleDeltas()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "delta-chain",
            30,
            4300,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        Assert.True(source.StartGame(in start));

        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));

        var target = new ShooterBattleRuntimePort();
        var fullSnapshot = source.ExportPackedSnapshot(98ul, isFullSnapshot: true, authorityOverride: true);
        Assert.True(target.ImportPackedSnapshot(in fullSnapshot));

        var baselineEntityCount = target.ExportPackedSnapshot(98ul).EntityCount;
        Assert.True(baselineEntityCount > 0);

        // Apply several delta snapshots without any full reset between them.
        for (var i = 0; i < 3; i++)
        {
            source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 1f, 0f, 1f, false) });
            Assert.True(source.Tick(1f / 30f));
            var delta = source.ExportPackedSnapshot(98ul, isFullSnapshot: false, authorityOverride: true);
            Assert.True(target.ImportPackedSnapshot(in delta));
        }

        Assert.Equal(baselineEntityCount, target.ExportPackedSnapshot(98ul).EntityCount);
        Assert.Equal(source.CurrentFrame, target.CurrentFrame);
    }

    private static ShooterPackedSnapshotPayload WithoutEnemyNavigation(in ShooterPackedSnapshotPayload snapshot)
    {
        var chunks = (ShooterPackedComponentChunk[])snapshot.ComponentChunks.Clone();
        for (var i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            if (chunk.ComponentKind != ShooterPackedComponentKinds.Transform ||
                chunk.EntityKind != ShooterPackedEntityKinds.Enemy)
            {
                continue;
            }

            chunks[i] = new ShooterPackedComponentChunk(
                chunk.ComponentKind,
                chunk.EntityKind,
                chunk.Count,
                chunk.EntityIds,
                chunk.ValueX,
                chunk.ValueY,
                chunk.ValueZ,
                chunk.ValueW,
                chunk.IntValues,
                chunk.Flags,
                chunk.OwnerIds,
                Array.Empty<int>());
        }

        return new ShooterPackedSnapshotPayload(
            snapshot.Version,
            snapshot.WorldId,
            snapshot.Frame,
            snapshot.ServerTick,
            snapshot.SnapshotFlags,
            snapshot.StateHash,
            snapshot.EntityCount,
            snapshot.ExtensionPayload,
            chunks);
    }

    private static void AssertEnemyComponentCounts(IShooterEntityManager entities, int expected)
    {
        var group = (Svelto.ECS.ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets;
        Assert.Equal(
            new[] { expected, expected, expected },
            new[]
            {
                entities.SveltoContext.EntitiesDB.Count<ShooterSveltoTransformComponent>(group),
                entities.SveltoContext.EntitiesDB.Count<ShooterSveltoHealthComponent>(group),
                entities.SveltoContext.EntitiesDB.Count<ShooterSveltoNavigationComponent>(group)
            });
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
}

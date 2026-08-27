using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientAuthoritativeInterpolationSyncControllerTests
{
    private static ShooterGatewaySnapshot RemoteSnapshot(int frame, long serverTicks, float actorX)
    {
        return new ShooterGatewaySnapshot(
            worldId: 9001ul,
            frame: frame,
            timestamp: 0d,
            serverTicks: serverTicks,
            isFullSnapshot: true,
            actors: new[]
            {
                new ShooterGatewayActorSnapshot(actorId: 7, x: actorX, y: 0f, rotation: 0f, velocityX: 0f, velocityY: 0f, hp: 100f, hpMax: 100f, teamId: 1)
            });
    }

    private static ShooterGatewaySnapshot RemoteSnapshot(int frame, long serverTicks, params ShooterGatewayActorSnapshot[] actors)
    {
        return new ShooterGatewaySnapshot(
            worldId: 9001ul,
            frame: frame,
            timestamp: 0d,
            serverTicks: serverTicks,
            isFullSnapshot: true,
            actors: actors);
    }

    private static ShooterGatewayActorSnapshot Actor(int actorId, float x) =>
        new ShooterGatewayActorSnapshot(actorId: actorId, x: x, y: 0f, rotation: 0f, velocityX: 0f, velocityY: 0f, hp: 100f, hpMax: 100f, teamId: 1);

    private static ShooterStartGamePayload SinglePlayerStart() =>
        new ShooterStartGamePayload(
            "authoritative-interpolation-prediction",
            30,
            9001,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

    private static ShooterGatewaySnapshot PackedProjectileSnapshot(int frame, long serverTicks, int ownerPlayerId, int bulletId)
    {
        var projectileLifecycle = new ShooterPackedComponentChunk(
            ShooterPackedComponentKinds.EntityLifecycle,
            ShooterPackedEntityKinds.Projectile,
            1,
            new[] { bulletId },
            Array.Empty<float>(),
            Array.Empty<float>(),
            Array.Empty<float>(),
            Array.Empty<float>(),
            Array.Empty<int>(),
            new[] { (byte)(ShooterPackedEntityFlags.Alive | ShooterPackedEntityFlags.Projectile) },
            new[] { ownerPlayerId },
            Array.Empty<int>());
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            9001ul,
            frame,
            serverTicks,
            ShooterPackedSnapshotFlags.Full,
            0u,
            1,
            Array.Empty<byte>(),
            new[] { projectileLifecycle });

        return new ShooterGatewaySnapshot(
            worldId: 9001ul,
            frame: frame,
            timestamp: 0d,
            serverTicks: serverTicks,
            isFullSnapshot: true,
            actors: Array.Empty<ShooterGatewayActorSnapshot>(),
            packedSnapshot: packed);
    }

    private static ShooterGatewaySnapshot PackedPlayerSnapshot(
        int frame,
        float x,
        float y = 0f,
        float aimX = 1f,
        float aimY = 0f,
        bool isFull = true,
        ulong worldId = 9001ul,
        int hp = 100,
        int score = 0,
        bool alive = true,
        uint flags = 0u)
    {
        var entityFlags = alive
            ? (byte)(ShooterPackedEntityFlags.Alive | ShooterPackedEntityFlags.Player)
            : ShooterPackedEntityFlags.Player;
        var chunks = new[]
        {
            new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.EntityLifecycle,
                ShooterPackedEntityKinds.Player,
                1,
                new[] { 1 },
                Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(),
                Array.Empty<int>(), new[] { entityFlags }, Array.Empty<int>(), Array.Empty<int>()),
            new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Transform,
                ShooterPackedEntityKinds.Player,
                1,
                new[] { 1 },
                new[] { x }, new[] { y }, new[] { aimX }, new[] { aimY },
                Array.Empty<int>(), Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<int>()),
            new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Health,
                ShooterPackedEntityKinds.Player,
                1,
                new[] { 1 },
                Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(),
                new[] { hp }, Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<int>()),
            new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Score,
                ShooterPackedEntityKinds.Player,
                1,
                new[] { 1 },
                Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(),
                new[] { score }, Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<int>())
        };
        var snapshotFlags = flags | (isFull ? ShooterPackedSnapshotFlags.Full : 0u);
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId,
            frame,
            frame * 100L,
            snapshotFlags,
            123u,
            1,
            Array.Empty<byte>(),
            chunks);
        return new ShooterGatewaySnapshot(
            worldId, frame, 0d, frame * 100L, isFull,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            packedSnapshot: packed);
    }

    private static ShooterGatewaySnapshot PureStatePlayerSnapshot(
        int frame,
        float x,
        float y = 0f,
        float aimX = 1f,
        float aimY = 0f,
        bool isFull = true,
        ulong worldId = 9001ul,
        int hp = 100,
        int score = 0,
        bool alive = true)
    {
        var entityFlags = alive
            ? (byte)(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible)
            : ShooterPureStateEntityFlags.Visible;
        var pureState = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            worldId,
            frame,
            frame * 100L,
            isFull ? ShooterPureStateSnapshotKinds.FullBaseline : ShooterPureStateSnapshotKinds.Delta,
            isFull ? 0 : frame - 1,
            0u,
            123u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    1,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    isFull ? ShooterPureStateDeltaKinds.Spawn : ShooterPureStateDeltaKinds.Update,
                    1,
                    (int)(x * 1000f),
                    (int)(y * 1000f),
                    (int)(aimX * 1000f),
                    (int)(aimY * 1000f),
                    hp,
                    score,
                    0,
                    entityFlags)
            },
            Array.Empty<ShooterPureStateVisibilityHint>());
        return new ShooterGatewaySnapshot(
            worldId, frame, 0d, frame * 100L, isFull,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            pureStateSnapshot: pureState);
    }

    private static ShooterGatewaySnapshot PureStateRemotePlayerSnapshot(
        int frame,
        float x,
        bool isFull,
        int baselineFrame = 0,
        uint baselineHash = 0u,
        uint stateHash = 123u,
        int entityId = 2,
        int? deltaKind = null,
        int deltaIntervalFrames = 10,
        int interpolationDelayFrames = 20,
        ShooterPureStateFrameSample[]? frameSamples = null,
        ShooterPureStateTransformSample[]? transformSamples = null)
    {
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 20,
            activeSyncBudget: 20,
            baselineIntervalFrames: 450,
            deltaIntervalFrames,
            lowFrequencyIntervalFrames: 90,
            interpolationDelayFrames,
            nearLodIntervalFrames: 10,
            midLodIntervalFrames: 30,
            farLodIntervalFrames: 90);
        var pureState = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            9001ul,
            frame,
            frame * 100L,
            isFull ? ShooterPureStateSnapshotKinds.FullBaseline : ShooterPureStateSnapshotKinds.Delta,
            isFull ? 0 : baselineFrame,
            isFull ? 0u : baselineHash,
            stateHash,
            settings,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    entityId,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    deltaKind ?? (isFull ? ShooterPureStateDeltaKinds.Spawn : ShooterPureStateDeltaKinds.Update),
                    entityId,
                    (int)(x * 1000f),
                    0,
                    1000,
                    0,
                    100,
                    0,
                    0,
                    deltaKind == ShooterPureStateDeltaKinds.Despawn
                        ? (byte)0
                        : (byte)(ShooterPureStateEntityFlags.Alive |
                            ShooterPureStateEntityFlags.Visible |
                            ShooterPureStateEntityFlags.LowFrequency))
            },
            Array.Empty<ShooterPureStateVisibilityHint>(),
            acknowledgedCommands: null,
            frameSamples,
            transformSamples);
        return new ShooterGatewaySnapshot(
            9001ul,
            frame,
            0d,
            frame * 100L,
            isFull,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            pureStateSnapshot: pureState);
    }

    private static ShooterClientAuthoritativeInterpolationSyncController StartedController(
        ShooterBattleRuntimePort runtime,
        ShooterPresentationFacade presentation,
        IShooterRoomGatewayClient? gateway = null)
    {
        presentation.ControlledPlayerId = 1;
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: gateway);
        var start = SinglePlayerStart();
        Assert.True(controller.StartGame(in start));
        return controller;
    }

    [Fact]
    public void ControllerBuffersRemoteSnapshotsAndSeedsTimeline()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        // Snap mode (catchUpRate 0) so the estimate seeds directly to the newest observed server
        // time; soft clock convergence is exercised by the timeline tests.
        var config = new InterpolationConfig(ticksPerSecond: 1000L, interpolationDelayTicks: 100L, bufferCapacity: 16, catchUpRate: 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null,
            config);

        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, controller.SyncModel);

        var first = controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 1, serverTicks: 1000L, actorX: 0f));
        var second = controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 2, serverTicks: 1100L, actorX: 10f));

        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, first);
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, second);
        Assert.Equal(2, controller.BufferedRemoteSnapshotCount);
        Assert.Equal(1100L, controller.EstimatedServerTicks);
    }

    [Fact]
    public void ControllerRejectsStaleRemoteSnapshot()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);

        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 2, serverTicks: 1100L, actorX: 10f));
        var stale = controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 1, serverTicks: 1000L, actorX: 0f));

        Assert.Equal(ShooterSnapshotApplyResult.IgnoredStaleSnapshot, stale);
        Assert.Equal(1, controller.BufferedRemoteSnapshotCount);
    }

    [Fact]
    public void ControllerPublishesInterpolatedRemoteActorBetweenSnapshots()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        // Millisecond timeline, 100ms interpolation delay, so playback sits between the two samples.
        // Snap mode (catchUpRate 0) keeps the playback clock deterministic for the interpolation
        // assertions below; soft convergence is covered separately by the timeline tests.
        var config = new InterpolationConfig(ticksPerSecond: 1000L, interpolationDelayTicks: 100L, bufferCapacity: 16, catchUpRate: 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null,
            config);

        ShooterSnapshotViewBatch? lastBatch = null;
        presentation.Snapshots.SnapshotApplied += batch => lastBatch = batch;

        // Two authoritative samples 100ms apart, actor moves 0 -> 10.
        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 1, serverTicks: 1000L, actorX: 0f));
        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 2, serverTicks: 1100L, actorX: 10f));

        // EstimatedServerTicks = 1100, playback = 1100 - 100 = 1000 -> alpha 0 (oldest sample).
        controller.Tick(0f);
        Assert.NotNull(lastBatch);
        var key = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 7);
        var atStart = TransformX(lastBatch!.Value, key);
        Assert.Equal(0f, atStart, 3);

        // Advance 50ms: estimated = 1150, playback = 1050 -> halfway between 1000 and 1100, X ~= 5.
        controller.Tick(0.05f);
        var midX = TransformX(lastBatch!.Value, key);
        Assert.Equal(5f, midX, 2);

        Assert.True(controller.HasPublishedRemoteFrame);
    }

    [Fact]
    public void ControllerTracksLocalPredictedPoseForStateSyncMode()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = SinglePlayerStart();
        Assert.True(controller.StartGame(in start));

        controller.SubmitLocalInput(1, moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false);
        controller.Tick(1f / 30f);

        var prediction = controller.PredictionState;
        Assert.True(prediction.HasPredictedPose);
        Assert.Equal(1, prediction.PlayerId);
        Assert.Equal(1, prediction.PredictedFrame);
        Assert.True(prediction.PredictedX > 0f);
        Assert.Equal(0f, prediction.PredictedY, 3);
    }

    [Fact]
    public void ControllerDoesNotApplyLocalFireUntilAuthoritativeProjectileArrives()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var config = new InterpolationConfig(ticksPerSecond: 1000L, interpolationDelayTicks: 0L, bufferCapacity: 16, catchUpRate: 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null,
            config);
        var start = SinglePlayerStart();
        Assert.True(controller.StartGame(in start));
        controller.SubmitLocalInput(1, moveX: 0f, moveY: 0f, aimX: 1f, aimY: 0f, fire: true);
        controller.Tick(1f / 30f);

        var localPrediction = controller.PredictionState;
        Assert.Equal(ShooterStateSyncPredictedAction.None, localPrediction.Action);
        Assert.Equal(0, localPrediction.ActionPlayerId);
        Assert.False(localPrediction.NeedsActionCatchUp);

        controller.BufferRemoteSnapshot(PackedProjectileSnapshot(frame: 1, serverTicks: 1000L, ownerPlayerId: 1, bulletId: 77));
        controller.Tick(1f / 30f);

        var authoritativePrediction = controller.PredictionState;
        Assert.Equal(ShooterStateSyncPredictedAction.Fire, authoritativePrediction.Action);
        Assert.Equal(1, authoritativePrediction.ActionPlayerId);
        Assert.Equal(1, authoritativePrediction.ActionSourceFrame);
        Assert.Equal(1000L, authoritativePrediction.ActionSourceServerTicks);
        Assert.True(authoritativePrediction.NeedsActionCatchUp);
        Assert.Equal(77, authoritativePrediction.ActionBulletId);
        Assert.True(authoritativePrediction.ActionCatchUpFrames >= 0);
    }

    [Fact]
    public void ControllerDoesNotPublishRemoteFrameWithoutSnapshots()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);

        controller.Tick(0.1f);

        Assert.False(controller.HasPublishedRemoteFrame);
    }

    [Fact]
    public void ControllerHoldsDespawningActorThroughInBetweenFrame()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var config = new InterpolationConfig(ticksPerSecond: 1000L, interpolationDelayTicks: 100L, bufferCapacity: 16, catchUpRate: 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null, config);

        ShooterSnapshotViewBatch? lastBatch = null;
        presentation.Snapshots.SnapshotApplied += batch => lastBatch = batch;

        // Actor 8 exists in the earlier sample but despawns in the later one.
        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 1, serverTicks: 1000L, Actor(7, 0f), Actor(8, 100f)));
        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 2, serverTicks: 1100L, Actor(7, 10f)));

        // playback = 1050 -> mid-interpolation. Actor 8 (despawned in 'to') must still be present,
        // holding its last pose rather than popping out mid-frame.
        controller.Tick(0.05f);

        var despawningKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 8);
        var heldX = TransformX(lastBatch!.Value, despawningKey);
        Assert.Equal(100f, heldX, 3);
    }

    [Fact]
    public void ControllerFlagsStarvedPlaybackWhenBufferRunsDry()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        // 50ms extrapolation tolerance, snap clock for deterministic playback timing.
        var config = new InterpolationConfig(
            ticksPerSecond: 1000L, interpolationDelayTicks: 0L, bufferCapacity: 16, catchUpRate: 0d, maxExtrapolationTicks: 50L);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null, config);

        controller.BufferRemoteSnapshot(RemoteSnapshot(frame: 1, serverTicks: 1000L, actorX: 0f));

        // No interpolation delay: playback already sits on the newest sample, within tolerance.
        controller.Tick(0f);
        Assert.True(controller.HasPublishedRemoteFrame);
        Assert.False(controller.IsRemotePlaybackStarved);

        // Advance 100ms with no new snapshots: playback runs 100 ticks past newest (> 50ms tolerance).
        controller.Tick(0.1f);
        Assert.True(controller.IsRemotePlaybackStarved);
    }

    [Fact]
    public void LocalSmallPositionErrorKeepsPredictionButRestoresAuthoritativeState()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 0, x: 0f));

        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(
            frame: 1,
            x: 0.04f,
            aimX: 0f,
            aimY: 1f,
            isFull: false,
            hp: 73,
            score: 9));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(0f, player.X, 3);
        Assert.Equal(0f, player.AimX, 3);
        Assert.Equal(1f, player.AimY, 3);
        Assert.Equal(73, player.Hp);
        Assert.Equal(9, player.Score);
    }

    [Fact]
    public void LocalPositionErrorAlwaysUsesBoundedCorrectionAfterInitialAuthority()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 0, x: 0f));

        // 权威帧落后客户端 1 帧：先扣除可达预测距离（PlayerSpeed * dt * 1 = 1/6），
        // 剩余漂移 0.4 - 1/6 由本帧预算 0.25 封顶修正。
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 1, x: 0.4f, isFull: false));
        Assert.True(runtime.TryGetPlayer(1, out var bounded));
        Assert.Equal(0.233f, bounded.X, 3);

        // 同一客户端帧内第二份权威快照共享剩余预算（0.25 - 0.233），只推进到 0.25。
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 2, x: 1f, isFull: false));
        Assert.True(runtime.TryGetPlayer(1, out var stillBounded));
        Assert.Equal(0.25f, stillBounded.X, 3);
    }

    [Fact]
    public void LocalInputDoesNotEnterAuthorityReplayBeforeGatewaySubmissionStarts()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());

        controller.SubmitLocalInput(1, moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false);
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 1, x: 0f));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(0f, player.X, 3);
        Assert.Equal(0, controller.PendingGatewayInputCount);
    }

    [Fact]
    public async Task LocalReconciliationReplaysOnlyLatestCommandForOneSimulationFrame()
    {
        var runtime = new ShooterBattleRuntimePort();
        var gateway = new SequencedGateway();
        var controller = StartedController(runtime, new ShooterPresentationFacade(), gateway);
        var context = new ShooterGatewayBattleInputContext("session", "battle", 9001ul, 10, 1u);

        var first = controller.SubmitLocalInput(1, moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false)
            .WithRequestedFrame(context.Frame);
        await controller.SubmitAcceptedInputToGatewayAsync(context, first);
        var latest = controller.SubmitLocalInput(1, moveX: -1f, moveY: 0f, aimX: -1f, aimY: 0f, fire: false)
            .WithRequestedFrame(context.Frame);
        await controller.SubmitAcceptedInputToGatewayAsync(context, latest);

        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 1, x: 0f));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(-ShooterBattleTuning.PlayerSpeed / 30f, player.X, 3);
        Assert.Equal(-1f, player.AimX, 3);
        Assert.Equal(2, controller.PendingGatewayInputCount);
        IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy = controller;
        Assert.Equal(1, strategy.GetReconciliationReport().ReplayTicks);
    }

    [Fact]
    public async Task FailedGatewaySubmissionDoesNotLeakPendingReplayInput()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade(), new FailingGateway());
        var context = new ShooterGatewayBattleInputContext("session", "battle", 9001ul, 10, 1u);
        var local = controller.SubmitLocalInput(1, 1f, 0f, 1f, 0f, false)
            .WithRequestedFrame(context.Frame);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.SubmitAcceptedInputToGatewayAsync(context, local));

        Assert.Equal(0, controller.PendingGatewayInputCount);
    }

    [Fact]
    public void PeriodicFullBaselineUsesBoundedCorrectionInsteadOfHardSnap()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 0, x: 0f));

        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 1, x: 2f, isFull: true));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(0.25f, player.X, 3);
    }

    [Fact]
    public void AuthorityOverrideUsesBoundedCorrectionInsteadOfHardSnap()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 0, x: 0f));

        // 同世界的 authority override 属于正常恢复流量：与全量基线一致走有界纠偏，
        // 只有世界切换才硬拉回（见 WorldChangeForcesDeltaSnapshotToSnapAndResetsFrameWatermark）。
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(
            frame: 1,
            x: 2f,
            isFull: false,
            flags: ShooterPackedSnapshotFlags.AuthorityOverride));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(0.25f, player.X, 3);
    }

    [Fact]
    public void StaleSnapshotCannotPullLocalPlayerBack()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());

        // 全量基线同世界内不做硬拉回：落后 2 帧扣除可达预测距离后按预算收敛到 0.25。
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 2, x: 2f));
        var result = controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 1, x: 9f));

        Assert.Equal(ShooterSnapshotApplyResult.IgnoredStaleSnapshot, result);
        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(0.25f, player.X, 3);
    }

    [Fact]
    public void PureStateSnapshotStopsBufferedActorFromRepopulatingAoiViews()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var config = new InterpolationConfig(1000L, 0L, 16, 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null, config);
        var publishedBatches = 0;
        var projection = new ShooterSnapshotViewProjection();
        presentation.Snapshots.SnapshotApplied += batch =>
        {
            publishedBatches++;
            projection.Apply(in batch);
        };

        controller.BufferRemoteSnapshot(RemoteSnapshot(1, 1000L, Actor(7, 7f)));
        controller.Tick(0f);
        Assert.Equal(1, publishedBatches);
        Assert.Equal(1, controller.BufferedRemoteSnapshotCount);

        var result = controller.BufferRemoteSnapshot(PureStatePlayerSnapshot(frame: 2, x: 0f));
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, result);
        Assert.Equal(2, publishedBatches);
        Assert.Equal(0, controller.BufferedRemoteSnapshotCount);
        var remotePlayer = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 7);
        Assert.False(projection.Store.ContainsEntity(remotePlayer));

        controller.Tick(0.1f);

        Assert.Equal(2, publishedBatches);
        Assert.False(controller.HasPublishedRemoteFrame);
        Assert.False(projection.Store.ContainsEntity(remotePlayer));
    }

    [Fact]
    public void PureStateRenderBatchCombinesRemoteStateWithImmediateControlledPrediction()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        controller.SubmitLocalInput(1, 1f, 0f, 1f, 0f, false);

        var result = controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 4f,
            isFull: true));
        controller.Tick(1f / 30f);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, result);
        var render = presentation.RenderBatch;
        var localKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
        var remoteKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        Assert.Contains(render.EntityChanges, change => change.Key.Equals(remoteKey));
        Assert.Contains(render.TransformChanges, change => change.Key.Equals(remoteKey));
        Assert.Contains(render.TransformChanges, change => change.Key.Equals(localKey));
    }

    [Fact]
    public void PureStateRenderBatchInterpolatesSparseRemoteTransformsEveryRenderTick()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        var remoteKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 0f,
            isFull: true,
            stateHash: 123u));
        controller.Tick(0f);
        var deltaResult = controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 10f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 124u));
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, deltaResult);
        var nextDeltaResult = controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 30,
            x: 20f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 125u));
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, nextDeltaResult);

        controller.Tick(5f / 30f);

        var interpolatedX = TransformX(presentation.RenderBatch, remoteKey);
        Assert.Equal(5f, interpolatedX, 2);
    }

    [Fact]
    public void PureStateSampleBlockPlaysIntermediateFramesAcrossRenderTicks()
    {
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(new ShooterBattleRuntimePort(), presentation);
        var remoteKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var frames = new[]
        {
            new ShooterPureStateFrameSample(1, 100L, 0, 1),
            new ShooterPureStateFrameSample(2, 200L, 1, 1)
        };
        var transforms = new[]
        {
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 1000, 0, 1000, 0, flags),
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 2000, 0, 1000, 0, flags)
        };

        var result = controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 3,
            x: 3f,
            isFull: true,
            deltaIntervalFrames: 1,
            interpolationDelayFrames: 1,
            frameSamples: frames,
            transformSamples: transforms));
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, result);

        controller.Tick(0f);
        Assert.Equal(1f, TransformX(presentation.RenderBatch, remoteKey), 2);
        controller.Tick(1f / 30f);
        Assert.Equal(2f, TransformX(presentation.RenderBatch, remoteKey), 2);

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(1, diagnostics.ReceivedSampleBlockCount);
        Assert.Equal(2, diagnostics.ReceivedFrameSampleCount);
        Assert.Equal(2, diagnostics.MaxTransformSampleCountPerBlock);
        Assert.Equal(2, diagnostics.ReceivedTransformSampleCount);
        Assert.Equal(1, diagnostics.ReceivedAuthoritativeTransformCount);
        Assert.Equal(1d, diagnostics.AverageTransformSamplesPerFrame);
        Assert.Equal(2d, diagnostics.HistoricalTransformAmplificationRatio);
        Assert.Equal(3, diagnostics.PublishedSnapshotCount);
        Assert.Equal(2f, diagnostics.BufferedFrameSpan);
        Assert.Equal(2, diagnostics.ObservedTransformSampleIntervalCount);
        Assert.Equal(1, diagnostics.TransformSampleIntervalP50Frames);
        Assert.Equal(1, diagnostics.TransformSampleIntervalP95Frames);
        Assert.Equal(1, diagnostics.TransformSampleIntervalP99Frames);
        Assert.Equal(1, diagnostics.TransformSampleIntervalMaxFrames);
    }

    [Fact]
    public void PureStateSampleBlockRejectsDuplicateHistoricalFramesWithoutRewinding()
    {
        var controller = StartedController(new ShooterBattleRuntimePort(), new ShooterPresentationFacade());
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var frames = new[] { new ShooterPureStateFrameSample(2, 200L, 0, 1) };
        var transforms = new[]
        {
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 2000, 0, 1000, 0, flags)
        };

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 3,
            x: 3f,
            isFull: true,
            deltaIntervalFrames: 1,
            interpolationDelayFrames: 1,
            frameSamples: frames,
            transformSamples: transforms));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 4,
            x: 4f,
            isFull: false,
            stateHash: 124u,
            deltaIntervalFrames: 1,
            interpolationDelayFrames: 1,
            frameSamples: frames,
            transformSamples: transforms));

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(2, diagnostics.ReceivedSampleBlockCount);
        Assert.Equal(1, diagnostics.ReceivedFrameSampleCount);
        Assert.Equal(1, diagnostics.RejectedFrameSampleCount);
        Assert.Equal(1, diagnostics.StaleFrameSampleCount);
        Assert.Equal(0, diagnostics.InvalidFrameSampleCount);
        Assert.Equal(3, diagnostics.PublishedSnapshotCount);
    }

    [Fact]
    public void PureStateSampleIntervalsExcludeTimeOutsideAoi()
    {
        var controller = StartedController(new ShooterBattleRuntimePort(), new ShooterPresentationFacade());
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var frames = new[]
        {
            new ShooterPureStateFrameSample(1, 100L, 0, 1),
            new ShooterPureStateFrameSample(2, 200L, 1, 1)
        };
        var transforms = new[]
        {
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 1000, 0, 1000, 0, flags),
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 2000, 0, 1000, 0, flags)
        };

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 3,
            x: 3f,
            isFull: true,
            deltaIntervalFrames: 1,
            interpolationDelayFrames: 1,
            frameSamples: frames,
            transformSamples: transforms));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 30,
            x: 3f,
            isFull: false,
            stateHash: 124u,
            deltaKind: ShooterPureStateDeltaKinds.Despawn));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 60,
            x: 4f,
            isFull: false,
            stateHash: 125u,
            deltaKind: ShooterPureStateDeltaKinds.Spawn));

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(2, diagnostics.ObservedTransformSampleIntervalCount);
        Assert.Equal(1, diagnostics.TransformSampleIntervalP99Frames);
        Assert.Equal(1, diagnostics.TransformSampleIntervalMaxFrames);
    }

    [Fact]
    public void PureStateSampleBlockClassifiesFutureHistoricalFrameAsInvalid()
    {
        var controller = StartedController(new ShooterBattleRuntimePort(), new ShooterPresentationFacade());
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var frames = new[] { new ShooterPureStateFrameSample(4, 400L, 0, 1) };
        var transforms = new[]
        {
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Player, 4000, 0, 1000, 0, flags)
        };

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 4,
            x: 4f,
            isFull: true,
            deltaIntervalFrames: 1,
            interpolationDelayFrames: 1,
            frameSamples: frames,
            transformSamples: transforms));

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(1, diagnostics.RejectedFrameSampleCount);
        Assert.Equal(0, diagnostics.StaleFrameSampleCount);
        Assert.Equal(1, diagnostics.InvalidFrameSampleCount);
    }

    [Fact]
    public void PureStatePlaybackUsesAtLeastTwoDeltaBlocksOfDelay()
    {
        var controller = StartedController(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade());

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 0,
            x: 0f,
            isFull: true,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(6, diagnostics.BaseDelayFrames);
        Assert.Equal(9, diagnostics.MaxDelayFrames);
        Assert.Equal(6f, diagnostics.CurrentDelayFrames);
        Assert.Equal(6f, diagnostics.TargetDelayFrames);
        Assert.Equal(1, diagnostics.PublishedSnapshotCount);
    }

    [Fact]
    public void PureStatePlaybackRaisesTargetToThreeBlocksAfterStarvation()
    {
        var controller = StartedController(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 0,
            x: 0f,
            isFull: true,
            stateHash: 100u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 3,
            x: 3f,
            isFull: false,
            stateHash: 101u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 6,
            x: 6f,
            isFull: false,
            stateHash: 102u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.Tick(0f);

        controller.Tick(7f / 30f);

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.True(diagnostics.IsStarved);
        Assert.Equal(9f, diagnostics.TargetDelayFrames);
        Assert.Equal(1, diagnostics.StarvedRenderTickCount);
        Assert.Equal(2, diagnostics.RenderTickCount);
    }

    [Fact]
    public void PureStatePlaybackRecoversDelayGraduallyWithoutMovingBackwards()
    {
        var controller = StartedController(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 0,
            x: 0f,
            isFull: true,
            stateHash: 100u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 3,
            x: 3f,
            isFull: false,
            stateHash: 101u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 6,
            x: 6f,
            isFull: false,
            stateHash: 102u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.Tick(0f);
        controller.Tick(7f / 30f);
        Assert.Equal(9f, controller.PureStatePlaybackDiagnostics.TargetDelayFrames);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 100,
            x: 100f,
            isFull: true,
            stateHash: 103u,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        var previousPlaybackFrame = controller.PureStatePlaybackDiagnostics.PlaybackFrame;
        for (var i = 0; i < 60; i++)
        {
            controller.Tick(1f / 30f);
            var current = controller.PureStatePlaybackDiagnostics.PlaybackFrame;
            Assert.True(current >= previousPlaybackFrame);
            previousPlaybackFrame = current;
        }

        Assert.Equal(9f, controller.PureStatePlaybackDiagnostics.TargetDelayFrames);
        controller.Tick(1f / 30f);
        var recoveryStarted = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(6f, recoveryStarted.TargetDelayFrames);
        Assert.Equal(9f, recoveryStarted.CurrentDelayFrames, 3);

        for (var i = 0; i < 24; i++)
        {
            controller.Tick(1f / 30f);
            var current = controller.PureStatePlaybackDiagnostics.PlaybackFrame;
            Assert.True(current >= previousPlaybackFrame);
            previousPlaybackFrame = current;
        }

        Assert.Equal(6f, controller.PureStatePlaybackDiagnostics.CurrentDelayFrames, 3);
    }

    [Fact]
    public void PureStatePlaybackDiagnosticsResetWhenLeavingPureStateMode()
    {
        var controller = StartedController(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade());
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 0,
            x: 0f,
            isFull: true,
            deltaIntervalFrames: 3,
            interpolationDelayFrames: 3));
        controller.Tick(0f);
        Assert.True(controller.PureStatePlaybackDiagnostics.RenderTickCount > 0);

        controller.BufferRemoteSnapshot(RemoteSnapshot(1, 1000L, actorX: 0f));

        var diagnostics = controller.PureStatePlaybackDiagnostics;
        Assert.Equal(0, diagnostics.RenderTickCount);
        Assert.Equal(0, diagnostics.PublishedSnapshotCount);
        Assert.Equal(2, diagnostics.BaseDelayFrames);
        Assert.Equal(3, diagnostics.MaxDelayFrames);
        Assert.Equal(0, diagnostics.BufferedSnapshotCount);
    }

    [Fact]
    public void PureStateRenderBatchPreservesMultipleLifecycleDeltasReceivedBeforeTick()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        var player2 = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
        var player3 = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 3);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 0f,
            isFull: true,
            stateHash: 123u));
        controller.Tick(0f);
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 0f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 124u,
            entityId: 2,
            deltaKind: ShooterPureStateDeltaKinds.Despawn));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 30,
            x: 3f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 125u,
            entityId: 3,
            deltaKind: ShooterPureStateDeltaKinds.Spawn));

        controller.Tick(1f / 30f);

        var render = presentation.RenderBatch;
        Assert.Contains(player2, render.RemovedEntities);
        Assert.Contains(render.EntityChanges, change => change.Key.Equals(player3) && change.Alive);
    }

    [Fact]
    public void PureStateLifecycleAccumulatorKeepsLaterSpawnAsFinalState()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        var remote = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 0f,
            isFull: true,
            stateHash: 123u));
        controller.Tick(0f);
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 0f,
            isFull: false,
            stateHash: 124u,
            deltaKind: ShooterPureStateDeltaKinds.Despawn));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 30,
            x: 3f,
            isFull: false,
            stateHash: 125u,
            deltaKind: ShooterPureStateDeltaKinds.Spawn));

        controller.Tick(1f / 30f);

        var render = presentation.RenderBatch;
        Assert.DoesNotContain(remote, render.RemovedEntities);
        Assert.Contains(render.EntityChanges, change => change.Key.Equals(remote) && change.Alive);
    }

    [Fact]
    public void PureStateLifecycleAccumulatorKeepsLaterDespawnAsFinalState()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        var remote = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 0f,
            isFull: true,
            stateHash: 123u));
        controller.Tick(0f);
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 3f,
            isFull: false,
            stateHash: 124u,
            deltaKind: ShooterPureStateDeltaKinds.Spawn));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 30,
            x: 3f,
            isFull: false,
            stateHash: 125u,
            deltaKind: ShooterPureStateDeltaKinds.Despawn));

        controller.Tick(1f / 30f);

        var render = presentation.RenderBatch;
        Assert.Contains(remote, render.RemovedEntities);
        Assert.DoesNotContain(render.EntityChanges, change => change.Key.Equals(remote) && change.Alive);
        Assert.DoesNotContain(render.TransformChanges, change => change.Key.Equals(remote));
    }

    [Fact]
    public void PureStateDelayedPlaybackCannotRecreateAoiDespawnFromOldTransform()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);
        var projection = new ShooterSnapshotViewProjection();
        var remote = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 2f,
            isFull: true,
            stateHash: 123u));
        controller.Tick(0f);
        projection.Apply(presentation.RenderBatch);
        Assert.True(projection.Store.ContainsEntity(remote));

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 2f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 124u,
            entityId: 2,
            deltaKind: ShooterPureStateDeltaKinds.Despawn));
        controller.Tick(1f / 30f);
        projection.Apply(presentation.RenderBatch);

        Assert.False(projection.Store.ContainsEntity(remote));
        Assert.DoesNotContain(
            presentation.RenderBatch.TransformChanges,
            change => change.Key.Equals(remote));
    }

    [Fact]
    public void PureStatePlaybackTickKeepsSteadyStateAllocationBoundedAfterWarmup()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = StartedController(runtime, presentation);

        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 10,
            x: 0f,
            isFull: true,
            stateHash: 123u));
        controller.BufferRemoteSnapshot(PureStateRemotePlayerSnapshot(
            frame: 20,
            x: 10f,
            isFull: false,
            baselineFrame: 0,
            baselineHash: 0u,
            stateHash: 124u));
        for (var i = 0; i < 128; i++)
        {
            controller.Tick(1f / 60f);
        }

        var checksum = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            // Zero simulation delta isolates render-frame playback/composition from fixed-step
            // world snapshot export, whose allocation budget is covered by runtime tests.
            controller.Tick(0f);
            checksum += presentation.RenderBatch.SampleFrame;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0f);
        Assert.True(allocated < 4_096L, $"PureState playback allocated {allocated} bytes after warmup.");
    }

    [Fact]
    public void AuthoritativeControllerSkipsWholeWorldPredictionHistoriesAndTickHashesByDefault()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());

        for (var i = 0; i < 32; i++)
        {
            var result = controller.Tick(1f / 30f);
            Assert.Equal(0u, result.StateHash);
        }

        Assert.False(controller.FrameSync.HasFrameworkInputHistory);
        Assert.False(controller.FrameSync.HasRollbackSnapshotHistory);
        Assert.False(controller.FrameSync.HasStateHashHistory);
        Assert.False(controller.FrameSync.ComputeTickResultStateHash);
        Assert.Equal(0L, runtime.StateHashCacheDiagnostics.ComputationCount);
    }

    [Fact]
    public void ControlledPlayerIsExcludedFromRemoteInterpolation()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var config = new InterpolationConfig(1000L, 0L, 16, 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null, config);
        ShooterSnapshotViewBatch? lastBatch = null;
        presentation.Snapshots.SnapshotApplied += batch => lastBatch = batch;

        controller.BufferRemoteSnapshot(RemoteSnapshot(1, 1000L, Actor(1, 20f), Actor(7, 7f)));
        controller.Tick(0f);

        Assert.NotNull(lastBatch);
        Assert.DoesNotContain(lastBatch!.Value.TransformChanges,
            change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1)));
        Assert.Contains(lastBatch.Value.TransformChanges,
            change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 7)));
    }

    [Fact]
    public void PackedAndPureStateSnapshotsApplyEquivalentLocalAuthority()
    {
        var packedRuntime = new ShooterBattleRuntimePort();
        var pureRuntime = new ShooterBattleRuntimePort();
        var packedController = StartedController(packedRuntime, new ShooterPresentationFacade());
        var pureController = StartedController(pureRuntime, new ShooterPresentationFacade());

        packedController.BufferRemoteSnapshot(PackedPlayerSnapshot(
            frame: 3, x: 2f, y: -1f, aimX: 0f, aimY: 1f, hp: 81, score: 4));
        pureController.BufferRemoteSnapshot(PureStatePlayerSnapshot(
            frame: 3, x: 2f, y: -1f, aimX: 0f, aimY: 1f, hp: 81, score: 4));

        Assert.True(packedRuntime.TryGetPlayer(1, out var packed));
        Assert.True(pureRuntime.TryGetPlayer(1, out var pure));
        Assert.Equal(packed.X, pure.X, 3);
        Assert.Equal(packed.Y, pure.Y, 3);
        Assert.Equal(packed.AimX, pure.AimX, 3);
        Assert.Equal(packed.AimY, pure.AimY, 3);
        Assert.Equal(packed.Hp, pure.Hp);
        Assert.Equal(packed.Score, pure.Score);
        Assert.Equal(packed.Alive, pure.Alive);
    }

    [Fact]
    public void WorldChangeForcesDeltaSnapshotToSnapAndResetsFrameWatermark()
    {
        var runtime = new ShooterBattleRuntimePort();
        var controller = StartedController(runtime, new ShooterPresentationFacade());

        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(frame: 10, x: 1f, worldId: 9001ul));
        controller.BufferRemoteSnapshot(PackedPlayerSnapshot(
            frame: 1, x: 3f, isFull: false, worldId: 9002ul));

        Assert.True(runtime.TryGetPlayer(1, out var player));
        Assert.Equal(3f, player.X, 3);
    }

    private static float TransformX(in ShooterSnapshotViewBatch batch, ShooterViewEntityKey key)
    {
        foreach (var change in batch.TransformChanges)
        {
            if (change.Key.Equals(key))
            {
                return change.X;
            }
        }

        throw new Xunit.Sdk.XunitException($"Transform change for {key.Kind}:{key.EntityId} not found in batch.");
    }

    private sealed class SequencedGateway : IShooterRoomGatewayClient
    {
        private ulong _sequence;

        public Task<ShooterGatewayBattleInputResult> SubmitBattleInputAsync(
            ShooterGatewayBattleInputContext context,
            ShooterInputPacket packet,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            _sequence++;
            return Task.FromResult(new ShooterGatewayBattleInputResult(
                success: true,
                acceptedFrame: context.Frame,
                message: string.Empty,
                currentFrame: context.Frame,
                status: string.Empty,
                shouldResync: false,
                serverTicks: 0L,
                commandSequence: _sequence));
        }
    }

    private sealed class FailingGateway : IShooterRoomGatewayClient
    {
        public Task<ShooterGatewayBattleInputResult> SubmitBattleInputAsync(
            ShooterGatewayBattleInputContext context,
            ShooterInputPacket packet,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ShooterGatewayBattleInputResult>(
                new InvalidOperationException("simulated gateway failure"));
        }
    }
}

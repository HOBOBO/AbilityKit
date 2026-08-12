using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private const float SynchronizationHealthSampleIntervalSeconds = 0.5f;

        private MobaClientAuthoritativeInterpolationSyncController _remoteInterpolationController =>
            _runtime.Replication.InterpolationController;
        private MobaClientReplicationPipeline _remoteReplicationPipeline =>
            _runtime.Replication.ReplicationPipeline;
        private MobaSynchronizationHealthEvaluator _synchronizationHealthEvaluator =>
            _runtime.Replication.SynchronizationHealthEvaluator;
        private MobaSynchronizationHealthSnapshot _synchronizationHealth
        {
            get => _runtime.Replication.SynchronizationHealth;
            set => _runtime.Replication.SynchronizationHealth = value;
        }
        private SyncHealthReport _synchronizationHealthReport
        {
            get => _runtime.Replication.SynchronizationHealthReport;
            set => _runtime.Replication.SynchronizationHealthReport = value;
        }
        private float _synchronizationHealthSampleElapsed
        {
            get => _runtime.Replication.SynchronizationHealthSampleElapsed;
            set => _runtime.Replication.SynchronizationHealthSampleElapsed = value;
        }
        private MobaSnapshotAdmission _snapshotAdmission =>
            _runtime.Replication.SnapshotAdmission;
        private MobaAuthoritativeSnapshotState _authoritativeSnapshotState =>
            _runtime.Replication.AuthoritativeSnapshotState;
        private const int MaxPendingReliableEventBatches = 32;
        private const int MaxReliableEventAckAttempts = 3;

        private MobaReliableBattleEventCursor _reliableEventCursor =>
            _runtime.Replication.ReliableEventCursor;
        private Queue<WireReliableBattleEventPush> _pendingReliableEventBatches =>
            _runtime.Replication.PendingReliableEventBatches;
        private NetworkTransport _interpolationTransport =>
            _runtime.Replication.Transport;
        private int _lastServerAckFrame
        {
            get => _runtime.Replication.LastServerAckFrame;
            set => _runtime.Replication.LastServerAckFrame = value;
        }
        private bool _pendingStateImport
        {
            get => _runtime.Replication.PendingStateImport;
            set => _runtime.Replication.PendingStateImport = value;
        }

        private BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions opts)
        {
            var world = _plan.World;
            var gateway = _plan.Gateway;

            if (_plan.HostMode == BattleHostMode.GatewayRemote && gateway.UseGatewayTransport)
            {
                if (!uint.TryParse(world.PlayerId, out var localPlayerId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric PlayerId. playerId='{world.PlayerId}'");
                }

                var roomId = gateway.NumericRoomId;
                if (roomId == 0 && !ulong.TryParse(world.WorldId, out roomId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric WorldId(roomId). worldId='{world.WorldId}'");
                }

                var transport = _transportFactory.CreateGatewayRemoteTransport(
                    _plan,
                    localPlayerId,
                    roomId,
                    _unityDispatcher,
                    _networkIoDispatcher);

                // 远端实体插值播放：Gateway 推送 SnapshotPushed → 统一复制管线 → 每帧投影。
                if (transport is NetworkTransport networkTransport)
                {
                    var reliableEventCheckpoint = _plan.ReliableEventCheckpoint;
                    var checkpointAccepted = _runtime.Replication.Build(
                        networkTransport,
                        _plan.World.TickRate,
                        roomId,
                        gateway.BattleId ?? string.Empty,
                        in reliableEventCheckpoint,
                        OnStateSyncSnapshotPushed,
                        OnReliableEventsPushed,
                        OnBattleConnectionClosed,
                        OnBattleConnectionEstablished,
                        OnBattleAuthenticationFailed);
                    if (!checkpointAccepted)
                    {
                        Log.Warning(
                            "[BattleSessionFeature] Reliable event checkpoint rejected " +
                            "because it does not match the active battle.");
                    }

                    _battleConnectionRecoveryPending = false;
                    if (_ctx != null)
                    {
                        _ctx.CanSubmitGameplayInput = false;
                        _ctx.EnableRemoteInterpolation = true;
                    }
                }

                return _sessionRegistry.Start(opts, remoteTransport: transport);
            }

            return _sessionRegistry.Start(opts);
        }

        private void OnStateSyncSnapshotPushed(object rawSnapshot)
        {
            if (rawSnapshot is not GatewayStateSyncSnapshot snapshot ||
                _snapshotAdmission == null)
            {
                return;
            }

            var admission = _snapshotAdmission.Admit(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.IsFullSnapshot,
                snapshot.SchemaVersion);
            if (!admission.Accepted)
            {
                Log.Warning(
                    $"[BattleSessionFeature] Snapshot rejected. status={admission.Status} " +
                    $"worldId={snapshot.WorldId} frame={snapshot.Frame} " +
                    $"lastAcceptedFrame={admission.LastAcceptedFrame}");
                if (admission.ShouldRequestFullResync)
                {
                    RequestFullStateSync(
                        $"snapshot-admission:{admission.Status}",
                        admission.LastAcceptedFrame);
                }
                return;
            }

            // Initial connect and reconnect both require a successfully imported full baseline.
            if (_pendingStateImport)
            {
                if (!snapshot.IsFullSnapshot || !TryImportStateIntoLogicWorld(in snapshot))
                {
                    _snapshotAdmission.RequireFullBaseline();
                    _authoritativeSnapshotState?.Reset();
                    RequestFullStateSync("state-import-failed", snapshot.Frame);
                    return;
                }
            }
            else if (!snapshot.IsFullSnapshot)
            {
                ApplyRemovedActorsToLogicWorld(in snapshot);
            }

            // Snapshot-authority sessions do not necessarily receive legacy FramePacket pushes.
            // A successfully imported authoritative snapshot satisfies the same first-frame barrier.
            NotifyFirstFrameReceivedOnce();

            var materialized = _authoritativeSnapshotState?.Apply(in snapshot) ?? snapshot;
            var sample = new MobaRemoteSnapshotSample(
                materialized.WorldId,
                materialized.Frame,
                materialized.Actors);
            _remoteReplicationPipeline?.ObserveRemote(in sample);
        }

        private void OnReliableEventsPushed(object rawPush)
        {
            if (rawPush is not WireReliableBattleEventPush push ||
                _reliableEventCursor == null)
            {
                return;
            }

            if (_pendingStateImport)
            {
                QueueReliableEventBatch(in push);
                return;
            }

            var result = _reliableEventCursor.Admit(in push);
            if (!result.Accepted)
            {
                Log.Warning(
                    $"[BattleSessionFeature] Reliable event batch rejected. " +
                    $"status={result.Status} epoch={result.Epoch} " +
                    $"expected={result.ExpectedSequence} received={result.ReceivedSequence}");
                InvalidateAuthoritativeTimeline(
                    $"reliable-events:{result.Status}");
                QueueReliableEventBatch(in push);
                return;
            }

            if (result.Events.Length == 0)
            {
                if (_reliableEventCursor.LastDeliveredSequence >
                    _reliableEventCursor.LastAcknowledgedSequence)
                {
                    _ = AcknowledgeReliableEventsAsync(
                        _reliableEventCursor.Epoch,
                        _reliableEventCursor.LastDeliveredSequence);
                }
                return;
            }

            try
            {
                for (var i = 0; i < result.Events.Length; i++)
                {
                    ReliableBattleEventReceived?.Invoke(result.Events[i]);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(
                    ex,
                    "[BattleSessionFeature] Reliable battle event delivery failed.");
                return;
            }

            if (!_reliableEventCursor.CommitDelivered(
                    result.Epoch,
                    result.CommitSequence))
            {
                InvalidateAuthoritativeTimeline(
                    "reliable-events:commit-rejected");
                return;
            }

            _ = AcknowledgeReliableEventsAsync(
                result.Epoch,
                result.CommitSequence);
        }

        private async Task AcknowledgeReliableEventsAsync(
            string epoch,
            long sequence)
        {
            var transport = _interpolationTransport;
            var cursor = _reliableEventCursor;
            if (transport == null || cursor == null ||
                string.IsNullOrWhiteSpace(epoch) || sequence <= 0)
            {
                return;
            }

            var acceptedSequence = -1L;
            for (var attempt = 1; attempt <= MaxReliableEventAckAttempts; attempt++)
            {
                acceptedSequence = await transport.AcknowledgeReliableEventsAsync(
                    epoch,
                    sequence);

                if (!ReferenceEquals(cursor, _reliableEventCursor) ||
                    !string.Equals(cursor.Epoch, epoch, StringComparison.Ordinal))
                {
                    return;
                }

                if (acceptedSequence >= 0 &&
                    cursor.ConfirmAcknowledged(epoch, acceptedSequence) &&
                    acceptedSequence >= sequence)
                {
                    PersistReliableEventCheckpoint(cursor);
                    return;
                }

                if (attempt < MaxReliableEventAckAttempts)
                {
                    await Task.Delay(50 * attempt);
                }
            }

            Log.Warning(
                $"[BattleSessionFeature] Reliable event ACK did not reach requested cursor. " +
                $"epoch={epoch} requested={sequence} accepted={acceptedSequence}");
            InvalidateAuthoritativeTimeline(
                "reliable-events:ack-incomplete");
        }

        private void QueueReliableEventBatch(in WireReliableBattleEventPush push)
        {
            if (_pendingReliableEventBatches.Count >= MaxPendingReliableEventBatches)
            {
                _pendingReliableEventBatches.Clear();
                RequestFullStateSync(
                    "reliable-events:pending-queue-overflow",
                    _snapshotAdmission?.LastAcceptedFrame ?? 0);
                return;
            }

            _pendingReliableEventBatches.Enqueue(push);
        }

        private bool CompleteReliableEventRecovery(
            in GatewayStateSyncSnapshot snapshot)
        {
            var cursor = _reliableEventCursor;
            if (cursor == null)
            {
                _pendingReliableEventBatches.Clear();
                _pendingStateImport = false;
                return true;
            }

            if (string.IsNullOrWhiteSpace(snapshot.EventEpoch))
            {
                Log.Warning(
                    "[BattleSessionFeature] Full snapshot rejected: reliable event epoch is missing.");
                _pendingReliableEventBatches.Clear();
                return false;
            }

            if (!cursor.AdoptAuthoritativeBaseline(
                    snapshot.EventEpoch,
                    snapshot.EventWatermark))
            {
                _pendingReliableEventBatches.Clear();
                return false;
            }

            PersistReliableEventCheckpoint(cursor);

            if (snapshot.EventWatermark > 0)
            {
                _ = AcknowledgeReliableEventsAsync(
                    snapshot.EventEpoch,
                    snapshot.EventWatermark);
            }

            _pendingStateImport = false;
            while (!_pendingStateImport &&
                   _pendingReliableEventBatches.Count > 0)
            {
                var pending = _pendingReliableEventBatches.Dequeue();
                if (!string.IsNullOrWhiteSpace(snapshot.EventEpoch) &&
                    !string.Equals(pending.Epoch, snapshot.EventEpoch, StringComparison.Ordinal))
                {
                    continue;
                }

                OnReliableEventsPushed(pending);
            }

            return !_pendingStateImport;
        }

        private void InvalidateAuthoritativeTimeline(string reason)
        {
            _pendingStateImport = true;
            if (_ctx != null) _ctx.CanSubmitGameplayInput = false;
            _snapshotAdmission?.RequireFullBaseline();
            _authoritativeSnapshotState?.Reset();
            _remoteInterpolationController?.Reset();
            RequestFullStateSync(reason, _snapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        private void PersistReliableEventCheckpoint(
            MobaReliableBattleEventCursor cursor)
        {
            if (cursor == null ||
                _bootstrapper is not IMobaReliableBattleEventCheckpointStore store)
            {
                return;
            }

            var checkpoint = cursor.CreateCheckpoint();
            if (checkpoint.IsValid)
            {
                store.Save(in checkpoint);
            }
        }

        /// <summary>
        /// 把 FullSnapshot 导入重建后的预测世界：
        /// 重建世界 → 解析 MobaLogicWorldStateImporter → 导入 actor 状态 → 对齐帧号。
        /// 导入成功后预测驱动与哈希对账从该帧恢复。
        /// </summary>
        private bool TryImportStateIntoLogicWorld(in GatewayStateSyncSnapshot snapshot)
        {
            if (_ctx == null || _handles.Session == null) return false;

            // 重建预测世界（EnsureStarted 幂等——世界在 ResetStateAfterReconnect 已销毁）
            StartRemoteDrivenLocalWorld();

            var world = _handles.RemoteDriven.World;
            if (world?.Services == null)
            {
                Log.Warning("[BattleSessionFeature] State import skipped: RemoteDriven world unavailable after recreate.");
                return false;
            }

            if (!world.Services.TryResolve<AbilityKit.Demo.Moba.Services.StateImport.MobaLogicWorldStateImporter>(out var importer) || importer == null)
            {
                Log.Warning("[BattleSessionFeature] State import skipped: MobaLogicWorldStateImporter not registered in world services.");
                return false;
            }

            var actors = snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
            var imports = new AbilityKit.Demo.Moba.Services.StateImport.MobaActorStateImport[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            {
                var a = actors[i];
                imports[i] = new AbilityKit.Demo.Moba.Services.StateImport.MobaActorStateImport(
                    a.ActorId, a.X, a.Y, a.Z, a.Rotation, a.Hp, a.HpMax, a.TeamId, a.Kind, a.Code, a.OwnerNetId);
            }

            var result = importer.Import(imports, snapshot.Frame, isFullSnapshot: true);
            Log.Info($"[BattleSessionFeature] State import done. frame={snapshot.Frame} {result}");
            if (result.Failed > 0)
            {
                Log.Warning(
                    $"[BattleSessionFeature] State import incomplete. frame={snapshot.Frame} " +
                    $"failed={result.Failed}");
                return false;
            }

            var runtime = _handles.RemoteDriven.Runtime;
            if (runtime == null ||
                !runtime.Features.TryGetFeature<IClientPredictionBaselineControl>(out var baseline) ||
                baseline == null ||
                !baseline.TryRebase(world.Id, new FrameIndex(snapshot.Frame)))
            {
                Log.Warning(
                    $"[BattleSessionFeature] State import could not rebase prediction history. " +
                    $"frame={snapshot.Frame} worldId={world.Id.Value}");
                return false;
            }

            // Frame alignment and recovery completion only follow a successful import and rebase.
            _runtime.Simulation.RemoteDrivenLastTickedFrame = snapshot.Frame;
            if (!CompleteReliableEventRecovery(in snapshot))
            {
                return false;
            }

            _ctx.CanSubmitGameplayInput = true;
            return true;
        }

        private void ApplyRemovedActorsToLogicWorld(in GatewayStateSyncSnapshot snapshot)
        {
            var removedActorIds = snapshot.RemovedActorIds;
            if (removedActorIds == null || removedActorIds.Length == 0)
            {
                return;
            }

            var world = _handles.RemoteDriven.World;
            if (world?.Services == null
                || !world.Services.TryResolve<AbilityKit.Demo.Moba.Services.StateImport.MobaLogicWorldStateImporter>(out var importer)
                || importer == null)
            {
                Log.Warning(
                    $"[BattleSessionFeature] Authoritative actor removals skipped: importer unavailable. " +
                    $"frame={snapshot.Frame} count={removedActorIds.Length}");
                return;
            }

            var removed = importer.ApplyRemovedActors(removedActorIds, snapshot.Frame);
            Log.Info(
                $"[BattleSessionFeature] Authoritative actor removals applied. " +
                $"frame={snapshot.Frame} requested={removedActorIds.Length} removed={removed}");
        }

        private void RequestFullStateSync(string reason, int lastAuthoritativeFrame)
        {
            if (_interpolationTransport == null) return;
            _ = _interpolationTransport.RequestFullStateSyncAsync(
                reason,
                lastAuthoritativeFrame);
        }

        public MobaSynchronizationHealthSnapshot SynchronizationHealth =>
            _runtime.Diagnostics.SynchronizationHealth;

        public SyncHealthReport SynchronizationHealthReport =>
            _runtime.Diagnostics.SynchronizationHealthReport;

        private void TickRemoteInterpolation(float deltaTime)
        {
            if (_remoteInterpolationController == null || _remoteReplicationPipeline == null || _ctx == null) return;

            _remoteReplicationPipeline.Tick(deltaTime);
            TickSynchronizationHealth(deltaTime);

            if (_remoteInterpolationController.TryProjectRemoteFrame(out var projected))
            {
                // 预测世界存在时本地玩家由 PredictionViewBridge 驱动（插值跳过）；
                // 不存在时（如断线重连降级后）本地玩家也交给插值驱动。
                var localActorId = _handles.RemoteDriven.World != null ? _ctx.LocalActorId : 0;
                BattleRemoteInterpolationApplier.Apply(_ctx, in projected, localActorId);
            }
        }

        private void TickSynchronizationHealth(float deltaTime)
        {
            var evaluator = _synchronizationHealthEvaluator;
            if (evaluator == null || _remoteInterpolationController == null || _remoteReplicationPipeline == null)
            {
                return;
            }

            _synchronizationHealthSampleElapsed += Math.Max(0f, deltaTime);
            if (_synchronizationHealthSampleElapsed < SynchronizationHealthSampleIntervalSeconds)
            {
                return;
            }
            _synchronizationHealthSampleElapsed = 0f;

            var replication = _remoteReplicationPipeline.GetDiagnostics();
            _synchronizationHealthReport = replication.Health;
            var interpolation = _remoteInterpolationController.GetInterpolationDiagnostics();
            var prediction = _ctx?.PredictionStats;
            var tuning = _ctx?.PredictionTuningControl;
            var sample = new MobaSynchronizationHealthSample(
                _pendingStateImport || replication.Reconciliation.NeedsFullSnapshot,
                replication.UnacknowledgedInputFrames,
                Math.Max(0, replication.LastObservedFrame - replication.LastTick.Frame),
                interpolation.IsRemotePlaybackStarved,
                interpolation.BufferedRemoteSnapshotCount,
                interpolation.PlaybackDelayTicks,
                prediction?.CurrentBacklogEwma ?? 0f,
                prediction?.IsPredictionStalledByWindow ?? false,
                prediction?.IsPredictionStalledByIdealFrame ?? false,
                prediction?.IsReplaying ?? false,
                prediction?.TotalRollbackCount ?? 0L,
                prediction?.TotalRollbackRestoreFailed ?? 0L,
                prediction?.TotalReplayTimeout ?? 0L,
                prediction?.TotalReconcileMismatch ?? 0L,
                tuning?.MaxPredictionAheadFrames ?? prediction?.MaxPredictionAheadFrames ?? 6,
                tuning?.MinPredictionWindow ?? prediction?.MinPredictionWindow ?? 2,
                tuning?.BacklogEwmaAlpha ?? prediction?.BacklogEwmaAlpha ?? 0.2f);

            _synchronizationHealth = evaluator.Evaluate(in sample);
            var recommendation = _synchronizationHealth.Tuning;
            ApplySynchronizationTuning(tuning, recommendation);
        }

        private static void ApplySynchronizationTuning(
            AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionTuningControl tuning,
            MobaPredictionTuningRecommendation recommendation)
        {
            if (tuning == null || !recommendation.ShouldApply)
            {
                return;
            }

            if (recommendation.ResetDefaults)
            {
                tuning.ResetDefaults();
                return;
            }

            tuning.SetMaxPredictionAheadFrames(recommendation.MaxPredictionAheadFrames);
            tuning.SetMinPredictionWindow(recommendation.MinPredictionWindow);
            tuning.SetBacklogEwmaAlpha(recommendation.BacklogEwmaAlpha);
        }

        private void DisposeRemoteInterpolation()
        {
            _battleConnectionRecoveryPending = false;
            _runtime.DisposeReplication();
            if (_ctx != null)
            {
                _ctx.EnableRemoteInterpolation = false;
                _ctx.CanSubmitGameplayInput = true;
            }
        }
    }
}

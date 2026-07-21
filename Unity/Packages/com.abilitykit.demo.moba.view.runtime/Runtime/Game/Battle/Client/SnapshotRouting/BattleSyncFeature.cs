using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSyncFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;

        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(3);

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Features.TryGet(out _ctx);

            var syncMode = _ctx != null ? _ctx.Plan.Sync.SyncMode : BattleSyncMode.Lockstep;

            if (_ctx != null && _ctx.TryGetFrameSnapshots(out var snapshots))
            {
                switch (syncMode)
                {
                    case BattleSyncMode.SnapshotAuthority:
                    case BattleSyncMode.Lockstep:
                    case BattleSyncMode.HybridPredictReconcile:
                    default:
                        SubscribeSnapshots(snapshots);
                        break;
                }
            }
            else
            {
                Log.Warning("[BattleSyncFeature] FrameSnapshots is null");
            }

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subscriptions.Clear();

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }

            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        private void SubscribeSnapshots(FrameSnapshotDispatcher snapshots)
        {
            _subscriptions.Clear();

            try
            {
                _subscriptions.Add(
                    snapshots.Subscribe<MobaActorSpawnSnapshotEntry[]>(
                        MobaOpCodes.Snapshot.ActorSpawn,
                        OnActorSpawnSnapshot));
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSyncFeature] Failed to subscribe ActorSpawnSnapshot");
            }

            _subscriptions.Add(
                snapshots.Subscribe<MobaActorTransformSnapshotEntry[]>(
                    MobaOpCodes.Snapshot.ActorTransform,
                    OnActorTransformSnapshot));
            _subscriptions.Add(
                snapshots.Subscribe<MobaStateHashSnapshotPayload>(
                    MobaOpCodes.Snapshot.StateHash,
                    OnStateHashSnapshot));
        }

        private void OnStateHashSnapshot(ISnapshotEnvelope packet, MobaStateHashSnapshotPayload snap)
        {
            BattleSnapshotEntityApplier.ApplyStateHash(_ctx, snap);

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = packet.WorldId;
                _ctx.HasRuntimeWorldId = true;
            }

            var target = _ctx?.PredictionReconcileTarget;
            if (target != null)
            {
                target.OnAuthoritativeStateHash(
                    packet.WorldId,
                    new FrameIndex(snap.Frame),
                    new AbilityKit.Ability.FrameSync.Rollback.WorldStateHash(snap.Hash));
            }

            CollectStateHashSnapshot(snap.Frame, snap.Hash);
        }

        private void CollectStateHashSnapshot(int authoritativeFrame, uint stateHash)
        {
            try
            {
                if (_ctx == null ||
                    !_ctx.TryGetRuntimeWorld(out var world) ||
                    world.Services == null ||
                    !world.Services.TryResolve<IMobaBattleDiagnosticEventSink>(out var collector) ||
                    collector == null)
                {
                    return;
                }

                var draft = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(
                    authoritativeFrame,
                    stateHash);
                collector.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSyncFeature] Failed to collect StateHash sync diagnostic");
            }
        }

        private void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries)
        {
            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = packet.WorldId;
                _ctx.HasRuntimeWorldId = true;
            }

            BattleSnapshotEntityApplier.ApplyTransform(_ctx, entries, logContext: "BattleSyncFeature");
        }

        private void OnActorSpawnSnapshot(ISnapshotEnvelope packet, MobaActorSpawnSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return;
            }

            BattleSnapshotEntityApplier.ApplySpawn(
                _ctx,
                entries,
                updateExisting: false,
                logContext: "BattleSyncFeature");
        }
    }
}

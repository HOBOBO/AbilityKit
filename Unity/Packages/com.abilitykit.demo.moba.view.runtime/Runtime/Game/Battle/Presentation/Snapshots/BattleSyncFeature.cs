using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSyncFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private IMobaBattleDiagnosticEventSink _diagnosticSink;

        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(4);

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Features.TryGet(out _ctx);
            _diagnosticSink = ResolveDiagnosticSink(_ctx);

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

            _diagnosticSink = null;
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
                    snapshots.Subscribe<ActorSpawnData[]>(
                        MobaOpCodes.Snapshot.ActorSpawn,
                        OnActorSpawnSnapshot));
                _subscriptions.Add(
                    snapshots.Subscribe<ActorDespawnData[]>(
                        MobaOpCodes.Snapshot.ActorDespawn,
                        OnActorDespawnSnapshot));
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSyncFeature] Failed to subscribe actor lifecycle snapshots");
            }

            if (_ctx == null || !_ctx.EnableRemoteInterpolation)
            {
                _subscriptions.Add(
                    snapshots.Subscribe<ActorTransformData[]>(
                        MobaOpCodes.Snapshot.ActorTransform,
                        OnActorTransformSnapshot));
            }

            _subscriptions.Add(
                snapshots.Subscribe<StateHashData>(
                    MobaOpCodes.Snapshot.StateHash,
                    OnStateHashSnapshot));
        }

        private void OnStateHashSnapshot(ISnapshotEnvelope packet, StateHashData snap)
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
                    new FrameIndex(snap.FrameIndex),
                    new AbilityKit.Ability.FrameSync.Rollback.WorldStateHash(snap.StateHash));
            }

            CollectStateHashSnapshot(snap.FrameIndex, snap.StateHash);
        }

        private void CollectStateHashSnapshot(int authoritativeFrame, uint stateHash)
        {
            try
            {
                if (_diagnosticSink == null) return;

                var draft = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(
                    authoritativeFrame,
                    stateHash);
                _diagnosticSink.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSyncFeature] Failed to collect StateHash sync diagnostic");
            }
        }

        private static IMobaBattleDiagnosticEventSink ResolveDiagnosticSink(BattleContext context)
        {
            if (context == null ||
                !context.TryGetRuntimeWorld(out var world) ||
                world.Services == null ||
                !world.Services.TryResolve<IMobaBattleDiagnosticEventSink>(out var sink))
            {
                return null;
            }

            return sink;
        }

        private void OnActorTransformSnapshot(ISnapshotEnvelope packet, ActorTransformData[] entries)
        {
            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = packet.WorldId;
                _ctx.HasRuntimeWorldId = true;
            }

            BattleSnapshotEntityApplier.ApplyTransform(_ctx, entries, logContext: "BattleSyncFeature");
        }

        private void OnActorSpawnSnapshot(ISnapshotEnvelope packet, ActorSpawnData[] entries)
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

        private void OnActorDespawnSnapshot(ISnapshotEnvelope packet, ActorDespawnData[] entries)
        {
            BattleSnapshotEntityApplier.ApplyDespawn(_ctx, entries);
        }
    }
}

using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSyncFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private FrameSnapshotDispatcher _dispatcher;

        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(4);

        public void OnAttach(in GamePhaseContext ctx)
        {
            _ctx = ctx.RequireFeature<BattleContext>(nameof(BattleSyncFeature));

            EnsureSnapshotSubscription();
            if (_dispatcher == null)
                Log.Warning("[BattleSyncFeature] FrameSnapshots is null");

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subscriptions.Clear();
            _dispatcher = null;

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }

            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            EnsureSnapshotSubscription();
        }

        private void EnsureSnapshotSubscription()
        {
            var snapshots = _ctx != null && _ctx.TryGetFrameSnapshots(out var current)
                ? current
                : null;
            if (ReferenceEquals(_dispatcher, snapshots)) return;
            _subscriptions.Clear();
            if (_dispatcher != null && _ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }
            _dispatcher = snapshots;
            if (snapshots != null) SubscribeSnapshots(snapshots);
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
                _subscriptions.Add(
                    snapshots.Subscribe<CharacterActionData[]>(
                        CharacterActionSnapshotRoute.OpCode,
                        (_, entries) => BattleSnapshotEntityApplier.ApplyCharacterActions(_ctx, entries)));
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

            _ctx?.NotifyStateHashSnapshot(snap.FrameIndex, snap.StateHash);
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

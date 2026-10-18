using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot
{
    public sealed class BattleSnapshotViewAdapter : IDisposable
    {
        private readonly FrameSnapshotDispatcher _snapshots;
        private readonly IBattleViewEventSink _sink;
        private readonly Func<bool> _isCurrent;
        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(6);

        public BattleSnapshotViewAdapter(
            FrameSnapshotDispatcher snapshots,
            IBattleViewEventSink sink,
            Func<bool> isCurrent = null)
        {
            _snapshots = snapshots;
            _sink = sink;
            _isCurrent = isCurrent;

            if (_snapshots == null || _sink == null) return;

            _subscriptions.Add(_snapshots.Subscribe<BattleEnterGameSnapshot>(
                MobaOpCodes.Snapshot.EnterGame,
                (packet, value) =>
                {
                    if (IsCurrent()) _sink.OnEnterGameSnapshot(packet, value);
                }));
            _subscriptions.Add(_snapshots.Subscribe<ActorTransformData[]>(
                MobaOpCodes.Snapshot.ActorTransform,
                (packet, entries) =>
                {
                    if (IsCurrent()) _sink.OnActorTransformSnapshot(packet, entries);
                }));
            _subscriptions.Add(_snapshots.Subscribe<ProjectileEventData[]>(
                MobaOpCodes.Snapshot.ProjectileEvent,
                (packet, entries) =>
                {
                    if (IsCurrent()) _sink.OnProjectileEventSnapshot(packet, entries);
                }));
            _subscriptions.Add(_snapshots.Subscribe<AreaEventData[]>(
                MobaOpCodes.Snapshot.AreaEvent,
                (packet, entries) =>
                {
                    if (IsCurrent()) _sink.OnAreaEventSnapshot(packet, entries);
                }));
            _subscriptions.Add(_snapshots.Subscribe<DamageEventData[]>(
                MobaOpCodes.Snapshot.DamageEvent,
                (packet, entries) =>
                {
                    if (IsCurrent()) _sink.OnDamageEventSnapshot(packet, entries);
                }));
            _subscriptions.Add(_snapshots.Subscribe<PresentationCueData[]>(
                MobaOpCodes.Snapshot.PresentationCue,
                (packet, entries) =>
                {
                    if (IsCurrent()) _sink.OnPresentationCueSnapshot(packet, entries);
                }));
        }

        private bool IsCurrent() => _isCurrent == null || _isCurrent();

        public void Dispose()
        {
            _subscriptions.Clear();
        }
    }
}

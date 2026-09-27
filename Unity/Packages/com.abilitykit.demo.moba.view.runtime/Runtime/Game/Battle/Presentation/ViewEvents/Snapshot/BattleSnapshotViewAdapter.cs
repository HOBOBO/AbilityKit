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
        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(6);

        public BattleSnapshotViewAdapter(FrameSnapshotDispatcher snapshots, IBattleViewEventSink sink)
        {
            _snapshots = snapshots;
            _sink = sink;

            if (_snapshots == null || _sink == null) return;

            _subscriptions.Add(_snapshots.Subscribe<BattleEnterGameSnapshot>(
                MobaOpCodes.Snapshot.EnterGame,
                _sink.OnEnterGameSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<ActorTransformData[]>(
                MobaOpCodes.Snapshot.ActorTransform,
                (packet, entries) => _sink.OnActorTransformSnapshot(packet, entries)));
            _subscriptions.Add(_snapshots.Subscribe<ProjectileEventData[]>(
                MobaOpCodes.Snapshot.ProjectileEvent,
                _sink.OnProjectileEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<AreaEventData[]>(
                MobaOpCodes.Snapshot.AreaEvent,
                _sink.OnAreaEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<DamageEventData[]>(
                MobaOpCodes.Snapshot.DamageEvent,
                _sink.OnDamageEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<PresentationCueData[]>(
                MobaOpCodes.Snapshot.PresentationCue,
                _sink.OnPresentationCueSnapshot));
        }

        public void Dispose()
        {
            _subscriptions.Clear();
        }
    }
}

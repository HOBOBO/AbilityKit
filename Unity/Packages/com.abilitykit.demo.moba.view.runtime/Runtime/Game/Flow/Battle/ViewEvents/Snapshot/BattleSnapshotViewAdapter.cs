using System;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Common.SnapshotRouting;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot
{
    public sealed class BattleSnapshotViewAdapter : IDisposable
    {
        private readonly FrameSnapshotDispatcher _snapshots;
        private readonly IBattleViewEventSink _sink;

        private IDisposable _subEnterGame;
        private IDisposable _subActorTransform;
        private IDisposable _subProjectileEvents;
        private IDisposable _subAreaEvents;
        private IDisposable _subDamageEvents;

        public BattleSnapshotViewAdapter(FrameSnapshotDispatcher snapshots, IBattleViewEventSink sink)
        {
            _snapshots = snapshots;
            _sink = sink;

            if (_snapshots == null || _sink == null) return;

            _subEnterGame = _snapshots.Subscribe<EnterMobaGameRes>((int)MobaOpCode.EnterGameSnapshot, _sink.OnEnterGameSnapshot);
            _subActorTransform = _snapshots.Subscribe<(int actorId, float x, float y, float z)[]>((int)MobaOpCode.ActorTransformSnapshot, _sink.OnActorTransformSnapshot);
            _subProjectileEvents = _snapshots.Subscribe<MobaProjectileEventSnapshotCodec.Entry[]>((int)MobaOpCode.ProjectileEventSnapshot, _sink.OnProjectileEventSnapshot);
            _subAreaEvents = _snapshots.Subscribe<MobaAreaEventSnapshotCodec.Entry[]>((int)MobaOpCode.AreaEventSnapshot, _sink.OnAreaEventSnapshot);
            _subDamageEvents = _snapshots.Subscribe<MobaDamageEventSnapshotCodec.Entry[]>((int)MobaOpCode.DamageEventSnapshot, _sink.OnDamageEventSnapshot);
        }

        public void Dispose()
        {
            _subEnterGame?.Dispose();
            _subActorTransform?.Dispose();
            _subProjectileEvents?.Dispose();
            _subAreaEvents?.Dispose();
            _subDamageEvents?.Dispose();

            _subEnterGame = null;
            _subActorTransform = null;
            _subProjectileEvents = null;
            _subAreaEvents = null;
            _subDamageEvents = null;
        }
    }
}

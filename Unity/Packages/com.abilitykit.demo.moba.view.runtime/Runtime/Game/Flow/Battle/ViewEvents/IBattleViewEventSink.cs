using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IBattleViewEventSink
    {
        void OnTriggerEvent(in TriggerEvent evt);

        void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res);

        void OnActorTransformSnapshot(ISnapshotEnvelope packet, (int actorId, float x, float y, float z)[] entries);

        void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotCodec.Entry[] entries);

        void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotCodec.Entry[] entries);

        void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotCodec.Entry[] entries);
    }
}

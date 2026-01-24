using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IBattleViewEventSink
    {
        void OnTriggerEvent(in TriggerEvent evt);

        void OnEnterGameSnapshot(FramePacket packet, EnterMobaGameRes res);

        void OnActorTransformSnapshot(FramePacket packet, (int actorId, float x, float y, float z)[] entries);

        void OnProjectileEventSnapshot(FramePacket packet, MobaProjectileEventSnapshotCodec.Entry[] entries);

        void OnAreaEventSnapshot(FramePacket packet, MobaAreaEventSnapshotCodec.Entry[] entries);

        void OnDamageEventSnapshot(FramePacket packet, MobaDamageEventSnapshotCodec.Entry[] entries);
    }
}

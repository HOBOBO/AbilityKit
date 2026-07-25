using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IBattleViewEventSink
    {
        void OnTriggerEvent(in TriggerEvent evt);

        void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload);

        void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res);

        void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries);

        void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotEntry[] entries);

        void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotEntry[] entries);

        void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotEntry[] entries);

        void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries);

        void Tick();
    }
}


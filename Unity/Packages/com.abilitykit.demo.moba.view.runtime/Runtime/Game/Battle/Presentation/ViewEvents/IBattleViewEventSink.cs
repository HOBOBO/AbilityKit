using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IBattleViewEventSink
    {
        void OnDamageResult(in DamageResult result);

        void OnProjectileHit(in ProjectileHitEvent evt);

        void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload);

        void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res);

        void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries);

        void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotEntry[] entries);

        void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotEntry[] entries);

        void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotEntry[] entries);

        void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries);

        void Tick();

        /// <summary>Releases transient state owned by this sink.</summary>
        void Clear();
    }
}

using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IBattleViewEventSink
    {
        void OnDamageResult(in DamageResult result);

        void OnProjectileHit(in ProjectileHitEvent evt);

        void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload);

        void OnEnterGameSnapshot(ISnapshotEnvelope packet, BattleEnterGameSnapshot res);

        void OnActorTransformSnapshot(ISnapshotEnvelope packet, ActorTransformData[] entries);

        void OnProjectileEventSnapshot(ISnapshotEnvelope packet, ProjectileEventData[] entries);

        void OnAreaEventSnapshot(ISnapshotEnvelope packet, AreaEventData[] entries);

        void OnDamageEventSnapshot(ISnapshotEnvelope packet, DamageEventData[] entries);

        void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries);

        void Tick();

        /// <summary>Releases transient state owned by this sink.</summary>
        void Clear();
    }
}

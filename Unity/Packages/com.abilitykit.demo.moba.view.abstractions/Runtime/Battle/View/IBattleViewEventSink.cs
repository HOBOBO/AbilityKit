using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.View.Abstractions.Shared.Types;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.View.Abstractions.Battle.View
{
    public interface IBattleViewEventSink
    {
        void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res);

        void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries);

        void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotEntry[] entries);

        void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotEntry[] entries);

        void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotEntry[] entries);

        void OnPresentationCueSnapshot(ISnapshotEnvelope packet, BattlePresentationCueData[] entries);
    }
}

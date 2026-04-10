using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class SharedSnapshotDeclarations
    {
        [SnapshotDecoder("shared", (int)MobaOpCode.StateHashSnapshot, typeof(MobaStateHashSnapshotCodec.SnapshotPayload))]
        internal static bool DecodeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotCodec.SnapshotPayload payload)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                payload = default;
                return false;
            }

            payload = MobaStateHashSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("shared", (int)MobaOpCode.ActorTransformSnapshot, typeof((int actorId, float x, float y, float z)[]))]
        internal static bool DecodeActorTransform(in WorldStateSnapshot snap, out (int actorId, float x, float y, float z)[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorTransformSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("shared", (int)MobaOpCode.ProjectileEventSnapshot, typeof(MobaProjectileEventSnapshotCodec.Entry[]))]
        internal static bool DecodeProjectileEvents(in WorldStateSnapshot snap, out MobaProjectileEventSnapshotCodec.Entry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaProjectileEventSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("shared", (int)MobaOpCode.AreaEventSnapshot, typeof(MobaAreaEventSnapshotCodec.Entry[]))]
        internal static bool DecodeAreaEvents(in WorldStateSnapshot snap, out MobaAreaEventSnapshotCodec.Entry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaAreaEventSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("shared", (int)MobaOpCode.DamageEventSnapshot, typeof(MobaDamageEventSnapshotCodec.Entry[]))]
        internal static bool DecodeDamageEvents(in WorldStateSnapshot snap, out MobaDamageEventSnapshotCodec.Entry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaDamageEventSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }
    }
}

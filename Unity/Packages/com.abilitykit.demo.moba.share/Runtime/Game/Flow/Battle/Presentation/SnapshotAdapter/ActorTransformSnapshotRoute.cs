using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to actor transforms.
    /// </summary>
    public static class ActorTransformSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ActorTransform;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out ActorTransformData[] transforms)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                transforms = Array.Empty<ActorTransformData>();
                return false;
            }

            transforms = ActorTransformSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<ActorTransformData[]>(OpCode, TryDecode);
        }
    }
}

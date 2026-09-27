using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to actor despawns.
    /// </summary>
    public static class ActorDespawnSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ActorDespawn;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out ActorDespawnData[] despawns)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                despawns = Array.Empty<ActorDespawnData>();
                return false;
            }

            despawns = ActorDespawnSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<ActorDespawnData[]>(OpCode, TryDecode);
        }
    }
}

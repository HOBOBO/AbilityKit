using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to actor spawns.
    /// </summary>
    public static class ActorSpawnSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ActorSpawn;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out ActorSpawnData[] spawns)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                spawns = Array.Empty<ActorSpawnData>();
                return false;
            }

            spawns = ActorSpawnSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<ActorSpawnData[]>(OpCode, TryDecode);
        }
    }
}

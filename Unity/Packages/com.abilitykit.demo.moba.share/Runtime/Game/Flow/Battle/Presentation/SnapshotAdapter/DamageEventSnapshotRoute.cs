using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to damage contracts.
    /// </summary>
    public static class DamageEventSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.DamageEvent;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out DamageEventData[] events)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                events = Array.Empty<DamageEventData>();
                return false;
            }

            events = DamageEventSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<DamageEventData[]>(OpCode, TryDecode);
        }
    }
}


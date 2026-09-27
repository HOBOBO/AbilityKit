using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    public static class AreaEventSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.AreaEvent;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out AreaEventData[] events)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                events = Array.Empty<AreaEventData>();
                return false;
            }

            events = AreaEventSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<AreaEventData[]>(OpCode, TryDecode);
        }
    }
}

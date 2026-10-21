using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ShieldStateSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ShieldState;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out ShieldStateData[] states)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                states = Array.Empty<ShieldStateData>();
                return false;
            }
            states = ShieldStateSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<ShieldStateData[]>(OpCode, TryDecode);
        }
    }
}

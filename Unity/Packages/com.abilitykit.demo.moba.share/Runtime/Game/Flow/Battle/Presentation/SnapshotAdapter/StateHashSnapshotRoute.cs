using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    public static class StateHashSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.StateHash;
        public const int SupportedVersion = StateHashSnapshotDecoder.SupportedVersion;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out StateHashData stateHash)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                stateHash = default;
                return false;
            }

            stateHash = StateHashSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<StateHashData>(OpCode, TryDecode);
        }
    }
}

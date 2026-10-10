using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Demo.Moba.Share
{
    public static class CharacterActionSnapshotRoute
    {
        public const int OpCode = CharacterActionSnapshotCodec.OpCode;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out CharacterActionData[] entries)
        {
            if (snapshot.OpCode == OpCode)
                return CharacterActionSnapshotCodec.TryDecode(snapshot.Payload, out entries);
            entries = Array.Empty<CharacterActionData>();
            return false;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<CharacterActionData[]>(OpCode, TryDecode);
        }
    }
}

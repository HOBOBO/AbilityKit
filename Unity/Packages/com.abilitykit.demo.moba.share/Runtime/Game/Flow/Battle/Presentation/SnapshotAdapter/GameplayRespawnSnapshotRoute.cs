using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayRespawnSnapshotRoute
    {
        public const int OpCode = GameplayRespawnSnapshotCodec.OpCode;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out GameplayRespawnData[] entries)
        {
            if (snapshot.OpCode == OpCode)
                return GameplayRespawnSnapshotCodec.TryDecode(snapshot.Payload, out entries);
            entries = null;
            return false;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<GameplayRespawnData[]>(OpCode, TryDecode);
        }
    }
}

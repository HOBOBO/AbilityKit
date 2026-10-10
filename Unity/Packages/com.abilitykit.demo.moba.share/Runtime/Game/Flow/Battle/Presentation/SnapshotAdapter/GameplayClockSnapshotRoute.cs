using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayClockSnapshotRoute
    {
        public const int OpCode = GameplayClockSnapshotCodec.OpCode;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out GameplayClockData clock)
        {
            if (snapshot.OpCode == OpCode)
                return GameplayClockSnapshotCodec.TryDecode(snapshot.Payload, out clock);
            clock = default;
            return false;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<GameplayClockData>(OpCode, TryDecode);
        }
    }
}

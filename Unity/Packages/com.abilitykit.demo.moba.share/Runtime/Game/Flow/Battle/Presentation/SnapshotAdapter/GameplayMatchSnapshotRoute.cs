using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayMatchSnapshotRoute
    {
        public const int OpCode = GameplayMatchSnapshotCodec.OpCode;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out GameplayMatchData state)
        {
            if (snapshot.OpCode == OpCode)
                return GameplayMatchSnapshotCodec.TryDecode(snapshot.Payload, out state);
            state = default;
            return false;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<GameplayMatchData>(OpCode, TryDecode);
        }
    }
}

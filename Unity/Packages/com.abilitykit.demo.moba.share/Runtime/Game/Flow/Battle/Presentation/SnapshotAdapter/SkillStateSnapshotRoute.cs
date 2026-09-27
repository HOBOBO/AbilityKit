using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to skill states.
    /// </summary>
    public static class SkillStateSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.SkillState;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out SkillStateData[] states)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                states = Array.Empty<SkillStateData>();
                return false;
            }

            states = SkillStateSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<SkillStateData[]>(OpCode, TryDecode);
        }
    }
}

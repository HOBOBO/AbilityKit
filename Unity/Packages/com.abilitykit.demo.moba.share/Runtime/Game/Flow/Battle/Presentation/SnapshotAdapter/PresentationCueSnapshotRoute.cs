using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Owns the platform-neutral snapshot route from a MOBA wire snapshot to presentation contracts.
    /// </summary>
    public static class PresentationCueSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.PresentationCue;

        public static bool TryDecode(
            in WorldStateSnapshot snapshot,
            out PresentationCueData[] cues)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                cues = Array.Empty<PresentationCueData>();
                return false;
            }

            cues = PresentationCueSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.RegisterDecoder<PresentationCueData[]>(OpCode, TryDecode);
        }
    }
}


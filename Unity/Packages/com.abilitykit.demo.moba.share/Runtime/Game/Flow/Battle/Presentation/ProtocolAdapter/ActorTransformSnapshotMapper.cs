using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps logic-world wire transforms into platform-neutral presentation contracts.
    /// </summary>
    public static class ActorTransformSnapshotMapper
    {
        public static ActorTransformData[] Map(MobaActorTransformSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<ActorTransformData>();
            }

            var transforms = new ActorTransformData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                transforms[i] = Map(in entries[i]);
            }

            return transforms;
        }

        public static ActorTransformData Map(in MobaActorTransformSnapshotEntry entry)
        {
            return new ActorTransformData(
                actorId: entry.ActorId,
                x: entry.X,
                y: entry.Y,
                z: entry.Z,
                forwardX: entry.ForwardX,
                forwardY: entry.ForwardY,
                forwardZ: entry.ForwardZ,
                rotationY: 0f,
                scale: 1f);
        }
    }
}

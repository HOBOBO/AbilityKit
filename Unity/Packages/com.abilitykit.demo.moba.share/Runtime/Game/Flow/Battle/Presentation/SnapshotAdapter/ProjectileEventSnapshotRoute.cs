using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ProjectileEventSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ProjectileEvent;

        public static bool TryDecode(in WorldStateSnapshot snapshot, out ProjectileEventData[] events)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length == 0)
            {
                events = Array.Empty<ProjectileEventData>();
                return false;
            }

            events = ProjectileEventSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<ProjectileEventData[]>(OpCode, TryDecode);
        }
    }
}

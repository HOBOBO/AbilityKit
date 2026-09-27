using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class AreaEventSnapshotMapper
    {
        public static AreaEventData[] Map(MobaAreaEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<AreaEventData>();
            }

            var events = new AreaEventData[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                events[i] = Map(in entries[i]);
            }

            return events;
        }

        public static AreaEventData Map(in MobaAreaEventSnapshotEntry entry)
        {
            return new AreaEventData(
                (AreaPresentationEventKind)entry.Kind,
                entry.AreaId,
                entry.OwnerActorId,
                entry.TemplateId,
                entry.X,
                entry.Y,
                entry.Z,
                entry.Radius);
        }
    }
}

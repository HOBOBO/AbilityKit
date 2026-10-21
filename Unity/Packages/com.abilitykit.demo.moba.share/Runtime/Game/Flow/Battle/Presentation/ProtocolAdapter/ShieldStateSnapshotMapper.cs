using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ShieldStateSnapshotMapper
    {
        public static ShieldStateData[] Map(MobaShieldStateSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return System.Array.Empty<ShieldStateData>();
            var result = new ShieldStateData[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                result[i] = new ShieldStateData(e.ActorId, e.CurrentShield, e.Capacity,
                    e.PersonalShield, e.SharedShield, e.ActiveLayerCount, e.HasShared);
            }
            return result;
        }
    }
}

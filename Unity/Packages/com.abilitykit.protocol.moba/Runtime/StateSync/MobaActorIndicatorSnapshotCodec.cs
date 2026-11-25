using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    /// <summary>
    /// 完整指示器集合的表现协议。合法空集合用于清除，空字节和非法数值不能作为基线。
    /// </summary>
    public static class MobaActorIndicatorSnapshotCodec
    {
        /// <summary>
        /// 编码只读状态集合。
        /// </summary>
        public static byte[] Serialize(MobaActorIndicatorSnapshotEntry[] entries)
        {
            Validate(entries ?? Array.Empty<MobaActorIndicatorSnapshotEntry>());
            return MemoryPackSerializer.Serialize(new MobaActorIndicatorSnapshotPayload
            { Entries = entries ?? Array.Empty<MobaActorIndicatorSnapshotEntry>() });
        }

        /// <summary>
        /// 解码并验证完整状态，不将畸形载荷转为空集。
        /// </summary>
        public static MobaActorIndicatorSnapshotEntry[] Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("Missing indicator payload.");
            var result = MemoryPackSerializer.Deserialize<MobaActorIndicatorSnapshotPayload>(bytes).Entries;
            if (result == null) throw new ArgumentException("Missing indicator entries.");
            Validate(result);
            return result;
        }

        private static void Validate(MobaActorIndicatorSnapshotEntry[] entries)
        {
            if (entries.Length > 16384) throw new ArgumentException("Indicator entry limit exceeded.");
            var seen = new System.Collections.Generic.HashSet<long>();
            var layouts = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var e in entries)
            {
                if (e.ActorId <= 0 || e.LayoutId <= 0 || e.IndicatorId <= 0 ||
                    e.Availability < 0 || e.Availability > 2 ||
                    float.IsNaN(e.Current) || float.IsInfinity(e.Current) ||
                    float.IsNaN(e.Max) || float.IsInfinity(e.Max) ||
                    e.Current < 0 || (e.HasMax && (e.Max <= 0 || e.Current > e.Max)) ||
                    (!e.HasMax && e.Max != 0) ||
                    (e.Availability != 1 && (e.Current != 0 || e.HasMax || e.Ready)) ||
                    !seen.Add(((long)e.ActorId << 32) | (uint)e.IndicatorId))
                    throw new ArgumentException("Invalid indicator state.");
                if (layouts.TryGetValue(e.ActorId, out var layout) && layout != e.LayoutId)
                    throw new ArgumentException("Conflicting actor indicator layouts.");
                layouts[e.ActorId] = e.LayoutId;
            }
        }
    }
}

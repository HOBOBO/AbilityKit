using System;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Blackboard
{
    /// <summary>
    /// 读写黑板快照的 JSON 表示；版本、成员和键校验仍由还原端执行。
    /// </summary>
    public static class BlackboardSnapshotJsonCodec
    {
        /// <summary>
        /// 将单个所有者快照写成紧凑 JSON。
        /// </summary>
        public static string Serialize(BlackboardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonConvert.SerializeObject(snapshot, Formatting.None);
        }

        /// <summary>
        /// 将所有者快照集合写成紧凑 JSON。
        /// </summary>
        public static string Serialize(BlackboardSnapshotSet snapshots)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            return JsonConvert.SerializeObject(snapshots, Formatting.None);
        }

        /// <summary>
        /// 读取非空的单所有者快照，不尝试将其应用到黑板。
        /// </summary>
        public static BlackboardSnapshot DeserializeSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot JSON is required.", nameof(json));
            return JsonConvert.DeserializeObject<BlackboardSnapshot>(json)
                ?? throw new InvalidOperationException("Snapshot JSON produced no snapshot.");
        }

        /// <summary>
        /// 读取非空的快照集合，不尝试将其应用到黑板。
        /// </summary>
        public static BlackboardSnapshotSet DeserializeSet(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot set JSON is required.", nameof(json));
            return JsonConvert.DeserializeObject<BlackboardSnapshotSet>(json)
                ?? throw new InvalidOperationException("Snapshot set JSON produced no snapshots.");
        }
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Blackboard
{
    public enum BlackboardSnapshotValueKind
    {
        Int = 0,
        Bool = 1,
        Float = 2,
        Double = 3,
        String = 4
    }

    [Serializable]
    public sealed class BlackboardSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long OwnerKey;
        public List<BlackboardSnapshotBoard> Boards = new List<BlackboardSnapshotBoard>();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static BlackboardSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot JSON is required.", nameof(json));
            var snapshot = JsonConvert.DeserializeObject<BlackboardSnapshot>(json);
            if (snapshot == null) throw new InvalidOperationException("Snapshot JSON produced no snapshot.");
            return snapshot;
        }
    }

    [Serializable]
    public sealed class BlackboardSnapshotSet
    {
        public const int CurrentVersion = BlackboardSnapshot.CurrentVersion;

        public int Version = CurrentVersion;
        public List<BlackboardSnapshot> Owners = new List<BlackboardSnapshot>();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static BlackboardSnapshotSet FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot set JSON is required.", nameof(json));
            var snapshots = JsonConvert.DeserializeObject<BlackboardSnapshotSet>(json);
            if (snapshots == null) throw new InvalidOperationException("Snapshot set JSON produced no snapshots.");
            return snapshots;
        }
    }

    [Serializable]
    public sealed class BlackboardSnapshotBoard
    {
        public int BoardId;
        public List<BlackboardSnapshotEntry> Entries = new List<BlackboardSnapshotEntry>();
    }

    [Serializable]
    public struct BlackboardSnapshotEntry
    {
        public int KeyId;
        public BlackboardKeyType Type;
        public bool HasValue;
        public BlackboardSnapshotValueKind ValueKind;
        public int IntValue;
        public bool BoolValue;
        public float FloatValue;
        public double DoubleValue;
        public string StringValue;

        public static BlackboardSnapshotEntry Missing(int keyId, BlackboardKeyType type)
        {
            return new BlackboardSnapshotEntry
            {
                KeyId = keyId,
                Type = type,
                HasValue = false
            };
        }
    }

    public interface IBlackboardSnapshotParticipant
    {
        bool TryCaptureSnapshot(int boardId, out BlackboardSnapshotBoard snapshot, out string error);
        bool ValidateSnapshot(BlackboardSnapshotBoard snapshot, out string error);
        bool TryRestoreSnapshot(BlackboardSnapshotBoard snapshot, out string error);
    }
}

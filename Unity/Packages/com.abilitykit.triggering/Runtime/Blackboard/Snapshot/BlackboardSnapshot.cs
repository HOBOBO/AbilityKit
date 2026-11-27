using System;
using System.Collections.Generic;

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
    }

    [Serializable]
    public sealed class BlackboardSnapshotSet
    {
        public const int CurrentVersion = BlackboardSnapshot.CurrentVersion;

        public int Version = CurrentVersion;
        public List<BlackboardSnapshot> Owners = new List<BlackboardSnapshot>();

        internal BlackboardSnapshotSet CopyDetached()
        {
            /*
             * 保留输入中的空集合引用和空条目，由现有还原校验决定是否接受。
             */
            var copy = new BlackboardSnapshotSet
            {
                Version = Version,
                Owners = Owners == null ? null! : new List<BlackboardSnapshot>(Owners.Count)
            };
            if (Owners == null) return copy;

            foreach (var owner in Owners)
                copy.Owners.Add(CopyOwner(owner));
            return copy;
        }

        private static BlackboardSnapshot CopyOwner(BlackboardSnapshot owner)
        {
            if (owner == null) return null!;
            var copy = new BlackboardSnapshot
            {
                Version = owner.Version,
                OwnerKey = owner.OwnerKey,
                Boards = owner.Boards == null ? null! : new List<BlackboardSnapshotBoard>(owner.Boards.Count)
            };
            if (owner.Boards == null) return copy;

            foreach (var board in owner.Boards)
            {
                copy.Boards.Add(board == null ? null! : new BlackboardSnapshotBoard
                {
                    BoardId = board.BoardId,
                    Entries = board.Entries == null ? null! : new List<BlackboardSnapshotEntry>(board.Entries)
                });
            }
            return copy;
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

using System;

namespace AbilityKit.Game.Battle
{
    public readonly struct BattleDebugEntityId :
        IEquatable<BattleDebugEntityId>,
        IComparable<BattleDebugEntityId>
    {
        public BattleDebugEntityId(int actorId)
        {
            ActorId = actorId;
        }

        public int ActorId { get; }

        public bool IsValid => ActorId != 0;

        public bool Equals(BattleDebugEntityId other) =>
            ActorId == other.ActorId;

        public override bool Equals(object obj) =>
            obj is BattleDebugEntityId other && Equals(other);

        public override int GetHashCode() => ActorId;

        public int CompareTo(BattleDebugEntityId other) =>
            ActorId.CompareTo(other.ActorId);

        public override string ToString() => ActorId.ToString();

        public static bool operator ==(
            BattleDebugEntityId left,
            BattleDebugEntityId right) => left.Equals(right);

        public static bool operator !=(
            BattleDebugEntityId left,
            BattleDebugEntityId right) => !left.Equals(right);
    }
}

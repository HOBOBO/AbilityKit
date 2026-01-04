using System;

namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        internal readonly int Id;

        internal GameplayTag(int id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public string Name => GameplayTagManager.Instance.GetName(this);

        public bool Equals(GameplayTag other) => Id == other.Id;

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => Id;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Id == b.Id;

        public static bool operator !=(GameplayTag a, GameplayTag b) => a.Id != b.Id;

        public override string ToString() => Name ?? string.Empty;

        public bool Matches(GameplayTag other)
        {
            return GameplayTagManager.Instance.Matches(this, other);
        }

        public bool MatchesExact(GameplayTag other)
        {
            return Id == other.Id;
        }

        public bool IsChildOf(GameplayTag parent)
        {
            return GameplayTagManager.Instance.IsChildOf(this, parent);
        }

        public bool IsParentOf(GameplayTag child)
        {
            return GameplayTagManager.Instance.IsChildOf(child, this);
        }
    }
}

using System;

namespace AbilityKit.Effects.Core
{
    public enum EffectScopeKind : byte
    {
        Global = 0,
        Unit = 1,
        SkillId = 2,
        LauncherId = 3,
        ProjectileId = 4,
        AoEId = 5,
    }

    public readonly struct EffectScopeKey : IEquatable<EffectScopeKey>
    {
        public readonly EffectScopeKind Kind;
        public readonly int Id;

        public EffectScopeKey(EffectScopeKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static EffectScopeKey Global() => new(EffectScopeKind.Global, 0);
        public static EffectScopeKey Unit(int actorId) => new(EffectScopeKind.Unit, actorId);
        public static EffectScopeKey SkillId(int skillId) => new(EffectScopeKind.SkillId, skillId);
        public static EffectScopeKey LauncherId(int launcherId) => new(EffectScopeKind.LauncherId, launcherId);
        public static EffectScopeKey ProjectileId(int projectileId) => new(EffectScopeKind.ProjectileId, projectileId);
        public static EffectScopeKey AoEId(int aoeId) => new(EffectScopeKind.AoEId, aoeId);

        public bool Equals(EffectScopeKey other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is EffectScopeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Id);
        public override string ToString() => $"{Kind}:{Id}";
    }
}

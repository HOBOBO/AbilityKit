using AbilityKit.Effects.Core;

namespace AbilityKit.Ability.Impl.Moba.Effects.Model
{
    internal static class MobaEffectScopeKeys
    {
        public static EffectScopeKey Global() => new((byte)MobaEffectScopeKind.Global, 0);
        public static EffectScopeKey Unit(int actorId) => new((byte)MobaEffectScopeKind.Unit, actorId);
        public static EffectScopeKey SkillId(int skillId) => new((byte)MobaEffectScopeKind.SkillId, skillId);
        public static EffectScopeKey LauncherId(int launcherId) => new((byte)MobaEffectScopeKind.LauncherId, launcherId);
        public static EffectScopeKey ProjectileId(int projectileId) => new((byte)MobaEffectScopeKind.ProjectileId, projectileId);
        public static EffectScopeKey AoEId(int aoeId) => new((byte)MobaEffectScopeKind.AoEId, aoeId);
    }
}

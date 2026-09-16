using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 由修饰器驱动的 MOBA 技能/动作参数运行时键。
    /// </summary>
    public static class MobaSkillParamModifierKeys
    {
        public static class Skill
        {
            public static readonly ModifierKey SkillId = ModifierKey.Create(ModifierKey.Categories.Skill, 1);
            public static readonly ModifierKey ResourceCost = ModifierKey.Create(ModifierKey.Categories.Skill, 3);
            public static readonly ModifierKey CooldownMs = ModifierKey.Create(ModifierKey.Categories.Skill, 4);
            public static readonly ModifierKey CastRange = ModifierKey.Create(ModifierKey.Categories.Skill, 5);
        }

        public static class Projectile
        {
            public static readonly ModifierKey LauncherId = ModifierKey.Create(ModifierKey.Categories.Projectile, 1);
            public static readonly ModifierKey ProjectileId = ModifierKey.Create(ModifierKey.Categories.Projectile, 2);
            public static readonly ModifierKey CountPerShot = ModifierKey.Create(ModifierKey.Categories.Projectile, 3);
            public static readonly ModifierKey FanAngleDeg = ModifierKey.Create(ModifierKey.Categories.Projectile, 4);
            public static readonly ModifierKey DurationMs = ModifierKey.Create(ModifierKey.Categories.Projectile, 5);
        }

        public static class Summon
        {
            public static readonly ModifierKey SummonId = ModifierKey.Create(ModifierKey.Categories.Skill, 2);
        }

        public static bool TryResolve(int parameterId, out ModifierKey key)
        {
            switch (parameterId)
            {
                case 1: key = Projectile.LauncherId; return true;
                case 2: key = Projectile.ProjectileId; return true;
                case 3: key = Projectile.CountPerShot; return true;
                case 4: key = Projectile.FanAngleDeg; return true;
                case 5: key = Projectile.DurationMs; return true;
                case 6: key = Skill.SkillId; return true;
                case 7: key = Summon.SummonId; return true;
                case 8: key = Skill.ResourceCost; return true;
                case 9: key = Skill.CooldownMs; return true;
                case 10: key = Skill.CastRange; return true;
                default:
                    key = ModifierKey.None;
                    return false;
            }
        }

        public static bool IsTriggerWritable(int parameterId)
        {
            return parameterId == 3 || parameterId == 4 || parameterId == 5 ||
                   parameterId == 8 || parameterId == 9 || parameterId == 10;
        }
    }
}

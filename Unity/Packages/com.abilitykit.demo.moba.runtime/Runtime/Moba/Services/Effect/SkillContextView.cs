using AbilityKit.Core.Math;
using AbilityKit.Ability.Share.ECS; using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct SkillContextView
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly Vec3 AimPos;
        public readonly Vec3 AimDir;
        public readonly IUnitFacade CasterUnit;

        public SkillContextView(int skillId, int skillSlot, in Vec3 aimPos, in Vec3 aimDir, IUnitFacade casterUnit)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            AimPos = aimPos;
            AimDir = aimDir;
            CasterUnit = casterUnit;
        }
    }
}

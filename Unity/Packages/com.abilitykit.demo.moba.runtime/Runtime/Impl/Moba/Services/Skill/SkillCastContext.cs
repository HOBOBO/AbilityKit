using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillCastContext
    {
        public int SkillId;
        public int SkillSlot;
        public int SkillLevel;

        public long SourceContextId;

        public int CasterActorId;
        public int TargetActorId;

        public Vec3 AimPos;
        public Vec3 AimDir;

        public IWorldResolver WorldServices;
        public IEventBus EventBus;
        public IUnitFacade CasterUnit;
        public IUnitFacade TargetUnit;

        public SkillCastContext()
        {
        }

        public SkillCastContext(
            int skillId,
            int skillSlot,
            int skillLevel,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            IWorldResolver worldServices,
            IEventBus eventBus,
            IUnitFacade casterUnit,
            IUnitFacade targetUnit)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            WorldServices = worldServices;
            EventBus = eventBus;
            CasterUnit = casterUnit;
            TargetUnit = targetUnit;
        }

        public static SkillCastContext FromRequest(in SkillCastRequest req, int skillLevel)
        {
            return new SkillCastContext(
                skillId: req.SkillId,
                skillSlot: req.SkillSlot,
                skillLevel: skillLevel,
                casterActorId: req.CasterActorId,
                targetActorId: req.TargetActorId,
                aimPos: in req.AimPos,
                aimDir: in req.AimDir,
                worldServices: req.WorldServices,
                eventBus: req.EventBus,
                casterUnit: req.CasterUnit,
                targetUnit: req.TargetUnit);
        }
    }
}

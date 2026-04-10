using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Core.Math;
using AbilityKit.Core.Common.Event;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public readonly struct PassiveSkillTriggerEventArgs
    {
        public readonly int PassiveSkillId;
        public readonly int TriggerId;
        public readonly long SourceContextId;

        public readonly int SourceActorId;
        public readonly int TargetActorId;

        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int SkillLevel;

        public readonly Vec3 AimPos;
        public readonly Vec3 AimDir;

        public readonly int IsExternalEvent;

        public readonly EffectSourceKind OriginKind;
        public readonly int OriginConfigId;
        public readonly long OriginContextId;
        public readonly int OriginSourceActorId;
        public readonly int OriginTargetActorId;

        public PassiveSkillTriggerEventArgs(
            int passiveSkillId,
            int triggerId,
            long sourceContextId,
            int sourceActorId,
            int targetActorId,
            int skillId,
            int skillSlot,
            int skillLevel,
            in Vec3 aimPos,
            in Vec3 aimDir,
            int isExternalEvent,
            EffectSourceKind originKind,
            int originConfigId,
            long originContextId,
            int originSourceActorId,
            int originTargetActorId)
        {
            PassiveSkillId = passiveSkillId;
            TriggerId = triggerId;
            SourceContextId = sourceContextId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            AimPos = aimPos;
            AimDir = aimDir;
            IsExternalEvent = isExternalEvent;
            OriginKind = originKind;
            OriginConfigId = originConfigId;
            OriginContextId = originContextId;
            OriginSourceActorId = originSourceActorId;
            OriginTargetActorId = originTargetActorId;
        }

        public static readonly EventKey<PassiveSkillTriggerEventArgs> EventKey = new EventKey<PassiveSkillTriggerEventArgs>(AbilityKit.Triggering.Eventing.StableStringId.Get("event:passive_skill_trigger"));
    }
}

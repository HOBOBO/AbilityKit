using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaSkillParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public int ResolveResourceCost(int actorId, int resourceCost, in MobaParameterRecord record)
        {
            RequireCastPrepare(in record);
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(actorId),
                MobaSkillParamModifierKeys.Skill.ResourceCost, resourceCost, in record);
        }

        public int ResolveCooldownMs(int actorId, int cooldownMs, in MobaParameterRecord record)
        {
            RequireCastPrepare(in record);
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(actorId),
                MobaSkillParamModifierKeys.Skill.CooldownMs, cooldownMs, in record);
        }

        public float ResolveCastRange(int actorId, float range, in MobaParameterRecord record)
        {
            RequireCastPrepare(in record);
            return _service.ResolveFloat(MobaModifierOwnerRef.Actor(actorId),
                MobaSkillParamModifierKeys.Skill.CastRange, range, in record);
        }

        private static void RequireCastPrepare(in MobaParameterRecord record)
        {
            if (record.Stage != MobaParameterStage.CastPrepare)
                throw new System.ArgumentException("Skill parameters require CastPrepare stage.", nameof(record));
        }
    }
}

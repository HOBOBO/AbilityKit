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

        public int ResolveSkillId(int actorId, int skillId, IModifierContext context = null)
        {
            return _service.ResolveInt(actorId, MobaSkillParamModifierKeys.Skill.SkillId, skillId, context);
        }

        public int ResolveResourceCost(int actorId, int resourceCost, IModifierContext context = null)
        {
            return _service.ResolveInt(actorId, MobaSkillParamModifierKeys.Skill.ResourceCost, resourceCost, context);
        }

        public int ResolveCooldownMs(int actorId, int cooldownMs, IModifierContext context = null)
        {
            return _service.ResolveInt(actorId, MobaSkillParamModifierKeys.Skill.CooldownMs, cooldownMs, context);
        }

        public float ResolveCastRange(int actorId, float range, IModifierContext context = null)
        {
            return _service.ResolveFloat(actorId, MobaSkillParamModifierKeys.Skill.CastRange, range, context);
        }
    }
}

using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBuffParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaBuffParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public int ResolveDurationMs(int ownerActorId, int durationMs, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.BuffApply)
                throw new ArgumentException("Buff duration requires BuffApply stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Buff.DurationMs, durationMs, in record, context);
        }
    }
}

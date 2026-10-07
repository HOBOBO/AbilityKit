using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaDamageParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaDamageParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public float ResolveRequestedBase(int ownerActorId, float requestedBase,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.Hit)
                throw new ArgumentException("Requested damage requires Hit stage.", nameof(record));
            var result = _service.ResolveFloat(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Damage.RequestedBase, requestedBase, in record, context);
            if (result < 0f || float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidOperationException($"Invalid requested damage {result}.");
            return result;
        }
    }
}

using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSummonParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaSummonParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public int ResolveLifetimeMs(int ownerActorId, int lifetimeMs,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.SummonCreate)
                throw new ArgumentException("Summon lifetime requires SummonCreate stage.", nameof(record));
            var result = _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Summon.LifetimeMs, lifetimeMs, in record, context);
            if (result < 0) throw new InvalidOperationException($"Invalid summon lifetime {result}.");
            return result;
        }

        public float ResolveInheritRatioScale(int ownerActorId, float scale,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.SummonCreate)
                throw new ArgumentException("Summon inheritance requires SummonCreate stage.", nameof(record));
            var result = _service.ResolveFloat(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Summon.InheritRatioScale, scale, in record, context);
            if (result < 0f || float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidOperationException($"Invalid summon inheritance scale {result}.");
            return result;
        }
    }
}

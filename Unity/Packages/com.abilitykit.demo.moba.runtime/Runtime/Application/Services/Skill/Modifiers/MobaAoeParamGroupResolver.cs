using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaAoeParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaAoeParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public float ResolveRadius(int casterActorId, float radius, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.AreaSpawn)
                throw new ArgumentException("Area radius requires AreaSpawn stage.", nameof(record));
            return _service.ResolveFloat(MobaModifierOwnerRef.Actor(casterActorId),
                MobaSkillParamModifierKeys.Aoe.Radius, radius, in record, context);
        }

        public int ResolveDurationMs(int casterActorId, int durationMs, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.AreaSpawn)
                throw new ArgumentException("Area duration requires AreaSpawn stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(casterActorId),
                MobaSkillParamModifierKeys.Aoe.DurationMs, durationMs, in record, context);
        }

        public int ResolveIntervalMs(int casterActorId, int intervalMs, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.AreaSpawn)
                throw new ArgumentException("Area interval requires AreaSpawn stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(casterActorId),
                MobaSkillParamModifierKeys.Aoe.IntervalMs, intervalMs, in record, context);
        }
    }
}

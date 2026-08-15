using System;
using AbilityKit.Attributes.Core;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaResourceAttributeContextProjector
    {
        public static void Refresh(MobaActorLookupService actors, int actorId)
        {
            if (actors == null) return;
            if (!actors.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            Refresh(entity);
        }

        public static void Refresh(global::ActorEntity entity)
        {
            if (entity == null || !entity.hasAttributeGroup) return;

            var ctx = entity.attributeGroup.Ctx;
            if (ctx == null) return;
            if (!entity.hasResourceContainer || entity.resourceContainer.Value == null || entity.resourceContainer.Value.Map == null) return;

            foreach (var pair in entity.resourceContainer.Value.Map)
            {
                var type = pair.Key;
                var state = pair.Value;
                if (type == ResourceType.None || state == null) continue;

                var name = type.ToString().ToLowerInvariant();
                var current = state.Current;
                var max = ResolveResourceMax(ctx, state);
                // 定点算 ratio（确定性）；AttributeContext 是 float 表现边界，出参单次换算。
                var ratio = max > Fixed64.Zero ? DeterministicMath.Clamp(current / max, Fixed64.Zero, Fixed64.One) : Fixed64.Zero;

                ctx.SetFloat($"resource.{name}.current", MobaResourceFixedConvert.ToSingle(current));
                ctx.SetFloat($"resource.{name}.max", MobaResourceFixedConvert.ToSingle(max));
                ctx.SetFloat($"resource.{name}.ratio", MobaResourceFixedConvert.ToSingle(ratio));
            }
        }

        private static Fixed64 ResolveResourceMax(AttributeContext ctx, ResourceState state)
        {
            if (state.MaxAttribute.IsValid)
            {
                var max = ctx.GetValue(state.MaxAttribute);
                if (max > 0f) return MobaResourceFixedConvert.ToFixed(max);
            }

            return state.LastMax;
        }
    }
}

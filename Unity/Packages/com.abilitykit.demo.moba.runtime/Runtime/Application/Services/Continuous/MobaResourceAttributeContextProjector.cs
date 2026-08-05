using System;
using AbilityKit.Attributes.Core;
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
                var ratio = max > 0f ? Math.Max(0f, Math.Min(1f, current / max)) : 0f;

                ctx.SetFloat($"resource.{name}.current", current);
                ctx.SetFloat($"resource.{name}.max", max);
                ctx.SetFloat($"resource.{name}.ratio", ratio);
            }
        }

        private static float ResolveResourceMax(AttributeContext ctx, ResourceState state)
        {
            if (state.MaxAttribute.IsValid)
            {
                var max = ctx.GetValue(state.MaxAttribute);
                if (max > 0f) return max;
            }

            return state.LastMax;
        }
    }
}

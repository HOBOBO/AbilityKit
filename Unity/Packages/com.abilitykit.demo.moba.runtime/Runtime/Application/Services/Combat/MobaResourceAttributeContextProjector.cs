using System;
using AbilityKit.Attributes.Core;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 将资源权威值投影到属性上下文，资源上限遵循当前属性求值规则。
    /// </summary>
    public static class MobaResourceAttributeContextProjector
    {
        private static readonly ResourceType[] ResourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        /// <summary>
        /// 刷新指定 Actor 的正常业务资源投影。
        /// </summary>
        public static void Refresh(MobaActorLookupService actors, int actorId)
        {
            if (actors == null) return;
            if (!actors.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            Refresh(entity);
        }

        /// <summary>
        /// 正常业务刷新，使用当前属性求取资源上限。
        /// </summary>
        public static void Refresh(global::ActorEntity entity)
        {
            Refresh(entity, false);
        }

        /// <summary>
        /// 恢复资源派生槽，按实时属性求取上限，同时抑制求值中的属性变化通知。
        /// 不修改资源组件，不补发历史通知。
        /// </summary>
        public static void RefreshRestored(global::ActorEntity entity)
        {
            Refresh(entity, true);
        }

        private static void Refresh(global::ActorEntity entity, bool restoring)
        {
            if (entity == null || !entity.hasAttributeGroup) return;

            var ctx = entity.attributeGroup.Ctx;
            if (ctx == null) return;
            var map = entity.hasResourceContainer ? entity.resourceContainer.Value?.Map : null;
            foreach (var type in ResourceTypes)
            {
                if (type == ResourceType.None) continue;
                var name = type.ToString().ToLowerInvariant();
                if (map == null || !map.TryGetValue(type, out var state) || state == null)
                {
                    /*
                     * 组件移除或容器清空后，已有派生槽必须回到缺省零值。
                     * 只更新已有槽，避免为从未配置的资源制造上下文数据。
                     */
                    ClearExisting(ctx, $"resource.{name}.current");
                    ClearExisting(ctx, $"resource.{name}.max");
                    ClearExisting(ctx, $"resource.{name}.ratio");
                    continue;
                }
                var current = state.Current;
                var max = ResolveResourceMax(ctx, state, restoring);
                /* 定点计算比例，仅在 AttributeContext 边界转换为 float。 */
                var ratio = max > Fixed64.Zero ? DeterministicMath.Clamp(current / max, Fixed64.Zero, Fixed64.One) : Fixed64.Zero;

                ctx.SetFloat($"resource.{name}.current", MobaResourceFixedConvert.ToSingle(current));
                ctx.SetFloat($"resource.{name}.max", MobaResourceFixedConvert.ToSingle(max));
                ctx.SetFloat($"resource.{name}.ratio", MobaResourceFixedConvert.ToSingle(ratio));
            }
        }

        private static void ClearExisting(AttributeContext ctx, string key)
        {
            if (ctx.TryGetFloat(key, out _)) ctx.SetFloat(key, 0f);
        }

        private static Fixed64 ResolveResourceMax(AttributeContext ctx, ResourceState state, bool restoring)
        {
            if (state.MaxAttribute.IsValid)
            {
                var max = restoring ? ctx.GetValueSilently(state.MaxAttribute) : ctx.GetValue(state.MaxAttribute);
                if (max > 0f) return MobaResourceFixedConvert.ToFixed(max);
            }

            return state.LastMax;
        }
    }

}

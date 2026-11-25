using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services.Projection
{
    /// <summary>
    /// 表现投影共用的资源读取，不变更容器或属性上下文；最大值始终读取当前有效属性。
    /// </summary>
    public static class MobaActorResourceReader
    {
        /// <summary>
        /// 读取资源当前值和可用上限，固定上限由内容显式提供。
        /// </summary>
        public static bool TryRead(ActorEntity actor, ResourceType type, float fixedMax, out float current, out float max)
        {
            current = max = 0;
            if (actor == null || !actor.hasResourceContainer || actor.resourceContainer.Value?.Map == null ||
                !actor.resourceContainer.Value.Map.TryGetValue(type, out var resource) || resource == null) return false;
            current = MobaResourceFixedConvert.ToSingle(resource.Current);
            max = fixedMax;
            if (fixedMax == 0 && actor.hasAttributeGroup && actor.attributeGroup.Group != null)
                max = actor.attributeGroup.Group.GetValue(resource.MaxAttribute);
            return IsFinite(current) && IsFinite(max);
        }

        /// <summary>
        /// 检查表现数值边界。
        /// </summary>
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

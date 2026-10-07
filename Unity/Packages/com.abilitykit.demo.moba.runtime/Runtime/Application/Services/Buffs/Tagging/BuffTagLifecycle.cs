using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services.Buffs.Tagging
{
    /// <summary>
    /// Buff 标签生命周期工具：解析持续标签模板，并判断 Buff 是否可激活或应被标签条件移除。
    /// </summary>
    internal static class BuffTagLifecycle
    {
        /// <summary>
        /// 从 Buff 配置引用的持续标签模板中解析运行时门禁条件。
        /// </summary>
        public static ContinuousTagRequirements ResolveRequirements(BuffMO buff, IMobaContinuousTagTemplateRegistry registry)
        {
            if (buff == null) return null;

            ContinuousTagRequirements template = null;
            if (buff.ContinuousTagTemplateId > 0)
            {
                if (registry == null || !registry.TryGet(buff.ContinuousTagTemplateId, out template) || template == null)
                    throw new System.InvalidOperationException($"Buff {buff.Id} references missing continuous tag template {buff.ContinuousTagTemplateId}.");
            }

            var hasDirectTags = buff.Tags != null && buff.Tags.Count > 0;
            if (template == null && !hasDirectTags) return null;

            var result = new ContinuousTagRequirements
            {
                ActivationRequired = template?.ActivationRequired ?? new GameplayTagRequirements(),
                ApplicationTags = CopyContainer(template?.ApplicationTags),
                RemovalRequired = template?.RemovalRequired ?? new GameplayTagRequirements(),
                OngoingRequired = template?.OngoingRequired ?? new GameplayTagRequirements(),
                ExecutionBlockedResultMask = template?.ExecutionBlockedResultMask ?? 0UL
            };

            result.ApplicationTags.AppendTags(buff.Tags);
            return result;
        }

        private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
        {
            var copy = new GameplayTagContainer();
            copy.AppendTags(source);
            return copy;
        }

        public static void AssignRequirements(BuffRuntime runtime, BuffMO buff, IMobaContinuousTagTemplateRegistry registry)
        {
            if (runtime == null) return;
            runtime.TagRequirements = ResolveRequirements(buff, registry);
        }

        public static bool CanActivate(IMobaEffectiveTagQueryService tags, int targetActorId, ContinuousTagRequirements requirements)
        {
            return tags == null ? requirements == null : tags.CanActivate(targetActorId, requirements);
        }

        public static bool ShouldEnd(IMobaEffectiveTagQueryService tags, int targetActorId, ContinuousTagRequirements requirements)
        {
            return tags != null && tags.ShouldRemove(targetActorId, requirements);
        }
    }
}

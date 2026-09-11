#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 示例分类
    /// </summary>
    public enum SampleCategory
    {
        /// <summary>
        /// 新手导览
        /// </summary>
        Onboarding = -1,

        /// <summary>
        /// 基础层
        /// </summary>
        Foundation = 0,

        /// <summary>
        /// 触发系统
        /// </summary>
        Triggering = 1,

        /// <summary>
        /// 标签系统
        /// </summary>
        Tags = 2,

        /// <summary>
        /// 修改器系统
        /// </summary>
        Modifiers = 3,

        /// <summary>
        /// 流程控制
        /// </summary>
        Flow = 4,

        /// <summary>
        /// 流水线
        /// </summary>
        Pipeline = 5,

        /// <summary>
        /// 状态机
        /// </summary>
        StateMachine = 6,

        /// <summary>
        /// 战斗系统
        /// </summary>
        Combat = 7,

        /// <summary>
        /// 技能系统
        /// </summary>
        Abilities = 8,

        /// <summary>
        /// 综合演示
        /// </summary>
        Demo = 9,

        /// <summary>
        /// 逻辑世界
        /// </summary>
        World = 10,

        /// <summary>
        /// 持续行为系统
        /// </summary>
        Continuous = 11,

        /// <summary>
        /// 目标搜索系统
        /// </summary>
        Targeting = 12,

        /// <summary>
        /// 行为树系统
        /// </summary>
        Behavior = 13,
    }

    /// <summary>
    /// 分类工具类
    /// </summary>
    public static class SampleCategoryExtensions
    {
        /// <summary>
        /// 获取分类的显示名称
        /// </summary>
        public static string GetDisplayName(this SampleCategory category)
        {
            return category switch
            {
                SampleCategory.Onboarding => "Onboarding",
                SampleCategory.Foundation => "Foundation",
                SampleCategory.Triggering => "Triggering",
                SampleCategory.Tags => "Tags",
                SampleCategory.Modifiers => "Modifiers",
                SampleCategory.Flow => "Flow",
                SampleCategory.Pipeline => "Pipeline",
                SampleCategory.StateMachine => "StateMachine",
                SampleCategory.Combat => "Combat",
                SampleCategory.Abilities => "Abilities",
                SampleCategory.World => "World",
                SampleCategory.Demo => "Demo",
                SampleCategory.Continuous => "Continuous",
                SampleCategory.Targeting => "Targeting",
                SampleCategory.Behavior => "Behavior",
                _ => "Unknown"
            };
        }
    }
}

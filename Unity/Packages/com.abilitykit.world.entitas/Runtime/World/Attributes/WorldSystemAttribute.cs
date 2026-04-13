using System;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 标记一个类为 World System，并指定其执行顺序和阶段。
    /// 使用此特性标记的类会被 AutoSystemInstaller 自动发现和注册。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorldSystemAttribute : MarkerAttribute
    {
        /// <summary>
        /// 获取系统的执行优先级，数值越小越先执行。
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// 获取或设置系统所属的阶段，默认值为 Execute 阶段。
        /// </summary>
        public WorldSystemPhase Phase { get; set; } = WorldSystemPhase.Execute;

        /// <summary>
        /// 创建 WorldSystemAttribute 实例。
        /// </summary>
        /// <param name="order">执行优先级，默认值为 0</param>
        public WorldSystemAttribute(int order = 0)
        {
            Order = order;
        }
    }

    /// <summary>
    /// 系统执行阶段的枚举。
    /// </summary>
    public enum WorldSystemPhase
    {
        /// <summary>预执行阶段，在主循环之前运行</summary>
        PreExecute = 0,

        /// <summary>主执行阶段，默认执行阶段</summary>
        Execute = 1,

        /// <summary>后执行阶段，在主循环之后运行</summary>
        PostExecute = 2,
    }
}
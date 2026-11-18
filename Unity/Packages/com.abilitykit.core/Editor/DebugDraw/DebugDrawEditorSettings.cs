using System;
using AbilityKit.Core.Debugging;

namespace AbilityKit.Core.Editor.Debugging
{
    /// <summary>
    /// 保存旧编辑器绘制的域内静态设置，不读写 EditorPrefs、项目资产或磁盘。
    /// 仅兼容至下一主版本，新代码使用 Diagnostics.Editor.DebugDraw 中的对应类型。
    /// </summary>
    /// <remarks>
    /// 设置没有变更通知、校验或同步，实际重置时机依赖编辑器域重载配置。
    /// 条目上限、碰撞颜色和层掩码不由旧 SceneView 驱动自动执行。
    /// </remarks>
    [Obsolete("Use AbilityKit.Diagnostics.Editor.DebugDraw.DebugDrawEditorSettings before the next major version.")]
    public static class DebugDrawEditorSettings
    {
        /// <summary>
        /// 保留旧绘制分类位的约定名称，不限制掩码只能使用这些位。
        /// </summary>
        public static class Masks
        {
            /// <summary>
            /// 获取零分类位，不启用任何分类。
            /// </summary>
            public static readonly DebugDrawMask None = DebugDrawMask.None;
            /// <summary>
            /// 获取碰撞分类位，原始值为一。
            /// </summary>
            public static readonly DebugDrawMask Collision = new DebugDrawMask(1 << 0);
            /// <summary>
            /// 获取选取目标分类位，原始值为二。
            /// </summary>
            public static readonly DebugDrawMask Targeting = new DebugDrawMask(1 << 1);
            /// <summary>
            /// 获取导航分类位，原始值为四。
            /// </summary>
            public static readonly DebugDrawMask Nav = new DebugDrawMask(1 << 2);
            /// <summary>
            /// 获取全部分类位，原始值为负一，包含尚未命名的位。
            /// </summary>
            public static readonly DebugDrawMask All = DebugDrawMask.All;
        }

        /// <summary>
        /// 获取或设置旧绘制开关，初始值为真。
        /// 旧驱动还要求处于播放模式且启用掩码非零；修改本属性不立即触发通知。
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 获取或设置分类掩码，初始值为碰撞分类，不验证未知位。
        /// </summary>
        public static DebugDrawMask EnabledMask { get; set; } = Masks.Collision;

        /// <summary>
        /// 获取或设置供内容提供方自行采用的条目上限，初始值为二千零四十八。
        /// 不校验零或负数，旧驱动不读取或强制执行此值。
        /// </summary>
        public static int MaxItemsPerContributor { get; set; } = 2048;

        /// <summary>
        /// 获取或设置供内容提供方自行采用的碰撞颜色，初始值为不透明绿色。
        /// 旧驱动不读取该值，也不替换各绘制请求的样式。
        /// </summary>
        public static DebugDrawColor CollisionColor { get; set; } = DebugDrawColor.Green;

        /// <summary>
        /// 获取或设置供内容提供方自行解释的碰撞层掩码，初始值为零。
        /// 旧驱动不读取该值，不将零解释为特定层过滤策略。
        /// </summary>
        public static int CollisionLayerMask { get; set; } = 0;
    }
}

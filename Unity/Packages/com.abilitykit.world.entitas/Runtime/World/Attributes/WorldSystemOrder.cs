namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 定义系统执行顺序常量的静态类。
    /// 用于在 WorldSystemAttribute 中设置 Order 值时参考。
    /// </summary>
    public static class WorldSystemOrder
    {
        /// <summary>
        /// 模块之间的步进值，用于分隔不同模块的执行顺序。
        /// </summary>
        public const int ModuleStep = 1000;

        /// <summary>
        /// 早期执行阶段的基准值。
        /// </summary>
        public const int Early = 100;

        /// <summary>
        /// 正常执行阶段的基准值。
        /// </summary>
        public const int Normal = 500;

        /// <summary>
        /// 晚期执行阶段的基准值。
        /// </summary>
        public const int Late = 900;

        /// <summary>
        /// 核心系统的基准执行顺序（0 * ModuleStep）。
        /// </summary>
        public const int CoreBase = 0 * ModuleStep;

        /// <summary>
        /// 调试系统的基准执行顺序（9 * ModuleStep）。
        /// </summary>
        public const int DebugBase = 9 * ModuleStep;
    }
}
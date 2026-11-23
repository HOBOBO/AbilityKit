namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 旧 JSON 校验包装器的配置；不参与目录加载、合并或宿主启动。
    /// </summary>
    public class TriggerValidationOptions
    {
        /// <summary>
        /// 是否在 JSON 写入内部数据库后自动校验。
        /// </summary>
        public bool ValidateOnLoad { get; set; } = false;

        /// <summary>
        /// 是否在自动校验失败后抛出异常；不回滚已加载数据。
        /// </summary>
        public bool ThrowOnValidationFailure { get; set; } = false;

        /// <summary>
        /// 自动校验时是否通过框架日志报告警告。
        /// </summary>
        public bool LogWarnings { get; set; } = true;

        /// <summary>
        /// 自动校验时是否通过框架日志报告错误。
        /// </summary>
        public bool LogErrors { get; set; } = true;

        /// <summary>
        /// 最大嵌套深度（UGC 限制）
        /// </summary>
        public int MaxNestingDepth { get; set; } = 10;

        /// <summary>
        /// 最大节点数（UGC 限制）
        /// </summary>
        public int MaxNodeCount { get; set; } = 100;

        /// <summary>
        /// 最大复杂度（UGC 限制）
        /// </summary>
        public int MaxComplexity { get; set; } = 50;

        /// <summary>
        /// 最大 Action 数量（UGC 限制）
        /// </summary>
        public int MaxActionCount { get; set; } = 20;

        /// <summary>
        /// 为兼容保留的递归深度设置；包装器创建默认校验器时未使用此值。
        /// </summary>
        public int MaxRecursionDepth { get; set; } = 5;

        /// <summary>
        /// 是否为 UGC 模式（更严格的限制）
        /// </summary>
        public bool IsUgcMode { get; set; } = false;

        /// <summary>
        /// UGC 模式的预设配置
        /// </summary>
        public static TriggerValidationOptions UgcPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = true,
            ThrowOnValidationFailure = true,
            LogWarnings = true,
            LogErrors = true,
            MaxNestingDepth = 5,
            MaxNodeCount = 50,
            MaxComplexity = 30,
            MaxActionCount = 10,
            MaxRecursionDepth = 3,
            IsUgcMode = true
        };

        /// <summary>
        /// 开发模式的预设配置
        /// </summary>
        public static TriggerValidationOptions DevelopmentPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = true,
            ThrowOnValidationFailure = false,
            LogWarnings = true,
            LogErrors = true,
            MaxNestingDepth = 50,
            MaxNodeCount = 500,
            MaxComplexity = 200,
            MaxActionCount = 100,
            MaxRecursionDepth = 10,
            IsUgcMode = false
        };

        /// <summary>
        /// 关闭自动校验的历史预设；不是宿主的生产启动配置。
        /// </summary>
        public static TriggerValidationOptions ProductionPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = false,
            ThrowOnValidationFailure = false,
            LogWarnings = false,
            LogErrors = true,
            MaxNestingDepth = 20,
            MaxNodeCount = 200,
            MaxComplexity = 100,
            MaxActionCount = 50,
            MaxRecursionDepth = 5,
            IsUgcMode = false
        };
    }
}

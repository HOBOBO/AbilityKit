namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 一次管线运行的上下文访问面，含运行信息及阶段间数据。
    /// 需要恢复的项目由调用方提供独立的状态捕获与恢复，不备份实时上下文对象。
    /// </summary>
    public interface IAbilityPipelineContext
    {
        /// <summary>
        /// 当前绑定的能力实例，例如技能或 Buff。
        /// 恢复时按领域的稳定身份重新绑定，不复制此引用。
        /// </summary>
        object? AbilityInstance { get; }

        /// <summary>
        /// 阶段间共享数据的兼容入口，不提供值类型或快照校验。
        /// 可恢复的值应声明类型和所有者；不要在此存放委托、服务、实体引用或订阅。
        /// </summary>
        System.Collections.Generic.Dictionary<string, object?> SharedData { get; }

        /// <summary>
        /// 读取指定类型的共享值。
        /// 键缺失或类型不匹配时使用默认值，因此不适合校验回滚所需的必填数据。
        /// </summary>
        T GetData<T>(string key, T defaultValue = default!);

        /// <summary>
        /// 写入共享值，不声明此值能被备份或恢复。
        /// 新增跨帧权威数据优先放在类型化的领域状态中。
        /// </summary>
        void SetData<T>(string key, T value);

        /// <summary>
        /// 尝试获取共享数据（强类型安全版本）。
        /// </summary>
        bool TryGetData<T>(string key, out T value);

        /// <summary>
        /// 移除共享数据。
        /// </summary>
        bool RemoveData(string key);

        /// <summary>
        /// 清除共享数据。
        /// </summary>
        void ClearData();

        /// <summary>
        /// 当前阶段 ID。
        /// </summary>
        AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        
        /// <summary>
        /// 管线状态。
        /// </summary>
        EAbilityPipelineState PipelineState { get; set; }
        
        /// <summary>
        /// 是否被中断。
        /// </summary>
        bool IsAborted { get; set; }
        
        /// <summary>
        /// 是否暂停。
        /// </summary>
        bool IsPaused { get; set; }
        
        /// <summary>
        /// 管线开始时间。
        /// </summary>
        float StartTime { get; set; }
        
        /// <summary>
        /// 由上下文实现提供的已运行时间。
        /// 参与确定性模拟的读数应来自宿主推进的逻辑时间。
        /// </summary>
        float ElapsedTime { get; }
        
        /// <summary>
        /// 重置上下文。
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 管线上下文接口（强类型能力实例）。
    /// </summary>
    public interface IAbilityPipelineContext<TAbilityInstance> : IAbilityPipelineContext
    {
        /// <summary>
        /// 强类型能力实例（技能、Buff 等）。
        /// </summary>
        new TAbilityInstance AbilityInstance { get; }
    }
}

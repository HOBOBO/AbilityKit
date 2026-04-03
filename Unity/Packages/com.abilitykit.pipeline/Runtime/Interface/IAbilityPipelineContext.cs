namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线上下文接口
    /// </summary>
    public interface IAbilityPipelineContext
    {
        /// <summary>
        /// 能力实例（技能、Buff等）
        /// </summary>
        object AbilityInstance { get; }

        /// <summary>
        /// 共享数据
        /// </summary>
        System.Collections.Generic.Dictionary<string, object> SharedData { get; }

        /// <summary>
        /// 获取共享数据
        /// </summary>
        T GetData<T>(string key, T defaultValue = default);

        /// <summary>
        /// 设置共享数据
        /// </summary>
        void SetData<T>(string key, T value);

        /// <summary>
        /// 尝试获取共享数据（强类型安全版本）
        /// </summary>
        bool TryGetData<T>(string key, out T value);

        /// <summary>
        /// 移除共享数据
        /// </summary>
        bool RemoveData(string key);

        /// <summary>
        /// 清除共享数据
        /// </summary>
        void ClearData();

        /// <summary>
        /// 当前阶段ID
        /// </summary>
        AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        
        /// <summary>
        /// 管线状态
        /// </summary>
        EAbilityPipelineState PipelineState { get; set; }
        
        /// <summary>
        /// 是否被中断
        /// </summary>
        bool IsAborted { get; set; }
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; set; }
        
        /// <summary>
        /// 管线开始时间
        /// </summary>
        float StartTime { get; set; }
        
        /// <summary>
        /// 已运行时间
        /// </summary>
        float ElapsedTime { get; }
        
        /// <summary>
        /// 重置上下文
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 管线上下文接口（强类型 AbilityInstance）
    /// </summary>
    public interface IAbilityPipelineContext<TAbilityInstance> : IAbilityPipelineContext
    {
        /// <summary>
        /// 能力实例（技能、Buff等）
        /// </summary>
        TAbilityInstance AbilityInstance { get; }
    }
}
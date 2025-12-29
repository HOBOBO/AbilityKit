using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线配置接口
    /// </summary>
    public interface IAbilityPipelineConfig
    {
        /// <summary>
        /// 配置ID
        /// </summary>
        int ConfigId { get; }
        
        /// <summary>
        /// 配置名称
        /// </summary>
        string ConfigName { get; }
        
        /// <summary>
        /// 阶段配置列表
        /// </summary>
        IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs { get; }
        
        /// <summary>
        /// 是否允许中断
        /// </summary>
        bool AllowInterrupt { get; }
        
        /// <summary>
        /// 是否允许暂停
        /// </summary>
        bool AllowPause { get; }
    }
    
    /// <summary>
    /// 阶段配置接口
    /// </summary>
    public interface IAbilityPhaseConfig
    {
        /// <summary>
        /// 阶段ID
        /// </summary>
        AbilityPipelinePhaseId PhaseId { get; }
        
        /// <summary>
        /// 阶段类型
        /// </summary>
        string PhaseType { get; }
        
        /// <summary>
        /// 阶段持续时间（-1表示无限）
        /// </summary>
        float Duration { get; }
        
        /// <summary>
        /// 阶段参数
        /// </summary>
        Dictionary<string, object> Parameters { get; }
    }
}
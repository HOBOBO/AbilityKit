using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线上下文抽象基类
    /// </summary>
    public abstract class AAbilityPipelineContext : IAbilityPipelineContext
    {
        /// <summary>
        /// 能力实例
        /// </summary>
        public object AbilityInstance { get; protected set; }
        
        /// <summary>
        /// 当前阶段ID
        /// </summary>
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        
        /// <summary>
        /// 管线状态
        /// </summary>
        public EAbilityPipelineState PipelineState { get; set; }
        
        /// <summary>
        /// 是否被中断
        /// </summary>
        public bool IsAborted { get; set; }
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused { get; set; }
        
        /// <summary>
        /// 管线开始时间
        /// </summary>
        public float StartTime { get; set; }
        
        /// <summary>
        /// 已运行时间
        /// </summary>
        public float ElapsedTime => Time.realtimeSinceStartup - StartTime;
        
        /// <summary>
        /// 共享数据
        /// </summary>
        public Dictionary<string, object> SharedData { get; } = new();
        
        /// <summary>
        /// 获取共享数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (SharedData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }
        
        /// <summary>
        /// 设置共享数据
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            SharedData[key] = value;
        }

        /// <summary>
        /// 尝试获取共享数据（强类型安全版本）
        /// </summary>
        public bool TryGetData<T>(string key, out T value)
        {
            if (SharedData.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }
        
        /// <summary>
        /// 移除共享数据
        /// </summary>
        public bool RemoveData(string key)
        {
            return SharedData.Remove(key);
        }
        
        /// <summary>
        /// 清空共享数据
        /// </summary>
        public void ClearData()
        {
            SharedData.Clear();
        }
        
        /// <summary>
        /// 重置上下文
        /// </summary>
        public virtual void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0;
            SharedData.Clear();
        }
        
        /// <summary>
        /// 初始化上下文
        /// </summary>
        public virtual void Initialize(object abilityInstance)
        {
            AbilityInstance = abilityInstance;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = Time.realtimeSinceStartup;
        }
    }
}
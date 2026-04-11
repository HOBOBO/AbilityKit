using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Examples
{
    /// <summary>
    /// 示例：管线上下文实现
    /// </summary>
    public class ExampleAbilityPipelineContext : IAbilityPipelineContext
    {
        /// <summary>
        /// 能力实例引用
        /// </summary>
        public object AbilityInstance { get; set; }

        /// <summary>
        /// 共享数据字典
        /// </summary>
        private readonly Dictionary<string, object> _sharedData = new Dictionary<string, object>();

        public Dictionary<string, object> SharedData => _sharedData;

        /// <summary>
        /// 获取共享数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_sharedData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置共享数据
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            _sharedData[key] = value;
        }

        /// <summary>
        /// 尝试获取共享数据
        /// </summary>
        public bool TryGetData<T>(string key, out T value)
        {
            if (_sharedData.TryGetValue(key, out var obj) && obj is T typedValue)
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
            return _sharedData.Remove(key);
        }

        /// <summary>
        /// 清除共享数据
        /// </summary>
        public void ClearData()
        {
            _sharedData.Clear();
        }

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
        public float ElapsedTime { get; set; }

        /// <summary>
        /// 重置上下文
        /// </summary>
        public void Reset()
        {
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Idle;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0;
            ElapsedTime = 0;
            _sharedData.Clear();
        }
    }
}

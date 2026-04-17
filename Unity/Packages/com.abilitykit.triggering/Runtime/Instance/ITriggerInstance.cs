using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Instance
{
    /// <summary>
    /// 触发器运行时实例接口
    /// 封装配置数据（只读）和运行时状态（可变）
    /// </summary>
    public interface ITriggerInstance : IDisposable
    {
        /// <summary>
        /// 配置数据引用（只读）- 改为 Spec 以符合命名规范
        /// </summary>
        ITriggerPlanConfig Spec { get; }

        /// <summary>
        /// 当前状态（可变）
        /// </summary>
        ETriggerState CurrentState { get; set; }

        /// <summary>
        /// 已运行时间（毫秒）
        /// </summary>
        long ElapsedMs { get; set; }

        /// <summary>
        /// 执行次数
        /// </summary>
        int ExecutionCount { get; set; }

        /// <summary>
        /// 起始服务器时间
        /// </summary>
        long StartServerTime { get; }

        /// <summary>
        /// 实例独享数据（类型安全，不参与网络同步）
        /// </summary>
        IReadOnlyDictionary<string, object> InstanceData { get; }

        /// <summary>
        /// 关联的行为实例
        /// </summary>
        ITriggerBehavior Behavior { get; }

        /// <summary>
        /// 是否已完成（终态）
        /// </summary>
        bool IsTerminated { get; }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 获取实例数据（类型安全）
        /// </summary>
        bool TryGetInstanceData<T>(string key, out T value);

        /// <summary>
        /// 设置实例数据
        /// </summary>
        void SetInstanceData<T>(string key, T value);

        /// <summary>
        /// 移除实例数据
        /// </summary>
        bool RemoveInstanceData(string key);

        /// <summary>
        /// 创建快照用于网络同步
        /// </summary>
        TriggerSnapshot CreateSnapshot();

        /// <summary>
        /// 从快照恢复状态
        /// </summary>
        void RestoreFromSnapshot(TriggerSnapshot snapshot);
    }

    /// <summary>
    /// 触发器运行时实例实现
    /// </summary>
    public class TriggerInstance : ITriggerInstance
    {
        // Spec（只读配置）
        public ITriggerPlanConfig Spec { get; }

        // 核心状态字段（直接暴露，替代独立的 TriggerState）
        public ETriggerState CurrentState { get; set; }
        public long ElapsedMs { get; set; }
        public int ExecutionCount { get; set; }
        public long StartServerTime { get; }

        // 实例独享数据（类型安全，不参与网络同步）
        private readonly Dictionary<string, object> _instanceData = new Dictionary<string, object>();
        public IReadOnlyDictionary<string, object> InstanceData => _instanceData;

        // 行为
        public ITriggerBehavior Behavior { get; set; }

        // 便捷属性
        public bool IsTerminated => CurrentState == ETriggerState.Completed || CurrentState == ETriggerState.Interrupted;
        public bool IsRunning => CurrentState == ETriggerState.Running;
        public bool IsCompleted => CurrentState == ETriggerState.Completed;
        public bool IsIdle => CurrentState == ETriggerState.Idle;
        public bool IsPaused => CurrentState == ETriggerState.Paused;

        // 事件：状态变化通知
        public event Action<ETriggerState, ETriggerState> OnStateChanged;

        public TriggerInstance(ITriggerPlanConfig config, int executorId, long serverTime)
        {
            Spec = config ?? throw new ArgumentNullException(nameof(config));
            StartServerTime = serverTime;
            CurrentState = ETriggerState.Idle;
            ElapsedMs = 0;
            ExecutionCount = 0;
            Behavior = null;
        }

        /// <summary>
        /// 尝试获取实例数据（类型安全）
        /// </summary>
        public bool TryGetInstanceData<T>(string key, out T value)
        {
            if (key != null && _instanceData.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 获取实例数据（如果不存在则返回默认值）
        /// </summary>
        public T GetInstanceData<T>(string key, T defaultValue = default)
        {
            if (key != null && _instanceData.TryGetValue(key, out var obj) && obj is T t)
            {
                return t;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置实例数据
        /// </summary>
        public void SetInstanceData<T>(string key, T value)
        {
            _instanceData[key] = value;
        }

        /// <summary>
        /// 移除实例数据
        /// </summary>
        public bool RemoveInstanceData(string key)
        {
            if (key == null) return false;
            return _instanceData.Remove(key);
        }

        /// <summary>
        /// 清除所有实例数据
        /// </summary>
        public void ClearInstanceData()
        {
            _instanceData.Clear();
        }

        /// <summary>
        /// 检查是否包含指定的实例数据键
        /// </summary>
        public bool ContainsInstanceDataKey(string key)
        {
            return key != null && _instanceData.ContainsKey(key);
        }

        /// <summary>
        /// 状态转换（带验证）
        /// </summary>
        public bool TryTransitionTo(ETriggerState expectedState, ETriggerState newState)
        {
            if (CurrentState != expectedState) return false;
            var oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
            return true;
        }

        /// <summary>
        /// 设置运行状态
        /// </summary>
        public void SetRunning()
        {
            if (TryTransitionTo(ETriggerState.Idle, ETriggerState.Running) ||
                TryTransitionTo(ETriggerState.Paused, ETriggerState.Running))
            {
                // 状态已转换
            }
        }

        /// <summary>
        /// 设置完成状态
        /// </summary>
        public void SetCompleted()
        {
            TryTransitionTo(ETriggerState.Running, ETriggerState.Completed);
            TryTransitionTo(ETriggerState.Idle, ETriggerState.Completed);
        }

        /// <summary>
        /// 设置中断状态
        /// </summary>
        public void SetInterrupted(string reason = null)
        {
            // 中断可以从任何状态（除了终态）发生
            if (!IsTerminated)
            {
                var oldState = CurrentState;
                CurrentState = ETriggerState.Interrupted;
                OnStateChanged?.Invoke(oldState, ETriggerState.Interrupted);
                // 可选：保存中断原因到 InstanceData
                if (!string.IsNullOrEmpty(reason))
                {
                    SetInstanceData("InterruptionReason", reason);
                }
            }
        }

        /// <summary>
        /// 设置暂停状态
        /// </summary>
        public void SetPaused()
        {
            TryTransitionTo(ETriggerState.Running, ETriggerState.Paused);
        }

        /// <summary>
        /// 恢复运行（从暂停）
        /// </summary>
        public void Resume()
        {
            TryTransitionTo(ETriggerState.Paused, ETriggerState.Running);
        }

        /// <summary>
        /// 重置实例（重新执行）
        /// </summary>
        public void Reset(long? serverTime = null)
        {
            CurrentState = ETriggerState.Idle;
            ElapsedMs = 0;
            ExecutionCount = 0;
            if (serverTime.HasValue && serverTime.Value > 0)
            {
                // 如果提供了新时间，需要私有setter支持，这里暂时不修改StartServerTime
                // 实际使用中可能不需要Reset时修改StartServerTime
            }
            ClearInstanceData();
        }

        /// <summary>
        /// 增加执行次数
        /// </summary>
        public void IncrementExecutionCount()
        {
            ExecutionCount++;
        }

        /// <summary>
        /// 获取进度百分比（如果有 DurationSeconds 配置）
        /// </summary>
        public float GetProgressPercentage()
        {
            // 如果 Spec 中有 DurationSeconds，可以计算进度
            // 这里作为示例，实际需要根据具体配置类型计算
            if (Spec is IHasDuration durationSpec && durationSpec.DurationSeconds > 0)
            {
                return Math.Min(1.0f, (float)(ElapsedMs / 1000.0 / durationSpec.DurationSeconds));
            }
            return 0f;
        }

        /// <summary>
        /// 获取剩余时间（毫秒）
        /// </summary>
        public long GetRemainingTime()
        {
            if (Spec is IHasDuration durationSpec && durationSpec.DurationSeconds > 0)
            {
                var totalMs = (long)(durationSpec.DurationSeconds * 1000);
                return Math.Max(0, totalMs - ElapsedMs);
            }
            return -1; // 无时长限制
        }

        public TriggerSnapshot CreateSnapshot()
        {
            return TriggerSnapshot.FromInstance(this);
        }

        public void RestoreFromSnapshot(TriggerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ElapsedMs = snapshot.ElapsedMs;
            ExecutionCount = snapshot.ExecutionCount;
            CurrentState = snapshot.State;
            // 注意：不恢复 InstanceData（实例独享数据不参与同步）
        }

        public void Dispose()
        {
            _instanceData.Clear();
        }
    }

    /// <summary>
    /// 具有持续时间的触发器配置接口（用于进度计算）
    /// </summary>
    public interface IHasDuration
    {
        float DurationSeconds { get; }
    }

    /// <summary>
    /// 可快照的实例接口
    /// </summary>
    public interface ISnapshotable<T> where T : class
    {
        T CreateSnapshot();
        void RestoreFromSnapshot(T snapshot);
    }
}
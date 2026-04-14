using System;

namespace AbilityKit.Triggering.Runtime.Instance
{
    /// <summary>
    /// 触发器状态枚举
    /// </summary>
    public enum ETriggerState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Interrupted,
    }

    /// <summary>
    /// 触发器快照（用于网络同步和断线重连）
    /// 只包含需要同步的核心状态字段
    /// </summary>
    [Serializable]
    public class TriggerSnapshot
    {
        public int TriggerId { get; set; }
        public int BehaviorTypeId { get; set; }
        public long ElapsedMs { get; set; }
        public int ExecutionCount { get; set; }
        public ETriggerState State { get; set; }
        public long ServerTime { get; set; }

        /// <summary>
        /// 实例独享数据快照（用于完整状态恢复）
        /// 注意：不参与网络同步，仅本地回滚使用
        /// </summary>
        public Dictionary<string, object> InstanceDataSnapshot { get; set; }

        /// <summary>
        /// 从触发器实例创建快照
        /// </summary>
        public static TriggerSnapshot FromInstance(TriggerInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new TriggerSnapshot
            {
                TriggerId = instance.Spec.TriggerId,
                BehaviorTypeId = instance.Behavior?.GetType().GetHashCode() ?? 0,
                ElapsedMs = instance.ElapsedMs,
                ExecutionCount = instance.ExecutionCount,
                State = instance.CurrentState,
                ServerTime = instance.StartServerTime + instance.ElapsedMs,
                // 保存实例数据快照
                InstanceDataSnapshot = instance.InstanceData != null 
                    ? new Dictionary<string, object>(instance.InstanceData) 
                    : null
            };
        }

        /// <summary>
        /// 将快照应用到触发器实例
        /// </summary>
        public void ApplyTo(TriggerInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            instance.ElapsedMs = ElapsedMs;
            instance.ExecutionCount = ExecutionCount;
            instance.CurrentState = State;
            
            // 恢复实例数据快照（如果存在）
            if (InstanceDataSnapshot != null)
            {
                // 清除现有数据并恢复快照
                // 注意：TriggerInstance 的 _instanceData 是私有的，需要通过公共方法操作
                foreach (var kvp in instance.InstanceData.ToList())
                {
                    instance.RemoveInstanceData(kvp.Key);
                }
                foreach (var kvp in InstanceDataSnapshot)
                {
                    instance.SetInstanceData(kvp.Key, kvp.Value);
                }
            }
            // 注意：不恢复 InstanceData（实例独享数据不参与同步）
        }
    }
}
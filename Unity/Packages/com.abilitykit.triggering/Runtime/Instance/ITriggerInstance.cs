using System;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Instance
{
    /// <summary>
    /// 触发器运行时实例接口
    /// 封装配置数据（只读）和运行时状态（可变）
    /// </summary>
    public interface ITriggerInstance
    {
        /// <summary>
        /// 配置数据引用（只读）
        /// </summary>
        ITriggerPlanConfig Config { get; }

        /// <summary>
        /// 运行时状态（可变）
        /// </summary>
        TriggerState State { get; }

        /// <summary>
        /// 关联的行为实例
        /// </summary>
        ITriggerBehavior Behavior { get; }

        /// <summary>
        /// 是否已完成（终态）
        /// </summary>
        bool IsTerminated { get; }

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
        public ITriggerPlanConfig Config { get; }
        public TriggerState State { get; }
        public ITriggerBehavior Behavior { get; set; }
        
        public bool IsTerminated => 
            State.CurrentState == ETriggerState.Completed ||
            State.CurrentState == ETriggerState.Interrupted;

        public TriggerInstance(ITriggerPlanConfig config, int executorId, long serverTime)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = TriggerState.Create(config.TriggerId, executorId, serverTime);
            Behavior = null;
        }

        public TriggerSnapshot CreateSnapshot()
        {
            return TriggerSnapshot.FromState(State, Behavior?.GetType().GetHashCode() ?? 0);
        }

        public void RestoreFromSnapshot(TriggerSnapshot snapshot)
        {
            snapshot.ApplyTo(State);
        }
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
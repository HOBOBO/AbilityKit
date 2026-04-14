using System;
using AbilityKit.Triggering.Runtime.Behavior;

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
    /// 触发器运行时状态（执行期间动态变化的数据）
    /// </summary>
    [Serializable]
    public class TriggerState
    {
        public int TriggerId { get; set; }
        public int ExecutorId { get; set; }
        public ETriggerState CurrentState { get; set; }
        public long ElapsedMs { get; set; }
        public int ExecutionCount { get; set; }
        public long StartServerTime { get; set; }
        public byte[] CustomData { get; set; }

        public static TriggerState Create(int triggerId, int executorId, long serverTime)
        {
            return new TriggerState
            {
                TriggerId = triggerId,
                ExecutorId = executorId,
                CurrentState = ETriggerState.Idle,
                ElapsedMs = 0,
                ExecutionCount = 0,
                StartServerTime = serverTime,
                CustomData = null
            };
        }

        public TriggerState Clone()
        {
            return new TriggerState
            {
                TriggerId = TriggerId,
                ExecutorId = ExecutorId,
                CurrentState = CurrentState,
                ElapsedMs = ElapsedMs,
                ExecutionCount = ExecutionCount,
                StartServerTime = StartServerTime,
                CustomData = CustomData != null ? (byte[])CustomData.Clone() : null
            };
        }
    }

    /// <summary>
    /// 触发器快照（用于网络同步和断线重连）
    /// </summary>
    [Serializable]
    public class TriggerSnapshot
    {
        public int TriggerId { get; set; }
        public int BehaviorTypeId { get; set; }
        public long ElapsedMs { get; set; }
        public int ExecutionCount { get; set; }
        public ETriggerState State { get; set; }
        public byte[] CustomData { get; set; }
        public long ServerTime { get; set; }

        public static TriggerSnapshot FromState(TriggerState state, int behaviorTypeId)
        {
            return new TriggerSnapshot
            {
                TriggerId = state.TriggerId,
                BehaviorTypeId = behaviorTypeId,
                ElapsedMs = state.ElapsedMs,
                ExecutionCount = state.ExecutionCount,
                State = state.CurrentState,
                CustomData = state.CustomData,
                ServerTime = state.StartServerTime + state.ElapsedMs
            };
        }

        public void ApplyTo(TriggerState state)
        {
            state.ElapsedMs = ElapsedMs;
            state.ExecutionCount = ExecutionCount;
            state.CurrentState = State;
            state.CustomData = CustomData;
        }
    }
}
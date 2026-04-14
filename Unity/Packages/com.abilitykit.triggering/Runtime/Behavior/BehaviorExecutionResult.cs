using System;

namespace AbilityKit.Triggering.Runtime.Behavior
{
    /// <summary>
    /// 行为执行结果
    /// </summary>
    [Serializable]
    public struct BehaviorExecutionResult
    {
        public bool IsSuccess { get; }
        public bool IsInterrupted { get; }
        public bool IsCompleted { get; }
        public string FailureReason { get; }
        public int ExecutedCount { get; }

        private BehaviorExecutionResult(bool success, bool interrupted, bool completed, string reason, int count)
        {
            IsSuccess = success;
            IsInterrupted = interrupted;
            IsCompleted = completed;
            FailureReason = reason;
            ExecutedCount = count;
        }

        public static BehaviorExecutionResult Success(int count = 0) => 
            new BehaviorExecutionResult(true, false, false, null, count);

        public static BehaviorExecutionResult Completed() => 
            new BehaviorExecutionResult(true, false, true, null, 0);

        public static BehaviorExecutionResult Interrupted(string reason) => 
            new BehaviorExecutionResult(false, true, false, reason ?? "Unknown", 0);

        public static BehaviorExecutionResult Failure(string reason) => 
            new BehaviorExecutionResult(false, false, false, reason ?? "Unknown", 0);
    }

    /// <summary>
    /// 行为状态枚举
    /// </summary>
    public enum EBehaviorState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Interrupted,
    }

    /// <summary>
    /// 行为快照（用于网络同步）
    /// </summary>
    [Serializable]
    public class BehaviorSnapshot
    {
        public int TriggerId { get; set; }
        public int BehaviorTypeId { get; set; }
        public long ElapsedMs { get; set; }
        public int ExecutionCount { get; set; }
        public EBehaviorState State { get; set; }
        public byte[] CustomData { get; set; }
    }
}
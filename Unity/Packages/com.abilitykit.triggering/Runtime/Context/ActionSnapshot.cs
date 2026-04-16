using System;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// Action 执行上下文快照
    /// 用于保存某一时刻的完整状态，支持回滚/网络同步
    /// </summary>
    [Serializable]
    public sealed class ActionSnapshot
    {
        /// <summary>
        /// 上下文实例 ID
        /// </summary>
        public int InstanceId { get; set; }

        /// <summary>
        /// 触发器 ID
        /// </summary>
        public int TriggerId { get; set; }

        /// <summary>
        /// Action ID
        /// </summary>
        public int ActionId { get; set; }

        /// <summary>
        /// Action 名称
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// 参数快照（配置参数）
        /// </summary>
        public ParameterBag Parameters { get; set; }

        /// <summary>
        /// 状态快照（运行时状态）
        /// </summary>
        public StateBag State { get; set; }

        /// <summary>
        /// 已执行次数
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// 累计运行时间（毫秒）
        /// </summary>
        public float ElapsedTimeMs { get; set; }

        /// <summary>
        /// 上次执行时间（毫秒）
        /// </summary>
        public float LastExecuteTimeMs { get; set; }

        /// <summary>
        /// 是否已中断
        /// </summary>
        public bool IsInterrupted { get; set; }

        /// <summary>
        /// 是否已取消
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// 快照创建时间戳
        /// </summary>
        public long CreatedTimestampMs { get; set; }

        /// <summary>
        /// 快照版本号（用于检测冲突）
        /// </summary>
        public int Version { get; set; }

        public override string ToString()
        {
            return $"ActionSnapshot[Id={InstanceId}, Action={ActionName}, Exec={ExecutionCount}, Elapsed={ElapsedTimeMs:F2}ms]";
        }
    }
}

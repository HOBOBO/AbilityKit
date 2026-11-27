using System;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 执行树调度条目的本地标识；不包含调度器身份或历史分支信息。
    /// </summary>
    public readonly struct TriggerScheduledExecutionHandle : IEquatable<TriggerScheduledExecutionHandle>
    {
        /// <summary>
        /// 以给定 ID 构造句柄，不查询调度器中的条目。
        /// </summary>
        public TriggerScheduledExecutionHandle(long id) => Id = id;

        /// <summary>
        /// 调度器分配的条目标识。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// ID 是否为正数；不表示该定义仍然存在。
        /// </summary>
        public bool IsValid => Id > 0L;

        /// <summary>
        /// 只比较 ID，不比较调度器或历史分支。
        /// </summary>
        public bool Equals(TriggerScheduledExecutionHandle other) => Id == other.Id;

        /// <summary>
        /// 判断对象是否为相同 ID 的调度句柄。
        /// </summary>
        public override bool Equals(object obj) => obj is TriggerScheduledExecutionHandle other && Equals(other);

        /// <summary>
        /// 返回 ID 的哈希值。
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// 输出有效 ID 或无效标记，供诊断使用。
        /// </summary>
        public override string ToString() => IsValid ? Id.ToString() : "Invalid";
    }
}

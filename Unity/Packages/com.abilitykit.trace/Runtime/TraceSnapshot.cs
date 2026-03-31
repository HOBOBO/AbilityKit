using System;

namespace AbilityKit.Trace
{
    /// <summary>
    /// 溯源节点快照
    /// 不可变的快照结构，用于外部访问节点数据
    /// </summary>
    [Serializable]
    public readonly struct TraceSnapshot : IEquatable<TraceSnapshot>
    {
        public long ContextId { get; }
        public long RootId { get; }
        public long ParentId { get; }
        public int Kind { get; }
        public int ConfigId { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public long OriginSourceId { get; }
        public string OriginSourceDisplay { get; }
        public long OriginTargetId { get; }
        public string OriginTargetDisplay { get; }
        public int CreatedFrame { get; }
        public int EndedFrame { get; }
        public int EndReason { get; }

        /// <summary>
        /// 是否已结束
        /// </summary>
        public bool IsEnded => EndedFrame != 0;

        /// <summary>
        /// 是否为根节点
        /// </summary>
        public bool IsRoot => ContextId == RootId;

        /// <summary>
        /// 是否为叶子节点（没有子节点）
        /// </summary>
        public bool IsLeaf => ParentId != 0;

        public TraceSnapshot(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int configId,
            long sourceActorId,
            long targetActorId,
            long originSourceId,
            string originSourceDisplay,
            long originTargetId,
            string originTargetDisplay,
            int createdFrame,
            int endedFrame,
            int endReason)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            OriginSourceId = originSourceId;
            OriginSourceDisplay = originSourceDisplay;
            OriginTargetId = originTargetId;
            OriginTargetDisplay = originTargetDisplay;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
        }

        internal TraceSnapshot(in TraceContextRecord record)
        {
            ContextId = record.ContextId;
            RootId = record.RootId;
            ParentId = record.ParentId;
            Kind = record.Kind;
            ConfigId = record.ConfigId;
            SourceActorId = record.SourceActorId;
            TargetActorId = record.TargetActorId;
            OriginSourceId = record.OriginSourceId;
            OriginSourceDisplay = record.OriginSourceDisplay;
            OriginTargetId = record.OriginTargetId;
            OriginTargetDisplay = record.OriginTargetDisplay;
            CreatedFrame = record.CreatedFrame;
            EndedFrame = record.EndedFrame;
            EndReason = record.EndReason;
        }

        public bool Equals(TraceSnapshot other)
        {
            return ContextId == other.ContextId;
        }

        public override bool Equals(object obj)
        {
            return obj is TraceSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ContextId.GetHashCode();
        }

        public static bool operator ==(TraceSnapshot left, TraceSnapshot right) => left.Equals(right);
        public static bool operator !=(TraceSnapshot left, TraceSnapshot right) => !left.Equals(right);

        public override string ToString()
        {
            return $"Trace[Id={ContextId}, Root={RootId}, Kind={Kind}, Ended={IsEnded}]";
        }
    }
}

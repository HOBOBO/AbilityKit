namespace AbilityKit.BehaviorTree
{
    /// <summary>节点运行状态。数值与序列化格式耦合，只允许追加。</summary>
    public enum BtNodeState
    {
        Inactive = 0,
        Running = 1,
        Success = 2,
        Failure = 3,
    }

    /// <summary>组合节点下条件节点的中断类型。数值与序列化格式耦合。</summary>
    public enum BtAbortType
    {
        None = 0,
        Self = 1,
        LowerPriority = 2,
        Both = 3,
    }

    /// <summary>节点类别，决定端口数量约束与编辑器呈现。</summary>
    public enum BtNodeKind
    {
        Composite = 0,
        Decorator = 1,
        Condition = 2,
        Action = 3,
    }

    /// <summary>黑板与属性允许的值类型（封闭集合，确定性友好）。</summary>
    public enum BtValueType
    {
        Bool = 0,
        Int64 = 1,
        Fixed64 = 2,
        String = 3,
    }
}

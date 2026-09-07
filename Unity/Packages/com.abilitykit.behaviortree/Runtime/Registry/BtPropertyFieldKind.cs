namespace AbilityKit.BehaviorTree
{
    /// <summary>属性字段的编辑语义：字面量 / 黑板 key 引用 / 枚举（Int64 索引）</summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public enum BtPropertyFieldKind
    {
        Literal = 0,
        BlackboardKeyRef = 1,
        Enum = 2,
    }
}

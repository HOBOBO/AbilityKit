namespace AbilityKit.BehaviorTree
{
    /// <summary>节点声明的黑板访问（可选元数据，用于加载期校验 key 存在且类型一致）</summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtBlackboardKeyRef
    {
        public string Key { get; }
        public BtValueType Type { get; }

        public BtBlackboardKeyRef(string key, BtValueType type)
        {
            Key = key;
            Type = type;
        }
    }
}

using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>黑板 key 声明：名+ 类型 + 可选默认值</summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtBlackboardKeyDefinition
    {
        public string Name { get; set; } = "";
        public BtValueType Type { get; set; } = BtValueType.Int64;
        public BtPropertyValue? Default { get; set; }
    }

    /// <summary>
    /// 黑板 schema：树加载前声明全key 与类型，运行期读写按 schema 校验    /// key 顺序即声明顺序（确定性遍历），不允许重名    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtBlackboardSchema
    {
        public List<BtBlackboardKeyDefinition> Keys { get; set; } = new();

        public bool TryGetType(string name, out BtValueType type)
        {
            foreach (var key in Keys)
            {
                if (string.Equals(key.Name, name, System.StringComparison.Ordinal))
                {
                    type = key.Type;
                    return true;
                }
            }

            type = default;
            return false;
        }
    }
}

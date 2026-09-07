using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 节点属性包。键按字典序存储，保证导JSON 与定义哈希的字节级稳定    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtPropertyBag
    {
        private readonly SortedDictionary<string, BtPropertyValue> _values = new();

        public IReadOnlyDictionary<string, BtPropertyValue> Values => _values;

        public bool TryGet(string name, out BtPropertyValue value) => _values.TryGetValue(name, out value!);

        public bool ContainsKey(string name) => _values.ContainsKey(name);

        public void Set(string name, BtPropertyValue value)
        {
            _values[name] = value;
        }

        public IEnumerable<string> Keys => _values.Keys;
    }

    /// <summary>
    /// 节点初始化阶段的类型化属性读取器：按描述schema 提供默认值，
    /// 读取成功即代表校验通过（校验由 BtTreeValidator 在加载时完成）    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public readonly struct BtPropertyReader
    {
        private readonly BtPropertyBag _bag;

        public BtPropertyReader(BtPropertyBag bag)
        {
            _bag = bag;
        }

        public bool GetBool(string name, bool fallback)
            => _bag != null && _bag.TryGet(name, out var v) && v.TryGetBool(out var b) ? b : fallback;

        public long GetInt64(string name, long fallback)
            => _bag != null && _bag.TryGet(name, out var v) && v.TryGetInt64(out var l) ? l : fallback;

        public int GetInt32(string name, int fallback)
            => (int)GetInt64(name, fallback);

        public Fixed64 GetFixed64(string name, Fixed64 fallback)
            => _bag != null && _bag.TryGet(name, out var v) && v.TryGetFixed64(out var f) ? f : fallback;

        public string GetString(string name, string fallback)
            => _bag != null && _bag.TryGet(name, out var v) && v.TryGetString(out var s) ? s : fallback;
    }
}

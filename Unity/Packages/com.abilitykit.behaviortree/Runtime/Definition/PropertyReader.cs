using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Definition
{
    public readonly struct PropertyReader
    {
        private readonly PropertyBag _bag;

        public PropertyReader(PropertyBag bag) => _bag = bag;
        public bool GetBool(string name, bool fallback) => _bag != null && _bag.TryGet(name, out var v) && v.TryGetBool(out var b) ? b : fallback;
        public long GetInt64(string name, long fallback) => _bag != null && _bag.TryGet(name, out var v) && v.TryGetInt64(out var l) ? l : fallback;
        public int GetInt32(string name, int fallback) => (int)GetInt64(name, fallback);
        public Fixed64 GetFixed64(string name, Fixed64 fallback) => _bag != null && _bag.TryGet(name, out var v) && v.TryGetFixed64(out var f) ? f : fallback;
        public string GetString(string name, string fallback) => _bag != null && _bag.TryGet(name, out var v) && v.TryGetString(out var s) ? s : fallback;
    }
}

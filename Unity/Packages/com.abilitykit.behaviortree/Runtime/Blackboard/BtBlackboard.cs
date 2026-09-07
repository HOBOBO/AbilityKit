using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>黑板值快照：schema key 顺序对齐的类型化数组</summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtBlackboardValueSnapshot
    {
        public List<string> KeyNames { get; set; } = new();
        public List<BtValueType> KeyTypes { get; set; } = new();
        public List<bool> BoolValues { get; set; } = new();
        public List<long> Int64Values { get; set; } = new();
        public List<long> Fixed64RawValues { get; set; } = new();
        public List<string> StringValues { get; set; } = new();
    }

    /// <summary>
    /// 类型化黑板：key 必须先在 schema 中声明；类型不匹配的读写是编程错误，直接抛出
    /// 值存储按 schema 槽位展开（无装箱），快照为纯数组拷贝    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtBlackboard
    {
        private readonly AbilityKit.BehaviorTree.Blackboard.Blackboard _canonical;

        private BtBlackboard(AbilityKit.BehaviorTree.Blackboard.Blackboard canonical)
            => _canonical = canonical;

        public BtBlackboardSchema Schema => _canonical.Schema.ToLegacy();

        public static BtBlackboard Create(BtBlackboardSchema schema)
            => new BtBlackboard(AbilityKit.BehaviorTree.Blackboard.Blackboard.Create(
                AbilityKit.BehaviorTree.Definition.BlackboardSchema.FromLegacy(schema)));

        internal static BtBlackboard Wrap(AbilityKit.BehaviorTree.Blackboard.Blackboard canonical)
            => new BtBlackboard(canonical);

        internal AbilityKit.BehaviorTree.Blackboard.Blackboard Canonical => _canonical;

        public bool GetBool(string key) => _canonical.GetBool(key);
        public long GetInt64(string key) => _canonical.GetInt64(key);
        public Fixed64 GetFixed64(string key) => _canonical.GetFixed64(key);
        public string GetString(string key) => _canonical.GetString(key);

        public bool TryGetBool(string key, out bool value) => _canonical.TryGetBool(key, out value);
        public bool TryGetInt64(string key, out long value) => _canonical.TryGetInt64(key, out value);
        public bool TryGetFixed64(string key, out Fixed64 value) => _canonical.TryGetFixed64(key, out value);
        public bool TryGetString(string key, out string value) => _canonical.TryGetString(key, out value);

        public void SetBool(string key, bool value) => _canonical.SetBool(key, value);
        public void SetInt64(string key, long value) => _canonical.SetInt64(key, value);
        public void SetFixed64(string key, Fixed64 value) => _canonical.SetFixed64(key, value);
        public void SetString(string key, string value) => _canonical.SetString(key, value);

        public BtBlackboardValueSnapshot CaptureValues() => _canonical.CaptureValues().ToLegacy();

        public void RestoreValues(BtBlackboardValueSnapshot snapshot)
        {
            if (snapshot == null) return;
            _canonical.RestoreValues(
                AbilityKit.BehaviorTree.Blackboard.BlackboardValueSnapshot.FromLegacy(snapshot));
        }

        internal void ValidateValues(BtBlackboardValueSnapshot snapshot)
            => _canonical.ValidateValues(
                AbilityKit.BehaviorTree.Blackboard.BlackboardValueSnapshot.FromLegacy(snapshot));
    }
}

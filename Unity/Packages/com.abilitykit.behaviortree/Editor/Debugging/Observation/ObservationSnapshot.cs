#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree;
using AbilityKit.Deterministic;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>
    /// 黑板值的一次不可变拷贝。与运行时的 <see cref="BlackboardValueSnapshot"/> 解耦，
    /// 保证观察端持有后不受运行时后续写入影响。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationBlackboard")]
    public sealed class ObservationBlackboard
    {
        private readonly string[] _keyNames;
        private readonly ValueType[] _keyTypes;
        private readonly bool[] _bools;
        private readonly long[] _int64s;
        private readonly long[] _fixedRaw;
        private readonly string[] _strings;
        private readonly Dictionary<string, int> _indexByKey;

        public int Count => _keyNames.Length;
        public IReadOnlyList<string> KeyNames => _keyNames;
        public IReadOnlyList<ValueType> KeyTypes => _keyTypes;

        private ObservationBlackboard(
            string[] keyNames, ValueType[] keyTypes,
            bool[] bools, long[] int64s, long[] fixedRaw, string[] strings)
        {
            _keyNames = keyNames;
            _keyTypes = keyTypes;
            _bools = bools;
            _int64s = int64s;
            _fixedRaw = fixedRaw;
            _strings = strings;
            _indexByKey = new Dictionary<string, int>(keyNames.Length, StringComparer.Ordinal);
            for (var i = 0; i < keyNames.Length; i++) _indexByKey[keyNames[i]] = i;
        }

        internal static ObservationBlackboard CreateForReplay(
            string[] keyNames,
            ValueType[] keyTypes,
            bool[] bools,
            long[] int64s,
            long[] fixedRaw,
            string[] strings)
        {
            keyNames ??= Array.Empty<string>();
            var count = keyNames.Length;
            return new ObservationBlackboard(
                CopyOrPad(keyNames, count, ""),
                CopyOrPad(keyTypes, count, ValueType.String),
                CopyOrPad(bools, count, false),
                CopyOrPad(int64s, count, 0L),
                CopyOrPad(fixedRaw, count, 0L),
                CopyOrPad(strings, count, ""));
        }

        public static ObservationBlackboard Copy(BlackboardValueSnapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var sourceNames = source.KeyNames ?? new List<string>();
            var sourceTypes = source.KeyTypes ?? new List<ValueType>();
            var sourceBools = source.BoolValues ?? new List<bool>();
            var sourceInt64s = source.Int64Values ?? new List<long>();
            var sourceFixed = source.Fixed64RawValues ?? new List<long>();
            var sourceStrings = source.StringValues ?? new List<string>();
            var count = sourceNames.Count;
            var keyNames = new string[count];
            var keyTypes = new ValueType[count];
            var bools = new bool[count];
            var int64s = new long[count];
            var fixedRaw = new long[count];
            var strings = new string[count];

            for (var i = 0; i < count; i++)
            {
                keyNames[i] = sourceNames[i] ?? "";
                keyTypes[i] = i < sourceTypes.Count ? sourceTypes[i] : ValueType.String;
                bools[i] = i < sourceBools.Count && sourceBools[i];
                int64s[i] = i < sourceInt64s.Count ? sourceInt64s[i] : 0L;
                fixedRaw[i] = i < sourceFixed.Count ? sourceFixed[i] : 0L;
                strings[i] = i < sourceStrings.Count ? sourceStrings[i] ?? "" : "";
            }

            return new ObservationBlackboard(keyNames, keyTypes, bools, int64s, fixedRaw, strings);
        }

        public int IndexOf(string key) => _indexByKey.TryGetValue(key, out var index) ? index : -1;

        public string KeyName(int index) => index >= 0 && index < _keyNames.Length ? _keyNames[index] : "";

        public ValueType KeyType(int index) =>
            index >= 0 && index < _keyTypes.Length ? _keyTypes[index] : ValueType.String;

        public bool TryGetBool(string key, out bool value)
        {
            if (_indexByKey.TryGetValue(key, out var i) && _keyTypes[i] == ValueType.Bool)
            { value = _bools[i]; return true; }
            value = false; return false;
        }

        public bool TryGetInt64(string key, out long value)
        {
            if (_indexByKey.TryGetValue(key, out var i) && _keyTypes[i] == ValueType.Int64)
            { value = _int64s[i]; return true; }
            value = 0L; return false;
        }

        public bool TryGetFixed64Raw(string key, out long raw)
        {
            if (_indexByKey.TryGetValue(key, out var i) && _keyTypes[i] == ValueType.Fixed64)
            { raw = _fixedRaw[i]; return true; }
            raw = 0L; return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (_indexByKey.TryGetValue(key, out var i) && _keyTypes[i] == ValueType.String)
            { value = _strings[i]; return true; }
            value = ""; return false;
        }

        /// <summary>按索引输出人类可读值，供观察端渲染与差异比较。</summary>
        public string GetDisplayValue(int index)
        {
            if (index < 0 || index >= _keyTypes.Length) return "?";
            return _keyTypes[index] switch
            {
                ValueType.Bool => _bools[index].ToString(),
                ValueType.Int64 => _int64s[index].ToString(),
                ValueType.Fixed64 => Fixed64.FromRaw(_fixedRaw[index]).ToString(),
                ValueType.String => _strings[index],
                _ => "?",
            };
        }

        public string GetDisplayValue(string key) => GetDisplayValue(IndexOf(key));

        private static T[] CopyOrPad<T>(T[]? source, int count, T fallback)
        {
            var result = new T[count];
            for (var i = 0; i < count; i++)
                result[i] = source != null && i < source.Length ? source[i] : fallback;
            return result;
        }
    }

    /// <summary>
    /// 一次不可变采样：frame、节点状态、黑板、活跃路径与来源映射。
    /// 观察窗口与图 overlay 消费同一份，避免各自重复拉取。构造后不可变，
    /// 其内容是对 <see cref="TreeDebugView"/> 快照数据的深拷贝。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationSnapshot")]
    public sealed class ObservationSnapshot
    {
        internal static readonly IReadOnlyDictionary<string, string> EmptySourceMap =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        private readonly NodeDebugInfo[] _nodes;
        private readonly string[] _activeNodeIds;
        private readonly HashSet<string>? _activeNodeSet;
        private readonly IReadOnlyDictionary<string, string> _sourceTree;
        private readonly IReadOnlyDictionary<string, string> _sourceNode;
        private readonly Dictionary<string, int> _nodeIndex;

        public long InstanceId { get; }
        public long Sequence { get; }
        public string TreeId { get; }
        public string DisplayName { get; }
        public string OwnerLabel { get; }
        public int Frame { get; }
        public ObservationBlackboard? Blackboard { get; }

        public IReadOnlyList<NodeDebugInfo> Nodes => _nodes;
        public int NodeCount => _nodes.Length;
        /// <summary>位于运行栈上的节点 id（OnStackCount &gt; 0），按拉取顺序。</summary>
        public IReadOnlyList<string> ActiveNodeIds => _activeNodeIds;
        /// <summary>nodeId -&gt; 来源 treeId（子树展开）。</summary>
        public IReadOnlyDictionary<string, string> SourceTree => _sourceTree;
        /// <summary>nodeId -&gt; 来源 authoring nodeId（子树展开）。</summary>
        public IReadOnlyDictionary<string, string> SourceNode => _sourceNode;

        private ObservationSnapshot(
            long instanceId, long sequence,
            string treeId, string displayName, string ownerLabel, int frame,
            NodeDebugInfo[] nodes, string[] activeNodeIds,
            IReadOnlyDictionary<string, string> sourceTree, IReadOnlyDictionary<string, string> sourceNode,
            ObservationBlackboard? blackboard)
        {
            InstanceId = instanceId;
            Sequence = sequence;
            TreeId = treeId;
            DisplayName = displayName;
            OwnerLabel = ownerLabel;
            Frame = frame;
            _nodes = nodes;
            _activeNodeIds = activeNodeIds;
            _sourceTree = sourceTree;
            _sourceNode = sourceNode;
            Blackboard = blackboard;
            _activeNodeSet = activeNodeIds.Length > 16
                ? new HashSet<string>(activeNodeIds, StringComparer.Ordinal)
                : null;

            _nodeIndex = new Dictionary<string, int>(nodes.Length, StringComparer.Ordinal);
            for (var i = 0; i < nodes.Length; i++) _nodeIndex[nodes[i].NodeId] = i;
        }

        internal static ObservationSnapshot CreateForReplay(
            long instanceId,
            long sequence,
            string treeId,
            string displayName,
            string ownerLabel,
            int frame,
            NodeDebugInfo[] nodes,
            string[] activeNodeIds,
            IReadOnlyDictionary<string, string>? sourceTree,
            IReadOnlyDictionary<string, string>? sourceNode,
            ObservationBlackboard? blackboard)
        {
            return new ObservationSnapshot(
                instanceId,
                sequence,
                treeId ?? "",
                displayName ?? "",
                ownerLabel ?? "",
                frame,
                nodes ?? Array.Empty<NodeDebugInfo>(),
                activeNodeIds ?? Array.Empty<string>(),
                sourceTree ?? EmptySourceMap,
                sourceNode ?? EmptySourceMap,
                blackboard);
        }

        public static ObservationSnapshot Capture(long instanceId, long sequence, TreeDebugView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            var raw = view.GetNodeStates() ?? new List<NodeDebugInfo>();
            var nodes = new NodeDebugInfo[raw.Count];
            for (var i = 0; i < raw.Count; i++) nodes[i] = raw[i];

            var active = new List<string>(nodes.Length);
            foreach (var node in nodes)
            {
                if (node.OnStackCount > 0) active.Add(node.NodeId);
            }

            var blackboard = view.GetBlackboard();
            var immutableBlackboard = blackboard == null ? null : ObservationBlackboard.Copy(blackboard);

            return new ObservationSnapshot(
                instanceId, sequence,
                view.TreeId ?? "", view.DisplayName ?? "", view.OwnerLabel ?? "", view.LastFrame,
                nodes, active.ToArray(),
                CopyMap(view.NodeSourceTree), CopyMap(view.NodeSourceNode),
                immutableBlackboard);
        }

        private static IReadOnlyDictionary<string, string> CopyMap(IReadOnlyDictionary<string, string>? source)
        {
            if (source == null || source.Count == 0) return EmptySourceMap;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source) result[pair.Key] = pair.Value;
            return result;
        }

        public bool TryGetNode(string nodeId, out NodeDebugInfo info)
        {
            if (_nodeIndex.TryGetValue(nodeId, out var index))
            {
                info = _nodes[index];
                return true;
            }
            info = null!;
            return false;
        }

        public NodeState StateOf(string nodeId) =>
            TryGetNode(nodeId, out var info) ? info.State : NodeState.Inactive;

        public bool IsActive(string nodeId) =>
            _activeNodeSet != null
                ? _activeNodeSet.Contains(nodeId)
                : Array.IndexOf(_activeNodeIds, nodeId) >= 0;
    }
}

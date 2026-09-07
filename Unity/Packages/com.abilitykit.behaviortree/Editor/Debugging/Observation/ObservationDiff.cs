#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree;

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
    /// <summary>单个节点的状态转换（A → B 比较结果）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtNodeStateChange")]
    public readonly struct NodeStateChange
    {
        public string NodeId { get; }
        public NodeState From { get; }
        public NodeState To { get; }

        public NodeStateChange(string nodeId, NodeState from, NodeState to)
        {
            NodeId = nodeId ?? "";
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// 两次采样之间的结构化差异（A/B diff）。不可变，用于：
    /// 采样追加时的事件、历史帧导航后的比较、以及"跳到发生变化的节点/key"。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationDiff")]
    public sealed class ObservationDiff
    {
        private static readonly ObservationDiff EmptyInstance = new ObservationDiff(
            Array.Empty<NodeStateChange>(),
            Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>());

        private readonly NodeStateChange[] _nodeChanges;
        private readonly string[] _addedNodes;
        private readonly string[] _removedNodes;
        private readonly string[] _changedKeys;
        private readonly string[] _addedKeys;
        private readonly string[] _removedKeys;
        private readonly string[] _changedNodeIds;
        private readonly string[] _changedKeyIds;

        /// <summary>状态发生变化的节点（两侧均存在）。</summary>
        public IReadOnlyList<NodeStateChange> NodeChanges => _nodeChanges;
        public IReadOnlyList<string> AddedNodes => _addedNodes;
        public IReadOnlyList<string> RemovedNodes => _removedNodes;
        /// <summary>值发生变化的黑板 key（两侧均存在）。</summary>
        public IReadOnlyList<string> ChangedBlackboardKeys => _changedKeys;
        public IReadOnlyList<string> AddedBlackboardKeys => _addedKeys;
        public IReadOnlyList<string> RemovedBlackboardKeys => _removedKeys;

        /// <summary>所有变化的节点 id（状态变化 + 新增 + 移除，去重）。</summary>
        public IReadOnlyList<string> ChangedNodeIds => _changedNodeIds;
        /// <summary>所有变化的黑板 key（值变化 + 新增 + 移除，去重）。</summary>
        public IReadOnlyList<string> ChangedBlackboardKeyIds => _changedKeyIds;

        public bool HasChanges =>
            _nodeChanges.Length > 0 || _addedNodes.Length > 0 || _removedNodes.Length > 0
            || _changedKeys.Length > 0 || _addedKeys.Length > 0 || _removedKeys.Length > 0;

        private ObservationDiff(
            NodeStateChange[] nodeChanges,
            string[] addedNodes, string[] removedNodes,
            string[] changedKeys, string[] addedKeys, string[] removedKeys,
            string[] changedNodeIds, string[] changedKeyIds)
        {
            _nodeChanges = nodeChanges;
            _addedNodes = addedNodes;
            _removedNodes = removedNodes;
            _changedKeys = changedKeys;
            _addedKeys = addedKeys;
            _removedKeys = removedKeys;
            _changedNodeIds = changedNodeIds;
            _changedKeyIds = changedKeyIds;
        }

        public static ObservationDiff Empty => EmptyInstance;

        public bool ContainsChangedNode(string nodeId) => Array.IndexOf(_changedNodeIds, nodeId) >= 0;

        public bool ContainsChangedBlackboardKey(string key) => Array.IndexOf(_changedKeyIds, key) >= 0;

        public static ObservationDiff Compare(ObservationSnapshot? previous, ObservationSnapshot current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (previous == null) return EmptyInstance;

            var nodeChanges = new List<NodeStateChange>();
            var addedNodes = new List<string>();
            var removedNodes = new List<string>();
            var changedKeys = new List<string>();
            var addedKeys = new List<string>();
            var removedKeys = new List<string>();
            var changedNodeIds = new List<string>();
            var seenNodes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in current.Nodes)
            {
                if (!previous.TryGetNode(node.NodeId, out var previousInfo))
                {
                    addedNodes.Add(node.NodeId);
                    continue;
                }
                if (previousInfo.State != node.State
                     || previousInfo.OnStackCount != node.OnStackCount
                    || previousInfo.RunningChildIndex != node.RunningChildIndex
                    || !string.Equals(previousInfo.SourceTreeId, node.SourceTreeId, StringComparison.Ordinal))
                {
                    if (previousInfo.State != node.State)
                        nodeChanges.Add(new NodeStateChange(node.NodeId, previousInfo.State, node.State));
                    AddUnique(changedNodeIds, seenNodes, node.NodeId);
                }
            }
            foreach (var previousNode in previous.Nodes)
            {
                if (!current.TryGetNode(previousNode.NodeId, out _))
                {
                    removedNodes.Add(previousNode.NodeId);
                }
            }

            var currentBb = current.Blackboard;
            var previousBb = previous.Blackboard;
            if (currentBb != null && previousBb != null)
            {
                for (var i = 0; i < currentBb.Count; i++)
                {
                    var key = currentBb.KeyName(i);
                    var previousIndex = previousBb.IndexOf(key);
                    if (previousIndex < 0)
                    {
                        addedKeys.Add(key);
                        continue;
                    }
                    if (!string.Equals(previousBb.GetDisplayValue(previousIndex), currentBb.GetDisplayValue(i), StringComparison.Ordinal))
                    {
                        changedKeys.Add(key);
                    }
                }
                for (var i = 0; i < previousBb.Count; i++)
                {
                    var key = previousBb.KeyName(i);
                    if (currentBb.IndexOf(key) < 0) removedKeys.Add(key);
                }
            }
            else if (currentBb != null && previousBb == null)
            {
                for (var i = 0; i < currentBb.Count; i++) addedKeys.Add(currentBb.KeyName(i));
            }
            else if (currentBb == null && previousBb != null)
            {
                for (var i = 0; i < previousBb.Count; i++) removedKeys.Add(previousBb.KeyName(i));
            }

            foreach (var id in addedNodes) AddUnique(changedNodeIds, seenNodes, id);
            foreach (var id in removedNodes) AddUnique(changedNodeIds, seenNodes, id);

            var changedKeyIds = new List<string>(changedKeys.Count + addedKeys.Count + removedKeys.Count);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in changedKeys) AddUnique(changedKeyIds, seenKeys, key);
            foreach (var key in addedKeys) AddUnique(changedKeyIds, seenKeys, key);
            foreach (var key in removedKeys) AddUnique(changedKeyIds, seenKeys, key);

            return new ObservationDiff(
                nodeChanges.ToArray(), addedNodes.ToArray(), removedNodes.ToArray(),
                changedKeys.ToArray(), addedKeys.ToArray(), removedKeys.ToArray(),
                changedNodeIds.ToArray(), changedKeyIds.ToArray());
        }

        private static void AddUnique(List<string> target, HashSet<string> seen, string value)
        {
            if (seen.Add(value)) target.Add(value);
        }
    }
}

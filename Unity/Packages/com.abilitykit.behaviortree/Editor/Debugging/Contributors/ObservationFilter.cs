#nullable enable

using System;
using AbilityKit.BehaviorTree;

using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
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
namespace AbilityKit.BehaviorTree.Editor.Debugging.Contributors
{
    /// <summary>
    /// 过滤候选上下文：按待过滤对象携带不同的非空字段。
    /// 实例用 <see cref="Entry"/>；节点用 <see cref="Node"/>；黑板 key 用 <see cref="BlackboardKey"/>；
    /// 事件用 <see cref="Change"/>；<see cref="Diff"/> 供 changed-only 类过滤使用。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationFilterContext")]
    public sealed class ObservationFilterContext
    {
        public DebugRegistryEntry? Entry { get; }
        public ObservationSnapshot? Snapshot { get; }
        public NodeDebugInfo? Node { get; }
        public string? BlackboardKey { get; }
        public string? BlackboardDisplayValue { get; }
        public ObservationChange? Change { get; }
        public ObservationDiff? Diff { get; }

        public ObservationFilterContext(
            DebugRegistryEntry? entry = null,
            ObservationSnapshot? snapshot = null,
            NodeDebugInfo? node = null,
            string? blackboardKey = null,
            string? blackboardDisplayValue = null,
            ObservationChange? change = null,
            ObservationDiff? diff = null)
        {
            Entry = entry;
            Snapshot = snapshot;
            Node = node;
            BlackboardKey = blackboardKey;
            BlackboardDisplayValue = blackboardDisplayValue;
            Change = change;
            Diff = diff;
        }

        public static ObservationFilterContext ForInstance(DebugRegistryEntry entry) =>
            new ObservationFilterContext(entry: entry);

        public static ObservationFilterContext ForNode(ObservationSnapshot snapshot, NodeDebugInfo node, ObservationDiff? diff) =>
            new ObservationFilterContext(snapshot: snapshot, node: node, diff: diff);

        public static ObservationFilterContext ForBlackboardKey(ObservationSnapshot snapshot, string key, string displayValue, ObservationDiff? diff) =>
            new ObservationFilterContext(snapshot: snapshot, blackboardKey: key, blackboardDisplayValue: displayValue, diff: diff);

        public static ObservationFilterContext ForChange(ObservationChange change) =>
            new ObservationFilterContext(change: change);
    }

    /// <summary>
    /// 实例/节点/黑板/事件的可组合过滤。谓词纯函数，可经 <see cref="ObservationFilters"/> 组合。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtObservationFilter")]
    public interface IObservationFilter
    {
        string Id { get; }
        string DisplayName { get; }
        bool Matches(ObservationFilterContext context);
    }

    /// <summary>可选的范围声明；未实现该接口的兼容过滤器仍适用于全部范围。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtScopedObservationFilter")]
    public interface IScopedObservationFilter : IObservationFilter
    {
        bool AppliesTo(ObservationFilterScope scope);
    }

    /// <summary>基于谓词的过滤实现（也是内置过滤器与组合子的载体）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationFilter")]
    public sealed class ObservationFilter : IScopedObservationFilter
    {
        private readonly Func<ObservationFilterContext, bool> _predicate;
        private readonly ObservationFilterScope[] _scopes;

        public string Id { get; }
        public string DisplayName { get; }

        public ObservationFilter(
            string id,
            string displayName,
            Func<ObservationFilterContext, bool> predicate,
            params ObservationFilterScope[] scopes)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? id;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _scopes = scopes ?? Array.Empty<ObservationFilterScope>();
        }

        public bool AppliesTo(ObservationFilterScope scope)
        {
            if (_scopes.Length == 0) return true;
            for (var index = 0; index < _scopes.Length; index++)
            {
                if (_scopes[index] == scope) return true;
            }
            return false;
        }

        public bool Matches(ObservationFilterContext context) => _predicate(context);
    }

    /// <summary>过滤上下文的目标范围，用于避免节点过滤器误伤实例、黑板或事件列表。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationFilterScope")]
    public enum ObservationFilterScope
    {
        Instance = 0,
        Node = 1,
        Blackboard = 2,
        Change = 3,
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationFilterContextExtensions")]
    public static class ObservationFilterContextExtensions
    {
        public static ObservationFilterScope Scope(this ObservationFilterContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Entry != null) return ObservationFilterScope.Instance;
            if (context.Node != null) return ObservationFilterScope.Node;
            if (context.BlackboardKey != null) return ObservationFilterScope.Blackboard;
            return ObservationFilterScope.Change;
        }
    }

    /// <summary>内置过滤器与组合子的工厂。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationFilters")]
    public static class ObservationFilters
    {
        /// <summary>文本过滤：匹配实例标签/树 id，节点 id/名称/类型，黑板 key，事件目标。</summary>
        public static IObservationFilter Text(string query, string id = "builtin.text")
        {
            var q = query ?? "";
            return new ObservationFilter(id, "Text", ctx =>
            {
                if (q.Length == 0) return true;
                if (ctx.Entry != null)
                {
                    var view = ctx.Entry.View;
                    if (view != null
                        && (Contains(ctx.Entry.Id.ToString(), q)
                            || Contains(view.TreeId, q)
                            || Contains(view.DisplayName, q)
                            || Contains(view.OwnerLabel, q)))
                    {
                        return true;
                    }
                }
                if (ctx.Node != null)
                {
                    if (Contains(ctx.Node.NodeId, q) || Contains(ctx.Node.Name, q) || Contains(ctx.Node.TypeId, q))
                        return true;
                }
                if (ctx.BlackboardKey != null && Contains(ctx.BlackboardKey, q)) return true;
                if (ctx.Change != null && Contains(ctx.Change.Target, q)) return true;
                return false;
            });
        }

        /// <summary>仅运行路径：匹配 OnStackCount &gt; 0 的节点。</summary>
        public static IObservationFilter RunningPath(string id = "builtin.running-path") =>
            new ObservationFilter(
                id,
                "Running Path",
                ctx => ctx.Node != null && ctx.Node.OnStackCount > 0,
                ObservationFilterScope.Node);

        /// <summary>状态过滤：匹配指定状态的节点。</summary>
        public static IObservationFilter NodeState(NodeState state, string id = "builtin.node-state") =>
            new ObservationFilter(
                id,
                "State " + state,
                ctx => ctx.Node != null && ctx.Node.State == state,
                ObservationFilterScope.Node);

        /// <summary>仅变化项：匹配出现在 <see cref="ObservationDiff"/> 中的节点/key；无差异信息时不匹配。</summary>
        public static IObservationFilter ChangedOnly(string id = "builtin.changed-only") =>
            new ObservationFilter(id, "Changed Only", ctx =>
            {
                if (ctx.Diff == null) return false;
                if (ctx.Node != null) return ctx.Diff.ContainsChangedNode(ctx.Node.NodeId);
                if (ctx.BlackboardKey != null) return ctx.Diff.ContainsChangedBlackboardKey(ctx.BlackboardKey);
                return ctx.Diff.HasChanges;
            }, ObservationFilterScope.Node, ObservationFilterScope.Blackboard);

        public static IObservationFilter And(IObservationFilter a, IObservationFilter b, string id = "composite.and") =>
            new ObservationFilter(id, "And", ctx => a.Matches(ctx) && b.Matches(ctx));

        public static IObservationFilter Or(IObservationFilter a, IObservationFilter b, string id = "composite.or") =>
            new ObservationFilter(id, "Or", ctx => a.Matches(ctx) || b.Matches(ctx));

        public static IObservationFilter Not(IObservationFilter a, string id = "composite.not") =>
            new ObservationFilter(id, "Not", ctx => !a.Matches(ctx));

        private static bool Contains(string value, string query) =>
            value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

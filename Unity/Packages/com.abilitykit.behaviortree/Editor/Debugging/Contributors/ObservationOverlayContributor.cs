#nullable enable

using System;
using System.Collections.Generic;
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
    /// <summary>图 overlay 的呈现形态（投影层据此绘制，不改 GraphView 语义）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationOverlayKind")]
    public enum ObservationOverlayKind
    {
        Badge = 0,
        Tooltip = 1,
        Border = 2,
        Marker = 3,
    }

    /// <summary>一条图 overlay：节点 id + 形态 + 文本 + 节点内排序权重。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationOverlay")]
    public sealed class ObservationOverlay
    {
        public string NodeId { get; }
        public ObservationOverlayKind Kind { get; }
        public string Text { get; }
        public int Priority { get; }

        public ObservationOverlay(string nodeId, ObservationOverlayKind kind, string text, int priority = 0)
        {
            NodeId = nodeId ?? "";
            Kind = kind;
            Text = text ?? "";
            Priority = priority;
        }
    }

    /// <summary>overlay 贡献上下文：实例 + 采样 + 目标节点。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationOverlayContext")]
    public sealed class ObservationOverlayContext
    {
        public long InstanceId { get; }
        public ObservationSnapshot? Snapshot { get; }
        public NodeDebugInfo Node { get; }

        public ObservationOverlayContext(long instanceId, ObservationSnapshot? snapshot, NodeDebugInfo node)
        {
            InstanceId = instanceId;
            Snapshot = snapshot;
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }
    }

    /// <summary>在不改 GraphView 语义的前提下贡献 badge/tooltip/border/marker overlay。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtObservationOverlayContributor")]
    public interface IObservationOverlayContributor
    {
        string Id { get; }
        int Priority { get; }
        IReadOnlyList<ObservationOverlay> GetOverlays(ObservationOverlayContext context);
    }

    /// <summary>内置 overlay 贡献者：按节点状态产生 Running/Success/Failure/Inactive 标记。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationOverlayContributors")]
    public static class ObservationOverlayContributors
    {
        public static IObservationOverlayContributor NodeState() => new NodeStateOverlayContributor();

        private sealed class NodeStateOverlayContributor : IObservationOverlayContributor
        {
            public string Id => "builtin.node-state";
            public int Priority => 0;

            public IReadOnlyList<ObservationOverlay> GetOverlays(ObservationOverlayContext context)
            {
                var kind = context.Node.State switch
                {
                    AbilityKit.BehaviorTree.Definition.NodeState.Running => ObservationOverlayKind.Border,
                    AbilityKit.BehaviorTree.Definition.NodeState.Success => ObservationOverlayKind.Badge,
                    AbilityKit.BehaviorTree.Definition.NodeState.Failure => ObservationOverlayKind.Badge,
                    _ => ObservationOverlayKind.Marker,
                };
                return new[] { new ObservationOverlay(context.Node.NodeId, kind, context.Node.State.ToString(), (int)context.Node.State) };
            }
        }
    }
}

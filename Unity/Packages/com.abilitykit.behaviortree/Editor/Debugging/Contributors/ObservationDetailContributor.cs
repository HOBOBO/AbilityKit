#nullable enable

using System;
using System.Collections.Generic;

using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Contributors
{
    /// <summary>详情面板的一行只读键值。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationDetailRow")]
    public sealed class ObservationDetailRow
    {
        public string Label { get; }
        public string Value { get; }

        public ObservationDetailRow(string label, string value)
        {
            Label = label ?? "";
            Value = value ?? "";
        }
    }

    /// <summary>详情面板的一个只读 section（标题 + 行）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationDetailSection")]
    public sealed class ObservationDetailSection
    {
        private readonly List<ObservationDetailRow> _rows;

        public string Title { get; }
        public IReadOnlyList<ObservationDetailRow> Rows => _rows;

        public ObservationDetailSection(string title, IEnumerable<ObservationDetailRow> rows)
        {
            Title = title ?? "";
            _rows = new List<ObservationDetailRow>(rows ?? Array.Empty<ObservationDetailRow>());
        }
    }

    /// <summary>详情贡献的只读上下文：选中实例 + 可选聚焦节点。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationDetailContext")]
    public sealed class ObservationDetailContext
    {
        public long InstanceId { get; }
        public ObservationSnapshot? Snapshot { get; }
        /// <summary>聚焦节点 id；null 表示实例级详情。</summary>
        public string? NodeId { get; }

        public ObservationDetailContext(long instanceId, ObservationSnapshot? snapshot, string? nodeId)
        {
            InstanceId = instanceId;
            Snapshot = snapshot;
            NodeId = nodeId;
        }
    }

    /// <summary>
    /// 为选中实例或节点贡献只读详情 section。贡献者只能产出展示数据，
    /// 不得返回任何运行时 mutation 动作；异常由注册中心隔离为 diagnostics。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtObservationDetailContributor")]
    public interface IObservationDetailContributor
    {
        /// <summary>全局唯一贡献者 id。</summary>
        string Id { get; }
        /// <summary>排序权重，数值越小越靠前。</summary>
        int Priority { get; }
        IReadOnlyList<ObservationDetailSection> GetSections(ObservationDetailContext context);
    }
}

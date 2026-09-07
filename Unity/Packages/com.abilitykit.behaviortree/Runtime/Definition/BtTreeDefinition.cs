using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.BehaviorTree.Definition;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 运行IR 树定义：导出格式的数据权威。不含布局/分组等编辑态数据，
    /// 不含 CLR 类型名。快照恢复前<see cref="ComputeDefinitionHash"/> 校验兼容性    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtTreeDefinition
    {
        public const int CurrentFormatVersion = 1;

        public string TreeId { get; set; } = "";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string RootNodeId { get; set; } = "";
        public List<BtNodeDefinition> Nodes { get; set; } = new();
        public BtBlackboardSchema Blackboard { get; set; } = new();

        /// <summary>转发到 canonical 实现的结构哈希，行为与 <see cref="TreeDefinition.ComputeDefinitionHash"/> 等价。</summary>
        public long ComputeDefinitionHash() => TreeDefinition.FromLegacy(this).ComputeDefinitionHash();

        /// <summary>转发到 canonical 实现的完整副本。</summary>
        public BtTreeDefinition DeepClone() => TreeDefinition.FromLegacy(this).DeepClone().ToLegacy();
    }
}

using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>
    /// 授权源文档（编辑态权威）：结构直接复用运行时 IR <see cref="BtTreeDefinition"/>，
    /// 布局（节点坐标/分组）以平行列表存放，导出时天然剥离——编辑配置与导出配置分离
    /// 由数据模型本身保证，而非导出步骤临时过滤。
    /// </summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.AuthoringSourceDocument.", false)]
    public sealed class BtAuthoringSourceDocument
    {
        public string Schema { get; set; } = BtAuthoringSchema.Id;
        public string Version { get; set; } = BtAuthoringSchema.Version;
        public BtAuthoringMetadata Metadata { get; set; } = new();
        public BtTreeDefinition Tree { get; set; } = new();
        public List<BtAuthoringNodeMetadata> NodeMetadata { get; set; } = new();
        public List<BtNodeLayoutData> Layout { get; set; } = new();
        public List<BtAuthoringGroupData> Groups { get; set; } = new();
        public List<BtAuthoringNoteData> Notes { get; set; } = new();

        public BtAuthoringNodeMetadata GetOrCreateNodeMetadata(string nodeId)
        {
            foreach (var metadata in NodeMetadata)
            {
                if (string.Equals(metadata.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    return metadata;
                }
            }

            var created = new BtAuthoringNodeMetadata { NodeId = nodeId ?? "" };
            NodeMetadata.Add(created);
            return created;
        }

        public bool TryGetNodeMetadata(string nodeId, out BtAuthoringNodeMetadata metadata)
        {
            foreach (var candidate in NodeMetadata)
            {
                if (string.Equals(candidate.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    metadata = candidate;
                    return true;
                }
            }

            metadata = null!;
            return false;
        }
    }
}

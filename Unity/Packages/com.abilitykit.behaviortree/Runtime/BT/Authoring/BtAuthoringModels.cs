using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>授权源文档 schema 标识与版本。</summary>
    public static class BtAuthoringSchema
    {
        public const string Id = "abilitykit-bt-authoring";
        public const string Version = "1.0";
    }

    /// <summary>
    /// 授权源文档（编辑态权威）：结构直接复用运行时 IR <see cref="BtTreeDefinition"/>，
    /// 布局（节点坐标/分组）以平行列表存放，导出时天然剥离——编辑配置与导出配置分离
    /// 由数据模型本身保证，而非导出步骤临时过滤。
    /// </summary>
    public sealed class BtAuthoringSourceDocument
    {
        public string Schema { get; set; } = BtAuthoringSchema.Id;
        public string Version { get; set; } = BtAuthoringSchema.Version;
        public BtAuthoringMetadata Metadata { get; set; } = new();
        public BtTreeDefinition Tree { get; set; } = new();
        public List<BtNodeLayoutData> Layout { get; set; } = new();
        public List<BtAuthoringGroupData> Groups { get; set; } = new();
    }

    public sealed class BtAuthoringMetadata
    {
        public string Author { get; set; } = "team";
        public string Description { get; set; } = "";
    }

    /// <summary>节点画布坐标（编辑态，不进运行时 IR）。</summary>
    public sealed class BtNodeLayoutData
    {
        public string NodeId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>编辑态分组框（视觉分组，不进运行时 IR）。</summary>
    public sealed class BtAuthoringGroupData
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public List<string> NodeIds { get; set; } = new();
    }
}

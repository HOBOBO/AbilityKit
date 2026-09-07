namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>节点显示信息（编辑态，不进运行时 IR）。</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.AuthoringNodeMetadata.", false)]
    public sealed class BtAuthoringNodeMetadata
    {
        public string NodeId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Comment { get; set; } = "";
    }
}

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>节点画布坐标（编辑态，不进运行时 IR）。</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.NodeLayoutData.", false)]
    public sealed class BtNodeLayoutData
    {
        public string NodeId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }
}

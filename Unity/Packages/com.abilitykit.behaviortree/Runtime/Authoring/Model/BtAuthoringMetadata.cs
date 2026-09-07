namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>授权源文档的作者与用途说明。</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.AuthoringMetadata.", false)]
    public sealed class BtAuthoringMetadata
    {
        public string Author { get; set; } = "team";
        public string Description { get; set; } = "";
    }
}

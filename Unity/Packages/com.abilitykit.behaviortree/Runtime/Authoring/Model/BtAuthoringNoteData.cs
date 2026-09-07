namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>画布注释（编辑态说明，不参与行为树结构和运行时导出）。</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.AuthoringNoteData.", false)]
    public sealed class BtAuthoringNoteData
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 240f;
        public float Height { get; set; } = 140f;
    }
}

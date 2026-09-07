using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>编辑态分组框（视觉分组，不进运行时 IR）。</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.AuthoringGroupData.", false)]
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

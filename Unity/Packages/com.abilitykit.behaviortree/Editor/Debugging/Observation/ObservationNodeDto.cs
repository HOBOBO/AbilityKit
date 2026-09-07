#nullable enable

using System;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationNodeDto")]
    public sealed class ObservationNodeDto
    {
        public string NodeId = "";
        public string Name = "";
        public string TypeId = "";
        public int Kind;
        public int State;
        public int Depth;
        public int OnStackCount;
        public int RunningChildIndex;
        public string SourceTreeId = "";
        public bool HasSourceTreeId;
    }
}

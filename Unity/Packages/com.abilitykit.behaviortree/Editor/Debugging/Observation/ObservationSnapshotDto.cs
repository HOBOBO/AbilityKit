#nullable enable

using System;
using System.Collections.Generic;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationSnapshotDto")]
    public sealed class ObservationSnapshotDto
    {
        public long InstanceId;
        public long Sequence;
        public string TreeId = "";
        public string DisplayName = "";
        public string OwnerLabel = "";
        public int Frame;
        public List<ObservationNodeDto> Nodes = new();
        public List<string> ActiveNodeIds = new();
        public List<ObservationStringMapEntryDto> SourceTree = new();
        public List<ObservationStringMapEntryDto> SourceNode = new();
        public ObservationBlackboardDto Blackboard = new();
        public bool HasBlackboard;
    }
}

#nullable enable

using System;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationStringMapEntryDto")]
    public sealed class ObservationStringMapEntryDto
    {
        public string Key = "";
        public string Value = "";
    }
}

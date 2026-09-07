#nullable enable

using System;
using System.Collections.Generic;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationBlackboardDto")]
    public sealed class ObservationBlackboardDto
    {
        public List<string> KeyNames = new();
        public List<int> KeyTypes = new();
        public List<bool> BoolValues = new();
        public List<long> Int64Values = new();
        public List<long> Fixed64RawValues = new();
        public List<string> StringValues = new();
    }
}

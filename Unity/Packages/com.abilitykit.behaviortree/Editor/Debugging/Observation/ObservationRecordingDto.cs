#nullable enable

using System;
using System.Collections.Generic;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationRecordingDto")]
    public sealed class ObservationRecordingDto
    {
        public int FormatVersion = ObservationRecording.FormatVersion;
        public string CreatedUtc = "";
        public ObservationRecordingSettingsDto Settings = new();
        public List<ObservationSnapshotDto> Samples = new();
    }
}

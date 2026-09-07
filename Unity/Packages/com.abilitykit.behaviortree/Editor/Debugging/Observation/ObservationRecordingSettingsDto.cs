#nullable enable

using System;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationRecordingSettingsDto")]
    public sealed class ObservationRecordingSettingsDto
    {
        public double SampleIntervalSeconds = ObservationSettings.DefaultSampleIntervalSeconds;
        public int TimelineCapacity = ObservationSettings.DefaultTimelineCapacity;
    }
}

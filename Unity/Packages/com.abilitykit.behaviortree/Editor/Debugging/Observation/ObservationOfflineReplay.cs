#nullable enable

using System;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationOfflineReplay")]
    public sealed class ObservationOfflineReplay
    {
        private double _playAccumulator;
        private double _playbackSpeed = 1d;

        public ObservationTimeline Timeline { get; }
        public int CurrentIndex { get; private set; }
        public bool IsPlaying { get; private set; }
        public int CompareIndexA { get; private set; } = -1;
        public int CompareIndexB { get; private set; } = -1;
        public int Count => Timeline.Count;
        public ObservationSnapshot? Current => Timeline.SampleAt(CurrentIndex);
        public ObservationSnapshot? Previous => Timeline.SampleAt(CurrentIndex - 1);
        public ObservationDiff? CurrentDiff => Timeline.DiffAt(CurrentIndex);
        public ObservationDiff? CompareDiff =>
            CompareIndexA >= 0 && CompareIndexB >= 0
                ? Timeline.Compare(CompareIndexA, CompareIndexB)
                : null;
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Math.Max(0.1d, Math.Min(16d, value));
        }

        public ObservationOfflineReplay(ObservationTimeline timeline)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            CurrentIndex = timeline.Count > 0 ? timeline.Count - 1 : 0;
        }

        public static ObservationOfflineReplay FromJson(string json) =>
            ObservationRecording.ReplayFromJson(json);

        public bool Seek(int index)
        {
            if (index < 0 || index >= Timeline.Count) return false;
            CurrentIndex = index;
            return true;
        }

        public bool StepPrevious() => Seek(CurrentIndex - 1);

        public bool StepNext() => Seek(CurrentIndex + 1);

        public bool SeekNormalized(float normalized)
        {
            if (Timeline.Count == 0) return false;
            var clamped = Math.Max(0f, Math.Min(1f, normalized));
            return Seek((int)Math.Round(clamped * (Timeline.Count - 1)));
        }

        public void Play()
        {
            if (Timeline.Count <= 1) return;
            IsPlaying = true;
        }

        public void Pause() => IsPlaying = false;

        public void TogglePlayback()
        {
            if (IsPlaying) Pause();
            else Play();
        }

        public void Tick(double deltaSeconds)
        {
            if (!IsPlaying || Timeline.Count <= 1) return;
            _playAccumulator += Math.Max(0d, deltaSeconds) * PlaybackSpeed;
            while (_playAccumulator >= 1d && IsPlaying)
            {
                _playAccumulator -= 1d;
                if (!StepNext())
                {
                    Pause();
                    JumpToLatest();
                }
            }
        }

        public void MarkCompareA() => CompareIndexA = CurrentIndex;

        public void MarkCompareB() => CompareIndexB = CurrentIndex;

        public void ClearCompare()
        {
            CompareIndexA = -1;
            CompareIndexB = -1;
        }

        public void JumpToLatest()
        {
            CurrentIndex = Timeline.Count > 0 ? Timeline.Count - 1 : 0;
        }
    }
}

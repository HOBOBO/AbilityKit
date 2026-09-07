#nullable enable

using System;
using System.Collections.Generic;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>结构化事件种类。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationChangeKind")]
    public enum ObservationChangeKind
    {
        NodeState = 0,
        BlackboardValue = 1,
    }

    /// <summary>一条扁平化的结构化事件（节点状态转换或黑板值变化）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationChange")]
    public sealed class ObservationChange
    {
        public int Frame { get; }
        public long Sequence { get; }
        public ObservationChangeKind Kind { get; }
        public string Target { get; }
        public string From { get; }
        public string To { get; }

        public ObservationChange(int frame, long sequence, ObservationChangeKind kind, string target, string from, string to)
        {
            Frame = frame;
            Sequence = sequence;
            Kind = kind;
            Target = target ?? "";
            From = from ?? "";
            To = to ?? "";
        }
    }

    /// <summary>
    /// 有界结构化采样/事件时间线：按追加顺序保存采样与逐帧差异，支持历史帧导航、
    /// 任意两样本 A/B 比较与扁平化事件枚举。取代旧的字符串事件列表。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationTimeline")]
    public sealed class ObservationTimeline
    {
        private int _sampleLimit;
        private readonly List<ObservationSnapshot> _samples = new();
        private readonly List<ObservationDiff> _diffs = new();

        public int SampleLimit => _sampleLimit;
        public int Count => _samples.Count;
        public IReadOnlyList<ObservationSnapshot> Samples => _samples;
        /// <summary>与 <see cref="Samples"/> 平行：diffs[i] 是 samples[i-1] → samples[i] 的差异（首个为空）。</summary>
        public IReadOnlyList<ObservationDiff> Diffs => _diffs;
        public ObservationSnapshot? Latest => Count > 0 ? _samples[Count - 1] : null;
        public ObservationDiff? LatestDiff => Count > 0 ? _diffs[Count - 1] : null;

        public ObservationTimeline(int sampleLimit = ObservationSettings.DefaultTimelineCapacity)
        {
            _sampleLimit = ObservationSettings.ClampTimelineCapacity(sampleLimit);
        }

        public void SetSampleLimit(int sampleLimit)
        {
            _sampleLimit = ObservationSettings.ClampTimelineCapacity(sampleLimit);
            TrimToLimit();
        }

        /// <summary>追加采样并计算相对前一采样的差异；超限时丢弃最旧样本。返回本次差异。</summary>
        public ObservationDiff Append(ObservationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var diff = ObservationDiff.Compare(Latest, snapshot);
            _samples.Add(snapshot);
            _diffs.Add(diff);
            TrimToLimit();
            return diff;
        }

        public ObservationSnapshot? SampleAt(int index) =>
            index >= 0 && index < _samples.Count ? _samples[index] : null;

        public ObservationDiff? DiffAt(int index) =>
            index >= 0 && index < _diffs.Count ? _diffs[index] : null;

        /// <summary>历史帧导航：返回 frame 不晚于给定帧的最近一次采样；无匹配时返回最早样本。</summary>
        public ObservationSnapshot? FindFrame(int frame)
        {
            for (var i = _samples.Count - 1; i >= 0; i--)
            {
                if (_samples[i].Frame <= frame) return _samples[i];
            }
            return Count > 0 ? _samples[0] : null;
        }

        /// <summary>两个历史样本的 A/B 比较（indexA 为基准，indexB 为对比）。</summary>
        public ObservationDiff Compare(int indexA, int indexB)
        {
            var b = SampleAt(indexB);
            if (b == null) return ObservationDiff.Empty;
            return ObservationDiff.Compare(SampleAt(indexA), b);
        }

        /// <summary>按时间顺序枚举全部结构化事件（节点状态转换 + 黑板值变化）。</summary>
        public IEnumerable<ObservationChange> EnumerateChanges()
        {
            for (var i = 0; i < _diffs.Count; i++)
            {
                var sample = _samples[i];
                var previous = i > 0 ? _samples[i - 1] : null;
                var diff = _diffs[i];

                foreach (var change in diff.NodeChanges)
                {
                    yield return new ObservationChange(
                        sample.Frame, sample.Sequence, ObservationChangeKind.NodeState,
                        change.NodeId, change.From.ToString(), change.To.ToString());
                }

                foreach (var key in diff.ChangedBlackboardKeys)
                {
                    var from = previous?.Blackboard == null
                        ? ""
                        : previous.Blackboard.GetDisplayValue(key);
                    var to = sample.Blackboard == null ? "" : sample.Blackboard.GetDisplayValue(key);
                    yield return new ObservationChange(
                        sample.Frame, sample.Sequence, ObservationChangeKind.BlackboardValue,
                        key, from, to);
                }
            }
        }

        public void Clear()
        {
            _samples.Clear();
            _diffs.Clear();
        }

        private void TrimToLimit()
        {
            var overflow = _samples.Count - _sampleLimit;
            if (overflow <= 0) return;
            _samples.RemoveRange(0, overflow);
            _diffs.RemoveRange(0, overflow);
        }
    }
}

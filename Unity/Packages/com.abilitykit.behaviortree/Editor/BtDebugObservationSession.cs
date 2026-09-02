#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>一次调试采样产生的状态变化记录。</summary>
    public readonly struct BtDebugObservationEvent
    {
        public int Frame { get; }
        public string Source { get; }
        public string Change { get; }

        public BtDebugObservationEvent(int frame, string source, string change)
        {
            Frame = frame;
            Source = source ?? "";
            Change = change ?? "";
        }
    }

    /// <summary>
    /// 调试观察会话：负责采样、前后帧差异和有界历史，不依赖具体窗口或绘制技术。
    /// </summary>
    public sealed class BtDebugObservationSession
    {
        private readonly int _historyLimit;
        private readonly List<BtNodeDebugInfo> _nodes = new();
        private readonly Dictionary<string, BtNodeState> _lastNodeStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastBlackboardDisplay = new(StringComparer.Ordinal);
        private readonly HashSet<string> _changedBlackboardKeys = new(StringComparer.Ordinal);
        private readonly List<BtDebugObservationEvent> _events = new();

        public IReadOnlyList<BtNodeDebugInfo> Nodes => _nodes;
        public BtBlackboardValueSnapshot? Blackboard { get; private set; }
        public IReadOnlyCollection<string> ChangedBlackboardKeys => _changedBlackboardKeys;
        public IReadOnlyList<BtDebugObservationEvent> Events => _events;
        public int LastFrame { get; private set; } = -1;
        public bool HasSample => LastFrame >= 0;

        public BtDebugObservationSession(int historyLimit = 200)
        {
            if (historyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(historyLimit));
            _historyLimit = historyLimit;
        }

        public void Capture(IBtTreeDebugView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            var nodes = view.GetNodeStates() ?? new List<BtNodeDebugInfo>();
            var blackboard = view.GetBlackboard();
            var frame = view.LastFrame;

            if (HasSample)
            {
                foreach (var node in nodes)
                {
                    if (_lastNodeStates.TryGetValue(node.NodeId, out var previous)
                        && previous != node.State)
                    {
                        AddEvent(frame, node.NodeId, previous + " -> " + node.State);
                    }
                }
            }

            _changedBlackboardKeys.Clear();
            var currentBlackboard = new Dictionary<string, string>(StringComparer.Ordinal);
            if (blackboard?.KeyNames != null)
            {
                for (var i = 0; i < blackboard.KeyNames.Count; i++)
                {
                    var key = blackboard.KeyNames[i];
                    var value = FormatBlackboardValue(blackboard, i);
                    currentBlackboard[key] = value;
                    if (_lastBlackboardDisplay.TryGetValue(key, out var previous)
                        && !string.Equals(previous, value, StringComparison.Ordinal))
                    {
                        _changedBlackboardKeys.Add(key);
                        AddEvent(frame, key, previous + " -> " + value);
                    }
                }
            }

            _lastBlackboardDisplay.Clear();
            foreach (var pair in currentBlackboard) _lastBlackboardDisplay[pair.Key] = pair.Value;
            _lastNodeStates.Clear();
            foreach (var node in nodes) _lastNodeStates[node.NodeId] = node.State;
            _nodes.Clear();
            _nodes.AddRange(nodes);
            Blackboard = blackboard;
            LastFrame = frame;
        }

        public void ClearHistory() => _events.Clear();

        public bool HasBlackboardChanged(string key) => _changedBlackboardKeys.Contains(key);

        public void Reset()
        {
            _nodes.Clear();
            _lastNodeStates.Clear();
            _lastBlackboardDisplay.Clear();
            _changedBlackboardKeys.Clear();
            _events.Clear();
            Blackboard = null;
            LastFrame = -1;
        }

        public static string FormatBlackboardValue(BtBlackboardValueSnapshot snapshot, int index)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (index < 0 || index >= snapshot.KeyTypes.Count) return "?";
            return snapshot.KeyTypes[index] switch
            {
                BtValueType.Bool when index < snapshot.BoolValues.Count => snapshot.BoolValues[index].ToString(),
                BtValueType.Int64 when index < snapshot.Int64Values.Count => snapshot.Int64Values[index].ToString(),
                BtValueType.Fixed64 when index < snapshot.Fixed64RawValues.Count
                    => Fixed64.FromRaw(snapshot.Fixed64RawValues[index]).ToString(),
                BtValueType.String when index < snapshot.StringValues.Count => snapshot.StringValues[index] ?? "",
                _ => "?",
            };
        }

        private void AddEvent(int frame, string source, string change)
        {
            _events.Add(new BtDebugObservationEvent(frame, source, change));
            if (_events.Count > _historyLimit) _events.RemoveAt(0);
        }
    }
}

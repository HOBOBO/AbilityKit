#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    /// <summary>
    /// 管线追踪记录器 Editor 完整实现
    /// 提供完整的调试追踪功能
    /// </summary>
    public sealed class EditorPipelineTraceRecorder : IPipelineTraceRecorder
    {
        public static readonly EditorPipelineTraceRecorder Instance = new EditorPipelineTraceRecorder();

        private readonly Dictionary<int, EditorPipelineRunTrace> _traces = new Dictionary<int, EditorPipelineRunTrace>(64);
        private readonly object _lock = new object();

        public bool IsEnabled => true;

        public void Record(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            if (owner == null) return;

            lock (_lock)
            {
                if (!_traces.TryGetValue(owner.OwnerId, out var trace))
                {
                    trace = new EditorPipelineRunTrace(2048);
                    _traces[owner.OwnerId] = trace;
                }
                trace.AddTrace(data);
            }
        }

        public IPipelineRunTrace? GetTrace(int ownerId)
        {
            lock (_lock)
            {
                return _traces.TryGetValue(ownerId, out var trace) ? trace : null;
            }
        }

        public IReadOnlyList<PipelineTraceEvent> GetSnapshot(int ownerId)
        {
            lock (_lock)
            {
                return _traces.TryGetValue(ownerId, out var trace)
                    ? trace.GetSnapshot()
                    : Array.Empty<PipelineTraceEvent>();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _traces.Clear();
            }
        }

        public void RemoveTrace(int ownerId)
        {
            lock (_lock)
            {
                _traces.Remove(ownerId);
            }
        }
    }

    /// <summary>
    /// 管线追踪记录 Editor 实现（Ring Buffer）
    /// </summary>
    public sealed class EditorPipelineRunTrace : IPipelineRunTrace
    {
        private readonly PipelineTraceEvent[] _buffer;
        private int _count;
        private int _head;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public EditorPipelineRunTrace(int capacity)
        {
            if (capacity < 16) capacity = 16;
            _buffer = new PipelineTraceEvent[capacity];
            _count = 0;
            _head = 0;
        }

        public void Add(EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
        {
            var evt = new PipelineTraceEvent(_count + 1, type, phaseId, state, message);
            _buffer[_head] = evt;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public void AddTrace(PipelineTraceData data)
        {
            var evt = new PipelineTraceEvent(data.Sequence, data.Type, data.PhaseId, data.State, data.Message, data.UtcTime);
            _buffer[_head] = evt;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public IReadOnlyList<PipelineTraceEvent> GetSnapshot()
        {
            if (_count == 0) return Array.Empty<PipelineTraceEvent>();

            var snapshot = new PipelineTraceEvent[_count];
            var start = _count == _buffer.Length ? _head : 0;
            for (int i = 0; i < _count; i++)
            {
                var idx = (start + i) % _buffer.Length;
                snapshot[i] = _buffer[idx];
            }

            return snapshot;
        }

        public void CopyTo(List<PipelineTraceEvent> dst)
        {
            if (dst == null) return;
            dst.Clear();
            var snapshot = GetSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                dst.Add(snapshot[i]);
            }
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _count = 0;
            _head = 0;
        }
    }
}

#endif

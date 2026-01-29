#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    public sealed class PipelineRunTrace
    {
        private readonly PipelineTraceEvent[] _buffer;
        private int _seq;
        private int _count;
        private int _head;

        public PipelineRunTrace(int capacity)
        {
            if (capacity < 16) capacity = 16;
            _buffer = new PipelineTraceEvent[capacity];
            _seq = 0;
            _count = 0;
            _head = 0;
        }

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public void Add(PipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
        {
            var e = new PipelineTraceEvent(++_seq, type, phaseId, state, message);
            _buffer[_head] = e;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public void CopyTo(List<PipelineTraceEvent> dst)
        {
            if (dst == null) return;
            dst.Clear();
            if (_count == 0) return;

            var start = _count == _buffer.Length ? _head : 0;
            for (int i = 0; i < _count; i++)
            {
                var idx = (start + i) % _buffer.Length;
                dst.Add(_buffer[idx]);
            }
        }
    }
}

#endif

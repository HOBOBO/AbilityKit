using System;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public sealed class CompositeFrameRecordSink : IFrameRecordSink
    {
        private readonly IFrameRecordSink[] _sinks;

        public CompositeFrameRecordSink(params IFrameRecordSink[] sinks)
        {
            _sinks = sinks;
        }

        public void AppendInput(in PlayerInputCommand cmd)
        {
            if (_sinks == null) return;
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.AppendInput(in cmd);
            }
        }

        public void AppendStateHash(int frame, int version, uint hash)
        {
            if (_sinks == null) return;
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.AppendStateHash(frame, version, hash);
            }
        }

        public void AppendSnapshot(int frame, int opCode, byte[] payload)
        {
            if (_sinks == null) return;
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.AppendSnapshot(frame, opCode, payload);
            }
        }

        public void Dispose()
        {
            if (_sinks == null) return;
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.Dispose();
            }
        }
    }
}

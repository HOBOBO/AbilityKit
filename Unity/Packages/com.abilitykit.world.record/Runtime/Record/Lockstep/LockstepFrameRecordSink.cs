using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Record.Core;

namespace AbilityKit.Ability.Share.Common.Record.Lockstep
{
    public sealed class LockstepFrameRecordSink : IFrameRecordSink
    {
        private readonly ILockstepInputRecordWriter _writer;

        public LockstepFrameRecordSink(ILockstepInputRecordWriter writer)
        {
            _writer = writer;
        }

        public void AppendInput(in PlayerInputCommand cmd)
        {
            _writer?.Append(in cmd);
        }

        public void AppendStateHash(int frame, int version, uint hash)
        {
            _writer?.AppendStateHash(frame, version, hash);
        }

        public void AppendSnapshot(int frame, int opCode, byte[] payload)
        {
            _writer?.AppendSnapshot(frame, opCode, payload);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}

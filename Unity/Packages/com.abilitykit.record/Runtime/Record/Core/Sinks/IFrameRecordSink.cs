using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Common.Record.Core
{
    public interface IFrameRecordSink : IDisposable
    {
        void AppendInput(in PlayerInputCommand cmd);

        void AppendStateHash(int frame, int version, uint hash);

        void AppendSnapshot(int frame, int opCode, byte[] payload);
    }
}

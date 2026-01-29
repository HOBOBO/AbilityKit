using System;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Common.Record.Lockstep
{
    public interface ILockstepInputRecordWriter : IDisposable
    {
        void Append(in PlayerInputCommand cmd);

        void AppendStateHash(int frame, int version, uint hash);

        void AppendSnapshot(int frame, int opCode, byte[] payload);
    }
}

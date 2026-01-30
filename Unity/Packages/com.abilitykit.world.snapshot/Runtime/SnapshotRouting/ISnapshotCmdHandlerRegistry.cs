using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public interface ISnapshotCmdHandlerRegistry
    {
        void RegisterCmdHandler<T>(int opCode, Action<object, FramePacket, T> handler);
    }
}

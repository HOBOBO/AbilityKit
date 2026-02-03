using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public interface ISnapshotCmdHandlerRegistry
    {
        void RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler);
    }
}

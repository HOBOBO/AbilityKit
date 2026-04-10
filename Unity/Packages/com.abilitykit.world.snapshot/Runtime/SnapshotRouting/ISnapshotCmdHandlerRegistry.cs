using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public interface ISnapshotCmdHandlerRegistry
    {
        void RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler);
    }
}

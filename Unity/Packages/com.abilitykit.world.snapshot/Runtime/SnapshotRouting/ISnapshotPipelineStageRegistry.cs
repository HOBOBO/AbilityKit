using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public interface ISnapshotPipelineStageRegistry
    {
        IDisposable AddPipelineStage<T>(int opCode, int order, Action<object, ISnapshotEnvelope, T> handler);
    }
}

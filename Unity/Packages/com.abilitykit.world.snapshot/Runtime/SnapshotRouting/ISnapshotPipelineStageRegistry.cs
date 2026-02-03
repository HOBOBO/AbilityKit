using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public interface ISnapshotPipelineStageRegistry
    {
        IDisposable AddPipelineStage<T>(int opCode, int order, Action<object, ISnapshotEnvelope, T> handler);
    }
}

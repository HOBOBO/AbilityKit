using System;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public interface ISnapshotPipelineStageRegistry
    {
        IDisposable AddPipelineStage<T>(int opCode, int order, Action<object, FramePacket, T> handler);
    }
}

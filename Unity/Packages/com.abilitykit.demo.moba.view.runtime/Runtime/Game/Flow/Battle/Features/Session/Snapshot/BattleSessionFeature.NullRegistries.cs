using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.SnapshotRouting;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private readonly struct NullDisposable : IDisposable
        {
            public void Dispose() { }
        }

        private sealed class NullSnapshotPipelineStageRegistry : ISnapshotPipelineStageRegistry
        {
            public IDisposable AddPipelineStage<T>(int opCode, int order, Action<object, ISnapshotEnvelope, T> handler)
            {
                return new NullDisposable();
            }
        }

        private sealed class NullSnapshotCmdHandlerRegistry : ISnapshotCmdHandlerRegistry
        {
            public void RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler)
            {
                // Intentionally ignore cmd handlers.
            }
        }
    }
}

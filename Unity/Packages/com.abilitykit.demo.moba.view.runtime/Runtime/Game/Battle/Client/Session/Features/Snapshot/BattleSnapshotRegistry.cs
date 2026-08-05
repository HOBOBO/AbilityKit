using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Game.Flow.Snapshot
{
    [SnapshotRegistry("battle")]
    public static partial class BattleSnapshotRegistry
    {
        public static void RegisterAll(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd)
        {
            RegisterAllGenerated(dispatcherDecoders, pipelineDecoders, pipeline, cmd);
        }

        static partial void RegisterAllGenerated(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd);
    }
}

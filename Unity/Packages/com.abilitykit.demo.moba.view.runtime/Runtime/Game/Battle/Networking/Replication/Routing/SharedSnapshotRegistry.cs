using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Game.Flow.Snapshot
{
    [SnapshotRegistry("shared")]
    public static partial class SharedSnapshotRegistry
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

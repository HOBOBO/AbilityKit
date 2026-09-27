using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;

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
            ActorTransformSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            ActorTransformSnapshotRoute.RegisterDecoder(pipelineDecoders);
            DamageEventSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            DamageEventSnapshotRoute.RegisterDecoder(pipelineDecoders);
            PresentationCueSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            PresentationCueSnapshotRoute.RegisterDecoder(pipelineDecoders);
            SkillStateSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            SkillStateSnapshotRoute.RegisterDecoder(pipelineDecoders);
            ProjectileEventSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            ProjectileEventSnapshotRoute.RegisterDecoder(pipelineDecoders);
            AreaEventSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            AreaEventSnapshotRoute.RegisterDecoder(pipelineDecoders);
            StateHashSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            StateHashSnapshotRoute.RegisterDecoder(pipelineDecoders);
            RegisterAllGenerated(dispatcherDecoders, pipelineDecoders, pipeline, cmd);
        }

        static partial void RegisterAllGenerated(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd);
    }
}

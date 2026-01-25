using AbilityKit.Ability.Share.Common.SnapshotRouting;

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

            dispatcherDecoders.RegisterDecoder<AbilityKit.Ability.Share.Impl.Moba.Services.MobaAreaEventSnapshotCodec.Entry[]>((int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.AreaEventSnapshot, AbilityKit.Game.Flow.Snapshot.BattleSnapshotDeclarations.DecodeAreaEvents);
            pipelineDecoders.RegisterDecoder<AbilityKit.Ability.Share.Impl.Moba.Services.MobaAreaEventSnapshotCodec.Entry[]>((int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.AreaEventSnapshot, AbilityKit.Game.Flow.Snapshot.BattleSnapshotDeclarations.DecodeAreaEvents);
        }

        static partial void RegisterAllGenerated(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd);
    }
}

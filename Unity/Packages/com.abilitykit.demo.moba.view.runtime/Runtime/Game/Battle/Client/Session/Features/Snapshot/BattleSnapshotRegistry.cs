using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

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
            ActorSpawnSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            ActorSpawnSnapshotRoute.RegisterDecoder(pipelineDecoders);
            ActorDespawnSnapshotRoute.RegisterDecoder(dispatcherDecoders);
            ActorDespawnSnapshotRoute.RegisterDecoder(pipelineDecoders);
            dispatcherDecoders.RegisterDecoder<MobaActionAckEntry[]>(
                MobaOpCodes.Snapshot.ActionAck,
                BattleSnapshotDeclarations.DecodeActionAck);
            pipelineDecoders.RegisterDecoder<MobaActionAckEntry[]>(
                MobaOpCodes.Snapshot.ActionAck,
                BattleSnapshotDeclarations.DecodeActionAck);
            cmd.RegisterCmdHandler<MobaActionAckEntry[]>(
                MobaOpCodes.Snapshot.ActionAck,
                BattleSnapshotDeclarations.HandleActionAck);
            RegisterAllGenerated(dispatcherDecoders, pipelineDecoders, pipeline, cmd);
        }

        static partial void RegisterAllGenerated(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd);
    }
}

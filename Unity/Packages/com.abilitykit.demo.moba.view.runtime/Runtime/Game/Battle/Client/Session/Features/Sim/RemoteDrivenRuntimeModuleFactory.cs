using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Ability.Host.Framework;

namespace AbilityKit.Game.Flow
{
    internal static class RemoteDrivenRuntimeModuleFactory
    {
        internal const int PredictionRollbackHistoryFrames = 600;

        public static HostRuntimeModuleHost Create(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            return new HostRuntimeModuleHost()
                .Add(CreatePredictionModule(options))
                .Add(new ServerFrameTimeModule(
                    options.FixedDelta,
                    advanceOnHostTickFallback: false))
                .Add(new WorldAutoStartModule());
        }

        private static ClientPredictionDriverModule CreatePredictionModule(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            return options.EnableClientPrediction
                ? CreateClientPredictionModule(options)
                : CreateRemoteOnlyModule(options);
        }

        private static ClientPredictionDriverModule CreateClientPredictionModule(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            return new ClientPredictionDriverModule(
                resolveRemoteInputs: options.ResolveRemoteInputs,
                resolveLocalInputs: options.ResolveLocalInputs,
                resolveIdealFrameLimit: options.ResolveIdealFrameLimit,
                inputDelayFrames: options.InputDelayFrames,
                maxPredictionAheadFrames: 30,
                minPredictionWindow: 1,
                backlogEwmaAlpha: 0.20f,
                enableRollback: true,
                rollbackHistoryFrames: PredictionRollbackHistoryFrames,
                rollbackCaptureEveryNFrames: 1,
                buildRollbackRegistry: options.BuildRollbackRegistry,
                buildComputeHash: options.BuildComputeHash,
                bufferOptions: options.PredictionBufferOptions);
        }

        private static ClientPredictionDriverModule CreateRemoteOnlyModule(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            return new ClientPredictionDriverModule(
                resolveRemoteInputs: options.ResolveRemoteInputs,
                resolveLocalInputs: _ => null,
                resolveIdealFrameLimit: options.ResolveIdealFrameLimit,
                inputDelayFrames: 0,
                maxPredictionAheadFrames: 0,
                minPredictionWindow: 0,
                backlogEwmaAlpha: 0.20f,
                enableRollback: false,
                rollbackHistoryFrames: 0,
                rollbackCaptureEveryNFrames: 0,
                buildRollbackRegistry: _ => new RollbackRegistry(),
                buildComputeHash: _ => null,
                bufferOptions: options.PredictionBufferOptions);
        }
    }
}

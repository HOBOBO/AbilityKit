#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.View.Loading;
using AbilityKit.Network.Room;

namespace AbilityKit.Demo.Tiny.View
{
    /// <summary>Tiny's procedural arena has no external assets; validate the launch manifest before ack.</summary>
    internal static class TinyAssetPreparation
    {
        private static readonly ClientLoadingPipeline Pipeline = new ClientLoadingPipeline(
            new ClientLoadingPipelineDefinition(new[]
            {
                new ClientLoadingStepDefinition("tiny-manifest", "tiny-manifest", 1)
            }),
            new ClientLoadingStepRegistry().Register("tiny-manifest", _ =>
                new DelegateClientLoadingStep((progress, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report(1f);
                    return Task.CompletedTask;
                })));

        public static Task PrepareAsync(RoomGatewaySnapshot room, CancellationToken cancellationToken)
        {
            if (room.LaunchManifestVersion <= 0 || string.IsNullOrWhiteSpace(room.LaunchManifestHash))
                throw new InvalidOperationException("Tiny launch manifest is incomplete.");
            return Pipeline.ExecuteAsync(cancellationToken: cancellationToken);
        }
    }
}

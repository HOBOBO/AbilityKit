#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Tiny;
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
            Validate(room);
            return Pipeline.ExecuteAsync(cancellationToken: cancellationToken);
        }

        public static void Validate(RoomGatewaySnapshot room) =>
            RoomGatewayLaunchManifestCompatibility.Require(room, 1,
                new[] { TinyBattle.AssetKey, TinyBattle.RulesKey },
                new Dictionary<string, string> { ["players"] = room.Players.Count.ToString() });
    }
}

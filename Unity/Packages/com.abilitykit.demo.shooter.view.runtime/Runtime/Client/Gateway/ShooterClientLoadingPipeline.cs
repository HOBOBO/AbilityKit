#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.Session;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Demo.Shooter.View
{
    public readonly struct ShooterClientLoadingContext
    {
        public ShooterClientLoadingContext(
            string sessionToken,
            string roomId,
            uint playerId,
            RoomGatewaySnapshot snapshot,
            string eventEpoch,
            long lastEventAck)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            PlayerId = playerId;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            EventEpoch = eventEpoch ?? string.Empty;
            LastEventAck = Math.Max(0L, lastEventAck);
        }

        public string SessionToken { get; }
        public string RoomId { get; }
        public uint PlayerId { get; }
        public RoomGatewaySnapshot Snapshot { get; }
        public string EventEpoch { get; }
        public long LastEventAck { get; }
    }

    public interface IShooterClientLoadingStepProvider
    {
        IClientLoadingStepResolver CreateResolver(in ShooterClientLoadingContext context);
    }

    public static class ShooterClientLoadingPipelineDefaults
    {
        public const string ManifestType = "shooter.manifest.validate";
        public const string RuntimeType = "shooter.runtime.prepare";
        public const string StateSyncType = "shooter.state-sync.prepare";
        public const string FinalizeType = "shooter.loading.finalize";

        public static ClientLoadingPipelineDefinition CreateDefinition()
        {
            return new ClientLoadingPipelineDefinition(new[]
            {
                new ClientLoadingStepDefinition("manifest", ManifestType, 20),
                new ClientLoadingStepDefinition("runtime", RuntimeType, 30),
                new ClientLoadingStepDefinition("state-sync", StateSyncType, 30),
                new ClientLoadingStepDefinition("finalize", FinalizeType, 20)
            });
        }
    }

    public sealed class DefaultShooterClientLoadingStepProvider : IShooterClientLoadingStepProvider
    {
        public IClientLoadingStepResolver CreateResolver(in ShooterClientLoadingContext context)
        {
            var captured = context;
            return new ClientLoadingStepRegistry()
                .Register(
                    ShooterClientLoadingPipelineDefaults.ManifestType,
                    _ => new DelegateClientLoadingStep((progress, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (captured.Snapshot.LaunchGeneration <= 0 ||
                            captured.Snapshot.LaunchManifestVersion <= 0 ||
                            string.IsNullOrWhiteSpace(captured.Snapshot.LaunchManifestHash))
                        {
                            throw new InvalidOperationException("Shooter launch manifest is incomplete.");
                        }

                        progress.Report(1f);
                        return Task.CompletedTask;
                    }))
                .Register(
                    ShooterClientLoadingPipelineDefaults.RuntimeType,
                    _ => new DelegateClientLoadingStep(async (progress, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (captured.PlayerId == 0u || string.IsNullOrWhiteSpace(captured.RoomId))
                        {
                            throw new InvalidOperationException("Shooter runtime identity is incomplete.");
                        }

                        await Task.Yield();
                        progress.Report(1f);
                    }))
                .Register(
                    ShooterClientLoadingPipelineDefaults.StateSyncType,
                    _ => new DelegateClientLoadingStep(async (progress, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (captured.LastEventAck < 0L)
                        {
                            throw new InvalidOperationException("Reliable event cursor is invalid.");
                        }

                        await Task.Yield();
                        progress.Report(1f);
                    }))
                .Register(
                    ShooterClientLoadingPipelineDefaults.FinalizeType,
                    _ => new DelegateClientLoadingStep(async (progress, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                        progress.Report(1f);
                    }));
        }
    }
}

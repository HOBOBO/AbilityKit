#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientInputCoordinator
    {
        private static readonly SyncHealthEvent[] EmptyHealthEvents = Array.Empty<SyncHealthEvent>();

        private readonly ShooterClientFrameSyncController _frameSync;
        private readonly IShooterRoomGatewayClient? _gateway;
        private SyncHealthEvent[] _lastHealthEvents = EmptyHealthEvents;
        private long _nextSubmissionId;

        public ShooterClientInputCoordinator(ShooterClientFrameSyncController frameSync)
            : this(frameSync, null)
        {
        }

        public ShooterClientInputCoordinator(ShooterClientFrameSyncController frameSync, IShooterRoomGatewayClient? gateway)
        {
            _frameSync = frameSync ?? throw new ArgumentNullException(nameof(frameSync));
            _gateway = gateway;
        }

        public bool HasGateway => _gateway != null;

        public IReadOnlyList<SyncHealthEvent> LastHealthEvents => _lastHealthEvents;

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            var packet = ShooterClientInputBuilder.CreatePacket(playerId, moveX, moveY, aimX, aimY, fire);
            var accepted = _frameSync.SubmitLocalInput(packet.Command);
            var submissionId = Interlocked.Increment(ref _nextSubmissionId);
            return new ShooterClientInputSubmitResult(accepted, _frameSync.CurrentFrame, packet, submissionId);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            var payload = ShooterInputCodec.Serialize(new[] { command });
            var packet = new ShooterInputPacket(ShooterOpCodes.Input.PlayerCommand, payload, in command);
            var accepted = _frameSync.SubmitLocalInput(in command);
            var submissionId = Interlocked.Increment(ref _nextSubmissionId);
            return new ShooterClientInputSubmitResult(accepted, _frameSync.CurrentFrame, packet, submissionId);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var local = SubmitLocalInput(in command).WithRequestedFrame(context.Frame);
            return SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken);
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (_gateway == null)
            {
                throw new InvalidOperationException("Shooter room gateway client is not configured.");
            }

            var remote = await _gateway.SubmitBattleInputAsync(context, local.Packet, timeout, cancellationToken).ConfigureAwait(false);
            if (ShouldRetryTooFarFutureInput(in context, in remote))
            {
                context = new ShooterGatewayBattleInputContext(
                    context.SessionToken,
                    context.BattleId,
                    context.WorldId,
                    remote.CurrentFrame,
                    context.PlayerId);
                local = local.WithRequestedFrame(remote.CurrentFrame);
                remote = await _gateway.SubmitBattleInputAsync(context, local.Packet, timeout, cancellationToken).ConfigureAwait(false);
            }

            CaptureRemoteInputHealthEvents(context, local, remote);
            if (remote.ShouldResync)
            {
                _frameSync.MarkGatewayInputResyncRequested(context.Frame, remote.CurrentFrame);
            }

            return new ShooterClientGatewayInputSubmitResult(in local, in remote);
        }

        private static bool ShouldRetryTooFarFutureInput(
            in ShooterGatewayBattleInputContext context,
            in ShooterGatewayBattleInputResult remote)
        {
            const int MaxFutureLeadFrames = 120;
            return !remote.Success
                && remote.ShouldResync
                && remote.CurrentFrame >= 0
                && context.Frame > remote.CurrentFrame + MaxFutureLeadFrames
                && string.Equals(
                    remote.Status,
                    "RejectedTooFarFuture",
                    StringComparison.Ordinal);
        }

        private void CaptureRemoteInputHealthEvents(
            in ShooterGatewayBattleInputContext context,
            in ShooterClientInputSubmitResult local,
            in ShooterGatewayBattleInputResult remote)
        {
            var accepted = remote.Success && !remote.ShouldResync;
            var inputEvent = accepted
                ? SyncHealthEvent.Info(SyncHealthEventKind.InputAccepted, remote.AcceptedFrame, remote.CurrentFrame)
                : SyncHealthEvent.Warning(SyncHealthEventKind.InputRejected, context.Frame, remote.CurrentFrame);

            if (!local.Packet.Command.Fire)
            {
                _lastHealthEvents = new[] { inputEvent };
                return;
            }

            var lagCompensationEvent = accepted
                ? SyncHealthEvent.Info(SyncHealthEventKind.LagCompensatedValidationAccepted, remote.AcceptedFrame, remote.ServerTicks)
                : SyncHealthEvent.Warning(SyncHealthEventKind.LagCompensatedValidationRejected, context.Frame, remote.ServerTicks);
            _lastHealthEvents = new[] { inputEvent, lagCompensationEvent };
        }
    }
}

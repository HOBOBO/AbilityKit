#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Battle;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Adapter that satisfies <see cref="IShooterRoomGatewayClient"/> (consumed by the sync controllers
    /// and <c>ShooterClientInputCoordinator</c>) while routing input submission through the unified
    /// <see cref="NetworkTransport"/> engine's awaitable <see cref="NetworkTransport.SendInputAsync"/>.
    /// Replaces the hand-written <see cref="ShooterRoomGatewayClient"/> per-submit wire glue (P2.2).
    /// The full per-submit result (<c>AcceptedFrame</c>/<c>ServerTicks</c>/<c>ShouldResync</c>) survives,
    /// so shooter's lag-compensation health events and its own <c>RejectedTooFarFuture</c> retry are unchanged.
    /// </summary>
    internal sealed class ShooterBattleTransportGatewayClient : IShooterRoomGatewayClient
    {
        private readonly NetworkTransport _transport;
        private readonly Func<uint, PlayerId> _playerIdFromUInt;
        private readonly Func<ulong, WorldId> _worldIdFromUlong;

        public ShooterBattleTransportGatewayClient(
            NetworkTransport transport,
            Func<uint, PlayerId> playerIdFromUInt,
            Func<ulong, WorldId> worldIdFromUlong)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _playerIdFromUInt = playerIdFromUInt ?? throw new ArgumentNullException(nameof(playerIdFromUInt));
            _worldIdFromUlong = worldIdFromUlong ?? throw new ArgumentNullException(nameof(worldIdFromUlong));
        }

        public async Task<ShooterGatewayBattleInputResult> SubmitBattleInputAsync(
            ShooterGatewayBattleInputContext context,
            ShooterInputPacket packet,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            Validate(in context);

            var request = new SubmitInputRequest(
                _worldIdFromUlong(context.WorldId),
                new PlayerInputCommand(
                    new FrameIndex(context.Frame),
                    _playerIdFromUInt(context.PlayerId),
                    packet.OpCode,
                    packet.Payload ?? Array.Empty<byte>()));

            var resp = await _transport.SendInputAsync(request, timeout, cancellationToken).ConfigureAwait(false);

            // Map the engine response back to shooter's result shape.
            // ServerFrame ↔ CurrentFrame; engine retry is disabled (factory sets RetryAtAuthoritativeFrame=false),
            // so ShouldResync is carried as a pure data field for the client's resync path.
            return new ShooterGatewayBattleInputResult(
                resp.Accepted,
                resp.AcceptedFrame,
                resp.Message,
                resp.ServerFrame,
                resp.Status,
                resp.ShouldResync,
                resp.ServerTicks);
        }

        private static void Validate(in ShooterGatewayBattleInputContext context)
        {
            if (string.IsNullOrWhiteSpace(context.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(context));
            if (string.IsNullOrWhiteSpace(context.BattleId)) throw new ArgumentException("battleId is required.", nameof(context));
            if (context.WorldId == 0) throw new ArgumentOutOfRangeException(nameof(context));
            if (context.Frame < 0) throw new ArgumentOutOfRangeException(nameof(context));
            if (context.PlayerId == 0) throw new ArgumentOutOfRangeException(nameof(context));
        }
    }
}

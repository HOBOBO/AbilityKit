using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Host
{
    public enum ShooterHostInputReasonCode
    {
        None = 0,
        InvalidPayload = 1,
        SessionNotBound = 2,
        WorldNotFound = 3,
        InputPortUnavailable = 4,
        BattleNotStarted = 5,
        InputRejected = 6,
        InvalidFrame = 7,
    }

    /// <summary>
    /// Transport-neutral adapter from Shooter input requests to the bound authoritative world.
    /// Client-provided player ids are overwritten with the authenticated session identity.
    /// </summary>
    public sealed class ShooterHostNetworkRequestHandler : IHostNetworkRequestHandler
    {
        private readonly IShooterHostRequestContextResolver _contextResolver;

        public ShooterHostNetworkRequestHandler(IShooterHostRequestContextResolver contextResolver = null)
        {
            _contextResolver = contextResolver ?? BoundShooterHostRequestContextResolver.Instance;
        }

        public void Handle(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (header.OpCode != (uint)ShooterOpCodes.Input.PlayerCommand ||
                (header.Flags & NetworkPacketFlags.Request) == 0)
            {
                return;
            }

            if (!TryDeserialize(payload, out var request) || request.Frame < 0)
            {
                Respond(session, header, false, request.Frame, 0, request.Frame < 0
                    ? ShooterHostInputReasonCode.InvalidFrame
                    : ShooterHostInputReasonCode.InvalidPayload);
                return;
            }

            if (!_contextResolver.TryResolve(session, in request, out var binding))
            {
                Respond(session, header, false, request.Frame, 0, ShooterHostInputReasonCode.SessionNotBound);
                return;
            }

            if (!runtime.TryGetWorld(new WorldId(binding.WorldId), out var world) || world == null)
            {
                Respond(session, header, false, request.Frame, 0, ShooterHostInputReasonCode.WorldNotFound);
                return;
            }

            if (!world.Services.TryResolve<IShooterBattleRuntimePort>(out var inputPort) || inputPort == null)
            {
                Respond(session, header, false, request.Frame, 0, ShooterHostInputReasonCode.InputPortUnavailable);
                return;
            }

            if (!inputPort.IsStarted)
            {
                Respond(session, header, false, inputPort.CurrentFrame, 0, ShooterHostInputReasonCode.BattleNotStarted);
                return;
            }

            var source = request.Commands ?? Array.Empty<ShooterPlayerCommand>();
            if (source.Length != 1)
            {
                Respond(session, header, false, inputPort.CurrentFrame, 0, ShooterHostInputReasonCode.InvalidPayload);
                return;
            }

            var authorized = new[] { source[0] };
            authorized[0].PlayerId = binding.PlayerId;

            var accepted = inputPort.SubmitInput(request.Frame, authorized);
            Respond(
                session,
                header,
                accepted == authorized.Length,
                inputPort.CurrentFrame,
                accepted,
                accepted == authorized.Length
                    ? ShooterHostInputReasonCode.None
                    : ShooterHostInputReasonCode.InputRejected);
        }

        private static bool TryDeserialize(
            ArraySegment<byte> payload,
            out ShooterHostInputRequest request)
        {
            request = default;
            if (payload.Array == null || payload.Count == 0) return false;
            try
            {
                request = ShooterHostInputCodec.DeserializeRequest(payload);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Respond(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            bool accepted,
            int serverFrame,
            int acceptedCommandCount,
            ShooterHostInputReasonCode reasonCode)
        {
            var response = new ShooterHostInputResponse(
                accepted,
                serverFrame,
                acceptedCommandCount,
                (int)reasonCode);
            session.SendResponse(header.OpCode, header.Seq, ShooterHostInputCodec.Serialize(in response));
        }
    }
}

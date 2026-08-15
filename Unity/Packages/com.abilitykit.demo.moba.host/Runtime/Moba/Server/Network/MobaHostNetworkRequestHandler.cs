using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;

namespace AbilityKit.Ability.Host.Extensions.Moba.Server.Network
{
    public enum MobaHostInputReasonCode
    {
        None = 0,
        InvalidPayload = 1,
        SessionNotBound = 2,
        WorldNotFound = 3,
        InputPortUnavailable = 4,
        InputRejected = 5,
        InvalidFrame = 6,
    }

    /// <summary>
    /// Transport-neutral adapter from the MOBA wire contract to an authoritative world input port.
    /// World and player identity come from the session resolver rather than untrusted payload fields.
    /// </summary>
    public sealed class MobaHostNetworkRequestHandler : IHostNetworkRequestHandler
    {
        private readonly IMobaHostRequestContextResolver _contextResolver;

        public MobaHostNetworkRequestHandler(IMobaHostRequestContextResolver contextResolver = null)
        {
            _contextResolver = contextResolver ?? BoundMobaHostRequestContextResolver.Instance;
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
            if (header.OpCode != OpCodes.SubmitFrameInput ||
                (header.Flags & NetworkPacketFlags.Request) == 0)
            {
                return;
            }

            if (!TryDeserialize(payload, out var request) || request.Frame < 0)
            {
                Respond(session, header, false, request.Frame, request.Frame < 0
                    ? MobaHostInputReasonCode.InvalidFrame
                    : MobaHostInputReasonCode.InvalidPayload);
                return;
            }

            if (!_contextResolver.TryResolve(session, in request, out var binding))
            {
                Respond(session, header, false, request.Frame, MobaHostInputReasonCode.SessionNotBound);
                return;
            }

            if (!runtime.TryGetWorld(new WorldId(binding.WorldId), out var world) || world == null)
            {
                Respond(session, header, false, request.Frame, MobaHostInputReasonCode.WorldNotFound);
                return;
            }

            if (!world.Services.TryResolve<IMobaBattleRuntimePort>(out var inputPort) || inputPort == null)
            {
                Respond(session, header, false, request.Frame, MobaHostInputReasonCode.InputPortUnavailable);
                return;
            }

            var frame = new FrameIndex(request.Frame);
            var command = new PlayerInputCommand(
                frame,
                new PlayerId(binding.PlayerId),
                request.InputOpCode,
                request.InputPayload ?? Array.Empty<byte>());
            var result = inputPort.Submit(frame, new[] { command });
            Respond(
                session,
                header,
                result.Succeeded && result.CommandCount > 0,
                request.Frame,
                result.Succeeded && result.CommandCount > 0
                    ? MobaHostInputReasonCode.None
                    : MobaHostInputReasonCode.InputRejected);
        }

        private static bool TryDeserialize(
            ArraySegment<byte> payload,
            out WireSubmitFrameInputReq request)
        {
            request = default;
            if (payload.Array == null || payload.Count == 0) return false;
            try
            {
                request = WireCustomBinary.DeserializeSubmitFrameInputReq(payload);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Respond(
            IServerNetworkSession session,
            NetworkPacketHeader requestHeader,
            bool accepted,
            int serverFrame,
            MobaHostInputReasonCode reasonCode)
        {
            var response = new WireSubmitFrameInputRes(accepted, serverFrame, (int)reasonCode);
            session.SendResponse(requestHeader.OpCode, requestHeader.Seq, WireCustomBinary.Serialize(in response));
        }
    }
}

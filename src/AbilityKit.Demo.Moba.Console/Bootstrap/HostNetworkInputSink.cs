using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    /// <summary>
    /// Sends local console input through the same framed Host request path used by external clients.
    /// The connection implementation remains injected and may be in-process, TCP, or another transport.
    /// </summary>
    public sealed class HostNetworkInputSink : IWorldInputSink
    {
        private readonly object _gate = new object();
        private readonly IConnection _connection;
        private readonly string _worldId;
        private int _nextSequence;
        private bool _disposed;
        private RuntimeInputDiagnostics _diagnostics;

        public HostNetworkInputSink(IConnection connection, string worldId)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _worldId = string.IsNullOrWhiteSpace(worldId)
                ? throw new ArgumentException("World id is required.", nameof(worldId))
                : worldId;
            _connection.PacketReceived += OnPacketReceived;
        }

        public RuntimeInputDiagnostics Diagnostics
        {
            get
            {
                lock (_gate) return _diagnostics;
            }
        }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (_disposed || inputs == null || inputs.Count == 0) return;

            lock (_gate)
            {
                _diagnostics = _diagnostics.RecordSubmission(frame.Value, inputs.Count);
            }

            for (var i = 0; i < inputs.Count; i++)
            {
                var command = inputs[i];
                var request = new WireSubmitFrameInputReq(
                    roomId: 0,
                    worldId: 0,
                    playerId: 0,
                    frame: frame.Value,
                    inputOpCode: command.OpCode,
                    inputPayload: command.Payload ?? Array.Empty<byte>());
                var sequence = unchecked((uint)Interlocked.Increment(ref _nextSequence));
                if (sequence == 0)
                {
                    sequence = unchecked((uint)Interlocked.Increment(ref _nextSequence));
                }

                try
                {
                    _connection.Send(
                        OpCodes.SubmitFrameInput,
                        WireCustomBinary.Serialize(in request),
                        (ushort)NetworkPacketFlags.Request,
                        sequence);
                }
                catch (Exception exception)
                {
                    lock (_gate)
                    {
                        _diagnostics = _diagnostics.RecordResponse(
                            frame.Value,
                            false,
                            0,
                            $"Send failed for world '{_worldId}': {exception.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connection.PacketReceived -= OnPacketReceived;
        }

        private void OnPacketReceived(uint opCode, uint sequence, ArraySegment<byte> payload)
        {
            if (_disposed || opCode != OpCodes.SubmitFrameInput || sequence == 0) return;

            try
            {
                var response = WireCustomBinary.DeserializeSubmitFrameInputRes(payload);
                lock (_gate)
                {
                    _diagnostics = _diagnostics.RecordResponse(
                        response.ServerFrame,
                        response.Accepted,
                        response.Accepted ? 1 : 0,
                        response.Accepted
                            ? "Accepted by Host network input handler"
                            : $"Rejected by Host network input handler. reason={response.ReasonCode}");
                }
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _diagnostics = _diagnostics.RecordResponse(
                        _diagnostics.LastFrame,
                        false,
                        0,
                        $"Invalid Host response: {exception.Message}");
                }
            }
        }
    }

    public readonly struct RuntimeInputDiagnostics
    {
        public static readonly RuntimeInputDiagnostics Empty = default;

        public RuntimeInputDiagnostics(
            int submitCount,
            int acceptedCount,
            int submittedCommandCount,
            int acceptedCommandCount,
            int lastFrame,
            bool lastSucceeded,
            string lastResult)
        {
            SubmitCount = submitCount;
            AcceptedCount = acceptedCount;
            SubmittedCommandCount = submittedCommandCount;
            AcceptedCommandCount = acceptedCommandCount;
            LastFrame = lastFrame;
            LastSucceeded = lastSucceeded;
            LastResult = lastResult;
        }

        public int SubmitCount { get; }
        public int AcceptedCount { get; }
        public int SubmittedCommandCount { get; }
        public int AcceptedCommandCount { get; }
        public int LastFrame { get; }
        public bool LastSucceeded { get; }
        public string LastResult { get; }

        public RuntimeInputDiagnostics RecordSubmission(int frame, int submittedCommands)
        {
            return new RuntimeInputDiagnostics(
                SubmitCount + 1,
                AcceptedCount,
                SubmittedCommandCount + submittedCommands,
                AcceptedCommandCount,
                frame,
                false,
                "Pending Host network response");
        }

        public RuntimeInputDiagnostics RecordResponse(
            int frame,
            bool succeeded,
            int acceptedCommands,
            string result)
        {
            return new RuntimeInputDiagnostics(
                SubmitCount,
                AcceptedCount + (succeeded ? 1 : 0),
                SubmittedCommandCount,
                AcceptedCommandCount + acceptedCommands,
                frame,
                succeeded,
                result);
        }
    }
}

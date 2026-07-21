using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

internal sealed class RecordingShooterRoomGatewayTransport : IShooterRoomGatewayRequestTransport
{
    private readonly WireSubmitBattleInputRes[] _responses;
    private readonly List<ArraySegment<byte>> _payloads = new();
    private int _requestCount;

    public RecordingShooterRoomGatewayTransport(params WireSubmitBattleInputRes[] responses)
    {
        if (responses == null || responses.Length == 0)
        {
            throw new ArgumentException("At least one response is required.", nameof(responses));
        }

        _responses = responses;
    }

    public uint LastOpCode { get; private set; }

    public ArraySegment<byte> LastPayload { get; private set; }

    public IReadOnlyList<ArraySegment<byte>> Payloads => _payloads;

    public int RequestCount => _requestCount;

    public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastOpCode = opCode;
        LastPayload = TestByteSegments.Copy(payload);
        _payloads.Add(LastPayload);
        var responseIndex = Math.Min(_requestCount, _responses.Length - 1);
        var responseValue = _responses[responseIndex];
        _requestCount++;
        var response = WireRoomGatewayBinary.Serialize(in responseValue);
        return Task.FromResult(response);
    }
}

using System;
using AbilityKit.Network.Protocol;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkFrameReaderTests
{
    [Fact]
    public void Append_RejectsFrameAboveConfiguredLimit()
    {
        var reader = new NetworkFrameReader(initialCapacity: 32)
        {
            MaxFrameLength = 64
        };
        var bytes = new byte[4];
        bytes[0] = 65;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            reader.Append(new ArraySegment<byte>(bytes)));

        Assert.Contains("Frame too large: 65", exception.Message);
        Assert.False(reader.TryRead(out _, out _));
    }
}

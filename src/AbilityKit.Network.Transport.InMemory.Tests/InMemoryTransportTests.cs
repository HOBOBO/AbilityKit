using System;
using AbilityKit.Network.Transport.InMemory;
using Xunit;

namespace AbilityKit.Network.Transport.InMemory.Tests;

public sealed class InMemoryTransportTests
{
    [Fact]
    public void CreateConnectedPair_RoutesSendToPeerBytesReceivedSynchronously()
    {
        var (client, server) = InMemoryTransport.CreateConnectedPair();

        var clientConnected = false;
        var serverConnected = false;
        client.Connected += () => clientConnected = true;
        server.Connected += () => serverConnected = true;

        client.Connect("x", 0);
        server.Connect("x", 0);

        Assert.True(clientConnected);
        Assert.True(serverConnected);
        Assert.True(client.IsConnected);

        byte[] gotAtServer = null;
        server.BytesReceived += bytes =>
        {
            gotAtServer = bytes.ToArray();
            server.Send(bytes); // echo back
        };

        byte[] echoedAtClient = null;
        client.BytesReceived += bytes => echoedAtClient = bytes.ToArray();

        var payload = new byte[] { 9, 8, 7, 6 };
        client.Send(new ArraySegment<byte>(payload));

        // synchronous delivery: both arrived before Send returned
        Assert.Equal(payload, gotAtServer);
        Assert.Equal(payload, echoedAtClient);

        client.Dispose();
        Assert.False(client.IsConnected);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Transport.LiteNet;
using LiteNetLib;
using Xunit;

namespace AbilityKit.Network.Transport.LiteNet.Tests;

public sealed class LiteNetTransportTests
{
    private const int Port = 18766;
    private const string Key = "abilitykit-test";

    [Fact]
    public async Task RoundTrip_SendsAndReceivesAReliableOrderedMessage()
    {
        var serverListener = new EventBasedNetListener();
        serverListener.ConnectionRequestEvent += request => request.Accept();
        var server = new NetManager(serverListener)
        {
            UnsyncedEvents = true,
            AutoRecycle = true,
            BroadcastReceiveEnabled = false,
        };
        Assert.True(server.Start(Port), "LiteNetLib server failed to start on port " + Port);

        // echo received bytes back to the peer
        serverListener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            var bytes = reader.GetRemainingBytes();
            peer.Send(bytes, DeliveryMethod.ReliableOrdered);
        };

        var transport = new LiteNetTransport(Key);
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Connected += () => connected.TrySetResult(true);
        transport.BytesReceived += bytes => received.TrySetResult(bytes.ToArray());

        try
        {
            transport.Connect("127.0.0.1", Port);
            await connected.Task.WaitAsync(TimeSpan.FromSeconds(8));

            var payload = new byte[] { 10, 20, 30, 40 };
            transport.Send(new ArraySegment<byte>(payload));

            var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Equal(payload, got);
        }
        finally
        {
            transport.Dispose();
            server.Stop();
        }
    }
}

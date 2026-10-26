using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Host.Kcp;
using AbilityKit.Network.Transport.Kcp;
using Xunit;

namespace AbilityKit.Network.Host.Kcp.Tests;

public sealed class KcpChannelListenerTests
{
    [Fact]
    public async Task ListenerAcceptsClientAndServerChannelCanReply()
    {
        using var listener = new KcpChannelListener(new KcpChannelListenerOptions
        {
            Address = IPAddress.Loopback,
            Port = 0,
            ConversationId = 11,
            UpdateIntervalMilliseconds = 5
        });
        var accepted = new TaskCompletionSource<AbilityKit.Network.Host.IServerChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.ChannelAccepted += channel =>
        {
            channel.BytesReceived += bytes =>
            {
                received.TrySetResult(bytes.ToArray());
                channel.Send(bytes);
            };
            accepted.TrySetResult(channel);
        };
        listener.Start();
        var endpoint = IPEndPoint.Parse(listener.Endpoint);

        using var client = new KcpTransport(new KcpTransportOptions { ConversationId = 11, UpdateIntervalMilliseconds = 5 });
        client.Connect("127.0.0.1", endpoint.Port);
        var response = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.BytesReceived += bytes => response.TrySetResult(bytes.ToArray());
        var payload = new byte[] { 9, 8, 7 };
        client.Send(payload);

        Assert.Equal(payload, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(payload, await response.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        var serverChannel = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
        Assert.False(listener.IsListening);
        Assert.True(serverChannel.IsConnected);
        serverChannel.Dispose();
    }
}

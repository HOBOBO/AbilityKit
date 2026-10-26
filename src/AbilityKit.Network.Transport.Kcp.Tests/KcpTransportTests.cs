using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Transport.Kcp;
using ET;
using Xunit;

namespace AbilityKit.Network.Transport.Kcp.Tests;

public sealed class KcpTransportTests
{
    [Fact]
    public async Task RoundTrip_SendsAndReceivesReliableMessage()
    {
        using var server = new UdpClient(0);
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var serverTask = RunEchoAsync(server, cts.Token);
        using var transport = new KcpTransport(new KcpTransportOptions
        {
            ConversationId = 7,
            Mtu = 1200,
            UpdateIntervalMilliseconds = 10
        });
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Connected += () => connected.TrySetResult(true);
        transport.BytesReceived += bytes => received.TrySetResult(bytes.ToArray());

        try
        {
            transport.Connect("127.0.0.1", endpoint.Port);
            await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));
            transport.Send(new ArraySegment<byte>(new byte[] { 1, 3, 5, 7 }));
            Assert.Equal(new byte[] { 1, 3, 5, 7 }, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            transport.Dispose();
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    private static async Task RunEchoAsync(UdpClient server, CancellationToken cancellationToken)
    {
        ET.Kcp? kcp = null;
        IPEndPoint? remote = null;
        var clock = Stopwatch.StartNew();
        var receiveTask = server.ReceiveAsync(cancellationToken).AsTask();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(receiveTask, Task.Delay(2, cancellationToken));
                if (completed == receiveTask)
                {
                    var result = await receiveTask;
                    remote = result.RemoteEndPoint;
                    kcp ??= CreateKcp(server, remote);
                    var inputResult = kcp.Input(result.Buffer);
                    while (kcp.PeekSize() > 0)
                    {
                        var size = kcp.PeekSize();
                        var payload = new byte[size];
                        kcp.Receive(payload, size);
                        kcp.Send(payload);
                        kcp.Flush();
                    }
                    receiveTask = server.ReceiveAsync(cancellationToken).AsTask();
                }

                kcp?.Update(unchecked((uint)clock.ElapsedMilliseconds));
                await Task.Delay(2, cancellationToken);
            }
        }
        finally
        {
            kcp?.Dispose();
        }
    }

    private static ET.Kcp CreateKcp(UdpClient server, IPEndPoint remote)
    {
        var kcp = new ET.Kcp(7, (bytes, count) => server.Client.SendTo(
            bytes,
            checked((int)ET.KCPBASIC.REVERSED_HEAD),
            count,
            SocketFlags.None,
            remote));
        kcp.SetNoDelay(1, 10, 2, 1);
        kcp.SetWindowSize(256, 256);
        kcp.SetMtu(1200);
        kcp.SetMinrto(30);
        return kcp;
    }
}

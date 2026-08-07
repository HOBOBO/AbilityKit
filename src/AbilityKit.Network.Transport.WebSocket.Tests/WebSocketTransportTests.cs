using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Transport.WebSocket;
using Xunit;

namespace AbilityKit.Network.Transport.WebSocket.Tests;

public sealed class WebSocketTransportTests
{
    private const int Port = 18765;

    [Fact]
    public async Task RoundTrip_SendsAndReceivesABinaryMessage()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Start();

        using var serverCts = new CancellationTokenSource();
        var serverTask = AcceptAndEchoAsync(listener, serverCts.Token);

        try
        {
            var transport = new WebSocketTransport();

            var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Connected += () => connected.TrySetResult(true);
            transport.BytesReceived += bytes => received.TrySetResult(bytes.ToArray());

            transport.Connect("localhost", Port);
            await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var payload = new byte[] { 1, 2, 3, 4, 5 };
            transport.Send(new ArraySegment<byte>(payload));

            var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, got);

            transport.Dispose();
        }
        finally
        {
            serverCts.Cancel();
            try { await serverTask; } catch { }
            listener.Stop();
        }
    }

    private static async Task AcceptAndEchoAsync(HttpListener listener, CancellationToken ct)
    {
        var ctx = await listener.GetContextAsync().WaitAsync(ct);
        if (!ctx.Request.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.Close();
            return;
        }

        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var ws = wsCtx.WebSocket;
        var buffer = new byte[64 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.Count > 0)
                    {
                        message.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (message.Length > 0)
                {
                    await ws.SendAsync(message.ToArray(), WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}

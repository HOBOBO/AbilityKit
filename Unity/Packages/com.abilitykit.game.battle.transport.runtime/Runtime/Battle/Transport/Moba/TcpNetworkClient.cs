using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AbilityKit.Network.Battle;
namespace AbilityKit.Game.Battle.Transport.Moba
{
    /// <summary>
    /// TCP 网络客户端
    /// 实现网络协议，与服务器通信
    /// </summary>
    public sealed class TcpNetworkClient : INetworkClient
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        private readonly object _writeLock = new object();
        private int _nextSeq;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<byte[]>>();

        private bool _disposed;

        public bool IsConnected => _client != null && _client.Connected;

        public event Action<uint, byte[]> OnServerPush;
        public event Action<byte[]> OnResponse;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<Exception> OnError;

        public void Connect(string host, int port)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpNetworkClient));
            if (IsConnected) return;

            _client = new TcpClient();
            _client.Connect(host, port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();

            _nextSeq = 0;
            _pendingRequests.Clear();

            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
            if (OnConnected != null) OnConnected();
        }

        public void Disconnect()
        {
            if (_disposed) return;

            if (_cts != null) _cts.Cancel();

            try
            {
                if (_stream != null) _stream.Close();
                if (_client != null) _client.Close();
            }
            catch (Exception)
            {
            }

            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetResult(new byte[0]);
            }
            _pendingRequests.Clear();

            if (OnDisconnected != null) OnDisconnected("disconnected");
        }

        public async Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpNetworkClient));
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            var seq = Interlocked.Increment(ref _nextSeq);
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingRequests.TryAdd(seq, tcs))
            {
                throw new InvalidOperationException(string.Format("Failed to track pending request with seq {0}", seq));
            }

            try
            {
                await SendFrameAsync(opCode, (uint)seq, payload, true, cancellationToken);
            }
            catch (Exception ex)
            {
                TaskCompletionSource<byte[]> removed;
                _pendingRequests.TryRemove(seq, out removed);
                throw;
            }

            using (var registration = cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }

        public Task SendServerPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpNetworkClient));
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            var seq = Interlocked.Increment(ref _nextSeq);
            return SendFrameAsync(opCode, (uint)seq, payload, false, cancellationToken);
        }

        private async Task SendFrameAsync(uint opCode, uint seq, byte[] payload, bool isRequest, CancellationToken cancellationToken)
        {
            var flags = isRequest ? NetworkPacketFlags.Request : NetworkPacketFlags.None;
            var header = new NetworkPacketHeader(flags, opCode, seq, (uint)payload.Length);

            var frameSize = NetworkFrameCodec.GetFrameSize(payload.Length);
            var buffer = ArrayPool<byte>.Shared.Rent(frameSize);
            try
            {
                NetworkFrameCodec.WriteFrame(buffer.AsSpan(0, frameSize), header, payload);
                await _stream.WriteAsync(buffer, 0, frameSize, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var buffered = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var n = await _stream.ReadAsync(buffer, buffered, buffer.Length - buffered, cancellationToken);
                    if (n <= 0) break;

                    buffered += n;

                    var offset = 0;
                    while (true)
                    {
                        int totalSize;
                        NetworkPacketHeader header;
                        byte[] payload;
                        if (!NetworkFrameCodec.TryParseFrame(
                            new ReadOnlySpan<byte>(buffer, offset, buffered - offset),
                            out totalSize, out header, out payload))
                        {
                            break;
                        }

                        if (totalSize > 1024 * 1024)
                        {
                            if (OnError != null) OnError(new InvalidOperationException(string.Format("Frame too large: {0}", totalSize)));
                            break;
                        }

                        HandlePacket(header, payload);

                        offset += totalSize;
                        if (offset >= buffered) break;
                    }

                    if (offset > 0)
                    {
                        Buffer.BlockCopy(buffer, offset, buffer, 0, buffered - offset);
                        buffered -= offset;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (OnError != null) OnError(ex);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                if (OnDisconnected != null) OnDisconnected("error");
            }
        }

        private void HandlePacket(NetworkPacketHeader header, byte[] payload)
        {
            if ((header.Flags & NetworkPacketFlags.Heartbeat) != 0)
            {
                _ = SendFrameAsync(header.OpCode, header.Seq, new byte[0], false, CancellationToken.None);
                return;
            }

            if ((header.Flags & NetworkPacketFlags.Response) != 0)
            {
                if (payload.Length > 4)
                {
                    var jsonPayload = new byte[payload.Length - 4];
                    Array.Copy(payload, 4, jsonPayload, 0, jsonPayload.Length);
                    TaskCompletionSource<byte[]> tcs;
                    if (_pendingRequests.TryRemove((int)header.Seq, out tcs))
                    {
                        tcs.TrySetResult(jsonPayload);
                    }
                    if (OnResponse != null) OnResponse(jsonPayload);
                }
                else
                {
                    TaskCompletionSource<byte[]> tcs;
                    if (_pendingRequests.TryRemove((int)header.Seq, out tcs))
                    {
                        tcs.TrySetResult(new byte[0]);
                    }
                    if (OnResponse != null) OnResponse(new byte[0]);
                }
                return;
            }

            if ((header.Flags & NetworkPacketFlags.ServerPush) != 0)
            {
                if (OnServerPush != null) OnServerPush(header.OpCode, payload);
                return;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Disconnect();

            if (_cts != null) _cts.Dispose();
            if (_stream != null) _stream.Dispose();
            if (_client != null) _client.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 网络包头
    /// </summary>
    public struct NetworkPacketHeader
    {
        public const int Size = 16;

        public readonly NetworkPacketFlags Flags;
        public readonly ushort HeaderSize;
        public readonly uint OpCode;
        public readonly uint Seq;
        public readonly uint PayloadLength;

        public NetworkPacketHeader(NetworkPacketFlags flags, uint opCode, uint seq, uint payloadLength)
        {
            Flags = flags;
            HeaderSize = Size;
            OpCode = opCode;
            Seq = seq;
            PayloadLength = payloadLength;
        }
    }

    /// <summary>
    /// 网络包标志
    /// </summary>
    [Flags]
    public enum NetworkPacketFlags : ushort
    {
        None = 0,
        Compressed = 1 << 0,
        Encrypted = 1 << 1,
        Heartbeat = 1 << 2,
        Request = 1 << 3,
        Response = 1 << 4,
        ServerPush = 1 << 5
    }

    /// <summary>
    /// 网络帧编解码器
    /// </summary>
    public static class NetworkFrameCodec
    {
        public static int GetFrameSize(int payloadLength) => 4 + NetworkPacketHeader.Size + payloadLength;

        public static void WriteFrame(Span<byte> destination, NetworkPacketHeader header, ReadOnlySpan<byte> payload)
        {
            if (payload.Length != header.PayloadLength)
                throw new ArgumentException("Payload length mismatch.");

            var frameLength = NetworkPacketHeader.Size + payload.Length;
            destination.WriteUInt32LE((uint)frameLength);
            header.WriteTo(destination.Slice(4, NetworkPacketHeader.Size));
            payload.CopyTo(destination.Slice(4 + NetworkPacketHeader.Size, payload.Length));
        }

        public static bool TryParseFrame(ReadOnlySpan<byte> source, out int totalSize, out NetworkPacketHeader header, out byte[] payload)
        {
            totalSize = 0;
            payload = new byte[0];
            header = new NetworkPacketHeader();

            if (source.Length < 4 + NetworkPacketHeader.Size) return false;

            var frameLength = source.ReadUInt32LE();
            if (frameLength < NetworkPacketHeader.Size) throw new InvalidOperationException("Invalid frame length.");

            totalSize = 4 + (int)frameLength;
            if (source.Length < totalSize) return false;

            header = HeaderReader.Read(source.Slice(4, NetworkPacketHeader.Size));
            if (header.PayloadLength != frameLength - NetworkPacketHeader.Size)
                throw new InvalidOperationException("Payload length mismatch.");

            payload = new byte[header.PayloadLength];
            for (int i = 0; i < header.PayloadLength; i++)
            {
                payload[i] = source[4 + NetworkPacketHeader.Size + i];
            }
            return true;
        }

        private static uint ReadUInt32LE(this ReadOnlySpan<byte> span) =>
            (uint)(span[0] | (span[1] << 8) | (span[2] << 16) | (span[3] << 24));

        private static void WriteUInt32LE(this Span<byte> span, uint value)
        {
            span[0] = (byte)(value);
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
        }

        private static void WriteTo(this NetworkPacketHeader header, Span<byte> bytes)
        {
            BitConverter.GetBytes((ushort)header.Flags).CopyTo(bytes.Slice(0, 2));
            BitConverter.GetBytes(header.HeaderSize).CopyTo(bytes.Slice(2, 2));
            BitConverter.GetBytes(header.OpCode).CopyTo(bytes.Slice(4, 4));
            BitConverter.GetBytes(header.Seq).CopyTo(bytes.Slice(8, 4));
            BitConverter.GetBytes(header.PayloadLength).CopyTo(bytes.Slice(12, 4));
        }
    }

    internal static class HeaderReader
    {
        public static NetworkPacketHeader Read(ReadOnlySpan<byte> bytes)
        {
            var flags = (NetworkPacketFlags)BitConverter.ToUInt16(bytes.Slice(0, 2));
            var headerSize = BitConverter.ToUInt16(bytes.Slice(2, 2));
            var opCode = BitConverter.ToUInt32(bytes.Slice(4, 4));
            var seq = BitConverter.ToUInt32(bytes.Slice(8, 4));
            var payloadLength = BitConverter.ToUInt32(bytes.Slice(12, 4));

            if (headerSize != NetworkPacketHeader.Size)
                throw new InvalidOperationException(string.Format("Unsupported header size: {0}", headerSize));

            return new NetworkPacketHeader(flags, opCode, seq, payloadLength);
        }
    }
}

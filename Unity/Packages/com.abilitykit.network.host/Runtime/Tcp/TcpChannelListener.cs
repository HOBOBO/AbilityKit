using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Host.Tcp
{
    /// <summary>Official TCP listener. NetworkHost itself remains transport-neutral.</summary>
    public sealed class TcpChannelListener : IChannelListener
    {
        private readonly object _gate = new object();
        private readonly TcpChannelListenerOptions _options;
        private readonly HashSet<TcpServerChannel> _channels = new HashSet<TcpServerChannel>();
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private long _nextChannelId;

        public TcpChannelListener(TcpChannelListenerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Address == null) throw new ArgumentException("Address is required.", nameof(options));
            if (_options.Port < 0 || _options.Port > 65535) throw new ArgumentOutOfRangeException(nameof(options), "Port must be between 0 and 65535.");
            if (_options.Backlog <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Backlog must be positive.");
            if (_options.ReceiveBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(options), "ReceiveBufferSize must be positive.");
        }

        public bool IsListening { get { lock (_gate) return _listener != null; } }
        public string Endpoint { get; private set; } = string.Empty;

        public event Action<IServerChannel> ChannelAccepted;
        public event Action<Exception> Error;

        public void Start()
        {
            TcpListener listener;
            CancellationTokenSource cts;
            lock (_gate)
            {
                if (_listener != null) throw new InvalidOperationException("Listener is already running.");
                listener = new TcpListener(_options.Address, _options.Port);
                listener.Start(_options.Backlog);
                cts = new CancellationTokenSource();
                _listener = listener;
                _cts = cts;
                Endpoint = ((IPEndPoint)listener.LocalEndpoint).ToString();
            }
            _ = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
        }

        public void Stop()
        {
            TcpListener listener;
            CancellationTokenSource cts;
            TcpServerChannel[] channels;
            lock (_gate)
            {
                listener = _listener;
                cts = _cts;
                _listener = null;
                _cts = null;
                channels = new TcpServerChannel[_channels.Count];
                _channels.CopyTo(channels);
                _channels.Clear();
            }
            try { cts?.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }
            foreach (var channel in channels)
            {
                channel.Closed -= OnChannelClosed;
                channel.Dispose();
            }
            cts?.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        client.Dispose();
                        break;
                    }
                    client.NoDelay = _options.NoDelay;
                    var id = Interlocked.Increment(ref _nextChannelId).ToString();
                    var channel = new TcpServerChannel(id, client, _options.ReceiveBufferSize);
                    channel.Closed += OnChannelClosed;
                    lock (_gate) _channels.Add(channel);

                    try
                    {
                        ChannelAccepted?.Invoke(channel);
                        channel.StartReceive();
                    }
                    catch (Exception ex)
                    {
                        channel.Dispose();
                        PublishError(ex);
                    }
                }
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                PublishError(ex);
            }
        }

        private void OnChannelClosed(IServerChannel closedChannel)
        {
            lock (_gate)
            {
                if (closedChannel is TcpServerChannel tcpChannel)
                {
                    _channels.Remove(tcpChannel);
                }
            }
        }

        private void PublishError(Exception exception)
        {
            try { Error?.Invoke(exception); } catch { }
        }
    }
}

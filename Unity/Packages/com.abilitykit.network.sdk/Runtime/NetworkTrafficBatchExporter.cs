#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk.Observability
{
    public interface INetworkTrafficBatchSink
    {
        Task WriteAsync(string json, CancellationToken cancellationToken);
    }

    public sealed class NetworkTrafficBatchExporterOptions
    {
        public int QueueCapacity { get; set; } = 4096;
        public int BatchSize { get; set; } = 128;
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public NetworkTrafficExportOptions Export { get; set; } = new NetworkTrafficExportOptions
        {
            PrettyPrint = false
        };

        internal void Validate()
        {
            if (QueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
            if (BatchSize <= 0 || BatchSize > QueueCapacity)
                throw new ArgumentOutOfRangeException(nameof(BatchSize));
            if (FlushInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(FlushInterval));
            if (ShutdownTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ShutdownTimeout));
            if (Export == null) throw new InvalidOperationException("Traffic export options are required.");
        }
    }

    public readonly struct NetworkTrafficBatchExporterSnapshot
    {
        internal NetworkTrafficBatchExporterSnapshot(
            int pending,
            long accepted,
            long dropped,
            long exportedEvents,
            long exportedBatches,
            long failedEvents,
            long failedBatches)
        {
            Pending = pending;
            Accepted = accepted;
            Dropped = dropped;
            ExportedEvents = exportedEvents;
            ExportedBatches = exportedBatches;
            FailedEvents = failedEvents;
            FailedBatches = failedBatches;
        }

        public int Pending { get; }
        public long Accepted { get; }
        public long Dropped { get; }
        public long ExportedEvents { get; }
        public long ExportedBatches { get; }
        public long FailedEvents { get; }
        public long FailedBatches { get; }
    }

    /// <summary>
    /// Non-blocking observer that exports bounded, redacted JSON batches on a background task.
    /// Queue overflow evicts the oldest pending event to retain the newest diagnostic context.
    /// </summary>
    public sealed class NetworkTrafficBatchExporter : INetworkTrafficObserver, IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<NetworkTrafficEvent> _queue = new Queue<NetworkTrafficEvent>();
        private readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);
        private readonly CancellationTokenSource _forcedShutdown = new CancellationTokenSource();
        private readonly NetworkTrafficInspector _inspector;
        private readonly NetworkTrafficJsonExporter _jsonExporter = new NetworkTrafficJsonExporter();
        private readonly INetworkTrafficBatchSink _sink;
        private readonly NetworkTrafficBatchExporterOptions _options;
        private readonly Task _worker;
        private bool _accepting = true;
        private bool _disposed;
        private int _wakePending;
        private long _accepted;
        private long _dropped;
        private long _exportedEvents;
        private long _exportedBatches;
        private long _failedEvents;
        private long _failedBatches;

        public NetworkTrafficBatchExporter(
            ProtocolCatalogRegistry catalogs,
            ProtocolPayloadDecoderRegistry decoders,
            INetworkTrafficBatchSink sink,
            NetworkTrafficBatchExporterOptions? options = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _options = options ?? new NetworkTrafficBatchExporterOptions();
            _options.Validate();
            _inspector = new NetworkTrafficInspector(
                catalogs ?? throw new ArgumentNullException(nameof(catalogs)),
                decoders ?? throw new ArgumentNullException(nameof(decoders)));
            _worker = Task.Run(RunAsync);
        }

        public long DroppedCount => Interlocked.Read(ref _dropped);
        public long FailedBatchCount => Interlocked.Read(ref _failedBatches);

        public void OnTraffic(NetworkTrafficEvent trafficEvent)
        {
            if (trafficEvent == null) throw new ArgumentNullException(nameof(trafficEvent));

            var wake = false;
            lock (_gate)
            {
                if (!_accepting)
                {
                    Interlocked.Increment(ref _dropped);
                    return;
                }

                if (_queue.Count == _options.QueueCapacity)
                {
                    _queue.Dequeue();
                    Interlocked.Increment(ref _dropped);
                }
                _queue.Enqueue(trafficEvent);
                Interlocked.Increment(ref _accepted);
                wake = _queue.Count >= _options.BatchSize;
            }
            if (wake) SignalWorker();
        }

        public NetworkTrafficBatchExporterSnapshot GetSnapshot()
        {
            int pending;
            lock (_gate) pending = _queue.Count;
            return new NetworkTrafficBatchExporterSnapshot(
                pending,
                Interlocked.Read(ref _accepted),
                Interlocked.Read(ref _dropped),
                Interlocked.Read(ref _exportedEvents),
                Interlocked.Read(ref _exportedBatches),
                Interlocked.Read(ref _failedEvents),
                Interlocked.Read(ref _failedBatches));
        }

        /// <summary>Stops acceptance and drains pending batches until completion or cancellation.</summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate) _accepting = false;
            SignalWorker();

            if (_worker.IsCompleted)
            {
                await _worker.ConfigureAwait(false);
                return;
            }

            using (cancellationToken.Register(() => _forcedShutdown.Cancel()))
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    await _worker.ConfigureAwait(false);
                    return;
                }

                var canceled = Task.Delay(Timeout.Infinite, cancellationToken);
                var completed = await Task.WhenAny(_worker, canceled).ConfigureAwait(false);
                if (!ReferenceEquals(completed, _worker))
                    cancellationToken.ThrowIfCancellationRequested();
                await _worker.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }

            using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
            try { StopAsync(timeout.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }

            if (_worker.IsCompleted)
            {
                _forcedShutdown.Dispose();
                _wake.Dispose();
            }
        }

        private async Task RunAsync()
        {
            while (true)
            {
                try
                {
                    await _wake.WaitAsync(_options.FlushInterval, _forcedShutdown.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_forcedShutdown.IsCancellationRequested)
                {
                    DropPending();
                    return;
                }
                finally
                {
                    Volatile.Write(ref _wakePending, 0);
                }

                while (true)
                {
                    var batch = DrainBatch(out var stoppedAndEmpty);
                    if (batch.Count == 0)
                    {
                        if (stoppedAndEmpty) return;
                        break;
                    }

                    try
                    {
                        var rows = _inspector.Inspect(batch);
                        var json = _jsonExporter.Export(rows, _options.Export);
                        await _sink.WriteAsync(json, _forcedShutdown.Token).ConfigureAwait(false);
                        Interlocked.Add(ref _exportedEvents, batch.Count);
                        Interlocked.Increment(ref _exportedBatches);
                    }
                    catch (OperationCanceledException) when (_forcedShutdown.IsCancellationRequested)
                    {
                        Interlocked.Add(ref _failedEvents, batch.Count);
                        Interlocked.Increment(ref _failedBatches);
                        DropPending();
                        return;
                    }
                    catch
                    {
                        Interlocked.Add(ref _failedEvents, batch.Count);
                        Interlocked.Increment(ref _failedBatches);
                    }

                    if (batch.Count < _options.BatchSize)
                    {
                        lock (_gate)
                        {
                            if (_accepting) break;
                        }
                    }
                }
            }
        }

        private IReadOnlyList<NetworkTrafficEvent> DrainBatch(out bool stoppedAndEmpty)
        {
            lock (_gate)
            {
                stoppedAndEmpty = !_accepting && _queue.Count == 0;
                if (_queue.Count == 0) return Array.Empty<NetworkTrafficEvent>();

                var count = Math.Min(_options.BatchSize, _queue.Count);
                var batch = new List<NetworkTrafficEvent>(count);
                for (var i = 0; i < count; i++) batch.Add(_queue.Dequeue());
                stoppedAndEmpty = false;
                return batch;
            }
        }

        private void DropPending()
        {
            int count;
            lock (_gate)
            {
                count = _queue.Count;
                _queue.Clear();
            }
            if (count > 0) Interlocked.Add(ref _dropped, count);
        }

        private void SignalWorker()
        {
            if (Interlocked.Exchange(ref _wakePending, 1) != 0) return;
            try { _wake.Release(); }
            catch (ObjectDisposedException) { }
        }
    }
}

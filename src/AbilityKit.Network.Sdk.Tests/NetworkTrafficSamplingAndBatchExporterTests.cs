using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using System.Diagnostics;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkTrafficSamplingAndBatchExporterTests
{
    [Fact]
    public void Sampler_UsesCatalogRatesDeterministicallyAndRetainsUnknownPackets()
    {
        var catalogs = CreateCatalogs();
        var metrics = new NetworkTrafficSamplingMetrics();
        var context = new NetworkTrafficConnectionContext(
            "battle-primary", 3, "battle", "project.battle", "host:1", "tcp");
        var first = NetworkTrafficCatalogSampler.CreateFilter(context, catalogs, metrics: metrics);
        var second = NetworkTrafficCatalogSampler.CreateFilter(context, catalogs, metrics: metrics);

        Assert.False(first(NetworkTrafficDirection.Outbound, Header(10, 1)));
        Assert.True(first(NetworkTrafficDirection.Outbound, Header(11, 1)));
        Assert.True(first(NetworkTrafficDirection.Outbound, Header(999, 1)));

        var captured = 0;
        for (uint sequence = 1; sequence <= 1000; sequence++)
        {
            var header = Header(12, sequence);
            var left = first(NetworkTrafficDirection.Outbound, header);
            var right = second(NetworkTrafficDirection.Outbound, header);
            Assert.Equal(left, right);
            if (left) captured++;
        }

        Assert.InRange(captured, 400, 600);
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(2003, snapshot.Examined);
        Assert.Equal(1, snapshot.SampledOut - (2000 - (captured * 2)));
        Assert.Equal(1, snapshot.Unresolved);
    }

    [Fact]
    public void BatchExporter_DoesNotBlockProducerAndMeasuresQueueOverflow()
    {
        var sink = new BlockingSink();
        var exporter = CreateExporter(sink, capacity: 2, batchSize: 1);
        var probe = CreateProbe(exporter);

        Emit(probe, 1);
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(5)));

        Emit(probe, 2);
        Emit(probe, 3);
        Emit(probe, 4);
        var blocked = exporter.GetSnapshot();
        Assert.Equal(4, blocked.Accepted);
        Assert.Equal(1, blocked.Dropped);
        Assert.Equal(2, blocked.Pending);

        sink.Release.Set();
        exporter.Dispose();

        var completed = exporter.GetSnapshot();
        Assert.Equal(0, completed.Pending);
        Assert.Equal(3, completed.ExportedEvents);
        Assert.Equal(3, completed.ExportedBatches);
        var exported = string.Concat(sink.Documents);
        Assert.DoesNotContain("\"sequence\":2", exported);
        Assert.Contains("\"sequence\":3", exported);
        Assert.Contains("\"sequence\":4", exported);
    }

    [Fact]
    public void BatchExporter_DisposeFlushesPartialBatch()
    {
        var sink = new RecordingSink();
        var exporter = CreateExporter(sink, capacity: 10, batchSize: 10);
        var probe = CreateProbe(exporter);
        Emit(probe, 1);
        Emit(probe, 2);

        exporter.Dispose();

        var snapshot = exporter.GetSnapshot();
        Assert.Equal(2, snapshot.ExportedEvents);
        Assert.Equal(1, snapshot.ExportedBatches);
        Assert.Single(sink.Documents);
        Assert.Contains("\"eventCount\":2", sink.Documents[0]);
    }

    [Fact]
    public void BatchExporter_ContainsSinkFailureAndContinuesWithNextBatch()
    {
        var sink = new FailFirstSink();
        var exporter = CreateExporter(sink, capacity: 10, batchSize: 1);
        var probe = CreateProbe(exporter);

        Emit(probe, 1);
        Assert.True(sink.FirstAttempted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(SpinWait.SpinUntil(
            () => exporter.FailedBatchCount == 1,
            TimeSpan.FromSeconds(5)));

        Emit(probe, 2);
        exporter.Dispose();

        var snapshot = exporter.GetSnapshot();
        Assert.Equal(1, snapshot.FailedBatches);
        Assert.Equal(1, snapshot.FailedEvents);
        Assert.Equal(1, snapshot.ExportedBatches);
        Assert.Equal(1, snapshot.ExportedEvents);
        Assert.Single(sink.Documents);
        Assert.Contains("\"sequence\":2", sink.Documents[0]);
    }

    [Fact]
    public async Task BatchExporter_StopAsyncCancellationStopsResponsiveSinkWithoutHanging()
    {
        var sink = new CancellationAwareBlockingSink();
        var exporter = CreateExporter(sink, capacity: 10, batchSize: 1);
        var probe = CreateProbe(exporter);
        Emit(probe, 1);
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(5)));
        Emit(probe, 2);
        Emit(probe, 3);

        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => exporter.StopAsync(deadline.Token));
        elapsed.Stop();

        await exporter.StopAsync();
        exporter.Dispose();

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3));
        var snapshot = exporter.GetSnapshot();
        Assert.Equal(0, snapshot.Pending);
        Assert.Equal(1, snapshot.FailedBatches);
        Assert.Equal(1, snapshot.FailedEvents);
        Assert.Equal(2, snapshot.Dropped);
        Assert.Equal(0, snapshot.ExportedEvents);
        Assert.Equal(
            snapshot.Accepted,
            snapshot.ExportedEvents + snapshot.FailedEvents + snapshot.Dropped);
    }

    [Fact]
    public async Task BatchExporter_StopAndDisposeAreIdempotent()
    {
        var sink = new RecordingSink();
        var exporter = CreateExporter(sink, capacity: 10, batchSize: 10);
        Emit(CreateProbe(exporter), 1);

        await exporter.StopAsync();
        await exporter.StopAsync();
        exporter.Dispose();
        exporter.Dispose();
        await exporter.StopAsync();

        var snapshot = exporter.GetSnapshot();
        Assert.Equal(1, snapshot.ExportedEvents);
        Assert.Equal(1, snapshot.ExportedBatches);
        Assert.Equal(0, snapshot.Pending);
    }

    [Fact]
    public void ObserverFanOut_ContainsOneObserverFailureAndContinues()
    {
        var recorded = new NetworkTrafficRingBuffer(2);
        var errors = 0;
        var fanOut = new NetworkTrafficObserverFanOut(
            new INetworkTrafficObserver[] { new ThrowingObserver(), recorded },
            _ => errors++);

        Emit(CreateProbe(fanOut), 1);

        Assert.Equal(1, errors);
        Assert.Equal(1, recorded.Count);
    }

    private static NetworkTrafficBatchExporter CreateExporter(
        INetworkTrafficBatchSink sink,
        int capacity,
        int batchSize) => new NetworkTrafficBatchExporter(
        CreateCatalogs(),
        new ProtocolPayloadDecoderRegistry(),
        sink,
        new NetworkTrafficBatchExporterOptions
        {
            QueueCapacity = capacity,
            BatchSize = batchSize,
            FlushInterval = TimeSpan.FromMinutes(1),
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        });

    private static ProtocolCatalogRegistry CreateCatalogs()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project.battle", "project", "battle", 1, "memorypack",
            new[]
            {
                Message("never.request", 10, 0d),
                Message("always.request", 11, 1d),
                Message("half.request", 12, 0.5d)
            }));
        return catalogs;
    }

    private static ProtocolMessageDefinition Message(string id, uint opCode, double rate) => new(
        id,
        opCode,
        ProtocolDirection.ClientToServer,
        ProtocolPacketKind.Request,
        "Payload",
        "memorypack",
        captureSampleRate: rate);

    private static NetworkPacketHeader Header(uint opCode, uint sequence) =>
        new NetworkPacketHeader(NetworkPacketFlags.Request, opCode, sequence, 0);

    private static NetworkTrafficProbeMiddleware CreateProbe(INetworkTrafficObserver observer) => new(
        new NetworkTrafficConnectionContext(
            "battle-primary", 1, "battle", "project.battle", "host:1", "tcp"),
        observer,
        maximumPayloadPreviewBytes: 8);

    private static void Emit(NetworkTrafficProbeMiddleware probe, uint sequence) =>
        probe.OnOutbound(
            null!,
            Header(11, sequence),
            new ArraySegment<byte>(new byte[] { 7 }),
            static (_, _) => { });

    private sealed class BlockingSink : INetworkTrafficBatchSink
    {
        public ManualResetEventSlim Started { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public List<string> Documents { get; } = new();

        public Task WriteAsync(string json, CancellationToken cancellationToken)
        {
            Started.Set();
            Release.Wait(cancellationToken);
            Documents.Add(json);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSink : INetworkTrafficBatchSink
    {
        public List<string> Documents { get; } = new();

        public Task WriteAsync(string json, CancellationToken cancellationToken)
        {
            Documents.Add(json);
            return Task.CompletedTask;
        }
    }

    private sealed class FailFirstSink : INetworkTrafficBatchSink
    {
        private int _attempts;

        public ManualResetEventSlim FirstAttempted { get; } = new(false);
        public List<string> Documents { get; } = new();

        public Task WriteAsync(string json, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                FirstAttempted.Set();
                return Task.FromException(new InvalidOperationException("expected"));
            }

            Documents.Add(json);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellationAwareBlockingSink : INetworkTrafficBatchSink
    {
        public ManualResetEventSlim Started { get; } = new(false);

        public Task WriteAsync(string json, CancellationToken cancellationToken)
        {
            Started.Set();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class ThrowingObserver : INetworkTrafficObserver
    {
        public void OnTraffic(NetworkTrafficEvent trafficEvent) =>
            throw new InvalidOperationException("expected");
    }
}

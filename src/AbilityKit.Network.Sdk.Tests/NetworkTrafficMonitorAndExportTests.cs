using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkTrafficMonitorAndExportTests
{
    [Fact]
    public void Monitor_BoundsMultiConnectionTrafficAndInspectsSnapshot()
    {
        var catalogs = CreateCatalogs();
        var monitor = new NetworkTrafficMonitor(
            capacity: 2,
            catalogs,
            new ProtocolPayloadDecoderRegistry());
        var roomProbe = CreateProbe(monitor, "room", "project.room");
        var battleProbe = CreateProbe(monitor, "battle", "project.room");

        Emit(roomProbe, 1);
        Emit(battleProbe, 2);
        Emit(roomProbe, 3);

        Assert.Equal(2, monitor.Count);
        Assert.Equal(1, monitor.DroppedCount);
        var rows = monitor.Inspect();
        Assert.Equal(new uint[] { 2, 3 }, rows.Select(row => row.Traffic.Sequence));
        Assert.Equal(new[] { "battle", "room" }, rows.Select(row => row.Traffic.Role));
        Assert.All(rows, row => Assert.True(row.IsKnown));
    }

    [Fact]
    public void Export_RedactsSensitiveFieldsRecursivelyAndOmitsRawPreviewByDefault()
    {
        var catalogs = CreateCatalogs();
        var decoders = new ProtocolPayloadDecoderRegistry();
        decoders.Register("project.room", "login.request", _ => new LoginPayload
        {
            UserName = "alice",
            SessionToken = "top-secret",
            Nested = new NestedPayload { sessionToken = "nested-secret", Region = "cn" }
        });
        var monitor = new NetworkTrafficMonitor(4, catalogs, decoders);
        Emit(CreateProbe(monitor, "room", "project.room"), 1);

        var json = new NetworkTrafficJsonExporter().Export(monitor.Inspect());

        Assert.Contains("alice", json);
        Assert.Contains("[REDACTED]", json);
        Assert.DoesNotContain("top-secret", json);
        Assert.DoesNotContain("nested-secret", json);
        Assert.DoesNotContain("payloadPreviewBase64", json);
    }

    [Fact]
    public void Export_RejectsSensitiveRawPreviewUnlessExplicitlyAllowed()
    {
        var monitor = new NetworkTrafficMonitor(
            4,
            CreateCatalogs(),
            new ProtocolPayloadDecoderRegistry());
        Emit(CreateProbe(monitor, "room", "project.room"), 1);
        var exporter = new NetworkTrafficJsonExporter();

        Assert.Throws<InvalidOperationException>(() => exporter.Export(
            monitor.Inspect(),
            new NetworkTrafficExportOptions { IncludeRawPayloadPreview = true }));

        var json = exporter.Export(
            monitor.Inspect(),
            new NetworkTrafficExportOptions
            {
                IncludeRawPayloadPreview = true,
                AllowSensitiveRawPayloadPreview = true
            });
        Assert.Contains("payloadPreviewBase64", json);
    }

    private static ProtocolCatalogRegistry CreateCatalogs()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project.room", "project", "room", 1, "memorypack",
            new[]
            {
                new ProtocolMessageDefinition(
                    "login.request", 10, ProtocolDirection.ClientToServer,
                    ProtocolPacketKind.Request, "LoginPayload", "memorypack",
                    sensitiveFields: new[] { "sessionToken" })
            }));
        return catalogs;
    }

    private static NetworkTrafficProbeMiddleware CreateProbe(
        INetworkTrafficObserver observer,
        string role,
        string catalogId) => new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext(role, 1, role, catalogId, "host:1", "tcp"),
            observer,
            maximumPayloadPreviewBytes: 8);

    private static void Emit(NetworkTrafficProbeMiddleware probe, uint sequence) =>
        probe.OnOutbound(
            null!,
            new NetworkPacketHeader(NetworkPacketFlags.Request, 10, sequence, 1),
            new ArraySegment<byte>(new byte[] { 7 }),
            static (_, _) => { });

    private sealed class LoginPayload
    {
        public string UserName { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public NestedPayload? Nested { get; set; }
    }

    private sealed class NestedPayload
    {
        public string sessionToken = string.Empty;
        public string Region = string.Empty;
    }
}

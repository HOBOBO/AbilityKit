using System.Text.Json;
using Xunit;

public sealed class ShooterSmokeSoakTelemetryTests
{
    [Fact]
    public void RecoveryCompletionRequiresNewBaselineAndComparableMatchingHashWithoutDeliverySample()
    {
        using var scope = new TemporaryDirectory();
        var controlPath = Path.Combine(scope.Path, "control.json");
        var metricsPath = Path.Combine(scope.Path, "metrics.jsonl");
        File.WriteAllText(
            controlPath,
            JsonSerializer.Serialize(
                new ShooterSmokeNetworkConditionCommand
                {
                    Id = "recovery-1",
                    Phase = "recovery-ideal",
                    ExpectRecovery = true
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        using var channel = new SmokeTcpGameFrameworkNetworkChannel("soak-telemetry-test");
        using (var telemetry = new ShooterSmokeSoakTelemetry(
            "client-1",
            scope.Path,
            controlPath,
            metricsPath))
        {
            Assert.True(telemetry.TryApplyCommand(channel, fullBaselinesApplied: 7, resyncRequests: 3));
            Assert.False(telemetry.TryCompleteRecovery(7, 3, comparableFrame: 101, comparableHashMatched: true));
            Assert.False(telemetry.TryCompleteRecovery(8, 3, comparableFrame: 0, comparableHashMatched: true));
            Assert.False(telemetry.TryCompleteRecovery(8, 3, comparableFrame: 101, comparableHashMatched: false));
            Assert.True(telemetry.TryCompleteRecovery(8, 5, comparableFrame: 101, comparableHashMatched: true));
            Assert.False(telemetry.TryCompleteRecovery(9, 5, comparableFrame: 102, comparableHashMatched: true));
        }

        var events = File.ReadAllLines(metricsPath)
            .Select(static line => JsonSerializer.Deserialize<ShooterSmokeSoakEvent>(
                line,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("network-condition-applied", events[0].Type);
        Assert.Equal("recovery-completed", events[1].Type);
        Assert.DoesNotContain(events, static item => item.Type == "delivery-sample");

        var recovery = Assert.IsType<ShooterSmokeRecoverySample>(events[1].Recovery);
        Assert.Equal(1, recovery.FullBaselinesAppliedDelta);
        Assert.Equal(2, recovery.ResyncRequestsDelta);
        Assert.Equal(101, recovery.ComparableFrame);
        Assert.True(recovery.CompletedAtUtc >= recovery.StartedAtUtc);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "abilitykit-shooter-soak-telemetry",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

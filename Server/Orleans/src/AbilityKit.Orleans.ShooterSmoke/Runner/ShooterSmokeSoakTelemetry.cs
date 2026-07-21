using System.Text.Json;
using AbilityKit.Protocol.Room;

internal sealed class ShooterSmokeSoakTelemetry : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _clientId;
    private readonly string _controlPath;
    private readonly string _metricsOutputPath;
    private readonly StreamWriter? _metricsWriter;
    private readonly object _writeSyncRoot = new();
    private readonly CancellationTokenSource _commandPollingCts = new();
    private Task? _commandPollingTask;
    private string _lastCommandId = string.Empty;
    private PendingRecovery? _pendingRecovery;
    private int _recoveryBaselineRequestPending;

    public ShooterSmokeSoakTelemetry(
        string clientId,
        string runRootPath,
        string controlPath,
        string metricsOutputPath)
    {
        _clientId = clientId;
        _controlPath = ResolveUnderRunRoot(runRootPath, controlPath);
        _metricsOutputPath = ResolveUnderRunRoot(runRootPath, metricsOutputPath);
        if (!string.IsNullOrWhiteSpace(_metricsOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_metricsOutputPath)!);
            _metricsWriter = new StreamWriter(
                new FileStream(_metricsOutputPath, FileMode.Append, FileAccess.Write, FileShare.Read),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
        }
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_controlPath)
        || !string.IsNullOrWhiteSpace(_metricsOutputPath);

    public string MetricsOutputPath => _metricsOutputPath;

    public void StartCommandPolling(
        SmokeTcpGameFrameworkNetworkChannel channel,
        Func<int> getFullBaselinesApplied,
        Func<int> getResyncRequests)
    {
        if (_commandPollingTask != null || string.IsNullOrWhiteSpace(_controlPath))
        {
            return;
        }

        _commandPollingTask = Task.Run(async () =>
        {
            while (!_commandPollingCts.IsCancellationRequested)
            {
                try
                {
                    TryApplyCommand(
                        channel,
                        getFullBaselinesApplied(),
                        getResyncRequests());
                    await Task.Delay(10, _commandPollingCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_commandPollingCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(50, _commandPollingCts.Token).ConfigureAwait(false);
                }
            }
        }, _commandPollingCts.Token);
    }

    public bool TryApplyCommand(
        SmokeTcpGameFrameworkNetworkChannel channel,
        int fullBaselinesApplied,
        int resyncRequests)
    {
        if (string.IsNullOrWhiteSpace(_controlPath) || !File.Exists(_controlPath))
        {
            return false;
        }

        ShooterSmokeNetworkConditionCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<ShooterSmokeNetworkConditionCommand>(
                File.ReadAllText(_controlPath),
                JsonOptions);
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }

        if (command == null
            || string.IsNullOrWhiteSpace(command.Id)
            || string.Equals(command.Id, _lastCommandId, StringComparison.Ordinal))
        {
            return false;
        }

        var condition = new SmokeNetworkConditionOptions(
            command.InboundLatencyMs,
            command.InboundJitterMs,
            command.InboundPacketLossRate,
            command.Seed,
            command.InboundBandwidthBytesPerSecond).Normalize();
        channel.UpdateNetworkCondition(condition);
        _lastCommandId = command.Id;
        var appliedAtUtc = DateTime.UtcNow;
        if (command.ExpectRecovery)
        {
            _pendingRecovery = new PendingRecovery(
                command.Id,
                command.Phase,
                appliedAtUtc,
                fullBaselinesApplied,
                resyncRequests);
            Volatile.Write(ref _recoveryBaselineRequestPending, 1);
        }
        WriteEvent(new ShooterSmokeSoakEvent
        {
            Type = "network-condition-applied",
            TimestampUtc = appliedAtUtc,
            ClientId = _clientId,
            CommandId = command.Id,
            Phase = command.Phase,
            NetworkCondition = condition
        });
        WriteAcknowledgement(command, condition, appliedAtUtc);
        return true;
    }

    public bool ConsumeRecoveryBaselineRequest()
    {
        return Interlocked.Exchange(ref _recoveryBaselineRequestPending, 0) == 1;
    }

    public void WriteDeliverySample(
        WireGetStateSyncDeliveryMetricsRes metrics,
        SmokeTcpGameFrameworkNetworkChannel channel,
        int snapshotPushes,
        int fullBaselinesApplied,
        int resyncRequests,
        int comparableFrame,
        bool comparableHashMatched)
    {
        var timestampUtc = DateTime.UtcNow;
        WriteEvent(new ShooterSmokeSoakEvent
        {
            Type = "delivery-sample",
            TimestampUtc = timestampUtc,
            ClientId = _clientId,
            CommandId = _lastCommandId,
            NetworkCondition = channel.NetworkCondition,
            Delivery = new ShooterSmokeDeliverySample
            {
                ProducedBytes = metrics.ProducedBytes,
                SentBytes = metrics.SentBytes,
                DroppedBytes = metrics.DroppedBytes,
                MergedBytes = metrics.MergedBytes,
                QueueLength = metrics.QueueLength,
                QueueAgeTicks = metrics.QueueAgeTicks,
                BaselineAgeTicks = metrics.BaselineAgeTicks,
                ResyncCount = metrics.ResyncCount,
                SnapshotPushes = snapshotPushes,
                FullBaselinesApplied = fullBaselinesApplied,
                ClientResyncRequests = resyncRequests,
                ComparableFrame = comparableFrame,
                ComparableHashMatched = comparableHashMatched
            }
        });

        var pending = _pendingRecovery;
        if (pending == null
            || fullBaselinesApplied <= pending.FullBaselinesAppliedAtStart
            || comparableFrame <= 0
            || !comparableHashMatched)
        {
            return;
        }

        WriteEvent(new ShooterSmokeSoakEvent
        {
            Type = "recovery-completed",
            TimestampUtc = timestampUtc,
            ClientId = _clientId,
            CommandId = pending.CommandId,
            Phase = pending.Phase,
            Recovery = new ShooterSmokeRecoverySample
            {
                StartedAtUtc = pending.StartedAtUtc,
                CompletedAtUtc = timestampUtc,
                DurationMs = (timestampUtc - pending.StartedAtUtc).TotalMilliseconds,
                FullBaselinesAppliedDelta = fullBaselinesApplied - pending.FullBaselinesAppliedAtStart,
                ResyncRequestsDelta = resyncRequests - pending.ResyncRequestsAtStart,
                ComparableFrame = comparableFrame
            }
        });
        _pendingRecovery = null;
    }

    public void Dispose()
    {
        _commandPollingCts.Cancel();
        try
        {
            _commandPollingTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
        }

        _metricsWriter?.Dispose();
        _commandPollingCts.Dispose();
    }

    private void WriteEvent(ShooterSmokeSoakEvent value)
    {
        if (_metricsWriter == null)
        {
            return;
        }

        lock (_writeSyncRoot)
        {
            _metricsWriter.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        }
    }

    private void WriteAcknowledgement(
        ShooterSmokeNetworkConditionCommand command,
        SmokeNetworkConditionOptions condition,
        DateTime appliedAtUtc)
    {
        var acknowledgementPath = _controlPath + ".ack.json";
        var temporaryPath = acknowledgementPath + $".{Environment.ProcessId}.tmp";
        var acknowledgement = new ShooterSmokeNetworkConditionAcknowledgement
        {
            Id = command.Id,
            ClientId = _clientId,
            Phase = command.Phase,
            AppliedAtUtc = appliedAtUtc,
            NetworkCondition = condition
        };
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(acknowledgement, JsonOptions));
        File.Move(temporaryPath, acknowledgementPath, overwrite: true);
    }

    private static string ResolveUnderRunRoot(string runRootPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(runRootPath))
        {
            return fullPath;
        }

        var root = Path.GetFullPath(runRootPath);
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Shooter soak path must be under run root. Path={fullPath}, RunRoot={root}");
        }

        return fullPath;
    }

    private sealed record PendingRecovery(
        string CommandId,
        string Phase,
        DateTime StartedAtUtc,
        int FullBaselinesAppliedAtStart,
        int ResyncRequestsAtStart);
}

internal sealed class ShooterSmokeNetworkConditionCommand
{
    public string Id { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int InboundLatencyMs { get; set; }
    public int InboundJitterMs { get; set; }
    public double InboundPacketLossRate { get; set; }
    public int InboundBandwidthBytesPerSecond { get; set; }
    public int Seed { get; set; }
    public bool ExpectRecovery { get; set; }
}

internal sealed class ShooterSmokeNetworkConditionAcknowledgement
{
    public string Id { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public SmokeNetworkConditionOptions NetworkCondition { get; set; }
}

internal sealed class ShooterSmokeSoakEvent
{
    public string Type { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string CommandId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public SmokeNetworkConditionOptions? NetworkCondition { get; set; }
    public ShooterSmokeDeliverySample? Delivery { get; set; }
    public ShooterSmokeRecoverySample? Recovery { get; set; }
}

internal sealed class ShooterSmokeDeliverySample
{
    public long ProducedBytes { get; set; }
    public long SentBytes { get; set; }
    public long DroppedBytes { get; set; }
    public long MergedBytes { get; set; }
    public int QueueLength { get; set; }
    public long QueueAgeTicks { get; set; }
    public long BaselineAgeTicks { get; set; }
    public long ResyncCount { get; set; }
    public int SnapshotPushes { get; set; }
    public int FullBaselinesApplied { get; set; }
    public int ClientResyncRequests { get; set; }
    public int ComparableFrame { get; set; }
    public bool ComparableHashMatched { get; set; }
}

internal sealed class ShooterSmokeRecoverySample
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public double DurationMs { get; set; }
    public int FullBaselinesAppliedDelta { get; set; }
    public int ResyncRequestsDelta { get; set; }
    public int ComparableFrame { get; set; }
}

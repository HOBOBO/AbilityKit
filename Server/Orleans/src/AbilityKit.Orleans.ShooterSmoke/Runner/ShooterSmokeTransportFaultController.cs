extern alias Gateway;

using System.Text.Json;
using AbilityKit.Orleans.Contracts.Battle;
using Orleans;
using GatewayNetworking = Gateway::AbilityKit.Orleans.Gateway.Networking;

internal sealed class ShooterSmokeTransportFaultController : IAsyncDisposable
{
    private readonly GatewayNetworking.TcpTransportServer _transportServer;
    private readonly IClusterClient _clusterClient;
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _transportCancellation;
    private Task? _transportTask;

    public ShooterSmokeTransportFaultController(
        GatewayNetworking.TcpTransportServer transportServer,
        IClusterClient clusterClient,
        string host,
        int port)
    {
        _transportServer = transportServer;
        _clusterClient = clusterClient;
        _host = host;
        _port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_transportTask is { IsCompleted: false })
            {
                return;
            }

            _transportCancellation?.Dispose();
            _transportCancellation = new CancellationTokenSource();
            _transportTask = _transportServer.StartAsync(_transportCancellation.Token);
        }
        finally
        {
            _gate.Release();
        }

        await ShooterSmokeScenarioBase.WaitForTcpAsync(
            _host,
            _port,
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _transportCancellation?.Cancel();
            await _transportServer.StopAsync(cancellationToken).ConfigureAwait(false);
            if (_transportTask is not null)
            {
                await AwaitTransportShutdownAsync(_transportTask).ConfigureAwait(false);
            }

            _transportTask = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RunFileControlAsync(string commandPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);
        var fullCommandPath = Path.GetFullPath(commandPath);
        var acknowledgementPath = fullCommandPath + ".ack.json";
        string? lastCommandId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(fullCommandPath))
            {
                ShooterSmokeFaultCommand? command = null;
                try
                {
                    await using var stream = new FileStream(
                        fullCommandPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    command = await JsonSerializer.DeserializeAsync<ShooterSmokeFaultCommand>(
                        stream,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }

                if (command is not null &&
                    !string.IsNullOrWhiteSpace(command.Id) &&
                    !string.Equals(command.Id, lastCommandId, StringComparison.Ordinal))
                {
                    lastCommandId = command.Id;
                    var receivedAtUtc = DateTime.UtcNow;
                    var status = "completed";
                    string? error = null;
                    StateSyncObserverReactivationEvidence? observerReactivation = null;
                    try
                    {
                        switch (command.Action.Trim().ToLowerInvariant())
                        {
                            case "gateway-offline":
                                await StopAsync(cancellationToken).ConfigureAwait(false);
                                break;
                            case "gateway-online":
                                await StartAsync(cancellationToken).ConfigureAwait(false);
                                break;
                            case "observer-reactivate":
                                observerReactivation = await ReactivateObserverAsync(
                                    command.ObserverKey,
                                    cancellationToken).ConfigureAwait(false);
                                break;
                            case "shutdown-control":
                                await WriteAcknowledgementAsync(
                                    acknowledgementPath,
                                    new ShooterSmokeFaultAcknowledgement(
                                        command.Id,
                                        command.Action,
                                        status,
                                        receivedAtUtc,
                                        DateTime.UtcNow,
                                        null,
                                        null),
                                    cancellationToken).ConfigureAwait(false);
                                return;
                            default:
                                throw new InvalidOperationException($"Unsupported fault command: {command.Action}");
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        status = "failed";
                        error = ex.Message;
                    }

                    await WriteAcknowledgementAsync(
                        acknowledgementPath,
                        new ShooterSmokeFaultAcknowledgement(
                            command.Id,
                            command.Action,
                            status,
                            receivedAtUtc,
                            DateTime.UtcNow,
                            error,
                            observerReactivation),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<StateSyncObserverReactivationEvidence> ReactivateObserverAsync(
        string? observerKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observerKey);
        var observer = _clusterClient.GetGrain<IStateSyncObserverLifecycleGrain>(observerKey);
        var before = await observer.GetActivationInfoAsync().ConfigureAwait(false);
        var requested = await observer.RequestDeactivationAsync().ConfigureAwait(false);
        if (!string.Equals(before.ActivationToken, requested.ActivationToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Observer activation changed before deactivation was requested. ObserverKey={observerKey}");
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            var current = await observer.GetActivationInfoAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(current.ActivationToken)
                && !string.Equals(current.ActivationToken, before.ActivationToken, StringComparison.Ordinal))
            {
                return new StateSyncObserverReactivationEvidence(
                    observerKey,
                    before.ActivationToken,
                    current.ActivationToken,
                    before.ActivatedAtUtcTicks,
                    current.ActivatedAtUtcTicks);
            }
        }

        throw new TimeoutException(
            $"Observer did not reactivate with a new activation token. ObserverKey={observerKey}, BeforeToken={before.ActivationToken}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _transportCancellation?.Dispose();
        _gate.Dispose();
    }

    private static async Task AwaitTransportShutdownAsync(Task transportTask)
    {
        try
        {
            await transportTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task WriteAcknowledgementAsync(
        string path,
        ShooterSmokeFaultAcknowledgement acknowledgement,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(acknowledgement, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }
}

internal sealed record ShooterSmokeFaultCommand(
    string Id,
    string Action,
    DateTime RequestedAtUtc,
    string? ObserverKey = null);

internal sealed record ShooterSmokeFaultAcknowledgement(
    string Id,
    string Action,
    string Status,
    DateTime ReceivedAtUtc,
    DateTime CompletedAtUtc,
    string? Error,
    StateSyncObserverReactivationEvidence? ObserverReactivation);

internal sealed record StateSyncObserverReactivationEvidence(
    string ObserverKey,
    string BeforeActivationToken,
    string AfterActivationToken,
    long BeforeActivatedAtUtcTicks,
    long AfterActivatedAtUtcTicks);

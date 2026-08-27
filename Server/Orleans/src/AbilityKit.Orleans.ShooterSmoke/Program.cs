extern alias Gateway;

using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using GatewayNetworking = Gateway::AbilityKit.Orleans.Gateway.Networking;

const string ShooterStateSyncPayloadModeEnvironmentVariable = "ABILITYKIT_SHOOTER_STATE_SYNC_PAYLOAD_MODE";

var options = ShooterSmokeProgramOptions.Parse(args);
var tcpGatewayHost = options.Host;

if (!options.ClientMode)
{
    Environment.SetEnvironmentVariable(
        ShooterStateSyncPayloadModeEnvironmentVariable,
        string.Equals(options.StateSyncPayloadMode, "template", StringComparison.OrdinalIgnoreCase)
            ? null
            : options.StateSyncPayloadMode);
}

if (options.ClientMode)
{
    var clientOptions = options.ToClientProcessOptions();

    try
    {
        var result = await ShooterSmokeClientProcessRunner.RunAsync(clientOptions);
        Console.WriteLine(ShooterSmokeClientProcessRunner.FormatResult(result));
    }
    catch (Exception ex)
    {
        Console.WriteLine(ShooterSmokeClientProcessRunner.FormatFailure(clientOptions, ex));
        Environment.ExitCode = 1;
    }

    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAbilityKitServerOptions(builder.Configuration);
builder.Services.AddStateSyncObserverOptions(builder.Configuration);
builder.Services.AddBattleInputSecurityOptions(builder.Configuration);
builder.Logging.AddAbilityKitServerLogging(builder.Configuration, "AbilityKit.Orleans.ShooterSmoke");

var storageOptions = builder.Configuration.GetAbilityKitStorageOptions();
builder.Services.AddAbilityKitGrainStateStorage(
    storageOptions.SessionStateProvider,
    storageOptions.RoomStateProvider,
    storageOptions.AllowInMemoryFallbackForUnsupportedProviders);

builder.Services.AddSingleton<ServerBattleWorldManager>(sp =>
    new ServerBattleWorldManager(sp.GetRequiredService<ILogger<ServerBattleWorldManager>>()));
builder.Services.AddShooterSmokeGateway(options.TcpGatewayPort);

builder.UseAbilityKitLocalOrleansSilo();

using var host = builder.Build();
await host.StartAsync();

var transportServer = host.Services.GetRequiredService<GatewayNetworking.TcpTransportServer>();
var clusterClient = host.Services.GetRequiredService<IClusterClient>();
await using var transportController = new ShooterSmokeTransportFaultController(
    transportServer,
    clusterClient,
    tcpGatewayHost,
    options.TcpGatewayPort);
await transportController.StartAsync();
using var faultControlCancellation = new CancellationTokenSource();
var faultControlTask = string.IsNullOrWhiteSpace(options.FaultControlPath)
    ? Task.CompletedTask
    : transportController.RunFileControlAsync(options.FaultControlPath, faultControlCancellation.Token);

try
{
    if (options.ServerMode)
    {
        Console.WriteLine($"Shooter state-sync server listening on {tcpGatewayHost}:{options.TcpGatewayPort}.");
        Console.WriteLine("Press Ctrl+C to stop.");
        await WaitForShutdownAsync();
    }
    else
    {
        var result = await ShooterSmokeRunner.RunAsync(clusterClient, tcpGatewayHost, options.TcpGatewayPort, options.InputLogicReplayOutputPath);
        Console.WriteLine(ShooterSmokeResultFormatter.FormatPassed(result));
    }
}
finally
{
    faultControlCancellation.Cancel();
    try
    {
        await faultControlTask;
    }
    catch (OperationCanceledException)
    {
    }

    await transportController.StopAsync();
    await host.StopAsync();
}

static Task WaitForShutdownAsync()
{
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        completion.TrySetResult();
    };

    AppDomain.CurrentDomain.ProcessExit += (_, _) => completion.TrySetResult();
    return completion.Task;
}

readonly record struct ShooterSmokeProgramOptions(
    bool ServerMode,
    bool ClientMode,
    ShooterSmokeClientProcessMode ClientProcessMode,
    string Host,
    int TcpGatewayPort,
    string RoomId,
    uint PlayerId,
    string ClientId,
    int RoomMaxPlayers,
    int BattleDurationFrames,
    int BattleVictoryTargetDefeats,
    bool ContinueAfterAllPlayersDefeated,
    int InputCount,
    int Seed,
    TimeSpan Timeout,
    bool WaitForMatchEnd,
    int ReconnectCount,
    int ReconnectDelayMs,
    int RecoverableFailureCount,
    int RetryBackoffMaxMs,
    SmokeNetworkConditionOptions NetworkCondition,
    string StateSyncPayloadMode,
    string InputStateReplayOutputPath,
    string InputLogicReplayOutputPath,
    string RunId,
    string CorrelationId,
    string RunRootPath,
    string DiagnosticOutputPath,
    string FaultControlPath,
    string ReconnectReleasePath,
    string CompletionReleasePath,
    string NetworkControlPath,
    string MetricsOutputPath,
    int MetricsSampleIntervalMs)
{
    public ShooterSmokeClientProcessOptions ToClientProcessOptions()
    {
        return new ShooterSmokeClientProcessOptions(
            ClientProcessMode,
            Host,
            TcpGatewayPort,
            RoomId,
            PlayerId,
            ClientId,
            RoomMaxPlayers,
            BattleDurationFrames,
            BattleVictoryTargetDefeats,
            ContinueAfterAllPlayersDefeated,
            InputCount,
            Seed,
            Timeout,
            WaitForMatchEnd,
            ReconnectCount,
            ReconnectDelayMs,
            RecoverableFailureCount,
            RetryBackoffMaxMs,
            NetworkCondition,
            StateSyncPayloadMode,
            InputStateReplayOutputPath,
            RunId,
            CorrelationId,
            RunRootPath,
            DiagnosticOutputPath,
            ReconnectReleasePath,
            CompletionReleasePath,
            NetworkControlPath,
            MetricsOutputPath,
            MetricsSampleIntervalMs);
    }

    public static ShooterSmokeProgramOptions Parse(string[] args)
    {
        var serverMode = false;
        var clientMode = false;
        var clientProcessMode = ShooterSmokeClientProcessMode.Create;
        var host = ShooterSmokeScenarioBase.DefaultTcpGatewayHost;
        var tcpGatewayPort = 41001;
        var roomId = string.Empty;
        uint playerId = 1;
        var clientId = $"shooter-mp-{Environment.ProcessId}";
        var roomMaxPlayers = 0;
        var battleDurationFrames = 0;
        var battleVictoryTargetDefeats = 0;
        var continueAfterAllPlayersDefeated = false;
        var inputCount = 3;
        var seed = 20260610;
        var timeout = TimeSpan.FromSeconds(15);
        var waitForMatchEnd = false;
        var reconnectCount = 0;
        var reconnectDelayMs = 500;
        var recoverableFailureCount = 0;
        var retryBackoffMaxMs = 2000;
        var conditionLatencyMs = 0;
        var conditionJitterMs = 0;
        var conditionPacketLossRate = 0d;
        var conditionBandwidthBytesPerSecond = 0;
        var conditionSeed = 20260610;
        var inputStateReplayOutputPath = string.Empty;
        var inputLogicReplayOutputPath = string.Empty;
        var stateSyncPayloadMode = "template";
        var runId = string.Empty;
        var correlationId = string.Empty;
        var runRootPath = string.Empty;
        var diagnosticOutputPath = string.Empty;
        var faultControlPath = string.Empty;
        var reconnectReleasePath = string.Empty;
        var completionReleasePath = string.Empty;
        var networkControlPath = string.Empty;
        var metricsOutputPath = string.Empty;
        var metricsSampleIntervalMs = 1000;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase))
            {
                serverMode = true;
            }
            else if (string.Equals(arg, "--client", StringComparison.OrdinalIgnoreCase))
            {
                clientMode = true;
            }
            else if (string.Equals(arg, "--client-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var value = args[++i];
                if (string.Equals(value, "join", StringComparison.OrdinalIgnoreCase))
                {
                    clientProcessMode = ShooterSmokeClientProcessMode.Join;
                }
                else if (string.Equals(value, "create", StringComparison.OrdinalIgnoreCase))
                {
                    clientProcessMode = ShooterSmokeClientProcessMode.Create;
                }
            }
            else if (string.Equals(arg, "--host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var value = args[++i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    host = value;
                }
            }
            else if (string.Equals(arg, "--tcp-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedPort) && parsedPort > 0 && parsedPort <= 65535)
                {
                    tcpGatewayPort = parsedPort;
                }
            }
            else if (string.Equals(arg, "--room-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                roomId = args[++i];
            }
            else if (string.Equals(arg, "--player-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (uint.TryParse(args[++i], out var parsedPlayerId) && parsedPlayerId > 0)
                {
                    playerId = parsedPlayerId;
                }
            }
            else if (string.Equals(arg, "--client-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var value = args[++i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    clientId = value;
                }
            }
            else if (string.Equals(arg, "--room-max-players", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedRoomMaxPlayers) && parsedRoomMaxPlayers > 0)
                {
                    roomMaxPlayers = parsedRoomMaxPlayers;
                }
            }
            else if (string.Equals(arg, "--battle-duration-frames", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedBattleDurationFrames) && parsedBattleDurationFrames > 0)
                {
                    battleDurationFrames = parsedBattleDurationFrames;
                }
            }
            else if (string.Equals(arg, "--battle-victory-target-defeats", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedBattleVictoryTargetDefeats) && parsedBattleVictoryTargetDefeats > 0)
                {
                    battleVictoryTargetDefeats = parsedBattleVictoryTargetDefeats;
                }
            }
            else if (string.Equals(arg, "--continue-after-all-players-defeated", StringComparison.OrdinalIgnoreCase))
            {
                continueAfterAllPlayersDefeated = true;
            }
            else if (string.Equals(arg, "--inputs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedInputs) && parsedInputs >= 0)
                {
                    inputCount = parsedInputs;
                }
            }
            else if (string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedSeed))
                {
                    seed = parsedSeed;
                }
            }
            else if (string.Equals(arg, "--wait-for-match-end", StringComparison.OrdinalIgnoreCase))
            {
                waitForMatchEnd = true;
            }
            else if (string.Equals(arg, "--reconnect-once", StringComparison.OrdinalIgnoreCase))
            {
                reconnectCount = Math.Max(reconnectCount, 1);
            }
            else if (string.Equals(arg, "--reconnect-count", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedReconnectCount) && parsedReconnectCount >= 0)
                {
                    reconnectCount = parsedReconnectCount;
                }
            }
            else if (string.Equals(arg, "--recoverable-failure-count", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedFailureCount) && parsedFailureCount >= 0)
                {
                    recoverableFailureCount = parsedFailureCount;
                }
            }
            else if (string.Equals(arg, "--retry-backoff-max-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedBackoffMaxMs) && parsedBackoffMaxMs >= 0)
                {
                    retryBackoffMaxMs = parsedBackoffMaxMs;
                }
            }
            else if (string.Equals(arg, "--reconnect-delay-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedDelayMs) && parsedDelayMs >= 0)
                {
                    reconnectDelayMs = parsedDelayMs;
                }
            }
            else if (string.Equals(arg, "--condition-latency-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedLatencyMs) && parsedLatencyMs >= 0)
                {
                    conditionLatencyMs = parsedLatencyMs;
                }
            }
            else if (string.Equals(arg, "--condition-jitter-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedJitterMs) && parsedJitterMs >= 0)
                {
                    conditionJitterMs = parsedJitterMs;
                }
            }
            else if (string.Equals(arg, "--condition-packet-loss-rate", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLossRate))
                {
                    conditionPacketLossRate = parsedLossRate;
                }
            }
            else if (string.Equals(arg, "--condition-bandwidth-bytes-per-second", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedBandwidth) && parsedBandwidth >= 0)
                {
                    conditionBandwidthBytesPerSecond = parsedBandwidth;
                }
            }
            else if (string.Equals(arg, "--condition-seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedConditionSeed))
                {
                    conditionSeed = parsedConditionSeed;
                }
            }
            else if (string.Equals(arg, "--timeout-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedSeconds) && parsedSeconds > 0)
                {
                    timeout = TimeSpan.FromSeconds(parsedSeconds);
                }
            }
            else if ((string.Equals(arg, "--state-sync-payload-mode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--payload-mode", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                var value = NormalizeStateSyncPayloadMode(args[++i]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    stateSyncPayloadMode = value;
                }
            }
            else if (string.Equals(arg, "--replay-output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                inputStateReplayOutputPath = args[++i];
            }
            else if ((string.Equals(arg, "--client-state-replay-output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--input-state-replay-output", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                inputStateReplayOutputPath = args[++i];
            }
            else if ((string.Equals(arg, "--server-frame-replay-output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--input-logic-replay-output", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                inputLogicReplayOutputPath = args[++i];
            }
            else if (string.Equals(arg, "--run-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                runId = args[++i];
            }
            else if (string.Equals(arg, "--correlation-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                correlationId = args[++i];
            }
            else if (string.Equals(arg, "--run-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                runRootPath = args[++i];
            }
            else if (string.Equals(arg, "--diagnostic-output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                diagnosticOutputPath = args[++i];
            }
            else if (string.Equals(arg, "--fault-control-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                faultControlPath = args[++i];
            }
            else if (string.Equals(arg, "--reconnect-release-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                reconnectReleasePath = args[++i];
            }
            else if (string.Equals(arg, "--completion-release-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                completionReleasePath = args[++i];
            }
            else if (string.Equals(arg, "--network-control-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                networkControlPath = args[++i];
            }
            else if (string.Equals(arg, "--metrics-output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                metricsOutputPath = args[++i];
            }
            else if (string.Equals(arg, "--metrics-sample-interval-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedSampleIntervalMs) && parsedSampleIntervalMs >= 100)
                {
                    metricsSampleIntervalMs = parsedSampleIntervalMs;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = $"{Environment.ProcessId}:{clientId}";
        }

        return new ShooterSmokeProgramOptions(
            serverMode,
            clientMode,
            clientProcessMode,
            host,
            tcpGatewayPort,
            roomId,
            playerId,
            clientId,
            roomMaxPlayers,
            battleDurationFrames,
            battleVictoryTargetDefeats,
            continueAfterAllPlayersDefeated,
            inputCount,
            seed,
            timeout,
            waitForMatchEnd,
            reconnectCount,
            reconnectDelayMs,
            recoverableFailureCount,
            retryBackoffMaxMs,
            new SmokeNetworkConditionOptions(
                conditionLatencyMs,
                conditionJitterMs,
                conditionPacketLossRate,
                conditionSeed,
                conditionBandwidthBytesPerSecond).Normalize(),
            stateSyncPayloadMode,
            inputStateReplayOutputPath,
            inputLogicReplayOutputPath,
            runId,
            correlationId,
            runRootPath,
            diagnosticOutputPath,
            faultControlPath,
            reconnectReleasePath,
            completionReleasePath,
            networkControlPath,
            metricsOutputPath,
            metricsSampleIntervalMs);
    }

    private static string NormalizeStateSyncPayloadMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "template", StringComparison.OrdinalIgnoreCase))
        {
            return "template";
        }

        if (string.Equals(value, "pure-state", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "purestate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "pure_state", StringComparison.OrdinalIgnoreCase))
        {
            return "pure-state";
        }

        if (string.Equals(value, "packed", StringComparison.OrdinalIgnoreCase))
        {
            return "packed";
        }

        throw new ArgumentException($"Unsupported Shooter state-sync payload mode: {value}");
    }
}


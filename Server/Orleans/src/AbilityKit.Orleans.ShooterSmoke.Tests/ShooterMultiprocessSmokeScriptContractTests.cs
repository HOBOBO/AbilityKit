using System.Diagnostics;
using System.Text.Json;
using Xunit;

public sealed class ShooterMultiprocessSmokeScriptContractTests
{
    private static readonly string ScriptPath = GetScriptPath();
    private static readonly string Script = File.ReadAllText(ScriptPath);
    private static readonly string ProgramSource = File.ReadAllText(GetProgramSourcePath());
    private static readonly string ClientRunnerSource = File.ReadAllText(GetClientRunnerSourcePath());
    private static readonly string RequestClientSource = File.ReadAllText(GetRequestClientPath());
    private static readonly string ProcessUtilsSource = File.ReadAllText(GetProcessUtilsPath());
    private static readonly string OwnershipCleanupProbeSource = File.ReadAllText(GetOwnershipCleanupProbePath());

    [Fact]
    public void ScriptParsesWithWindowsPowerShellAst()
    {
        var escapedPath = ScriptPath.Replace("'", "''", StringComparison.Ordinal);
        var command = "$tokens=$null;$errors=$null;" +
            $"$ast=[System.Management.Automation.Language.Parser]::ParseFile('{escapedPath}',[ref]$tokens,[ref]$errors);" +
            "if($errors.Count -gt 0){$errors|%{$_.ToString()};exit 1};" +
            "$required=@('Get-ShooterFaultMatrixPlan','Get-BoundedTimeoutSeconds','Get-FailureClassification','Assert-BoundedConvergence','Invoke-GatewayFaultCommand','Wait-ForPortClosed','Wait-ForClientReconnectReady','Invoke-MatrixTimeoutOwnedCleanup','Register-RunProcess','Wait-ForSoakRecoveryEvidence','Invoke-ShooterSoakPhases','Get-ShooterSoakSummary','Assert-ShooterSoakSummary');" +
            "$actual=@($ast.FindAll({param($n)$n -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true).Name);" +
            "$missing=@($required|?{$_ -notin $actual});if($missing.Count -gt 0){Write-Error ('Missing functions: '+($missing -join ','));exit 2};'AST_OK'";

        var result = RunPowerShellCommand(command);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("AST_OK", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimalPlanExpandsRecoverableRetryWithBoundedTiming()
    {
        using var plan = RunPlan("-Profile", "minimal", "-ScenarioTimeoutSeconds", "37", "-GlobalTimeoutSeconds", "91");
        var root = plan.RootElement;
        var scenarios = root.GetProperty("scenarios");

        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("minimal", root.GetProperty("profile").GetString());
        Assert.Equal(91, root.GetProperty("globalTimeoutSeconds").GetInt32());
        Assert.Equal(1, scenarios.GetArrayLength());

        var scenario = scenarios[0];
        Assert.Equal("recoverable-retry", scenario.GetProperty("name").GetString());
        Assert.Equal(37, scenario.GetProperty("timeoutSeconds").GetInt32());
        Assert.Equal(1, scenario.GetProperty("reconnectCount").GetInt32());
        Assert.Equal(3, scenario.GetProperty("recoverableFailureCount").GetInt32());
        Assert.Equal(30, scenario.GetProperty("convergenceTimeoutSeconds").GetInt32());
    }

    [Fact]
    public void CompatibilityPlanUsesOrthogonalPayloadFanoutAndNetworkCases()
    {
        using var plan = RunPlan(
            "-Profile", "compatibility",
            "-TcpPort", "44201",
            "-SiloPort", "15211",
            "-OrleansGatewayPort", "34201",
            "-ScenarioTimeoutSeconds", "45");
        var root = plan.RootElement;
        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();

        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("compatibility", root.GetProperty("profile").GetString());
        Assert.Equal(900, root.GetProperty("globalTimeoutSeconds").GetInt32());
        Assert.Equal(
            new[]
            {
                "packed-recoverable-single",
                "pure-state-slow-consumer-fanout",
                "packed-gateway-offline-fanout",
                "pure-state-reconnect-cycles-fanout",
                "pure-state-observer-reactivation",
            },
            scenarios.Select(item => item.GetProperty("caseId").GetString()).ToArray());
        Assert.Equal(
            new[] { "packed", "pure-state", "packed", "pure-state", "pure-state" },
            scenarios.Select(item => item.GetProperty("payloadMode").GetString()).ToArray());
        Assert.Equal(
            new[] { 1, 2, 2, 2, 1 },
            scenarios.Select(item => item.GetProperty("joinClients").GetInt32()).ToArray());
        Assert.Equal(new[] { 44201, 44211, 44221, 44231, 44241 }, scenarios.Select(item => item.GetProperty("tcpPort").GetInt32()).ToArray());
        Assert.Equal(20, scenarios[3].GetProperty("conditionLatencyMs").GetInt32());
        Assert.Equal(5, scenarios[3].GetProperty("conditionJitterMs").GetInt32());
        Assert.Contains("'-JoinClients', $plan.joinClients", Script, StringComparison.Ordinal);
        Assert.Contains("'-PayloadMode', $plan.payloadMode", Script, StringComparison.Ordinal);
        Assert.Contains("'-ConditionLatencyMs', $plan.conditionLatencyMs", Script, StringComparison.Ordinal);
        Assert.Contains("$childRunId = \"$RunId-$($plan.caseId)\"", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void FullPlanExpandsFiveIsolatedFaultScenarios()
    {
        using var plan = RunPlan(
            "-Profile", "full",
            "-TcpPort", "42001",
            "-SiloPort", "13111",
            "-OrleansGatewayPort", "32001",
            "-ScenarioTimeoutSeconds", "45");
        var root = plan.RootElement;
        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();

        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("globalTimeoutIsAutomatic").GetBoolean());
        Assert.Equal(900, root.GetProperty("globalTimeoutSeconds").GetInt32());
        Assert.Equal(
            new[] { "slow-consumer", "gateway-offline", "recoverable-retry", "reconnect-cycles", "observer-reactivation" },
            scenarios.Select(item => item.GetProperty("name").GetString()).ToArray());
        Assert.Equal(new[] { 42001, 42011, 42021, 42031, 42041 }, scenarios.Select(item => item.GetProperty("tcpPort").GetInt32()).ToArray());
        Assert.Equal(new[] { 13111, 13121, 13131, 13141, 13151 }, scenarios.Select(item => item.GetProperty("siloPort").GetInt32()).ToArray());
        Assert.Equal(new[] { 32001, 32011, 32021, 32031, 32041 }, scenarios.Select(item => item.GetProperty("orleansGatewayPort").GetInt32()).ToArray());
        Assert.True(scenarios[0].GetProperty("slowConsumer").GetBoolean());
        Assert.True(scenarios[1].GetProperty("gatewayOffline").GetBoolean());
        Assert.Equal(3, scenarios[2].GetProperty("recoverableFailureCount").GetInt32());
        Assert.Equal(3, scenarios[3].GetProperty("reconnectCount").GetInt32());
        Assert.True(scenarios[4].GetProperty("observerReactivation").GetBoolean());
        Assert.Equal(1, scenarios[4].GetProperty("reconnectCount").GetInt32());
        Assert.Equal("pure-state", scenarios[0].GetProperty("payloadMode").GetString());
        Assert.All(scenarios, scenario => Assert.Equal(1, scenario.GetProperty("joinClients").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(60, scenario.GetProperty("startupTimeoutSeconds").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(60, scenario.GetProperty("setupTimeoutSeconds").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(180, scenario.GetProperty("executionTimeoutSeconds").GetInt32()));
        Assert.Equal(new[] { 0, 180, 360, 540, 720 }, scenarios.Select(item => item.GetProperty("offsetSeconds").GetInt32()).ToArray());
    }

    [Fact]
    public void SoakPlanExpandsSixteenAndSixtyFourObserverLongRuns()
    {
        using var plan = RunPlan(
            "-Profile", "soak",
            "-SoakDurationSeconds", "1800",
            "-TcpPort", "44401",
            "-SiloPort", "15411",
            "-OrleansGatewayPort", "34401");
        var root = plan.RootElement;
        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();

        Assert.Equal("soak", root.GetProperty("profile").GetString());
        Assert.Equal(2, scenarios.Length);
        Assert.Equal(new[] { "soak-16", "soak-64" }, scenarios.Select(item => item.GetProperty("caseId").GetString()).ToArray());
        Assert.Equal(new[] { 15, 63 }, scenarios.Select(item => item.GetProperty("joinClients").GetInt32()).ToArray());
        Assert.Equal(new[] { 16, 64 }, scenarios.Select(item => item.GetProperty("observerCount").GetInt32()).ToArray());
        Assert.All(scenarios, scenario => Assert.True(scenario.GetProperty("soak").GetBoolean()));
        Assert.All(scenarios, scenario => Assert.Equal("pure-state", scenario.GetProperty("payloadMode").GetString()));
        Assert.All(scenarios, scenario => Assert.Equal(0, scenario.GetProperty("reconnectCount").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(1800, scenario.GetProperty("timeoutSeconds").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(30, scenario.GetProperty("resultTimeoutSeconds").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(30, scenario.GetProperty("convergenceTimeoutSeconds").GetInt32()));
        Assert.All(scenarios, scenario => Assert.Equal(60, scenario.GetProperty("requestedSetupTimeoutSeconds").GetInt32()));
        Assert.Equal(new[] { 105, 249 }, scenarios.Select(item => item.GetProperty("setupTimeoutSeconds").GetInt32()).ToArray());
        Assert.Equal(new[] { 2040, 2184 }, scenarios.Select(item => item.GetProperty("executionTimeoutSeconds").GetInt32()).ToArray());
        Assert.Equal(new[] { 0, 2184 }, scenarios.Select(item => item.GetProperty("offsetSeconds").GetInt32()).ToArray());
        Assert.Equal(new[] { 44401, 44411 }, scenarios.Select(item => item.GetProperty("tcpPort").GetInt32()).ToArray());

        using var explicitSoak16 = RunPlan("-Profile", "custom", "-Scenario", "soak-16", "-PayloadMode", "packed");
        var explicitSoak16Scenario = explicitSoak16.RootElement.GetProperty("scenarios")[0];
        Assert.Equal("pure-state", explicitSoak16Scenario.GetProperty("payloadMode").GetString());
        Assert.Equal(15, explicitSoak16Scenario.GetProperty("joinClients").GetInt32());
        Assert.Equal(16, explicitSoak16Scenario.GetProperty("observerCount").GetInt32());

        using var explicitSoak64 = RunPlan("-Profile", "custom", "-Scenario", "soak-64", "-JoinClients", "1");
        var explicitSoak64Scenario = explicitSoak64.RootElement.GetProperty("scenarios")[0];
        Assert.Equal("pure-state", explicitSoak64Scenario.GetProperty("payloadMode").GetString());
        Assert.Equal(63, explicitSoak64Scenario.GetProperty("joinClients").GetInt32());
        Assert.Equal(64, explicitSoak64Scenario.GetProperty("observerCount").GetInt32());

        Assert.Contains("'-SoakDurationSeconds', $SoakDurationSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("'-SoakMetricsSampleIntervalMs', $SoakMetricsSampleIntervalMs", Script, StringComparison.Ordinal);
        Assert.Contains("'-SoakResourceSampleIntervalSeconds', $SoakResourceSampleIntervalSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("$setupFanoutAllowanceSeconds = if ($isSoak) { [Math]::Min(240, [int]$case.joinClients * 3) } else { 0 }", Script, StringComparison.Ordinal);
        Assert.Contains("$setupTimeoutBudgetSeconds = $SetupTimeoutSeconds + $setupFanoutAllowanceSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("$clientTimeoutSeconds = if ($isSoakRun)", Script, StringComparison.Ordinal);
        Assert.Contains("[Math]::Max($TimeoutSeconds, [int]$activePlan.executionTimeoutSeconds)", Script, StringComparison.Ordinal);
        Assert.Contains("'--timeout-seconds', $ClientTimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("'--timeout-seconds', $TimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.Equal(2, Script.Split("-ClientTimeoutSeconds $clientTimeoutSeconds", StringSplitOptions.None).Length - 1);
        Assert.Contains("-RoomMaxPlayers ($JoinClients + 1)", Script, StringComparison.Ordinal);
        Assert.Contains("$arguments += @('--room-max-players', $RoomMaxPlayers)", Script, StringComparison.Ordinal);
        Assert.Contains("[long]$activePlan.executionTimeoutSeconds * 60", Script, StringComparison.Ordinal);
        Assert.Contains("-BattleDurationFrames $(if ($isSoakRun)", Script, StringComparison.Ordinal);
        Assert.Contains("$JoinClients = [int]$activePlan.joinClients", Script, StringComparison.Ordinal);
        Assert.Contains("$PayloadMode = [string]$activePlan.payloadMode", Script, StringComparison.Ordinal);
        Assert.Contains("if ($ClientMode -eq 'create' -and $BattleDurationFrames -gt 0)", Script, StringComparison.Ordinal);
        Assert.Contains("$arguments += @('--battle-duration-frames', $BattleDurationFrames)", Script, StringComparison.Ordinal);
        Assert.Equal(1, Script.Split("-BattleDurationFrames $(if ($isSoakRun)", StringSplitOptions.None).Length - 1);
        Assert.Contains("-BattleVictoryTargetDefeats $(if ($isSoakRun) { [int]::MaxValue } else { 0 })", Script, StringComparison.Ordinal);
        Assert.Contains("$arguments += @('--battle-victory-target-defeats', $BattleVictoryTargetDefeats)", Script, StringComparison.Ordinal);
        Assert.Contains("-ContinueAfterAllPlayersDefeated:$isSoakRun", Script, StringComparison.Ordinal);
        Assert.Contains("[switch]$ContinueAfterAllPlayersDefeated", Script, StringComparison.Ordinal);
        Assert.Contains("$arguments += '--continue-after-all-players-defeated'", Script, StringComparison.Ordinal);
        Assert.Contains("--continue-after-all-players-defeated", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("continueAfterAllPlayersDefeated = true", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("options.ContinueAfterAllPlayersDefeated", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("ShooterRoomLaunchTagKeys.ContinueAfterAllPlayersDefeated", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("bool.TrueString", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("--battle-duration-frames", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("battleDurationFrames = parsedBattleDurationFrames", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("--battle-victory-target-defeats", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("battleVictoryTargetDefeats = parsedBattleVictoryTargetDefeats", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("options.BattleDurationFrames", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("tags[ShooterRoomLaunchTagKeys.DurationFrames]", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("battleDurationFrames.ToString(CultureInfo.InvariantCulture)", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("options.BattleVictoryTargetDefeats", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("tags[ShooterRoomLaunchTagKeys.VictoryTargetDefeats]", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("battleVictoryTargetDefeats.ToString(CultureInfo.InvariantCulture)", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("SHOOTER_MP_LAUNCH_SPEC", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("durationFramesTag=", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("victoryTargetDefeatsTag=", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("function Wait-ForClientCompletionReady", Script, StringComparison.Ordinal);
        Assert.Contains("Wait-ForClientCompletionReady -Client $client", Script, StringComparison.Ordinal);
        Assert.Contains("'SHOOTER_MP_CLIENT_COMPLETION_READY'", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void SoakRuntimeUsesPerClientControlTelemetryAndP0Assertions()
    {
        Assert.Contains("--network-control-path', $NetworkControlPath", Script, StringComparison.Ordinal);
        Assert.Contains("function Read-ControlAcknowledgementIfPresent", Script, StringComparison.Ordinal);
        Assert.Contains("[System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete", Script, StringComparison.Ordinal);
        Assert.Contains("catch [System.IO.IOException]", Script, StringComparison.Ordinal);
        Assert.Contains("catch [System.Text.Json.JsonException]", Script, StringComparison.Ordinal);
        Assert.Contains("Read-ControlAcknowledgementIfPresent -Path $client.NetworkControlAckPath", Script, StringComparison.Ordinal);
        Assert.Contains("$ack = Read-ControlAcknowledgementIfPresent -Path $ackPath", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("$ack = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json", Script, StringComparison.Ordinal);
        Assert.Contains("[System.IO.File]::Replace($temporaryPath, $Path, $backupPath)", Script, StringComparison.Ordinal);
        Assert.Contains("[System.IO.File]::Move($temporaryPath, $Path)", Script, StringComparison.Ordinal);
        Assert.Contains("$replaceDeadline = [DateTime]::UtcNow.AddSeconds(2)", Script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Move-Item -LiteralPath $temporaryPath -Destination $Path -Force", Script, StringComparison.Ordinal);
        Assert.Contains("--metrics-output', $MetricsOutputPath", Script, StringComparison.Ordinal);
        Assert.Contains("--metrics-sample-interval-ms', $MetricsSampleIntervalMs", Script, StringComparison.Ordinal);
        Assert.Contains("--condition-bandwidth-bytes-per-second', $BandwidthBytesPerSecond", Script, StringComparison.Ordinal);
        Assert.Contains("$Scenario -eq 'slow-consumer' -or $isSoakRun", Script, StringComparison.Ordinal);
        Assert.Contains("name = 'disruptive-pressure'; latencyMs = 250; jitterMs = 100; loss = 0.20; bandwidth = 8192; recovery = $false", Script, StringComparison.Ordinal);
        Assert.Contains("name = 'recovery-ideal'; latencyMs = 0; jitterMs = 0; loss = 0.0; bandwidth = 0; recovery = $true", Script, StringComparison.Ordinal);
        Assert.Contains("ConsumeRecoveryBaselineRequest", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("RequestInitialFullStateSyncWhileTickingAsync", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("RequestRecoveryFullStateSyncWhileTickingAsync", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("ShooterClientResyncReason.None.ToString()", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("\"SoakRecovery\"", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("RequestFullSnapshotBaselineAsync(reason, timeout)", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("Request timeout. opCode={opCode} seq={seq}", RequestClientSource, StringComparison.Ordinal);
        Assert.Contains("TryTimeout(opCode, seq)", RequestClientSource, StringComparison.Ordinal);
        Assert.Contains("type = 'process-resource-sample'", Script, StringComparison.Ordinal);
        Assert.Contains("minimum observer sent-byte delta divided by median observer sent-byte delta", Script, StringComparison.Ordinal);
        Assert.Contains("Get-PercentileValue -Values @($recoveryDurations) -Percentile 0.95", Script, StringComparison.Ordinal);
        Assert.Contains("Get-PercentileValue -Values @($recoveryDurations) -Percentile 0.99", Script, StringComparison.Ordinal);
        Assert.Contains("maximumProcessPrivateMemoryGrowthMb", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'soak-recovery-percentiles'", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'soak-observer-fairness'", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'soak-resource-trend'", Script, StringComparison.Ordinal);
        Assert.Contains("$timeoutPhase = 'soak result collection'", Script, StringComparison.Ordinal);
        Assert.Contains("$scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($activePlan.resultTimeoutSeconds)", Script, StringComparison.Ordinal);
        Assert.Contains("function Wait-ForSoakRecoveryEvidence", Script, StringComparison.Ordinal);
        Assert.Contains("$deadline = [DateTime]::UtcNow.AddSeconds($activePlan.convergenceTimeoutSeconds)", Script, StringComparison.Ordinal);
        Assert.Contains("Wait-ForSoakRecoveryEvidence -Clients $Clients", Script, StringComparison.Ordinal);
        Assert.True(
            Script.IndexOf("Wait-ForSoakRecoveryEvidence -Clients $Clients", StringComparison.Ordinal)
            < Script.IndexOf("New-Item -ItemType File -Path $completionReleasePath -Force", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectedScenarioIsNotExpandedAsCharacters()
    {
        using var plan = RunPlan("-Profile", "custom", "-Scenario", "recoverable-retry");
        var scenarios = plan.RootElement.GetProperty("scenarios");

        Assert.Equal(1, scenarios.GetArrayLength());
        Assert.Equal("recoverable-retry", scenarios[0].GetProperty("caseId").GetString());
        Assert.Equal("recoverable-retry", scenarios[0].GetProperty("name").GetString());
    }

    [Fact]
    public void ReconnectCyclesUsesThreeRealJoinClientDisconnectAndRecoveryCycles()
    {
        using var plan = RunPlan(
            "-Profile", "custom",
            "-Scenario", "reconnect-cycles",
            "-PayloadMode", "pure-state");
        var scenario = plan.RootElement.GetProperty("scenarios")[0];

        Assert.Equal("reconnect-cycles", scenario.GetProperty("name").GetString());
        Assert.Equal(3, scenario.GetProperty("reconnectCount").GetInt32());
        Assert.Equal(0, scenario.GetProperty("recoverableFailureCount").GetInt32());
        Assert.Contains("-ClientReconnectCount $(if ($i -eq 1) { $ReconnectCount } else { 0 })", Script, StringComparison.Ordinal);
        Assert.Contains("--state-sync-payload-mode', $PayloadMode", Script, StringComparison.Ordinal);
        Assert.Contains("for (var cycle = 1; cycle <= options.ReconnectCount; cycle++)", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("connection.Close();", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("reconnected.Flow.EntryKind != ShooterRoomGatewayEntryKind.Reconnect", ClientRunnerSource, StringComparison.Ordinal);
        Assert.Contains("getPushCount() <= pushesBeforeCycle", ClientRunnerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptRetainsArtifactAndFailureClassificationContracts()
    {
        Assert.Contains("$logDir = Join-Path $artifactRootPath $RunId", Script, StringComparison.Ordinal);
        Assert.Contains("Smoke run directory already exists", Script, StringComparison.Ordinal);
        Assert.Contains("if ($Status -ne 'running' -and (Test-Path $logDir))", Script, StringComparison.Ordinal);
        Assert.Contains("sha256 = (Get-FileHash", Script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $logDir $diagnosticPath", Script, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $resolvedDiagnosticPath", Script, StringComparison.Ordinal);
        Assert.Contains("$reachableTargetFrame = if ($reconnectCount -gt 0)", Script, StringComparison.Ordinal);
        Assert.Contains("$lastPushFrame", Script, StringComparison.Ordinal);
        Assert.Contains("StartedAtUtc = $startedAtUtc", Script, StringComparison.Ordinal);
        Assert.Contains("startedAtUtc = $client.StartedAtUtc.ToString('O')", Script, StringComparison.Ordinal);
        Assert.Contains("exitedAtUtc = $exitedAtUtc.ToString('O')", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("$client.Process.StartTime", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("$client.Process.ExitTime", Script, StringComparison.Ordinal);
        Assert.Contains("$childManifestStatus -ne 'passed'", Script, StringComparison.Ordinal);
        Assert.Contains("manifestStatus = $childManifestStatus", Script, StringComparison.Ordinal);
        Assert.Contains("New-Item -ItemType Directory -Force -Path $matrixRoot", Script, StringComparison.Ordinal);
        var buildIndex = Script.IndexOf("dotnet build $project", StringComparison.Ordinal);
        var matrixTimerIndex = Script.IndexOf("$matrixStartedAtUtc = [DateTime]::UtcNow", StringComparison.Ordinal);
        Assert.True(buildIndex >= 0 && matrixTimerIndex > buildIndex, "The fault-matrix timeout must start after the one-time build.");
        Assert.Contains("$applicationDll = Join-Path $projectDirectory", Script, StringComparison.Ordinal);
        Assert.Contains("Get-AbilityKitProcessIdentity -ProcessId $ProcessId -RetryCount 40 -RetryDelayMilliseconds 50", Script, StringComparison.Ordinal);
        Assert.Contains("$arguments = @($applicationDll)", Script, StringComparison.Ordinal);
        Assert.Contains("$serverArgs = @($applicationDll)", Script, StringComparison.Ordinal);
        Assert.Contains("Shooter smoke application artifact was not found", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("'run', '--project', $project, '-c', $Configuration, '--no-build'", Script, StringComparison.Ordinal);
        var serverListeningIndex = Script.IndexOf("Add-AssertionResult -Name 'server-listening'", StringComparison.Ordinal);
        var setupTimerIndex = Script.LastIndexOf("$scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($timeoutBudgetSeconds)", StringComparison.Ordinal);
        var allJoinsReadyIndex = Script.IndexOf("$timeoutPhase = 'active scenario'", StringComparison.Ordinal);
        var scenarioTimerIndex = Script.LastIndexOf("$scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($activePlan.timeoutSeconds)", StringComparison.Ordinal);
        Assert.True(serverListeningIndex >= 0 && setupTimerIndex > serverListeningIndex, "The setup timeout must start after the server is listening.");
        Assert.True(allJoinsReadyIndex > setupTimerIndex && scenarioTimerIndex > allJoinsReadyIndex, "The active scenario timeout must start only after all join clients are ready.");
        Assert.Contains("$timeoutBudgetSeconds = [int]$activePlan.setupTimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("$childTimeoutSeconds = [Math]::Min($remainingGlobalSeconds, $plan.executionTimeoutSeconds)", Script, StringComparison.Ordinal);
        Assert.Contains("-TimeoutSeconds', $TimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("Wait-ForPort -Port $TcpPort -TimeoutSeconds $StartupTimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("-Prefix 'SHOOTER_MP_CLIENT_READY' -TimeoutSeconds $SetupTimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("-Prefix 'SHOOTER_MP_CLIENT_READY' -TimeoutSeconds $TimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("-CompletionReleasePath $(if ($Scenario -eq 'slow-consumer' -or $isSoakRun)", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'slow-consumer-pressure-window-completed'", Script, StringComparison.Ordinal);
        Assert.Contains("$summaries += [PSCustomObject][ordered]@{", Script, StringComparison.Ordinal);
        Assert.Contains("$null -eq $diagnostic.observer.serverDroppedItems", Script, StringComparison.Ordinal);
        Assert.Contains("serverDroppedItems = [long]$diagnostic.observer.serverDroppedItems", Script, StringComparison.Ordinal);
        Assert.Contains("serverCoalescedItems = [long]$diagnostic.observer.serverCoalescedItems", Script, StringComparison.Ordinal);
        Assert.Contains("serverBaselineInvalidations = [long]$diagnostic.observer.serverBaselineInvalidations", Script, StringComparison.Ordinal);
        Assert.Contains("Measure-Object -Property serverDroppedItems -Sum", Script, StringComparison.Ordinal);
        Assert.Contains("Measure-Object -Property serverCoalescedItems -Sum", Script, StringComparison.Ordinal);
        Assert.Contains("Measure-Object -Property fullBaselinesApplied -Sum", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Measure-Object -Property observerDropped -Sum", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Measure-Object -Property observerCoalesced -Sum", Script, StringComparison.Ordinal);
        Assert.Contains("($deltasApplied + $resyncRequests + $fullBaselinesApplied) -ge 2", Script, StringComparison.Ordinal);
        Assert.Contains("$actualPlayerId = Read-ResultInt -Fields $fields -Name 'playerId'", Script, StringComparison.Ordinal);
        Assert.Contains("if ($isSoakRun) {", Script, StringComparison.Ordinal);
        Assert.Contains("$actualPlayerId -lt 1 -or $actualPlayerId -gt ($JoinClients + 1)", Script, StringComparison.Ordinal);
        Assert.Contains("elseif ($actualPlayerId -ne $client.PlayerId)", Script, StringComparison.Ordinal);
        Assert.Contains("(Read-ResultInt -Fields $fields -Name 'entities') -lt 1", Script, StringComparison.Ordinal);
        Assert.Contains("Client snapshot did not contain any visible entities", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("'entities') -lt $actualPlayerId", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpectedAtLeast=$actualPlayerId", Script, StringComparison.Ordinal);
        Assert.Contains("$actualPlayerIds = @(", Script, StringComparison.Ordinal);
        Assert.Contains("$expectedPlayerIds = @(1..($JoinClients + 1))", Script, StringComparison.Ordinal);
        Assert.Contains("Soak client player ids did not uniquely cover the room player range", Script, StringComparison.Ordinal);
        Assert.Contains("$hasStrictPureStateActivity", Script, StringComparison.Ordinal);
        Assert.Contains("$hasConvergedSoakFinalFrame", Script, StringComparison.Ordinal);
        Assert.Contains("$isSoakRun `", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("$isSoakRun `\r\n            -and $WaitForMatchEnd `", Script, StringComparison.Ordinal);
        Assert.Contains("-and $pushes -ge 2 `", Script, StringComparison.Ordinal);
        Assert.Contains("-and -not $lastResyncNeeded `", Script, StringComparison.Ordinal);
        Assert.Contains("-and $snapshotHashMatched `", Script, StringComparison.Ordinal);
        Assert.Contains("-and $diffStatus -eq 'Identical' `", Script, StringComparison.Ordinal);
        Assert.Contains("-and $sourceFrame -eq $baselineFrame `", Script, StringComparison.Ordinal);
        Assert.Contains("-and $sourceFrame -eq $lastPushFrame", Script, StringComparison.Ordinal);
        Assert.Contains("[AllowEmptyCollection()][double[]]$Values", Script, StringComparison.Ordinal);
        Assert.Contains("if ($Values.Count -eq 0)", Script, StringComparison.Ordinal);
        Assert.Contains("if (-not $hasStrictPureStateActivity -and", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("-not $activePlan.slowConsumer -or $fullBaselinesApplied -lt 2", Script, StringComparison.Ordinal);
        Assert.Contains("operationTimeoutSeconds = $TimeoutSeconds", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("-TimeoutSeconds', $remainingGlobalSeconds", Script, StringComparison.Ordinal);
        Assert.Contains("return 'PreconditionFailed'", Script, StringComparison.Ordinal);
        Assert.Contains("return 'FaultRecoveryFailed'", Script, StringComparison.Ordinal);
        Assert.Contains("$timeoutPhase -eq 'startup' -or $timeoutPhase -eq 'setup'", Script, StringComparison.Ordinal);
        Assert.Contains("$timeoutPhase = 'client result validation'", Script, StringComparison.Ordinal);
        Assert.Contains("$timeoutPhase -eq 'client result validation'", Script, StringComparison.Ordinal);
        Assert.Contains("'validation'", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'scenario-completed' -Passed $true", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'scenario-completed' -Passed $false", Script, StringComparison.Ordinal);
        Assert.Contains("Invoke-MatrixTimeoutOwnedCleanup", Script, StringComparison.Ordinal);
        Assert.Contains("Stop-AbilityKitOwnedProcess -ExpectedIdentity $orchestratorIdentity", Script, StringComparison.Ordinal);
        Assert.Contains("Stop-AbilityKitOwnedProcess -ExpectedIdentity $candidate", Script, StringComparison.Ordinal);
        Assert.Contains("-Ports @($plan.tcpPort, $plan.siloPort, $plan.orleansGatewayPort)", Script, StringComparison.Ordinal);
        Assert.Contains("$childManifest.status = 'failed'", Script, StringComparison.Ordinal);
        Assert.Contains("Add-Member -NotePropertyName cleanupEvidence", Script, StringComparison.Ordinal);
        var childTimeoutIndex = Script.IndexOf("if ($childTimedOut)", StringComparison.Ordinal);
        var matrixManifestIndex = Script.IndexOf("$matrixManifestPath = Join-Path $matrixRoot", StringComparison.Ordinal);
        Assert.True(childTimeoutIndex >= 0 && matrixManifestIndex > childTimeoutIndex, "A child timeout must flow through matrix manifest generation.");
        Assert.DoesNotContain("throw \"Shooter fault matrix scenario", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("-CommandPatterns @('AbilityKit.Orleans.ShooterSmoke.csproj'", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("taskkill", Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$arguments += $commonArgs", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeoutCleanupRequiresExactManifestOwnedProcessIdentity()
    {
        Assert.Contains("function Get-AbilityKitProcessIdentity", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("creationTimeUtc = $creationTimeUtc", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("executablePath = [System.IO.Path]::GetFullPath", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("commandLine = [string]$process.CommandLine", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("function Test-AbilityKitProcessIdentity", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("$result.status = 'identity-mismatch'", ProcessUtilsSource, StringComparison.Ordinal);
        Assert.Contains("$result.status = 'termination-failed'", ProcessUtilsSource, StringComparison.Ordinal);

        var identityCheckIndex = ProcessUtilsSource.IndexOf("Test-AbilityKitProcessIdentity -ExpectedIdentity", StringComparison.Ordinal);
        var stopIndex = ProcessUtilsSource.IndexOf("Stop-Process -Id $processId -Force -ErrorAction Stop", StringComparison.Ordinal);
        Assert.True(identityCheckIndex >= 0 && stopIndex > identityCheckIndex, "Owned process identity must be checked before termination.");

        Assert.Contains("Register-RunProcess -Role 'orchestrator'", Script, StringComparison.Ordinal);
        Assert.Contains("Register-RunProcess -Role 'server'", Script, StringComparison.Ordinal);
        Assert.Contains("Register-RunProcess -Role \"client-$ClientMode-$ClientId\"", Script, StringComparison.Ordinal);
        Assert.Contains("Write-RunManifest -Status 'running'", Script, StringComparison.Ordinal);
        Assert.Contains("creationTimeUtc = $identity.creationTimeUtc", Script, StringComparison.Ordinal);
        Assert.Contains("executablePath = $identity.executablePath", Script, StringComparison.Ordinal);
        Assert.Contains("commandLine = $identity.commandLine", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipCleanupProbeTreatsExpectedRunnerFailureAsAcceptanceInput()
    {
        Assert.Contains("'-OwnershipCleanupProbe'", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("runner-failed-as-expected", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("owned-processes-exited", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("failure-stage-preserved", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("matrix-cleanup-evidence-recorded", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("unrelated-process-survived", OwnershipCleanupProbeSource, StringComparison.Ordinal);
        Assert.Contains("Test-AbilityKitTcpPort", OwnershipCleanupProbeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GatewayOfflineScenarioProvesTransportOutageBeforeRecovery()
    {
        Assert.Contains(
            "-not (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $Port -TimeoutMilliseconds 250)",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ReconnectReleasePath $(if (($Scenario -eq 'gateway-offline' -or $Scenario -eq 'observer-reactivation') -and $i -eq 1)",
            Script,
            StringComparison.Ordinal);

        var reconnectReadyIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'join-inputs-completed-before-fault'",
            StringComparison.Ordinal);
        var offlineAckIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'gateway-offline-acknowledged'",
            StringComparison.Ordinal);
        var unreachableIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'gateway-offline-unreachable'",
            StringComparison.Ordinal);
        var onlineAckIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'gateway-online-acknowledged'",
            StringComparison.Ordinal);
        var reconnectReleaseIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'join-reconnect-released-after-recovery'",
            StringComparison.Ordinal);

        Assert.True(reconnectReadyIndex >= 0, "The join client must finish inputs before the fault.");
        Assert.True(offlineAckIndex > reconnectReadyIndex, "The Gateway fault must start after the client reaches the reconnect gate.");
        Assert.True(unreachableIndex > offlineAckIndex, "Port unreachability must be proven after the offline acknowledgement.");
        Assert.True(onlineAckIndex > unreachableIndex, "Gateway recovery must happen after the offline port probe.");
        Assert.True(reconnectReleaseIndex > onlineAckIndex, "Reconnect must be released only after the Gateway is listening again.");
    }

    [Fact]
    public void ObserverReactivationRequiresNewActivationBeforeFormalReconnect()
    {
        using var plan = RunPlan(
            "-Profile", "custom",
            "-Scenario", "observer-reactivation",
            "-PayloadMode", "pure-state");
        var scenario = plan.RootElement.GetProperty("scenarios")[0];

        Assert.True(scenario.GetProperty("observerReactivation").GetBoolean());
        Assert.Equal(1, scenario.GetProperty("reconnectCount").GetInt32());
        Assert.Contains("ObserverKey = $ObserverKey", Script, StringComparison.Ordinal);
        Assert.Contains("$reactivation.BeforeActivationToken -eq $reactivation.AfterActivationToken", Script, StringComparison.Ordinal);
        Assert.Contains("Add-AssertionResult -Name 'observer-reactivation-token-changed'", Script, StringComparison.Ordinal);

        var readyIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'join-inputs-completed-before-observer-deactivation'",
            StringComparison.Ordinal);
        var commandIndex = Script.IndexOf(
            "Invoke-GatewayFaultCommand -Action 'observer-reactivate'",
            StringComparison.Ordinal);
        var tokenIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'observer-reactivation-token-changed'",
            StringComparison.Ordinal);
        var releaseIndex = Script.IndexOf(
            "Add-AssertionResult -Name 'join-reconnect-released-after-observer-reactivation'",
            StringComparison.Ordinal);

        Assert.True(readyIndex >= 0, "The join client must finish inputs before observer deactivation.");
        Assert.True(commandIndex > readyIndex, "Observer deactivation must be requested after the reconnect gate is reached.");
        Assert.True(tokenIndex > commandIndex, "A different activation token must be proven after the command.");
        Assert.True(releaseIndex > tokenIndex, "Formal reconnect must be released only after reactivation is proven.");
    }

    [Fact]
    public void LagCompensationAllowsDeterministicHistoryWindowRejection()
    {
        Assert.Contains(
            "$acceptableReasons = @('Hit', 'HistoryUnavailable', 'RewindWindowExceeded')",
            Script,
            StringComparison.Ordinal);
        Assert.Contains("if ($requestedFrame -lt 0)", Script, StringComparison.Ordinal);
        Assert.Contains("if ($accepted)", Script, StringComparison.Ordinal);
    }

    private static JsonDocument RunPlan(params string[] arguments)
    {
        var processArguments = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            ScriptPath,
            "-PlanOnly",
        };
        processArguments.AddRange(arguments);
        var result = RunPowerShell(processArguments);

        Assert.True(result.ExitCode == 0, $"PowerShell exited with {result.ExitCode}: {result.StandardError}");
        return JsonDocument.Parse(result.StandardOutput);
    }

    private static ProcessResult RunPowerShellCommand(string command) =>
        RunPowerShell(new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command });

    private static ProcessResult RunPowerShell(IEnumerable<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "PowerShell contract test timed out.");
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GetRequestClientPath() =>
        Path.GetFullPath(Path.Combine(
            GetOrleansWorkspacePath(),
            "..",
            "..",
            "Unity",
            "Packages",
            "com.abilitykit.network.runtime",
            "Runtime",
            "Network",
            "Runtime",
            "RequestResponse",
            "RequestClient.cs"));

    private static string GetScriptPath() =>
        Path.Combine(GetOrleansWorkspacePath(), "tools", "run_shooter_multiprocess_smoke.ps1");

    private static string GetProgramSourcePath() =>
        Path.Combine(GetOrleansWorkspacePath(), "src", "AbilityKit.Orleans.ShooterSmoke", "Program.cs");

    private static string GetClientRunnerSourcePath() =>
        Path.Combine(
            GetOrleansWorkspacePath(),
            "src",
            "AbilityKit.Orleans.ShooterSmoke",
            "Runner",
            "ShooterSmokeClientProcessRunner.cs");

    private static string GetProcessUtilsPath() =>
        Path.Combine(GetOrleansWorkspacePath(), "tools", "abilitykit_process_utils.ps1");

    private static string GetOwnershipCleanupProbePath() =>
        Path.Combine(GetOrleansWorkspacePath(), "tools", "test_shooter_multiprocess_ownership_cleanup.ps1");

    private static string GetOrleansWorkspacePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "AbilityKit.Orleans.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Orleans workspace from the test output directory.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

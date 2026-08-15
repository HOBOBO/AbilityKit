param(
    [string]$ConfigPath = 'tools\test-gates.json',
    [string]$WorkflowPath = '.github\workflows\abilitykit-test-gates.yml',
    [string]$OutputPath = 'artifacts\test-gates\contract-validation\report.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$checks = [System.Collections.Generic.List[object]]::new()

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $checks.Add([pscustomobject]@{
        name = $Name
        passed = $Passed
        detail = $Detail
    })

    $color = if ($Passed) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] {1}: {2}" -f $(if ($Passed) { 'PASS' } else { 'FAIL' }), $Name, $Detail) -ForegroundColor $color
}

function Assert-Condition {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$SuccessDetail,
        [string]$FailureDetail
    )

    Add-Check -Name $Name -Passed $Condition -Detail $(if ($Condition) { $SuccessDetail } else { $FailureDetail })
}

function Get-Gate {
    param(
        [object]$Config,
        [string]$Name
    )

    return @($Config.gates | Where-Object { [string]$_.name -eq $Name }) | Select-Object -First 1
}

$configFullPath = Resolve-RepoPath $ConfigPath
$workflowFullPath = Resolve-RepoPath $WorkflowPath
$outputFullPath = Resolve-RepoPath $OutputPath
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputFullPath)

try {
    $config = Get-Content -LiteralPath $configFullPath -Raw | ConvertFrom-Json
    Add-Check -Name 'Gate JSON parseability' -Passed $true -Detail $ConfigPath

    $workflowText = Get-Content -LiteralPath $workflowFullPath -Raw

    $pythonLauncher = Get-Command py -ErrorAction SilentlyContinue
    $python = Get-Command python -ErrorAction SilentlyContinue
    $pythonCommand = if ($null -ne $pythonLauncher) { $pythonLauncher.Source } elseif ($null -ne $python) { $python.Source } else { $null }
    Assert-Condition -Name 'Python YAML parser prerequisite' `
        -Condition ($null -ne $pythonCommand) `
        -SuccessDetail $pythonCommand `
        -FailureDetail 'Python 3 is required to validate workflow YAML parseability.'

    if ($null -ne $pythonCommand) {
        $yamlProbe = 'import pathlib, sys, yaml; yaml.safe_load(pathlib.Path(sys.argv[1]).read_bytes())'
        if ($null -ne $pythonLauncher) {
            & $pythonLauncher.Source '-3' '-c' $yamlProbe $workflowFullPath
        }
        else {
            & $python.Source '-c' $yamlProbe $workflowFullPath
        }

        $yamlProbeExitCode = $LASTEXITCODE
        Assert-Condition -Name 'Workflow YAML parseability' `
            -Condition ($yamlProbeExitCode -eq 0) `
            -SuccessDetail $WorkflowPath `
            -FailureDetail "PyYAML could not parse the workflow (exit code $yamlProbeExitCode). Install PyYAML 6.x for the selected Python 3 interpreter."
    }

    $requiredGates = @(
        'moba-codegen',
        'core-stability',
        'shooter-fast',
        'shooter-integration',
        'shooter-unity-playmode',
        'shooter-multiprocess',
        'shooter-multiprocess-compatibility',
        'shooter-multiprocess-soak',
        'shooter-multiprocess-ownership-cleanup',
        'shooter-performance'
    )
    foreach ($gateName in $requiredGates) {
        Assert-Condition -Name "Required gate '$gateName'" `
            -Condition ($null -ne (Get-Gate -Config $config -Name $gateName)) `
            -SuccessDetail 'Defined.' `
            -FailureDetail 'Missing from gate configuration.'
    }

    foreach ($gate in @($config.gates)) {
        foreach ($step in @($gate.steps)) {
            if ($step.PSObject.Properties['project']) {
                $projectPath = [string]$step.project
                Assert-Condition -Name "Referenced project: $projectPath" `
                    -Condition (Test-Path -LiteralPath (Resolve-RepoPath $projectPath) -PathType Leaf) `
                    -SuccessDetail 'Exists.' `
                    -FailureDetail "Referenced by gate '$($gate.name)' but not found."
            }

            if ($step.PSObject.Properties['script']) {
                $scriptPath = [string]$step.script
                $scriptFullPath = Resolve-RepoPath $scriptPath
                $scriptExists = Test-Path -LiteralPath $scriptFullPath -PathType Leaf
                Assert-Condition -Name "Referenced script: $scriptPath" `
                    -Condition $scriptExists `
                    -SuccessDetail 'Exists.' `
                    -FailureDetail "Referenced by gate '$($gate.name)' but not found."

                if ($scriptExists) {
                    $tokens = $null
                    $errors = $null
                    [void][System.Management.Automation.Language.Parser]::ParseFile($scriptFullPath, [ref]$tokens, [ref]$errors)
                    Assert-Condition -Name "PowerShell syntax: $scriptPath" `
                        -Condition (@($errors).Count -eq 0) `
                        -SuccessDetail 'Valid.' `
                        -FailureDetail ((@($errors | ForEach-Object { $_.Message })) -join '; ')
                }
            }

            if ($step.PSObject.Properties['projectPath']) {
                $projectPath = [string]$step.projectPath
                Assert-Condition -Name "Referenced Unity project: $projectPath" `
                    -Condition (Test-Path -LiteralPath (Resolve-RepoPath $projectPath) -PathType Container) `
                    -SuccessDetail 'Exists.' `
                    -FailureDetail "Referenced by gate '$($gate.name)' but not found."
            }
        }
    }

    $unityGate = Get-Gate -Config $config -Name 'shooter-unity-playmode'
    $unityStep = @($unityGate.steps) | Select-Object -First 1
    Assert-Condition -Name 'Unity gate test mode' `
        -Condition ([string]$unityStep.kind -eq 'unity-playmode-test' -and [string]$unityStep.testPlatform -ieq 'PlayMode') `
        -SuccessDetail 'kind=unity-playmode-test and testPlatform=PlayMode.' `
        -FailureDetail 'Unity integration must execute in PlayMode.'

    $playModeTestPath = 'Unity\Packages\com.abilitykit.demo.shooter.view.runtime\Tests\PlayMode\ShooterSynchronizationPlayModeSmokeTests.cs'
    $playModeAsmdefPath = 'Unity\Packages\com.abilitykit.demo.shooter.view.runtime\Tests\PlayMode\AbilityKit.Demo.Shooter.PlayMode.Tests.asmdef'
    Assert-Condition -Name 'Unity PlayMode smoke source' `
        -Condition (Test-Path -LiteralPath (Resolve-RepoPath $playModeTestPath) -PathType Leaf) `
        -SuccessDetail $playModeTestPath `
        -FailureDetail 'PlayMode smoke source is missing.'
    Assert-Condition -Name 'Unity PlayMode test assembly' `
        -Condition (Test-Path -LiteralPath (Resolve-RepoPath $playModeAsmdefPath) -PathType Leaf) `
        -SuccessDetail $playModeAsmdefPath `
        -FailureDetail 'PlayMode test assembly is missing.'

    $multiprocessGate = Get-Gate -Config $config -Name 'shooter-multiprocess'
    $multiprocessStep = @($multiprocessGate.steps) | Select-Object -First 1
    $multiprocessArguments = @($multiprocessStep.arguments)
    $globalTimeoutIndex = [Array]::IndexOf($multiprocessArguments, '-GlobalTimeoutSeconds')
    $globalTimeout = if ($globalTimeoutIndex -ge 0 -and $globalTimeoutIndex + 1 -lt $multiprocessArguments.Count) {
        [int]$multiprocessArguments[$globalTimeoutIndex + 1]
    }
    else {
        0
    }
    Assert-Condition -Name 'TEST-01B bounded cleanup window' `
        -Condition ([int]$multiprocessStep.timeoutSeconds -gt $globalTimeout -and $globalTimeout -gt 0) `
        -SuccessDetail "Runner timeout $($multiprocessStep.timeoutSeconds)s exceeds scenario global timeout ${globalTimeout}s." `
        -FailureDetail 'Outer timeout must exceed the TEST-01B global timeout so script cleanup can run.'

    $compatibilityGate = Get-Gate -Config $config -Name 'shooter-multiprocess-compatibility'
    $compatibilityStep = @($compatibilityGate.steps) | Select-Object -First 1
    $compatibilityArguments = @($compatibilityStep.arguments)
    $compatibilityProfileIndex = [Array]::IndexOf($compatibilityArguments, '-Profile')
    $compatibilityTimeoutIndex = [Array]::IndexOf($compatibilityArguments, '-GlobalTimeoutSeconds')
    $compatibilityGlobalTimeout = if ($compatibilityTimeoutIndex -ge 0 -and $compatibilityTimeoutIndex + 1 -lt $compatibilityArguments.Count) {
        [int]$compatibilityArguments[$compatibilityTimeoutIndex + 1]
    }
    else {
        0
    }
    Assert-Condition -Name 'Compatibility profile selection' `
        -Condition ($compatibilityProfileIndex -ge 0 -and [string]$compatibilityArguments[$compatibilityProfileIndex + 1] -eq 'compatibility') `
        -SuccessDetail 'The scheduled multiprocess gate selects Profile=compatibility.' `
        -FailureDetail 'The scheduled multiprocess gate must explicitly select Profile=compatibility.'
    Assert-Condition -Name 'Compatibility bounded cleanup window' `
        -Condition ([int]$compatibilityStep.timeoutSeconds -gt $compatibilityGlobalTimeout -and $compatibilityGlobalTimeout -ge 900) `
        -SuccessDetail "Runner timeout $($compatibilityStep.timeoutSeconds)s exceeds matrix timeout ${compatibilityGlobalTimeout}s." `
        -FailureDetail 'The compatibility outer timeout must exceed an explicit matrix budget of at least 900 seconds.'

    $soakGate = Get-Gate -Config $config -Name 'shooter-multiprocess-soak'
    $soakStep = @($soakGate.steps) | Select-Object -First 1
    $soakArguments = @($soakStep.arguments)
    $soakProfileIndex = [Array]::IndexOf($soakArguments, '-Profile')
    $soakDurationIndex = [Array]::IndexOf($soakArguments, '-SoakDurationSeconds')
    $soakTimeoutIndex = [Array]::IndexOf($soakArguments, '-GlobalTimeoutSeconds')
    $soakDuration = if ($soakDurationIndex -ge 0 -and $soakDurationIndex + 1 -lt $soakArguments.Count) { [int]$soakArguments[$soakDurationIndex + 1] } else { 0 }
    $soakGlobalTimeout = if ($soakTimeoutIndex -ge 0 -and $soakTimeoutIndex + 1 -lt $soakArguments.Count) { [int]$soakArguments[$soakTimeoutIndex + 1] } else { 0 }
    Assert-Condition -Name 'P0 soak CI policy' `
        -Condition ([string]$soakGate.level -eq 'P0' -and -not [bool]$soakGate.ciPolicy.runOnPullRequest -and -not [bool]$soakGate.ciPolicy.runOnPush -and [bool]$soakGate.ciPolicy.runOnSchedule -and [bool]$soakGate.ciPolicy.artifactRequired) `
        -SuccessDetail 'P0 scheduled/manual artifact-required gate.' `
        -FailureDetail 'The soak gate must be P0, excluded from pull requests and pushes, scheduled, and artifact-required.'
    Assert-Condition -Name 'P0 soak profile and duration' `
        -Condition ($soakProfileIndex -ge 0 -and [string]$soakArguments[$soakProfileIndex + 1] -eq 'soak' -and $soakDuration -ge 1800) `
        -SuccessDetail "Profile=soak, duration=${soakDuration}s." `
        -FailureDetail 'The P0 soak gate must explicitly select Profile=soak for at least 1800 seconds.'
    Assert-Condition -Name 'P0 soak bounded cleanup window' `
        -Condition ([int]$soakStep.timeoutSeconds -gt $soakGlobalTimeout -and $soakGlobalTimeout -ge ($soakDuration * 2)) `
        -SuccessDetail "Runner timeout $($soakStep.timeoutSeconds)s exceeds matrix timeout ${soakGlobalTimeout}s." `
        -FailureDetail 'The P0 soak outer timeout must exceed a global budget covering both long-running scenarios and cleanup.'

    $ownershipCleanupGate = Get-Gate -Config $config -Name 'shooter-multiprocess-ownership-cleanup'
    $ownershipCleanupStep = @($ownershipCleanupGate.steps) | Select-Object -First 1
    $ownershipCleanupArguments = @($ownershipCleanupStep.arguments)
    $ownershipTimeoutIndex = [Array]::IndexOf($ownershipCleanupArguments, '-GlobalTimeoutSeconds')
    $ownershipGlobalTimeout = if ($ownershipTimeoutIndex -ge 0 -and $ownershipTimeoutIndex + 1 -lt $ownershipCleanupArguments.Count) {
        [int]$ownershipCleanupArguments[$ownershipTimeoutIndex + 1]
    }
    else {
        0
    }
    Assert-Condition -Name 'TEST-01C dedicated ownership probe' `
        -Condition ([string]$ownershipCleanupStep.script -eq 'Server/Orleans/tools/test_shooter_multiprocess_ownership_cleanup.ps1') `
        -SuccessDetail ([string]$ownershipCleanupStep.script) `
        -FailureDetail 'TEST-01C must execute the dedicated expected-failure acceptance harness.'
    Assert-Condition -Name 'TEST-01C bounded cleanup window' `
        -Condition ([int]$ownershipCleanupStep.timeoutSeconds -gt ($ownershipGlobalTimeout + 45) -and $ownershipGlobalTimeout -ge 20) `
        -SuccessDetail "Runner timeout $($ownershipCleanupStep.timeoutSeconds)s exceeds probe matrix and cleanup budget $($ownershipGlobalTimeout + 45)s." `
        -FailureDetail 'The TEST-01C outer timeout must leave at least 45 seconds after the explicit matrix timeout for cleanup and evidence collection.'

    $requiredWorkflowFragments = [ordered]@{
        'Pull request trigger' = 'pull_request:'
        'Manual trigger' = 'workflow_dispatch:'
        'Scheduled trigger' = 'schedule:'
        'MOBA CodeGen job' = 'moba-codegen:'
        'MOBA CodeGen gate command' = '-Gate moba-codegen'
        'Core stability job' = 'core-stability:'
        'Core stability gate command' = '-Gate core-stability'
        'Fast job' = 'shooter-fast:'
        'Integration job' = 'shooter-integration:'
        'Unity PlayMode job' = 'shooter-unity-playmode:'
        'Multiprocess job' = 'shooter-multiprocess:'
        'Multiprocess compatibility job' = 'shooter-multiprocess-compatibility:'
        'Multiprocess compatibility gate command' = '-Gate shooter-multiprocess-compatibility'
        'Multiprocess soak job' = 'shooter-multiprocess-soak:'
        'Multiprocess soak gate command' = '-Gate shooter-multiprocess-soak'
        'Multiprocess soak schedule/manual policy' = "if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'"
        'Multiprocess soak artifact path' = 'artifacts/test-gates/shooter-multiprocess-soak'
        'Multiprocess ownership cleanup job' = 'shooter-multiprocess-ownership-cleanup:'
        'Multiprocess ownership cleanup gate command' = '-Gate shooter-multiprocess-ownership-cleanup'
        'Performance smoke job' = 'shooter-performance-smoke:'
        'Performance full job' = 'shooter-performance-full:'
        'Unity PlayMode gate command' = '-Gate shooter-unity-playmode'
        'Performance smoke selection' = '-StepName "AOI LOD smoke"'
        'Performance full selection' = '-StepName "AOI LOD full"'
        'TEST-01B non-cancelling concurrency' = 'group: abilitykit-shooter-multiprocess-fault-run'
    }
    foreach ($entry in $requiredWorkflowFragments.GetEnumerator()) {
        Assert-Condition -Name $entry.Key `
            -Condition $workflowText.Contains([string]$entry.Value) `
            -SuccessDetail ([string]$entry.Value) `
            -FailureDetail "Workflow fragment missing: $($entry.Value)"
    }

    $jobNames = @(
        'contract-validation',
        'precheck',
        'moba-codegen',
        'core-stability',
        'shooter-fast',
        'shooter-integration',
        'shooter-unity-playmode',
        'shooter-performance-smoke',
        'shooter-multiprocess',
        'shooter-multiprocess-compatibility',
        'shooter-multiprocess-soak',
        'shooter-multiprocess-ownership-cleanup',
        'shooter-performance-full',
        'regression'
    )
    foreach ($jobName in $jobNames) {
        $jobPattern = "(?ms)^  $([regex]::Escape($jobName)):\r?\n(?:(?!^  [a-zA-Z0-9_-]+:).)*?uses: actions/upload-artifact@v4"
        $jobMatch = [regex]::Match($workflowText, $jobPattern)
        $hasAlwaysUpload = $jobMatch.Success -and $jobMatch.Value -match '(?m)^\s+if: always\(\)\s*$'
        Assert-Condition -Name "Always-upload artifacts: $jobName" `
            -Condition $hasAlwaysUpload `
            -SuccessDetail 'Artifact upload is guarded by always().' `
            -FailureDetail 'Job is missing actions/upload-artifact@v4 with if: always().'
    }
}
catch {
    Add-Check -Name 'Validator execution' -Passed $false -Detail $_.Exception.Message
}
finally {
    $failedChecks = @($checks | Where-Object { -not $_.passed })
    $report = [ordered]@{
        generatedAt = (Get-Date).ToString('o')
        status = if ($failedChecks.Count -eq 0) { 'Passed' } else { 'Failed' }
        total = $checks.Count
        failed = $failedChecks.Count
        checks = @($checks)
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFullPath -Encoding UTF8
    Write-Host "Contract validation report: $outputFullPath" -ForegroundColor Cyan
}

if (@($checks | Where-Object { -not $_.passed }).Count -gt 0) {
    exit 1
}

exit 0

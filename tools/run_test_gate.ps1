param(
    [Alias('GateId')]
    [string]$Gate,
    [string]$StepName,
    [string]$ConfigPath = 'tools\test-gates.json',
    [string]$Configuration = 'Debug',
    [string]$ResultsDirectory = 'local\Logs\test-gates',
    [switch]$List,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$CI
)

$ErrorActionPreference = 'Stop'

if ($CI) {
    $ProgressPreference = 'SilentlyContinue'
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

if ([System.IO.Path]::IsPathRooted($ConfigPath)) {
    $resolvedConfigPath = $ConfigPath
}
else {
    $resolvedConfigPath = Join-Path $repoRoot $ConfigPath
}

if (-not (Test-Path $resolvedConfigPath)) {
    throw "Test gate config not found: $resolvedConfigPath"
}

$config = Get-Content -Path $resolvedConfigPath -Raw | ConvertFrom-Json
$gatesByName = @{}
foreach ($gateDef in $config.gates) {
    $gatesByName[[string]$gateDef.name] = $gateDef
}

function ConvertTo-SafeName {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 'unnamed'
    }

    $safe = [string]$Value
    foreach ($invalidChar in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safe = $safe.Replace($invalidChar, '_')
    }

    $safe = $safe -replace '\s+', '_'
    return $safe
}

function Format-StringList {
    param([object]$Value)

    if ($null -eq $Value) {
        return ''
    }

    $items = @($Value) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
    if ($items.Count -eq 0) {
        return ''
    }

    return ($items -join ', ')
}

function Get-OptionalStringProperty {
    param(
        [object]$Value,
        [string]$PropertyName,
        [string]$Fallback = 'Unspecified'
    )

    if ($null -eq $Value -or -not $Value.PSObject.Properties[$PropertyName]) {
        return $Fallback
    }

    return [string]$Value.PSObject.Properties[$PropertyName].Value
}

function Show-GateList {
    Write-Host 'Available test gates:' -ForegroundColor Cyan
    foreach ($gateDef in $config.gates) {
        $level = if ($gateDef.PSObject.Properties['level']) { [string]$gateDef.level } else { 'Unspecified' }
        $owner = if ($gateDef.PSObject.Properties['owner']) { [string]$gateDef.owner } else { 'Unspecified' }
        $scope = Format-StringList $gateDef.scope
        $requiredBefore = Format-StringList $gateDef.requiredBefore
        $failurePolicy = if ($gateDef.PSObject.Properties['failurePolicy']) { [string]$gateDef.failurePolicy } else { 'Unspecified' }

        Write-Host ("- {0} [{1}]" -f $gateDef.name, $level)
        Write-Host ("  Owner: {0}" -f $owner)
        Write-Host ("  Scope: {0}" -f $(if ([string]::IsNullOrWhiteSpace($scope)) { 'Unspecified' } else { $scope }))
        Write-Host ("  Required before: {0}" -f $(if ([string]::IsNullOrWhiteSpace($requiredBefore)) { 'Unspecified' } else { $requiredBefore }))
        Write-Host ("  Failure policy: {0}" -f $failurePolicy)
        Write-Host ("  Description: {0}" -f $gateDef.description)
    }
}

if ($List) {
    Show-GateList
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Gate)) {
    $Gate = [string]$config.defaultGate
}

if (-not $gatesByName.ContainsKey($Gate)) {
    throw "Unknown test gate '$Gate'. Use -List to inspect available gates."
}

$resultsRoot = Resolve-RepoPath $ResultsDirectory
$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), (ConvertTo-SafeName $Gate)
$runRoot = Join-Path $resultsRoot $runId
$null = New-Item -ItemType Directory -Force -Path $runRoot

function Write-GateHeader {
    param($GateDef, [string]$GateOutputDirectory)

    $level = Get-OptionalStringProperty -Value $GateDef -PropertyName 'level'
    $owner = Get-OptionalStringProperty -Value $GateDef -PropertyName 'owner'
    Write-Host ("`n>>> Gate: {0} [{1}]" -f $GateDef.name, $level) -ForegroundColor Green
    Write-Host ("Owner: {0}" -f $owner) -ForegroundColor DarkGray
    Write-Host $GateDef.description -ForegroundColor DarkGray
    if ($GateDef.PSObject.Properties['scope']) {
        Write-Host ("Scope: {0}" -f (Format-StringList $GateDef.scope)) -ForegroundColor DarkGray
    }
    if ($GateDef.PSObject.Properties['requiredBefore']) {
        Write-Host ("Required before: {0}" -f (Format-StringList $GateDef.requiredBefore)) -ForegroundColor DarkGray
    }
    if ($GateDef.PSObject.Properties['failurePolicy']) {
        Write-Host ("Failure policy: {0}" -f [string]$GateDef.failurePolicy) -ForegroundColor DarkGray
    }
    Write-Host ("Output directory: {0}" -f $GateOutputDirectory) -ForegroundColor DarkGray
}

function Invoke-DotNetStep {
    param(
        [string]$DisplayName,
        [string]$Kind,
        [string[]]$Arguments,
        [string]$LogFilePath,
        [string]$ProjectPath,
        [string]$Filter,
        [string]$ResultsDirectory,
        [string]$TrxFilePath
    )

    $startedAt = Get-Date
    Write-Host ("=== {0} ===" -f $DisplayName) -ForegroundColor Cyan
    Write-Host ("dotnet {0}" -f ($Arguments -join ' ')) -ForegroundColor DarkGray

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell wraps native stderr as ErrorRecord objects. Preserve that output
        # without allowing it to bypass exit-code and TRX result handling.
        $ErrorActionPreference = 'Continue'
        if (-not [string]::IsNullOrWhiteSpace($LogFilePath)) {
            $null = New-Item -ItemType Directory -Force -Path (Split-Path $LogFilePath)
            & dotnet @Arguments 2>&1 | Tee-Object -FilePath $LogFilePath | ForEach-Object { Write-Host $_ }
        }
        else {
            & dotnet @Arguments
        }

        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $endedAt = Get-Date

    $record = [ordered]@{
        name             = $DisplayName
        kind             = $Kind
        project          = $ProjectPath
        filter           = $Filter
        command          = ('dotnet {0}' -f ($Arguments -join ' '))
        status           = $(if ($exitCode -eq 0) { 'Passed' } else { 'Failed' })
        exitCode         = $exitCode
        startedAt        = $startedAt.ToString('o')
        endedAt          = $endedAt.ToString('o')
        elapsedSeconds   = [math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
        logFile          = $LogFilePath
        resultsDirectory = $ResultsDirectory
        trxFile          = $TrxFilePath
    }

    return [pscustomobject]$record
}

function Assert-DotNetTrxResult {
    param(
        [pscustomobject]$Record,
        [string]$DisplayName,
        [string]$TrxFilePath
    )

    $failureReason = $null
    $testTotal = $null
    $testPassed = $null
    $testFailed = $null
    $testResultCount = $null

    if (-not (Test-Path -LiteralPath $TrxFilePath -PathType Leaf)) {
        $failureReason = "dotnet test did not create expected TRX results file '$TrxFilePath'."
    }
    else {
        try {
            [xml]$trxXml = Get-Content -LiteralPath $TrxFilePath -Raw
            $testRun = $trxXml.DocumentElement
            if ($null -eq $testRun -or $testRun.LocalName -ne 'TestRun') {
                throw "Root element 'TestRun' was not found."
            }

            $counters = $trxXml.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
            if ($null -eq $counters -or -not $counters.Attributes['total']) {
                throw "ResultSummary/Counters total attribute was not found."
            }

            $testTotal = [int]$counters.Attributes['total'].Value
            $testPassed = if ($counters.Attributes['passed']) { [int]$counters.Attributes['passed'].Value } else { $null }
            $testFailed = if ($counters.Attributes['failed']) { [int]$counters.Attributes['failed'].Value } else { $null }
            $testResultCount = @($trxXml.SelectNodes("/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']")).Count

            if ($testTotal -le 0) {
                $failureReason = "dotnet test step '$DisplayName' matched zero tests."
            }
            elseif ($testResultCount -le 0) {
                $failureReason = "dotnet test step '$DisplayName' produced a TRX file without test results."
            }
        }
        catch {
            $failureReason = "dotnet test TRX results file '$TrxFilePath' is invalid: $($_.Exception.Message)"
        }
    }

    $Record | Add-Member -NotePropertyName trxFileExists -NotePropertyValue (Test-Path -LiteralPath $TrxFilePath -PathType Leaf) -Force
    $Record | Add-Member -NotePropertyName testTotal -NotePropertyValue $testTotal -Force
    $Record | Add-Member -NotePropertyName testPassed -NotePropertyValue $testPassed -Force
    $Record | Add-Member -NotePropertyName testFailed -NotePropertyValue $testFailed -Force
    $Record | Add-Member -NotePropertyName testResultCount -NotePropertyValue $testResultCount -Force
    $Record | Add-Member -NotePropertyName failureReason -NotePropertyValue $failureReason -Force

    if ($null -ne $failureReason) {
        $Record.status = 'Failed'
    }

    return $Record
}

function Invoke-PowerShellScriptStep {
    param(
        [string]$DisplayName,
        [object]$Step,
        [string]$GateOutputDirectory,
        [int]$StepIndex
    )

    $scriptPath = Resolve-RepoPath ([string]$Step.script)
    $repoRootPath = [System.IO.Path]::GetFullPath([string]$repoRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullScriptPath = [System.IO.Path]::GetFullPath($scriptPath)
    if (-not $fullScriptPath.StartsWith($repoRootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PowerShell step '$DisplayName' references a script outside the repository: $fullScriptPath"
    }
    if (-not (Test-Path -LiteralPath $fullScriptPath -PathType Leaf)) {
        throw "PowerShell step '$DisplayName' script was not found: $fullScriptPath"
    }

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $fullScriptPath)
    if ($Step.PSObject.Properties['arguments']) {
        $arguments += @($Step.arguments | ForEach-Object {
            ([string]$_).Replace('{GateOutputDirectory}', $GateOutputDirectory)
        })
    }

    $logFile = Join-Path $GateOutputDirectory (('{0:00}-{1}.log' -f $StepIndex, (ConvertTo-SafeName $DisplayName)))
    $errorLogFile = Join-Path $GateOutputDirectory (('{0:00}-{1}.error.log' -f $StepIndex, (ConvertTo-SafeName $DisplayName)))
    $commandText = 'powershell {0}' -f (($arguments | ForEach-Object {
        if ([string]$_ -match '\s') { '"{0}"' -f $_ } else { [string]$_ }
    }) -join ' ')
    $timeoutSeconds = if ($Step.PSObject.Properties['timeoutSeconds']) { [int]$Step.timeoutSeconds } else { 0 }
    $startedAt = Get-Date
    Write-Host ("=== {0} ===" -f $DisplayName) -ForegroundColor Cyan
    Write-Host $commandText -ForegroundColor DarkGray

    $nativeArguments = ($arguments | ForEach-Object {
        $value = [string]$_
        if ($value -match '[\s"]') { '"{0}"' -f $value.Replace('"', '\"') } else { $value }
    }) -join ' '
    $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processStartInfo.FileName = 'powershell.exe'
    $processStartInfo.Arguments = $nativeArguments
    $processStartInfo.UseShellExecute = $false
    $processStartInfo.CreateNoWindow = $true
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processStartInfo
    $null = $process.Start()
    $standardOutputStream = [System.IO.File]::Create($logFile)
    $standardErrorStream = [System.IO.File]::Create($errorLogFile)
    $standardOutputTask = $process.StandardOutput.BaseStream.CopyToAsync($standardOutputStream)
    $standardErrorTask = $process.StandardError.BaseStream.CopyToAsync($standardErrorStream)
    $timedOut = $timeoutSeconds -gt 0 -and -not $process.WaitForExit($timeoutSeconds * 1000)
    if ($timedOut) {
        try { $process.Kill() } catch { }
        $process.WaitForExit()
        $exitCode = 124
    }
    else {
        $process.WaitForExit()
        $exitCode = [int]$process.ExitCode
    }
    $standardOutputAwaiter = $standardOutputTask.GetAwaiter()
    $standardErrorAwaiter = $standardErrorTask.GetAwaiter()
    $null = $standardOutputAwaiter.GetResult()
    $null = $standardErrorAwaiter.GetResult()
    $standardOutputStream.Dispose()
    $standardErrorStream.Dispose()
    if (Test-Path -LiteralPath $logFile) { Get-Content -LiteralPath $logFile | ForEach-Object { Write-Host $_ } }
    if (Test-Path -LiteralPath $errorLogFile) { Get-Content -LiteralPath $errorLogFile | ForEach-Object { Write-Host $_ -ForegroundColor Red } }
    $endedAt = Get-Date

    return [pscustomobject][ordered]@{
        name = $DisplayName
        kind = 'powershell-script'
        script = $fullScriptPath
        command = $commandText
        status = $(if ($exitCode -eq 0) { 'Passed' } else { 'Failed' })
        exitCode = $exitCode
        timedOut = $timedOut
        timeoutSeconds = $timeoutSeconds
        startedAt = $startedAt.ToString('o')
        endedAt = $endedAt.ToString('o')
        elapsedSeconds = [math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
        logFile = $logFile
        errorLogFile = $errorLogFile
    }
}

function Get-UnityEditorPath {
    param([object]$Step)

    if ($Step.PSObject.Properties['editorPath'] -and -not [string]::IsNullOrWhiteSpace([string]$step.editorPath)) {
        return [string]$step.editorPath
    }

    $defaultEditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe'
    return $defaultEditorPath
}

function Invoke-UnityBatchModeStep {
    param(
        [string]$DisplayName,
        [string]$Kind,
        [object]$Step,
        [string]$GateOutputDirectory,
        [string[]]$Arguments,
        [string]$ResultsFilePath,
        [string]$Filter,
        [string]$ExtraSummary = $null
    )

    $editorPath = Get-UnityEditorPath -Step $Step
    if (-not (Test-Path $editorPath)) {
        throw "Unity editor not found: $editorPath"
    }

    $projectPath = Resolve-RepoPath ([string]$Step.projectPath)
    if (-not (Test-Path $projectPath)) {
        throw "Unity project path not found: $projectPath"
    }

    $unityArtifactsDirectory = Join-Path $GateOutputDirectory 'unity-results'
    $null = New-Item -ItemType Directory -Force -Path $unityArtifactsDirectory

    $safeName = ConvertTo-SafeName $DisplayName
    $logFilePath = Join-Path $unityArtifactsDirectory ($safeName + '.log')
    $commandFilePath = Join-Path $unityArtifactsDirectory ($safeName + '.command.txt')
    $resolvedResultsFilePath = $null
    if (-not [string]::IsNullOrWhiteSpace($ResultsFilePath)) {
        $resolvedResultsFilePath = Join-Path $unityArtifactsDirectory $ResultsFilePath
    }

    $fullArguments = @(
        '-batchmode'
        '-projectPath'
        $projectPath
    )

    $includeNoGraphics = -not ($Step.PSObject.Properties['noGraphics'] -and (-not [bool]$Step.noGraphics))
    if ($includeNoGraphics) { $fullArguments += '-nographics' }

    $fullArguments += $Arguments
    $fullArguments += @('-logFile', $logFilePath)

    $isUnityTestRun = @($Arguments) -contains '-runTests'
    $includeQuit = -not $isUnityTestRun
    if ($Step.PSObject.Properties['quit']) {
        $includeQuit = [bool]$Step.quit
    }
    if ($includeQuit) { $fullArguments += '-quit' }

    if ($Step.PSObject.Properties['extraArgs']) {
        $extraArgs = @($Step.extraArgs) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
        if ($extraArgs.Count -gt 0) {
            $fullArguments += $extraArgs
        }
    }

    $commandText = '"{0}" {1}' -f $editorPath, (($fullArguments | ForEach-Object {
        if ([string]$_ -match '\s') { '"{0}"' -f $_ } else { [string]$_ }
    }) -join ' ')
    Set-Content -Path $commandFilePath -Value $commandText -Encoding UTF8

    $startedAt = Get-Date
    Write-Host ("=== {0} ===" -f $DisplayName) -ForegroundColor Cyan
    Write-Host $commandText -ForegroundColor DarkGray

    $process = Start-Process -FilePath $editorPath -ArgumentList $fullArguments -Wait -PassThru

    $exitCode = $process.ExitCode
    $endedAt = Get-Date
    $hasResultsFile = $true
    if (-not [string]::IsNullOrWhiteSpace($resolvedResultsFilePath)) {
        $hasResultsFile = Test-Path $resolvedResultsFilePath
    }

    $failureReason = $null
    if ($exitCode -ne 0) {
        $failureReason = "Unity exited with code $exitCode."
    }
    elseif (-not $hasResultsFile) {
        $failureReason = "Unity did not create expected results file '$resolvedResultsFilePath'."
    }

    $record = [ordered]@{
        name              = $DisplayName
        kind              = $Kind
        project           = $projectPath
        filter            = $Filter
        command           = $commandText
        status            = $(if ($null -eq $failureReason) { 'Passed' } else { 'Failed' })
        exitCode          = $exitCode
        startedAt         = $startedAt.ToString('o')
        endedAt           = $endedAt.ToString('o')
        elapsedSeconds    = [math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
        logFile           = $logFilePath
        resultsFile       = $resolvedResultsFilePath
        commandFile       = $commandFilePath
        unityEditorPath   = $editorPath
        resultsDirectory  = $unityArtifactsDirectory
        extraSummary      = $ExtraSummary
        failureReason     = $failureReason
    }

    return [pscustomobject]$record
}

function Invoke-UnityTestStep {
    param(
        [string]$DisplayName,
        [object]$Step,
        [string]$GateOutputDirectory
    )

    $testPlatform = if ($Step.PSObject.Properties['testPlatform'] -and -not [string]::IsNullOrWhiteSpace([string]$Step.testPlatform)) { [string]$Step.testPlatform } else { 'EditMode' }
    $testFilter = if ($Step.PSObject.Properties['testFilter']) { [string]$Step.testFilter } else { '' }

    $unityArtifactsDirectory = Join-Path $GateOutputDirectory 'unity-results'
    $null = New-Item -ItemType Directory -Force -Path $unityArtifactsDirectory
    $safeName = ConvertTo-SafeName $DisplayName
    $xmlFilePath = Join-Path $unityArtifactsDirectory ($safeName + '.xml')

    $arguments = @(
        '-runTests'
        '-testPlatform'
        $testPlatform
    )
    if (-not [string]::IsNullOrWhiteSpace($testFilter)) {
        $arguments += @('-testFilter', $testFilter)
    }
    $arguments += @('-testResults', $xmlFilePath)

    $kind = if ($testPlatform -ieq 'PlayMode') { 'unity-playmode-test' } else { 'unity-editmode-test' }
    $record = Invoke-UnityBatchModeStep -DisplayName $DisplayName -Kind $kind -Step $Step -GateOutputDirectory $GateOutputDirectory -Arguments $arguments -ResultsFilePath ($safeName + '.xml') -Filter $testFilter

    $recoveredResultsFrom = $null
    $testRunnerStarted = $false
    $savedResultsPathFromLog = $null
    $batchmodeQuitInvoked = $false
    $hasResultsFile = Test-Path $xmlFilePath
    if (Test-Path $record.logFile) {
        $testRunnerStarted = $null -ne (Select-String -Path $record.logFile -Pattern '^Running tests for\s+' | Select-Object -First 1)
        $saveLine = Select-String -Path $record.logFile -Pattern '^Saving results to:\s*(.+)$' | Select-Object -First 1
        if ($null -ne $saveLine) {
            $savedResultsPathFromLog = $saveLine.Matches[0].Groups[1].Value.Trim()
        }
        $batchmodeQuitInvoked = $null -ne (Select-String -Path $record.logFile -Pattern '^Batchmode quit successfully invoked - shutting down!$' | Select-Object -First 1)
    }

    if (-not $hasResultsFile -and -not [string]::IsNullOrWhiteSpace($savedResultsPathFromLog) -and (Test-Path $savedResultsPathFromLog)) {
        Copy-Item -Path $savedResultsPathFromLog -Destination $xmlFilePath -Force
        $recoveredResultsFrom = $savedResultsPathFromLog
        $hasResultsFile = Test-Path $xmlFilePath
    }

    $failureReason = $record.failureReason
    if ($null -eq $failureReason -and -not $hasResultsFile) {
        if (-not $testRunnerStarted -and $batchmodeQuitInvoked) {
            $failureReason = "Unity exited with code 0 before the command-line Test Runner started. Log contains 'Batchmode quit successfully invoked - shutting down!' but no 'Running tests for ...' marker. This commonly indicates the requested -testFilter batch did not survive domain reload or was not accepted by the Unity Test Framework command-line runner."
        }
        elseif ($testRunnerStarted) {
            $failureReason = "Unity started the command-line Test Runner but did not produce a recoverable results file for '$xmlFilePath'."
        }
        else {
            $failureReason = "Unity exited with code 0 but did not create test results file '$xmlFilePath'."
        }
    }

    $testResult = $null
    $testTotal = $null
    $testPassed = $null
    $testFailed = $null
    if ($hasResultsFile) {
        try {
            [xml]$resultsXml = Get-Content -Path $xmlFilePath -Raw
            $testRun = $resultsXml.'test-run'
            if ($null -eq $testRun) {
                throw "Root element 'test-run' was not found."
            }

            $testResult = [string]$testRun.result
            $testTotal = [int]$testRun.total
            $testPassed = [int]$testRun.passed
            $testFailed = [int]$testRun.failed

            if ($null -eq $failureReason -and $testTotal -le 0) {
                $failureReason = "Unity test filter '$testFilter' matched zero tests."
            }
            elseif ($null -eq $failureReason -and ($testFailed -gt 0 -or $testResult -ne 'Passed')) {
                $failureReason = "Unity test results were not successful: result=$testResult, total=$testTotal, passed=$testPassed, failed=$testFailed."
            }
        }
        catch {
            if ($null -eq $failureReason) {
                $failureReason = "Unity results file '$xmlFilePath' is invalid: $($_.Exception.Message)"
            }
        }
    }

    $record | Add-Member -NotePropertyName resultsFileExists -NotePropertyValue $hasResultsFile -Force
    $record | Add-Member -NotePropertyName testRunnerStarted -NotePropertyValue $testRunnerStarted -Force
    $record | Add-Member -NotePropertyName batchmodeQuitInvoked -NotePropertyValue $batchmodeQuitInvoked -Force
    $record | Add-Member -NotePropertyName savedResultsPathFromLog -NotePropertyValue $savedResultsPathFromLog -Force
    $record | Add-Member -NotePropertyName recoveredResultsFrom -NotePropertyValue $recoveredResultsFrom -Force
    $record | Add-Member -NotePropertyName testPlatform -NotePropertyValue $testPlatform -Force
    $record | Add-Member -NotePropertyName testResult -NotePropertyValue $testResult -Force
    $record | Add-Member -NotePropertyName testTotal -NotePropertyValue $testTotal -Force
    $record | Add-Member -NotePropertyName testPassed -NotePropertyValue $testPassed -Force
    $record | Add-Member -NotePropertyName testFailed -NotePropertyValue $testFailed -Force
    $record.failureReason = $failureReason
    $record.status = if ($null -eq $failureReason) { 'Passed' } else { 'Failed' }

    return $record
}

function Invoke-UnityExecuteMethodStep {
    param(
        [string]$DisplayName,
        [object]$Step,
        [string]$GateOutputDirectory
    )

    $executeMethod = if ($Step.PSObject.Properties['executeMethod']) { [string]$Step.executeMethod } else { '' }
    if ([string]::IsNullOrWhiteSpace($executeMethod)) {
        throw "Unity execute-method step '$DisplayName' is missing executeMethod."
    }

    $arguments = @('-executeMethod', $executeMethod)
    $resultsFileName = $null
    if ($Step.PSObject.Properties['resultsFile'] -and -not [string]::IsNullOrWhiteSpace([string]$Step.resultsFile)) {
        $resultsFileName = [string]$Step.resultsFile
    }

    return Invoke-UnityBatchModeStep -DisplayName $DisplayName -Kind 'unity-execute-method' -Step $Step -GateOutputDirectory $GateOutputDirectory -Arguments $arguments -ResultsFilePath $resultsFileName -Filter $null -ExtraSummary $executeMethod
}

function Invoke-Gate {
    param(
        [string]$GateName,
        [System.Collections.Generic.HashSet[string]]$Visiting,
        [string[]]$GatePath,
        [string]$RunRoot,
        [string]$OnlyStepName = $null
    )

    if (-not $gatesByName.ContainsKey($GateName)) {
        throw "Nested gate '$GateName' is not defined."
    }

    if ($Visiting.Contains($GateName)) {
        throw "Circular gate dependency detected at '$GateName'."
    }

    $null = $Visiting.Add($GateName)
    $gateDef = $gatesByName[$GateName]
    $pathSegments = @($GatePath)
    if ($pathSegments.Count -eq 0) {
        $pathSegments = @($GateName)
    }

    $safePathSegments = foreach ($segment in $pathSegments) {
        ConvertTo-SafeName ([string]$segment)
    }
    $gateOutputDirectory = Join-Path $RunRoot (($safePathSegments -join [System.IO.Path]::DirectorySeparatorChar))
    $null = New-Item -ItemType Directory -Force -Path $gateOutputDirectory

    $startedAt = Get-Date
    $stepResults = New-Object System.Collections.Generic.List[object]
    $status = 'Passed'
    $failureMessage = $null

    try {
        Write-GateHeader -GateDef $gateDef -GateOutputDirectory $gateOutputDirectory

        $stepIndex = 0
        $matchedOnlyStep = [string]::IsNullOrWhiteSpace($OnlyStepName)
        foreach ($step in $gateDef.steps) {
            $stepIndex++
            $stepName = [string]$step.name
            if (-not [string]::IsNullOrWhiteSpace($OnlyStepName) -and $stepName -ne $OnlyStepName) {
                continue
            }

            $matchedOnlyStep = $true
            switch ([string]$step.kind) {
                'gate' {
                    $nestedGateName = [string]$step.gate
                    $nestedGatePath = @($pathSegments + $nestedGateName)
                    $nestedResult = Invoke-Gate -GateName $nestedGateName -Visiting $Visiting -GatePath $nestedGatePath -RunRoot $RunRoot
                    $stepResults.Add([pscustomobject]@{
                        name             = $stepName
                        kind             = 'gate'
                        gate             = $nestedGateName
                        status           = $nestedResult.status
                        outputDirectory  = $nestedResult.outputDirectory
                        summaryPath      = $nestedResult.summaryPath
                        startedAt        = $nestedResult.startedAt
                        endedAt          = $nestedResult.endedAt
                        elapsedSeconds   = $nestedResult.elapsedSeconds
                    })
                }
                'dotnet-build' {
                    $project = Resolve-RepoPath ([string]$step.project)
                    $arguments = @('build', $project, '-c', $Configuration)
                    if ($NoRestore) { $arguments += '--no-restore' }
                    if ($CI) { $arguments += '--nologo' }
                    $logFile = Join-Path $gateOutputDirectory (('{0:00}-{1}.log' -f $stepIndex, (ConvertTo-SafeName $stepName)))
                    $record = Invoke-DotNetStep -DisplayName $stepName -Kind 'dotnet-build' -Arguments $arguments -LogFilePath $logFile -ProjectPath $project -Filter $null -ResultsDirectory $null -TrxFilePath $null
                    $stepResults.Add($record)
                    if ($record.status -ne 'Passed') {
                        throw "Step '$stepName' failed with exit code $($record.exitCode)."
                    }
                }
                'dotnet-test' {
                    $project = Resolve-RepoPath ([string]$step.project)
                    $testResultsDirectory = Join-Path $gateOutputDirectory 'test-results'
                    $null = New-Item -ItemType Directory -Force -Path $testResultsDirectory
                    $trxFileName = ('{0:00}-{1}.trx' -f $stepIndex, (ConvertTo-SafeName $stepName))
                    $trxFilePath = Join-Path $testResultsDirectory $trxFileName
                    $logFile = Join-Path $gateOutputDirectory (('{0:00}-{1}.log' -f $stepIndex, (ConvertTo-SafeName $stepName)))
                    $arguments = @('test', $project, '-c', $Configuration, '-v', 'minimal', '--logger', ("trx;LogFileName=$trxFileName"), '--results-directory', $testResultsDirectory)
                    if ($NoRestore) { $arguments += '--no-restore' }
                    if ($NoBuild) { $arguments += '--no-build' }
                    if ($CI) { $arguments += '--nologo' }
                    if ($step.PSObject.Properties['filter'] -and -not [string]::IsNullOrWhiteSpace([string]$step.filter)) {
                        $arguments += @('--filter', [string]$step.filter)
                    }
                    $record = Invoke-DotNetStep -DisplayName $stepName -Kind 'dotnet-test' -Arguments $arguments -LogFilePath $logFile -ProjectPath $project -Filter ([string]$step.filter) -ResultsDirectory $testResultsDirectory -TrxFilePath $trxFilePath
                    $record = Assert-DotNetTrxResult -Record $record -DisplayName $stepName -TrxFilePath $trxFilePath
                    $stepResults.Add($record)
                    if ($record.status -ne 'Passed') {
                        $failureDetail = if ([string]::IsNullOrWhiteSpace([string]$record.failureReason)) { "exit code $($record.exitCode)" } else { [string]$record.failureReason }
                        throw "Step '$stepName' failed: $failureDetail"
                    }
                }
                'powershell-script' {
                    $record = Invoke-PowerShellScriptStep -DisplayName $stepName -Step $step -GateOutputDirectory $gateOutputDirectory -StepIndex $stepIndex
                    $stepResults.Add($record)
                    if ($record.status -ne 'Passed') {
                        throw "Step '$stepName' failed with exit code $($record.exitCode)."
                    }
                }
                { $_ -in @('unity-editmode-test', 'unity-playmode-test') } {
                    $record = Invoke-UnityTestStep -DisplayName $stepName -Step $step -GateOutputDirectory $gateOutputDirectory
                    $stepResults.Add($record)
                    if ($record.status -ne 'Passed') {
                        throw "Step '$stepName' failed with exit code $($record.exitCode)."
                    }
                }
                'unity-execute-method' {
                    $record = Invoke-UnityExecuteMethodStep -DisplayName $stepName -Step $step -GateOutputDirectory $gateOutputDirectory
                    $stepResults.Add($record)
                    if ($record.status -ne 'Passed') {
                        throw "Step '$stepName' failed with exit code $($record.exitCode)."
                    }
                }
                default {
                    throw "Unsupported gate step kind '$($step.kind)' in '$GateName'."
                }
            }
        }

        if (-not $matchedOnlyStep) {
            throw "Step '$OnlyStepName' was not found in gate '$GateName'."
        }
    }
    catch {
        $status = 'Failed'
        $failureMessage = $_.Exception.Message
        throw
    }
    finally {
        $endedAt = Get-Date
        $gateLevel = Get-OptionalStringProperty -Value $gateDef -PropertyName 'level'
        $gateOwner = Get-OptionalStringProperty -Value $gateDef -PropertyName 'owner'
        $summaryPath = Join-Path $gateOutputDirectory 'gate-summary.json'
        $summary = New-Object psobject
        $summary | Add-Member -MemberType NoteProperty -Name gate -Value $GateName
        $summary | Add-Member -MemberType NoteProperty -Name gatePath -Value ([object[]]@($pathSegments))
        $summary | Add-Member -MemberType NoteProperty -Name level -Value $gateLevel
        $summary | Add-Member -MemberType NoteProperty -Name owner -Value $gateOwner
        $summary | Add-Member -MemberType NoteProperty -Name status -Value $status
        $summary | Add-Member -MemberType NoteProperty -Name startedAt -Value ($startedAt.ToString('o'))
        $summary | Add-Member -MemberType NoteProperty -Name endedAt -Value ($endedAt.ToString('o'))
        $summary | Add-Member -MemberType NoteProperty -Name elapsedSeconds -Value ([math]::Round(($endedAt - $startedAt).TotalSeconds, 3))
        $summary | Add-Member -MemberType NoteProperty -Name outputDirectory -Value $gateOutputDirectory
        $summary | Add-Member -MemberType NoteProperty -Name summaryPath -Value $summaryPath
        $summary | Add-Member -MemberType NoteProperty -Name failureMessage -Value $failureMessage
        $summary | Add-Member -MemberType NoteProperty -Name steps -Value ([object[]]$stepResults.ToArray())

        $summary | ConvertTo-Json -Depth 12 | Set-Content -Path $summaryPath -Encoding UTF8
        $Visiting.Remove($GateName) | Out-Null
    }

    return $summary
}

$startedAt = Get-Date
try {
    $gateResult = Invoke-Gate -GateName $Gate -Visiting ([System.Collections.Generic.HashSet[string]]::new()) -GatePath @($Gate) -RunRoot $runRoot -OnlyStepName $StepName
    $elapsed = (Get-Date) - $startedAt
    Write-Host ("`nGate '{0}' passed in {1:n1}s." -f $Gate, $elapsed.TotalSeconds) -ForegroundColor Green
    Write-Host ("Summary: {0}" -f $gateResult.summaryPath) -ForegroundColor DarkGray
    exit 0
}
catch {
    Write-Host ("`nGate '{0}' failed. See summary under {1}." -f $Gate, $runRoot) -ForegroundColor Red
    throw
}

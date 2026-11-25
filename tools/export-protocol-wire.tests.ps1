$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot 'export-protocol-wire.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('abilitykit-protocol-wire-export-' + [guid]::NewGuid().ToString('N'))

$failures = 0

function Assert-Equal {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        Write-Host ("FAIL: {0} (expected '{1}', got '{2}')" -f $Message, $Expected, $Actual) -ForegroundColor Red
        $script:failures++
    } else {
        Write-Host ("PASS: {0}" -f $Message) -ForegroundColor Green
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    Assert-Equal -Expected $true -Actual $Condition -Message $Message
}

function New-FixtureRepo {
    param(
        [string]$Name
    )

    $root = Join-Path $tempRoot $Name
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'Protocols/Catalogs') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'Protocols/WireSchemas') | Out-Null

    $catalog = @'
schemaVersion: 1
catalogId: fixture.battle
projectId: abilitykit.shooter
domain: battle
revision: 1
defaultCodec: memorypack
messages:
  - id: state.push
    opCode: 5202
    direction: s2c
    kind: push
    payloadType: Fixture.Protocol.FixturePayload
'@
    [System.IO.File]::WriteAllText((Join-Path $root 'Protocols/Catalogs/fixture.protocol.yaml'), $catalog, (New-Object System.Text.UTF8Encoding($false)))

$wireSchema = @'
schemaVersion: 2
projectId: abilitykit.shooter
groupId: battle
namespace: Fixture.Protocol
types:
  - name: FixturePayload
    fields:
      - id: 0
        name: value
        scalarType: int32
        required: true
'@
    [System.IO.File]::WriteAllText((Join-Path $root 'Protocols/WireSchemas/fixture-payload.wire.yaml'), $wireSchema, (New-Object System.Text.UTF8Encoding($false)))
    return $root
}

function Invoke-ExportScript {
    param(
        [string]$FixtureRoot,
        [string[]]$ProjectList = @('shooter'),
        [string[]]$ScriptSwitches = @()
    )

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptUnderTest, '-RepositoryRoot', $FixtureRoot, '-Projects', $ProjectList)
    $arguments += $ScriptSwitches
    # Windows PowerShell 5.1 在 $ErrorActionPreference 为 'Stop' 时，会将重定向的原生命令标准错误转为终止错误；
    # 因此仅在子进程调用期间临时放宽此设置。
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell @arguments 2>&1
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output | Out-String)
    }
}

try {
    $fixture = New-FixtureRepo -Name 'main'
    $exportDirectory = Join-Path $fixture 'Unity/Packages/com.abilitykit.protocol.shooter/Runtime/Generated'

    # 1. 写入模式将确定的产物集合导出到包目录。
    $write = Invoke-ExportScript -FixtureRoot $fixture
    Assert-Equal -Expected 0 -Actual $write.ExitCode -Message ("Write mode should succeed. Output: " + $write.Output)
    foreach ($expectedFile in @(
        'FixturePayload.MemoryPack.g.cs',
        'ProtocolCatalogs.g.cs',
        'ProjectMemoryPackCodecs.g.cs',
        'protocol-export.json')) {
        Assert-True -Condition (Test-Path (Join-Path $exportDirectory $expectedFile)) -Message ("Write mode should export {0}." -f $expectedFile)
    }
    $manifest = Get-Content (Join-Path $exportDirectory 'protocol-export.json') -Raw | ConvertFrom-Json
    Assert-Equal -Expected 1 -Actual $manifest.generatedGroups.Count -Message "Manifest should contain one generated group."
    Assert-Equal -Expected 'battle' -Actual $manifest.generatedGroups[0].groupId -Message "Manifest should retain wire group ownership."
    Assert-Equal -Expected 'Fixture.Protocol.FixturePayload' -Actual $manifest.generatedGroups[0].generatedTypes[0] -Message "Manifest should assign the generated type to its group."

    # 2. 写入后，即使 Git 已统一行尾，检查模式仍应通过。
    $dtoPath = Join-Path $exportDirectory 'FixturePayload.MemoryPack.g.cs'
    $dtoContent = [System.IO.File]::ReadAllText($dtoPath)
    $normalized = $dtoContent.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($dtoPath, $normalized, (New-Object System.Text.UTF8Encoding($false)))
    $checkAfterEolFlip = Invoke-ExportScript -FixtureRoot $fixture -ScriptSwitches @('-Check')
    Assert-Equal -Expected 0 -Actual $checkAfterEolFlip.ExitCode -Message ("Check should ignore CRLF/LF drift. Output: " + $checkAfterEolFlip.Output)

    # 3. 内容发生漂移时，检查模式以退出码 3 失败。
    [System.IO.File]::WriteAllText($dtoPath, $dtoContent + "// tampered`r`n", (New-Object System.Text.UTF8Encoding($false)))
    $checkTampered = Invoke-ExportScript -FixtureRoot $fixture -ScriptSwitches @('-Check')
    Assert-Equal -Expected 3 -Actual $checkTampered.ExitCode -Message "Check should fail with exit 3 on drifted content."
    Assert-True -Condition ($checkTampered.Output -match 'StaleFile') -Message "Drifted content should be reported as a stale file."

    # 4. 清单缺失时，检查模式以退出码 3 失败。
    Remove-Item -Force (Join-Path $exportDirectory 'protocol-export.json')
    $checkMissingManifest = Invoke-ExportScript -FixtureRoot $fixture -ScriptSwitches @('-Check')
    Assert-Equal -Expected 3 -Actual $checkMissingManifest.ExitCode -Message "Check should fail with exit 3 on a missing manifest."

    # 5. 存在过期残留导出文件时，检查模式以退出码 3 失败。
    New-Item -ItemType Directory -Force -Path $exportDirectory | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $exportDirectory 'RemovedDto.MemoryPack.g.cs'), '// stale', (New-Object System.Text.UTF8Encoding($false)))
    $checkExtra = Invoke-ExportScript -FixtureRoot $fixture -ScriptSwitches @('-Check')
    Assert-Equal -Expected 3 -Actual $checkExtra.ExitCode -Message "Check should fail with exit 3 on stale leftover export files."

    # 6. 操作者移除已报告的残留后，重新写入应恢复检查通过。
    # 写入模式仅删除上一清单拥有的文件；手工放入的文件由检查报告，必须手工移除。
    Remove-Item -Force (Join-Path $exportDirectory 'RemovedDto.MemoryPack.g.cs')
    $rewrite = Invoke-ExportScript -FixtureRoot $fixture
    Assert-Equal -Expected 0 -Actual $rewrite.ExitCode -Message ("Rewrite after drift should succeed. Output: " + $rewrite.Output)
    $checkConverged = Invoke-ExportScript -FixtureRoot $fixture -ScriptSwitches @('-Check')
    Assert-Equal -Expected 0 -Actual $checkConverged.ExitCode -Message ("Check after rewrite should pass. Output: " + $checkConverged.Output)

    # 7. 未知项目键应立即失败。
    $unknown = Invoke-ExportScript -FixtureRoot $fixture -ProjectList @('does-not-exist')
    Assert-Equal -Expected 1 -Actual $unknown.ExitCode -Message "Unknown project key should fail with exit 1."

    # 8. 尚未编辑线协议结构的项目，应在调用编译器前被拒绝。
    $notOnboarded = Invoke-ExportScript -FixtureRoot $fixture -ProjectList @('moba')
    Assert-Equal -Expected 1 -Actual $notOnboarded.ExitCode -Message "Project without wire schemas should fail with exit 1."
    Assert-True -Condition ($notOnboarded.Output -match 'owns no') -Message "Not-onboarded failure should explain the missing wire schemas."
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Recurse -Force $tempRoot
    }
}

if ($failures -gt 0) {
    Write-Host ("export-protocol-wire tests FAILED: {0} assertion(s)." -f $failures) -ForegroundColor Red
    exit 1
}

Write-Host 'export-protocol-wire tests PASSED.' -ForegroundColor Green
exit 0

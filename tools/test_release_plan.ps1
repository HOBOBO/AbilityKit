param(
    [string]$EvidenceDirectory = 'local/Logs/core-stabilization/release-plan-tests'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidencePath = if ([System.IO.Path]::IsPathRooted($EvidenceDirectory)) { $EvidenceDirectory } else { Join-Path $repoRoot $EvidenceDirectory }
$evidenceRoot = [System.IO.Path]::GetFullPath($evidencePath)
$null = New-Item -ItemType Directory -Force -Path $evidenceRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$startedAt = (Get-Date).ToUniversalTime().ToString('o')

<#
    运行真实发布 CLI 的隔离仓库测试，并保留 TAP 与输入哈希，供统一门禁运行器收集。
    测试不会读取正式发布清单，也不会在工作仓库创建标签或访问远端。
#>
$testScript = Join-Path $repoRoot 'tools/publish/release.test.js'
$inputs = @('tools/publish/release.js', 'tools/publish/release-inputs.js', 'tools/publish/release.test.js', 'tools/test_release_plan.ps1')
$hashes = @{}
foreach ($inputPath in $inputs) {
    $hashes[$inputPath] = (Get-FileHash -LiteralPath (Join-Path $repoRoot $inputPath) -Algorithm SHA256).Hash.ToLowerInvariant()
}
$output = & node --test --test-reporter=tap $testScript 2>&1
$testExit = $LASTEXITCODE
$text = $output -join "`n"
$logPath = Join-Path $evidenceRoot 'release-plan-tests.tap'
[System.IO.File]::WriteAllText($logPath, $text + "`n", $utf8)
foreach ($inputPath in $inputs) {
    $finalHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $inputPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hashes[$inputPath] -ne $finalHash) { throw "测试期间输入已变化，不能使用本次证据：$inputPath" }
}
$summary = [ordered]@{
    startedAt = $startedAt
    endedAt = (Get-Date).ToUniversalTime().ToString('o')
    exitCode = $testExit
    nodeVersion = (& node --version | Out-String).Trim()
    gitVersion = (& git --version | Out-String).Trim()
    inputs = $hashes
    log = $logPath
}
[System.IO.File]::WriteAllText((Join-Path $evidenceRoot 'results.json'), ($summary | ConvertTo-Json -Depth 5) + "`n", $utf8)
Write-Output $text
if ($testExit -ne 0) { throw "发布计划隔离测试失败，退出码 $testExit，见 $logPath" }

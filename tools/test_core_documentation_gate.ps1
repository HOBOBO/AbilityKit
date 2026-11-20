param(
    [string]$EvidenceDirectory = 'local/Logs/core-stabilization/core-xml-gate-probes'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidencePath = if ([System.IO.Path]::IsPathRooted($EvidenceDirectory)) { $EvidenceDirectory } else { Join-Path $repoRoot $EvidenceDirectory }
$evidenceRoot = [System.IO.Path]::GetFullPath($evidencePath)
$fixtureRoot = Join-Path $evidenceRoot ([Guid]::NewGuid().ToString('N'))
$utf8 = New-Object System.Text.UTF8Encoding($false)
$results = New-Object 'System.Collections.Generic.List[object]'

<#
    在独立工程中导入真实构建策略，以编译器诊断验证门禁，而不修改正式源码或 API 基线。
    夹具保留在本地证据目录，不进入包；它不编译 Unity，也不验证运行时行为。
#>
function Write-Utf8 {
    param([string]$Path, [string]$Text)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

function New-Fixture {
    param([string]$Name, [string]$Policy)
    $directory = Join-Path $fixtureRoot $Name
    $null = New-Item -ItemType Directory -Force -Path $directory
    $policyXml = [System.Security.SecurityElement]::Escape($Policy)
    Write-Utf8 (Join-Path $directory 'Probe.csproj') @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AbilityKitStable>true</AbilityKitStable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DirectoryBuildTargetsPath>$policyXml</DirectoryBuildTargetsPath>
  </PropertyGroup>
</Project>
"@
    $restoreOutput = & dotnet restore (Join-Path $directory 'Probe.csproj') --source $fixtureRoot --verbosity minimal 2>&1
    $restoreExit = $LASTEXITCODE
    Write-Utf8 (Join-Path $directory 'restore.log') ($restoreOutput -join "`n")
    if ($restoreExit -ne 0) { throw "夹具恢复失败：$directory" }
    return $directory
}

function Invoke-Probe {
    param([string]$Directory, [string]$Name, [string]$Source, [string[]]$Codes, [bool]$ExpectFailure)
    Write-Utf8 (Join-Path $Directory 'Probe.cs') $Source
    $output = & dotnet build (Join-Path $Directory 'Probe.csproj') --no-restore --no-incremental --verbosity minimal 2>&1
    $buildExit = $LASTEXITCODE
    $buildText = $output -join "`n"
    $logPath = Join-Path $Directory "$Name.log"
    Write-Utf8 $logPath $buildText
    if (($buildExit -ne 0) -ne $ExpectFailure) { throw "夹具退出状态不符合预期：$Name，见 $logPath" }
    $severity = if ($ExpectFailure) { 'error' } else { 'warning' }
    foreach ($code in $Codes) {
        if ($buildText -notmatch "\b$severity $code\b") { throw "未得到预期 $severity $code：见 $logPath" }
    }
    $results.Add([pscustomobject]@{ name = $Name; exitCode = $buildExit; expectedSeverity = $severity; codes = $Codes; log = $logPath })
}

$null = New-Item -ItemType Directory -Force -Path $fixtureRoot
$core = New-Fixture 'core' (Join-Path $repoRoot 'src/AbilityKit.Core/Directory.Build.targets')
$shared = New-Fixture 'shared' (Join-Path $repoRoot 'src/Directory.Build.targets')
$cases = [ordered]@{
    CS1591 = 'public class MissingDocumentation { }'
    CS1570 = @'
/// <summary>
/// 未闭合的摘要。
public class MalformedDocumentation { }
'@
    CS1573 = @'
/// <summary>
/// 参数说明夹具。
/// </summary>
public class ParameterDocumentation
{
    /// <summary>
    /// 检查部分参数遗漏说明。
    /// </summary>
    /// <param name="first">第一个值。</param>
    public void Run(int first, int second) { }
}
'@
    CS1574 = @'
/// <summary>
/// 引用不存在的类型 <see cref="MissingDocumentationType"/>。
/// </summary>
public class UnresolvedDocumentation { }
'@
    CS1587 = @'
/// <summary>
/// 不能附着到命名空间的文档。
/// </summary>
namespace MisplacedDocumentation { }
'@
}
foreach ($case in $cases.GetEnumerator()) {
    Invoke-Probe $core $case.Key $case.Value @($case.Key) $true
}
Invoke-Probe $core 'valid' @'
/// <summary>
/// 正确的中文多行文档。
/// </summary>
public class ValidDocumentation { }
'@ @() $false
Invoke-Probe $shared 'shared-baseline' ($cases.Values -join "`n") @($cases.Keys) $false

Write-Utf8 (Join-Path $evidenceRoot 'latest-results.json') (ConvertTo-Json -InputObject @($results.ToArray()) -Depth 4)
Write-Output "Core XML 文档门禁：五类错误拒绝、有效文档通过、共享策略仍保留五类警告，共 $($results.Count) 项验证通过。"

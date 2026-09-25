[CmdletBinding()]
param([string]$OutputPath = '')

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$unityRoot = Join-Path $repositoryRoot 'Unity'
$packageName = 'com.abilitykit.demo.tiny.logic'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot (
        'local/Logs/tiny-logic-validation-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
$projectRoot = [System.IO.Path]::GetFullPath($OutputPath)
if ($projectRoot.Equals($unityRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $projectRoot.StartsWith($unityRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be outside the source Unity project.'
}
if ((Test-Path -LiteralPath $projectRoot) -and
    (Get-ChildItem -LiteralPath $projectRoot -Force | Select-Object -First 1)) {
    throw "OutputPath already contains files: $projectRoot"
}

$sourcePackages = Join-Path $unityRoot 'Packages'
$sourceManifest = Get-Content -LiteralPath (Join-Path $sourcePackages 'manifest.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
$sourceLock = Get-Content -LiteralPath (Join-Path $sourcePackages 'packages-lock.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
$packageSource = Join-Path $sourcePackages $packageName
$package = Get-Content -LiteralPath (Join-Path $packageSource 'package.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
if (@($package.dependencies.PSObject.Properties).Count -ne 0) {
    throw 'Tiny Logic is no longer a zero-dependency package.'
}

$projectPackages = Join-Path $projectRoot 'Packages'
$projectSettings = Join-Path $projectRoot 'ProjectSettings'
[System.IO.Directory]::CreateDirectory((Join-Path $projectRoot 'Assets')) | Out-Null
[System.IO.Directory]::CreateDirectory($projectPackages) | Out-Null
[System.IO.Directory]::CreateDirectory($projectSettings) | Out-Null
Copy-Item -LiteralPath $packageSource -Destination (Join-Path $projectPackages $packageName) -Recurse
$sampleRoot = Join-Path $projectRoot 'Assets/Samples'
[System.IO.Directory]::CreateDirectory($sampleRoot) | Out-Null
foreach ($sample in $package.samples) {
    $source = Join-Path $packageSource $sample.path
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing Tiny Logic sample: $source" }
    Copy-Item -LiteralPath $source -Destination $sampleRoot -Recurse
}
Copy-Item -LiteralPath (Join-Path $unityRoot 'ProjectSettings/ProjectVersion.txt') `
    -Destination $projectSettings

$dependencies = [ordered]@{
    $packageName = $package.version
    'com.unity.test-framework' = $sourceLock.dependencies.'com.unity.test-framework'.version
}
foreach ($module in $sourceManifest.dependencies.PSObject.Properties) {
    if ($module.Name.StartsWith('com.unity.modules.', [System.StringComparison]::Ordinal)) {
        $dependencies[$module.Name] = $module.Value
    }
}
$manifest = [ordered]@{
    scopedRegistries = $sourceManifest.scopedRegistries
    dependencies = $dependencies
    testables = @($packageName)
}
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $projectPackages 'manifest.json'),
    ($manifest | ConvertTo-Json -Depth 20), $utf8)
Write-Host "Tiny Logic validation project: $projectRoot"

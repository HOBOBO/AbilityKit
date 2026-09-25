[CmdletBinding()]
param(
    [string]$OutputPath = '',
    [switch]$Standalone
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$unityRoot = Join-Path $repositoryRoot 'Unity'
$packagesRoot = Join-Path $unityRoot 'Packages'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot (
        'local/Logs/tiny-unity-validation-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
$projectRoot = [System.IO.Path]::GetFullPath($OutputPath)
if ($projectRoot.Equals($unityRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $projectRoot.StartsWith($unityRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be outside the source Unity project.'
}
if (Test-Path -LiteralPath $projectRoot) {
    if (Get-ChildItem -LiteralPath $projectRoot -Force | Select-Object -First 1) {
        throw "OutputPath already contains files: $projectRoot"
    }
}
$sourceManifest = Get-Content -LiteralPath (Join-Path $packagesRoot 'manifest.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
$sourceLock = Get-Content -LiteralPath (Join-Path $packagesRoot 'packages-lock.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json

$available = @{}
foreach ($directory in Get-ChildItem -LiteralPath $packagesRoot -Directory) {
    $packageFile = Join-Path $directory.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $packageFile)) { continue }
    $package = Get-Content -LiteralPath $packageFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($available.ContainsKey($package.name)) {
        throw "Duplicate local Unity package: $($package.name)"
    }
    $available[$package.name] = @{ Directory = $directory.FullName; Manifest = $package }
}

$dependencies = [ordered]@{}
$pending = [System.Collections.Generic.Queue[string]]::new()
if (-not $Standalone) { $pending.Enqueue('com.abilitykit.demo.starter') }
$pending.Enqueue('com.abilitykit.demo.tiny')
while ($pending.Count -gt 0) {
    $name = $pending.Dequeue()
    if ($dependencies.Contains($name)) { continue }
    if (-not $available.ContainsKey($name)) { throw "Missing local Unity package: $name" }
    $entry = $available[$name]
    $dependencies[$name] = $entry.Manifest.version
    foreach ($dependency in $entry.Manifest.dependencies.PSObject.Properties) {
        if ($available.ContainsKey($dependency.Name)) {
            $pending.Enqueue($dependency.Name)
        }
        else {
            $declared = $sourceManifest.dependencies.PSObject.Properties[$dependency.Name]
            $dependencies[$dependency.Name] = if ($null -ne $declared) {
                $declared.Value
            } else {
                $dependency.Value
            }
        }
    }
}
if ($Standalone -and $dependencies.Contains('com.abilitykit.demo.starter')) {
    throw 'Standalone Tiny project unexpectedly depends on Starter.'
}

foreach ($module in $sourceManifest.dependencies.PSObject.Properties) {
    if ($module.Name.StartsWith('com.unity.modules.', [System.StringComparison]::Ordinal)) {
        $dependencies[$module.Name] = $module.Value
    }
}
$dependencies['com.unity.test-framework'] =
    $sourceLock.dependencies.'com.unity.test-framework'.version

$projectPackages = Join-Path $projectRoot 'Packages'
$projectScenes = Join-Path $projectRoot 'Assets/Scenes'
$projectSettings = Join-Path $projectRoot 'ProjectSettings'
foreach ($directory in @($projectPackages, $projectScenes, $projectSettings)) {
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}
foreach ($name in $dependencies.Keys) {
    if (-not $available.ContainsKey($name)) { continue }
    $destination = Join-Path $projectPackages $name
    [System.IO.Directory]::CreateDirectory($destination) | Out-Null
    Get-ChildItem -LiteralPath $available[$name].Directory -Force |
        Copy-Item -Destination $destination -Recurse -Force
}
$sampleRoot = Join-Path $projectRoot 'Assets/Samples/Tiny'
[System.IO.Directory]::CreateDirectory($sampleRoot) | Out-Null
foreach ($name in @('com.abilitykit.demo.tiny.logic', 'com.abilitykit.demo.tiny')) {
    foreach ($sample in $available[$name].Manifest.samples) {
        $source = Join-Path $available[$name].Directory $sample.path
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing Tiny sample: $source" }
        Copy-Item -LiteralPath $source -Destination $sampleRoot -Recurse
    }
}
$manifest = [ordered]@{
    scopedRegistries = $sourceManifest.scopedRegistries
    dependencies = $dependencies
    testables = @('com.abilitykit.demo.tiny')
}
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $projectPackages 'manifest.json'),
    ($manifest | ConvertTo-Json -Depth 20), $utf8)

$tinySceneMeta = Join-Path $packagesRoot 'com.abilitykit.demo.tiny/Scenes/TinyDemoGameplayScene.unity.meta'
if ($Standalone) {
    $templateAssets = Join-Path $PSScriptRoot 'tiny-consumer-template/Assets'
    Get-ChildItem -LiteralPath $templateAssets -Force |
        Copy-Item -Destination (Join-Path $projectRoot 'Assets') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tiny-consumer-template/README.md') `
        -Destination (Join-Path $projectRoot 'README.md')
    $entryScene = Join-Path $projectScenes 'ConsumerLobby.unity'
    $entryScenePath = 'Assets/Scenes/ConsumerLobby.unity'
}
else {
    $entryScene = Join-Path $unityRoot 'Assets/Scenes/StarterScene.unity'
    Copy-Item -LiteralPath $entryScene -Destination $projectScenes -Force
    Copy-Item -LiteralPath ($entryScene + '.meta') -Destination $projectScenes -Force
    $entryScenePath = 'Assets/Scenes/StarterScene.unity'
}
Copy-Item -LiteralPath (Join-Path $unityRoot 'ProjectSettings/ProjectVersion.txt') -Destination $projectSettings -Force
$entryGuid = ([regex]::Match((Get-Content -LiteralPath ($entryScene + '.meta') -Raw),
    '(?m)^guid: ([0-9a-f]{32})\r?$')).Groups[1].Value
$tinyGuid = ([regex]::Match((Get-Content -LiteralPath $tinySceneMeta -Raw),
    '(?m)^guid: ([0-9a-f]{32})\r?$')).Groups[1].Value
if (-not $entryGuid -or -not $tinyGuid) { throw 'Tiny scene GUID metadata is incomplete.' }
$buildSettings = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: $entryScenePath
    guid: $entryGuid
  - enabled: 1
    path: Packages/com.abilitykit.demo.tiny/Scenes/TinyDemoGameplayScene.unity
    guid: $tinyGuid
  m_configObjects: {}
"@
[System.IO.File]::WriteAllText((Join-Path $projectSettings 'EditorBuildSettings.asset'),
    $buildSettings, $utf8)

Write-Host "Tiny validation project: $projectRoot"
Write-Host "Mode: $(if ($Standalone) { 'standalone consumer' } else { 'Starter' })"
Write-Host "Local packages: $(@($dependencies.Keys | Where-Object { $available.ContainsKey($_) }).Count)"

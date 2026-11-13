[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameResRoot
)

$ErrorActionPreference = 'Stop'

# The script is intentionally anchored to the repository instead of the caller's
# current directory. This keeps the same command usable from an elevated shell,
# Rider, or the Unity package directory.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unityRoot = Join-Path $repositoryRoot 'Unity'
$packageRoot = Join-Path $unityRoot 'Packages/com.abilitykit.demo.moba.view.runtime'
$sourceAssets = Join-Path (Resolve-Path -LiteralPath $GameResRoot).Path 'Unity/Assets'
$runtimeMount = Join-Path $packageRoot 'Resources/ChampionVfx'
$dependencyMount = Join-Path $packageRoot 'GameAssets/GameResDependencies'

if (-not (Test-Path -LiteralPath $sourceAssets -PathType Container)) {
    throw "Game-res Unity/Assets directory does not exist: $sourceAssets"
}
if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "moba.view.runtime package directory does not exist: $packageRoot"
}
$puppetShaders = Join-Path $sourceAssets 'Puppet/Shaders'
if (-not (Test-Path -LiteralPath $puppetShaders -PathType Container)) {
    throw "Required shared shader directory does not exist: $puppetShaders"
}

$heroes = @(
    '08.Jinx_Arcanist', '09.Veigar_Hextech', '10.Gragas_StarGuardian',
    '11.Karthus_Project', '12.Lux_DarkStar', '13.Jhin_Battlecast',
    '14.Void_Annie'
)

# These are all dependencies observed from the extracted hero prefabs. Keep the
# complete set here so linking one hero never produces a partial Unity import.
$dependencies = @(
    '01.KarmaQ_StarGuardian', '02.BraumR_Arcade', '03.BardW_Project',
    '05.BrandW_Void', '07.AsheR_Arclight', '08.Jinx_Arcanist',
    '09.Veigar_Hextech', '10.Gragas_StarGuardian', '11.Karthus_Project',
    '12.Lux_DarkStar', '13.Jhin_Battlecast', '14.Void_Annie',
    # Hero materials reference the shared Puppet shaders by GUID. Mount the
    # Puppet root so Unity sees Puppet/Shaders and its .meta files together.
    'Puppet'
)

function Get-CanonicalPath([string]$path) {
    return ([IO.Path]::GetFullPath((Resolve-Path -LiteralPath $path).Path)).TrimEnd('\').ToLowerInvariant()
}

function Get-JunctionTarget([System.IO.FileSystemInfo]$item) {
    $target = $item.Target
    if ($target -is [Array]) { $target = $target[0] }
    if ([string]::IsNullOrWhiteSpace([string]$target)) { return $null }
    if ([IO.Path]::IsPathRooted([string]$target)) { return [string]$target }
    return Join-Path $item.DirectoryName ([string]$target)
}

function Add-Junction([string]$link, [string]$target) {
    if (-not (Test-Path -LiteralPath $target -PathType Container)) {
        throw "Required source directory does not exist: $target"
    }
    if (-not (Test-Path -LiteralPath ($target + '.meta') -PathType Leaf)) {
        throw "Required source directory .meta file does not exist: $target.meta"
    }

    if (Test-Path -LiteralPath $link) {
        $item = Get-Item -LiteralPath $link -Force
        if ($item.LinkType -ne 'Junction') {
            throw "Refusing to replace existing non-Junction path: $link"
        }

        $existingTarget = Get-JunctionTarget $item
        if ($null -ne $existingTarget -and
            (Get-CanonicalPath $existingTarget) -eq (Get-CanonicalPath $target)) {
            Write-Host "Already linked: $link -> $target"
            return
        }
        throw "Refusing to replace existing Junction with a different target: $link"
    }

    New-Item -ItemType Junction -Path $link -Target (Get-CanonicalPath $target) | Out-Null
    Write-Host "Created Junction: $link -> $target"
}

New-Item -ItemType Directory -Force -Path $runtimeMount, $dependencyMount | Out-Null

foreach ($hero in $heroes) {
    Add-Junction (Join-Path $runtimeMount $hero) (Join-Path $sourceAssets "ExtractedPrefabs/$hero")
}
foreach ($dependency in $dependencies) {
    Add-Junction (Join-Path $dependencyMount $dependency) (Join-Path $sourceAssets $dependency)
}

Write-Host ''
Write-Host 'Game-res Junctions are ready for com.abilitykit.demo.moba.view.runtime.'
Write-Host 'Refresh the Unity AssetDatabase or restart Unity before loading Resources assets.'

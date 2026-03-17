param(
  [Parameter(Mandatory=$true)][string]$TargetDirectory,
  [Parameter(Mandatory=$true)][string]$ProjectName,
  [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$Profile,
  [string]$RepoUnityDirectory,
  [switch]$LinkOdin,
  [switch]$CopyUnsafe,
  [switch]$LinkOnly,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string]$Path) {
  return [System.IO.Path]::GetFullPath($Path)
}

function Ensure-Directory([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path | Out-Null
  }
}

function Copy-Directory([string]$SourceDir, [string]$DestDir) {
  Ensure-Directory $DestDir
  Get-ChildItem -LiteralPath $SourceDir | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $DestDir -Recurse -Force
  }
}

function Remove-IfExists([string]$Path) {
  if (Test-Path -LiteralPath $Path) {
    $maxRetry = 8
    $delayMs = 350

    for ($i = 1; $i -le $maxRetry; $i++) {
      try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return
      }
      catch {
        if ($i -eq $maxRetry) {
          $msg = "Failed to delete path: $Path`n" +
                 "Reason: $($_.Exception.Message)`n" +
                 "Hint: Close Unity/UnityHub and any IDE/file explorer that may be using the project (common lock: Library/Search/transactions.db), then retry."
          throw $msg
        }

        Start-Sleep -Milliseconds $delayMs
      }
    }
  }
}

function Load-Profile([string]$ProfilesDir, [string]$ProfileName) {
  $profilePath = Join-Path $ProfilesDir ($ProfileName + '.json')
  if (-not (Test-Path -LiteralPath $profilePath)) {
    throw "Profile not found: $profilePath"
  }

  $raw = Get-Content -LiteralPath $profilePath -Raw
  $json = $raw | ConvertFrom-Json
  if ($null -eq $json.packages -or $json.packages.Count -eq 0) {
    throw "Profile has no packages: $profilePath"
  }

  return $json
}

function Ensure-Junction([string]$LinkPath, [string]$TargetPath) {
  if (Test-Path -LiteralPath $LinkPath) {
    Remove-Item -LiteralPath $LinkPath -Recurse -Force
  }

  $null = New-Item -ItemType Junction -Path $LinkPath -Target $TargetPath
}

if ([string]::IsNullOrWhiteSpace($RepoUnityDirectory)) {
  $RepoUnityDirectory = Split-Path -Parent $PSScriptRoot
}

$RepoUnityDirectory = Resolve-FullPath $RepoUnityDirectory
$RepoPackagesDirectory = Join-Path $RepoUnityDirectory 'Packages'
$RepoProjectSettingsDirectory = Join-Path $RepoUnityDirectory 'ProjectSettings'
$RepoManifestPath = Join-Path $RepoPackagesDirectory 'manifest.json'
$RepoAssetsDirectory = Join-Path $RepoUnityDirectory 'Assets'
$ProfilesDirectory = Join-Path $PSScriptRoot 'Profiles'

if (-not (Test-Path -LiteralPath $RepoPackagesDirectory)) { throw "Repo Packages directory not found: $RepoPackagesDirectory" }
if (-not (Test-Path -LiteralPath $RepoProjectSettingsDirectory)) { throw "Repo ProjectSettings directory not found: $RepoProjectSettingsDirectory" }
if (-not (Test-Path -LiteralPath $RepoManifestPath)) { throw "Repo manifest.json not found: $RepoManifestPath" }

$TargetDirectory = Resolve-FullPath $TargetDirectory
$ProjectPath = Join-Path $TargetDirectory $ProjectName
$TargetPackagesDirectory = Join-Path $ProjectPath 'Packages'
$TargetProjectSettingsDirectory = Join-Path $ProjectPath 'ProjectSettings'
$TargetAssetsDirectory = Join-Path $ProjectPath 'Assets'
$TargetPluginsDirectory = Join-Path $TargetAssetsDirectory 'Plugins'

$profileJson = Load-Profile $ProfilesDirectory $Profile

if (-not $LinkOnly) {
  if (Test-Path -LiteralPath $ProjectPath) {
    if (-not $Force) {
      throw "Target project already exists: $ProjectPath (use -Force to overwrite)"
    }
    Remove-IfExists $ProjectPath
  }

  Ensure-Directory $ProjectPath
  Ensure-Directory $TargetPackagesDirectory
  Ensure-Directory $TargetAssetsDirectory

  Copy-Directory $RepoProjectSettingsDirectory $TargetProjectSettingsDirectory
  Copy-Item -LiteralPath $RepoManifestPath -Destination (Join-Path $TargetPackagesDirectory 'manifest.json') -Force
}

Ensure-Directory $TargetPackagesDirectory

if ($LinkOdin) {
  Ensure-Directory $TargetAssetsDirectory
  Ensure-Directory $TargetPluginsDirectory

  $odinDirs = @(
    'Sirenix',
    'Sirenix 1'
  )

  foreach ($d in $odinDirs) {
    $src = Join-Path (Join-Path $RepoAssetsDirectory 'Plugins') $d
    if (Test-Path -LiteralPath $src) {
      $dst = Join-Path $TargetPluginsDirectory $d
      Ensure-Junction $dst $src
    }
  }
}

if ($CopyUnsafe) {
  Ensure-Directory $TargetAssetsDirectory
  Ensure-Directory $TargetPluginsDirectory

  $unsafeDll = 'System.Runtime.CompilerServices.Unsafe.dll'
  $unsafeSrc = Join-Path (Join-Path $RepoAssetsDirectory 'Plugins') $unsafeDll
  $unsafeMetaSrc = $unsafeSrc + '.meta'

  if (Test-Path -LiteralPath $unsafeSrc) {
    Copy-Item -LiteralPath $unsafeSrc -Destination (Join-Path $TargetPluginsDirectory $unsafeDll) -Force
  }

  if (Test-Path -LiteralPath $unsafeMetaSrc) {
    Copy-Item -LiteralPath $unsafeMetaSrc -Destination (Join-Path $TargetPluginsDirectory ($unsafeDll + '.meta')) -Force
  }
}

foreach ($pkg in $profileJson.packages) {
  $src = Join-Path $RepoPackagesDirectory $pkg
  $dst = Join-Path $TargetPackagesDirectory $pkg

  if (-not (Test-Path -LiteralPath $src)) {
    throw "Package directory not found in repo: $src"
  }

  Ensure-Junction $dst $src
}

Write-Host "Done. Project: $ProjectPath"
Write-Host "Profile: $Profile"
Write-Host "Linked packages: $($profileJson.packages.Count)"

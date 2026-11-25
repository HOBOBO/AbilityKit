param(
    [Parameter(Mandatory = $false)]
    [string]$AuthoritativeResourcesRoot = "Unity\Packages\com.abilitykit.demo.moba.view.runtime\Resources",

    [Parameter(Mandatory = $false)]
    [string]$ConsoleReplicaRoot = "src\AbilityKit.Demo.Moba.Console\Configs",

    [Parameter(Mandatory = $false)]
    [string[]]$ConfigDirs = @("moba", "ability"),

    [Parameter(Mandatory = $false)]
    [switch]$Check,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$Apply,

    [Parameter(Mandatory = $false)]
    [switch]$DeleteExtra
)

$ErrorActionPreference = "Stop"

$selectedModes = @($Check, $DryRun, $Apply) | Where-Object { $_ }
if ($selectedModes.Count -gt 1) {
    throw "Check, DryRun, and Apply are mutually exclusive."
}

# 无参数调用仅审计。将包拥有的权威 JSON 发布到文件系统副本，始终需要显式授权。
$isCheckMode = $Check -or (!$DryRun -and !$Apply)

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageResourcesRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "Unity\Packages\com.abilitykit.demo.moba.view.runtime\Resources"))
$absSourceRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $AuthoritativeResourcesRoot))
$absTargetRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ConsoleReplicaRoot))
$unityRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "Unity"))
$unityPrefix = $unityRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (!$absSourceRoot.Equals($packageResourcesRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "AuthoritativeResourcesRoot must be Unity\Packages\com.abilitykit.demo.moba.view.runtime\Resources. Production MOBA JSON has one authoritative source."
}

if ($absTargetRoot.Equals($unityRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $absTargetRoot.StartsWith($unityPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ConsoleReplicaRoot must not target Unity. Package-owned resources are authoritative and cannot be a publication target."
}

if ($ConfigDirs.Count -eq 0) {
    throw "ConfigDirs must include at least one owned publication directory."
}

$allowedConfigDirs = @("moba", "ability")
foreach ($dir in $ConfigDirs) {
    if ([string]::IsNullOrWhiteSpace($dir) -or $allowedConfigDirs -notcontains $dir) {
        throw "ConfigDirs may only contain the owned publication directories: moba, ability."
    }
}

function Write-Info {
    param([string]$Message)
    Write-Host "[sync_moba_json_configs] $Message"
}

function Resolve-RelativePath {
    param(
        [string]$BasePath,
        [string]$FullPath
    )

    $baseUri = [System.Uri](([System.IO.Path]::GetFullPath($BasePath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar)
    $fullUri = [System.Uri]([System.IO.Path]::GetFullPath($FullPath))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fullUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-NormalizedJson {
    param([string]$Path)

    $text = Get-Content -Path $Path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ""
    }

    try {
        # Windows PowerShell 5.1 不支持 ConvertFrom-Json -Depth。
        # 将深度限制保留在 ConvertTo-Json 上，使归一化时仍保留嵌套玩法配置。
        $json = ConvertFrom-Json -InputObject $text
        return ($json | ConvertTo-Json -Depth 100 -Compress)
    }
    catch {
        throw "Invalid JSON: $Path. $($_.Exception.Message)"
    }
}

function Test-SemanticJsonEqual {
    param(
        [string]$LeftPath,
        [string]$RightPath
    )

    return (Get-NormalizedJson $LeftPath) -eq (Get-NormalizedJson $RightPath)
}

function Ensure-Directory {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        if ($DryRun -or $isCheckMode) {
            Write-Info "Would create directory: $Path"
        }
        else {
            New-Item -ItemType Directory -Force -Path $Path | Out-Null
        }
    }
}

if (!(Test-Path $absSourceRoot)) {
    Write-Error "Authoritative resource root not found: $absSourceRoot"
    exit 1
}

Write-Info "AuthoritativeResourcesRoot: $absSourceRoot"
Write-Info "ConsoleReplicaRoot: $absTargetRoot"
Write-Info "Owned ConfigDirs: $($ConfigDirs -join ', ')"
Write-Info "Mode: $(if ($isCheckMode) { 'Check' } elseif ($DryRun) { 'DryRun' } else { 'Apply' })"
Write-Info "DeleteExtra: $DeleteExtra"

$totalCopied = 0
$totalChanged = 0
$totalMissing = 0
$totalExtra = 0
$totalInvalid = 0

foreach ($dir in $ConfigDirs) {
    if ([string]::IsNullOrWhiteSpace($dir)) {
        continue
    }

    $sourceDir = Join-Path $absSourceRoot $dir
    $targetDir = Join-Path $absTargetRoot $dir

    if (!(Test-Path $sourceDir)) {
        Write-Info "Skip missing source dir: $sourceDir"
        continue
    }

    Ensure-Directory $targetDir

    $sourceFiles = Get-ChildItem -Path $sourceDir -Filter "*.json" -File -Recurse | Sort-Object FullName
    foreach ($sourceFile in $sourceFiles) {
        $relative = Resolve-RelativePath $sourceDir $sourceFile.FullName
        $targetFile = Join-Path $targetDir $relative
        $targetFileDir = Split-Path -Parent $targetFile

        try {
            [void](Get-NormalizedJson $sourceFile.FullName)
        }
        catch {
            Write-Info "Invalid source JSON: $($sourceFile.FullName)"
            Write-Info $_.Exception.Message
            $totalInvalid++
            continue
        }

        if (!(Test-Path $targetFile)) {
            Write-Info "Missing target: $dir\$relative"
            $totalMissing++
            if (!$isCheckMode) {
                Ensure-Directory $targetFileDir
                if ($DryRun) {
                    Write-Info "Would copy: $($sourceFile.FullName) -> $targetFile"
                }
                else {
                    Copy-Item -Path $sourceFile.FullName -Destination $targetFile -Force
                    $totalCopied++
                }
            }
            continue
        }

        try {
            $equal = Test-SemanticJsonEqual $sourceFile.FullName $targetFile
        }
        catch {
            Write-Info "Invalid target JSON: $targetFile"
            Write-Info $_.Exception.Message
            $totalInvalid++
            $equal = $false
        }

        if (!$equal) {
            Write-Info "Changed target: $dir\$relative"
            $totalChanged++
            if (!$isCheckMode) {
                if ($DryRun) {
                    Write-Info "Would copy: $($sourceFile.FullName) -> $targetFile"
                }
                else {
                    Copy-Item -Path $sourceFile.FullName -Destination $targetFile -Force
                    $totalCopied++
                }
            }
        }
    }

    if ($DeleteExtra -or $isCheckMode) {
        if (Test-Path $targetDir) {
            $targetFiles = Get-ChildItem -Path $targetDir -Filter "*.json" -File -Recurse | Sort-Object FullName
            foreach ($targetFile in $targetFiles) {
                $relative = Resolve-RelativePath $targetDir $targetFile.FullName
                $sourceFile = Join-Path $sourceDir $relative
                if (!(Test-Path $sourceFile)) {
                    Write-Info "Extra target: $dir\$relative"
                    $totalExtra++
                    if ($DeleteExtra -and !$isCheckMode) {
                        if ($DryRun) {
                            Write-Info "Would delete: $($targetFile.FullName)"
                        }
                        else {
                            Remove-Item -Path $targetFile.FullName -Force
                        }
                    }
                }
            }
        }
    }
}

Write-Info "Summary: copied=$totalCopied, changed=$totalChanged, missing=$totalMissing, extra=$totalExtra, invalid=$totalInvalid"

if ($isCheckMode -and ($totalChanged -gt 0 -or $totalMissing -gt 0 -or $totalExtra -gt 0 -or $totalInvalid -gt 0)) {
    Write-Info "Check failed. Package-owned authoritative JSON and Console replicas have drifted. Review with -DryRun, then publish with -Apply."
    exit 2
}

if ($totalInvalid -gt 0) {
    exit 3
}

Write-Info "Done."

param(
    [Parameter(Mandatory = $false)]
    [string]$LubanDllPath = "..\\Tools\\Luban\\Luban.dll",

    [Parameter(Mandatory = $false)]
    [string]$LubanConfPath = "MiniTemplate\\luban.conf",

    [Parameter(Mandatory = $false)]
    [string]$OutputJsonDir = "..\\..\\Unity\\Assets\\Resources\\moba",

    [Parameter(Mandatory = $false)]
    [string]$OutputBytesDir = "..\\..\\Unity\\Assets\\Resources\\moba_bytes"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$absJsonDir = [System.IO.Path]::GetFullPath((Join-Path $root $OutputJsonDir))
$absBytesDir = [System.IO.Path]::GetFullPath((Join-Path $root $OutputBytesDir))

New-Item -ItemType Directory -Force -Path $absJsonDir | Out-Null
New-Item -ItemType Directory -Force -Path $absBytesDir | Out-Null

$stageJsonDir = [System.IO.Path]::GetFullPath((Join-Path $root ".generated\\json"))
$stageBytesDir = [System.IO.Path]::GetFullPath((Join-Path $root ".generated\\bytes"))
$stageCodeDir = [System.IO.Path]::GetFullPath((Join-Path $root ".generated\\code"))

New-Item -ItemType Directory -Force -Path $stageJsonDir | Out-Null
New-Item -ItemType Directory -Force -Path $stageBytesDir | Out-Null
New-Item -ItemType Directory -Force -Path $stageCodeDir | Out-Null

Write-Host "[export_moba_configs] OutputJsonDir: $absJsonDir"
Write-Host "[export_moba_configs] OutputBytesDir: $absBytesDir"
Write-Host "[export_moba_configs] StageJsonDir: $stageJsonDir"
Write-Host "[export_moba_configs] StageBytesDir: $stageBytesDir"
Write-Host "[export_moba_configs] StageCodeDir: $stageCodeDir"

$absLubanDll = [System.IO.Path]::GetFullPath((Join-Path $root $LubanDllPath))
$absConf = [System.IO.Path]::GetFullPath((Join-Path $root $LubanConfPath))

Write-Host "[export_moba_configs] LubanDll: $absLubanDll"
Write-Host "[export_moba_configs] Conf: $absConf"

if (!(Test-Path $absLubanDll)) {
    Write-Host "[export_moba_configs] Luban dll not found: $absLubanDll"
    exit 1
}

if (!(Test-Path $absConf)) {
    Write-Host "[export_moba_configs] luban.conf not found: $absConf"
    exit 1
}

dotnet $absLubanDll -t all -d json --conf $absConf -x outputDataDir=$stageJsonDir
dotnet $absLubanDll -t all -d bin --conf $absConf -x outputDataDir=$stageBytesDir
dotnet $absLubanDll -t client -c cs-bin --conf $absConf -x outputCodeDir=$stageCodeDir

Copy-Item -Path (Join-Path $stageJsonDir "*") -Destination $absJsonDir -Recurse -Force
Copy-Item -Path (Join-Path $stageBytesDir "*") -Destination $absBytesDir -Recurse -Force

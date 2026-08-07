[CmdletBinding()]
param(
    [string]$PackagesRoot = '',
    [string[]]$PackageNames = @(
        'com.abilitykit.demo.moba.runtime',
        'com.abilitykit.demo.moba.view.runtime',
        'com.abilitykit.demo.shooter.runtime',
        'com.abilitykit.demo.shooter.view.runtime'
    )
)

$ErrorActionPreference = 'Stop'
$PackageNames = @(
    $PackageNames |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)
if ($PackageNames.Count -eq 0) {
    throw 'At least one package name is required.'
}

if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    $PackagesRoot = Join-Path $PSScriptRoot '..\Unity\Packages'
}
$resolvedPackagesRoot = (Resolve-Path -LiteralPath $PackagesRoot).Path
$assemblyOwners = @{}
$guidOwners = @{}
$externalAssemblyOwners = @{
    'MemoryPack' = 'com.cysharp.memorypack'
    'MemoryPack.dll' = 'com.cysharp.memorypack'
    'Newtonsoft.Json' = 'com.unity.nuget.newtonsoft-json'
    'Newtonsoft.Json.dll' = 'com.unity.nuget.newtonsoft-json'
}

function Read-JsonFile([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-PackageOwner([string]$Path) {
    $relative = $Path.Substring($resolvedPackagesRoot.Length + 1)
    $packageFolder = $relative.Split([IO.Path]::DirectorySeparatorChar)[0]
    $manifestPath = Join-Path $resolvedPackagesRoot "$packageFolder\package.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return $null
    }

    try {
        return (Read-JsonFile $manifestPath).name
    }
    catch {
        Write-Warning "Skipping package with invalid manifest '$manifestPath'."
        return $null
    }
}

function Test-IsProductionAssembly([string]$PackageRoot, [string]$AssemblyPath) {
    $relative = $AssemblyPath.Substring($PackageRoot.Length + 1)
    return $relative -notmatch '(^|[\\/])(Tests?|Editor|Samples?)([\\/]|$)'
}

Get-ChildItem -LiteralPath $resolvedPackagesRoot -Recurse -Filter '*.asmdef' | ForEach-Object {
    $owner = Get-PackageOwner $_.FullName
    if ([string]::IsNullOrWhiteSpace($owner)) {
        return
    }

    $assembly = Read-JsonFile $_.FullName
    if (-not [string]::IsNullOrWhiteSpace($assembly.name)) {
        $assemblyOwners[$assembly.name] = $owner
    }

    $metaPath = "$($_.FullName).meta"
    if (Test-Path -LiteralPath $metaPath) {
        $guidLine = Select-String -LiteralPath $metaPath -Pattern '^guid:\s*(\S+)' | Select-Object -First 1
        if ($guidLine) {
            $guidOwners[$guidLine.Matches[0].Groups[1].Value] = $owner
        }
    }
}

$hasMissingDependencies = $false
foreach ($packageName in $PackageNames) {
    $packageRoot = Join-Path $resolvedPackagesRoot $packageName
    $manifestPath = Join-Path $packageRoot 'package.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Package manifest not found: $manifestPath"
    }

    $manifest = Read-JsonFile $manifestPath
    $declaredDependencies = @{}
    if ($manifest.dependencies) {
        foreach ($property in $manifest.dependencies.PSObject.Properties) {
            $declaredDependencies[$property.Name] = $true
        }
    }

    $requiredPackages = @{}
    $unmappedAssemblies = @{}
    Get-ChildItem -LiteralPath $packageRoot -Recurse -Filter '*.asmdef' |
        Where-Object { Test-IsProductionAssembly $packageRoot $_.FullName } |
        ForEach-Object {
            $assembly = Read-JsonFile $_.FullName
            $references = @($assembly.references) + @($assembly.precompiledReferences)
            foreach ($reference in $references) {
                if ([string]::IsNullOrWhiteSpace($reference)) {
                    continue
                }

                $owner = $null
                if ($reference.StartsWith('GUID:', [StringComparison]::OrdinalIgnoreCase)) {
                    $owner = $guidOwners[$reference.Substring(5)]
                }
                elseif ($assemblyOwners.ContainsKey($reference)) {
                    $owner = $assemblyOwners[$reference]
                }
                elseif ($externalAssemblyOwners.ContainsKey($reference)) {
                    $owner = $externalAssemblyOwners[$reference]
                }
                else {
                    $unmappedAssemblies[$reference] = $true
                }

                if (-not [string]::IsNullOrWhiteSpace($owner) -and $owner -ne $packageName) {
                    $requiredPackages[$owner] = $true
                }
            }
        }

    $missing = @($requiredPackages.Keys | Where-Object { -not $declaredDependencies.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0) {
        $hasMissingDependencies = $true
        Write-Host "[$packageName] missing direct dependencies:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }
    else {
        Write-Host "[$packageName] production asmdef dependencies are complete." -ForegroundColor Green
    }

    if ($unmappedAssemblies.Count -gt 0) {
        $names = $unmappedAssemblies.Keys | Sort-Object
        Write-Warning "[$packageName] owner unknown for: $($names -join ', ')"
    }
}

if ($hasMissingDependencies) {
    exit 1
}

<#
.SYNOPSIS
按程序集引用审计选定 UPM 包的直接依赖，只读，不修改清单。
.DESCRIPTION
默认检查运行时，排除测试及样例。IncludeEditor 同时检查随包交付的编辑器程序集。
StrictInternalReferences 拒绝无法定位的 AbilityKit 程序集和 GUID 引用；其它未知外部程序集仅告警。
此检查不分析源码条件编译、解析注册表或替代干净 Unity 工程消费验证。
#>
[CmdletBinding()]
param(
    [string]$PackagesRoot = '',
    [string[]]$PackageNames = @(
        'com.abilitykit.demo.moba.runtime',
        'com.abilitykit.demo.moba.view.runtime',
        'com.abilitykit.demo.shooter.runtime',
        'com.abilitykit.demo.shooter.view.runtime'
    ),
    [switch]$IncludeEditor,
    [switch]$StrictInternalReferences
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

function Test-IsAuditedAssembly([string]$PackageRoot, [string]$AssemblyPath, [object]$Assembly) {
    $relative = $AssemblyPath.Substring($PackageRoot.Length + 1)
    if ($relative -match '(^|[\\/])(Tests?|Samples?)~?([\\/]|$)') {
        return $false
    }

    <#
    测试程序集也可能放在 Runtime 下，不能只按目录识别。
    #>
    if (@($Assembly.optionalUnityReferences) -contains 'TestAssemblies' -or
        @($Assembly.references) -contains 'UnityEngine.TestRunner' -or
        @($Assembly.references) -contains 'UnityEditor.TestRunner') {
        return $false
    }

    $platforms = @($Assembly.includePlatforms)
    if (-not $IncludeEditor -and
        ($relative -match '(^|[\\/])Editor([\\/]|$)' -or
         ($platforms.Count -eq 1 -and $platforms[0] -eq 'Editor'))) {
        return $false
    }
    return $true
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

$hasErrors = $false
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
    $auditedAssemblyCount = 0
    Get-ChildItem -LiteralPath $packageRoot -Recurse -Filter '*.asmdef' |
        ForEach-Object {
            $assembly = Read-JsonFile $_.FullName
            if (-not (Test-IsAuditedAssembly $packageRoot $_.FullName $assembly)) {
                return
            }
            $auditedAssemblyCount++
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
                if ([string]::IsNullOrWhiteSpace($owner)) {
                    $unmappedAssemblies[$reference] = $true
                }

                if (-not [string]::IsNullOrWhiteSpace($owner) -and $owner -ne $packageName) {
                    $requiredPackages[$owner] = $true
                }
            }
        }

    $missing = @($requiredPackages.Keys | Where-Object { -not $declaredDependencies.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0) {
        $hasErrors = $true
        Write-Host "[$packageName] missing direct dependencies:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }
    else {
        Write-Host "[$packageName] audited asmdef dependencies are complete (assemblies=$auditedAssemblyCount, includeEditor=$IncludeEditor)." -ForegroundColor Green
    }

    if ($unmappedAssemblies.Count -gt 0) {
        $names = $unmappedAssemblies.Keys | Sort-Object
        Write-Warning "[$packageName] owner unknown for: $($names -join ', ')"
        $unresolvedInternal = @($names | Where-Object { $_ -match '^(AbilityKit[.]|com[.]abilitykit[.]|GUID:)' })
        if ($StrictInternalReferences -and $unresolvedInternal.Count -gt 0) {
            $hasErrors = $true
            Write-Host "[$packageName] unresolved internal assembly references: $($unresolvedInternal -join ', ')" -ForegroundColor Red
        }
    }
}

if ($hasErrors) {
    exit 1
}

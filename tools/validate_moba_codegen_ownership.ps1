param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [System.Collections.Generic.List[string]]::new()

function Resolve-RepoPath {
    param([string]$Path)

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Test-Contract {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$Failure
    )

    if ($Condition) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
        return
    }

    Write-Host "[FAIL] $Name - $Failure" -ForegroundColor Red
    $failures.Add("${Name}: $Failure")
}

# NOTE: com.abilitykit.codegen (framework source generator) was removed in a9e5e0b2.
# Only the analyzer remains on the framework side; collect existing roots dynamically.
$frameworkSourceRoots = @(
    'Unity/Packages/com.abilitykit.analyzer/DotNet~/AbilityKit.Analyzer'
) | Where-Object { Test-Path (Resolve-RepoPath $_) }
$frameworkSources = @($frameworkSourceRoots | ForEach-Object {
    Get-ChildItem -LiteralPath (Resolve-RepoPath $_) -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
})
$mobaNamedSources = @($frameworkSources | Where-Object { $_.Name -like 'Moba*.cs' })
$frameworkLeaks = @($frameworkSources | Select-String -Pattern 'AbilityKit\.Demo\.Moba\.CodeGen|AK2\d{3}')

Test-Contract -Name 'Framework packages contain no Moba source files' `
    -Condition ($mobaNamedSources.Count -eq 0) `
    -Failure (($mobaNamedSources.FullName) -join ', ')
Test-Contract -Name 'Framework packages contain no MOBA namespace or diagnostic IDs' `
    -Condition ($frameworkLeaks.Count -eq 0) `
    -Failure (($frameworkLeaks | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join ', ')

$mobaProject = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/DotNet~/AbilityKit.Demo.Moba.CodeGen/AbilityKit.Demo.Moba.CodeGen.csproj'
$mobaDll = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/AbilityKit.Demo.Moba.CodeGen.dll'
$mobaDllMeta = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/AbilityKit.Demo.Moba.CodeGen.dll.meta'
$mobaPackage = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/package.json'
$runtimePackage = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/package.json'

Test-Contract -Name 'Dedicated MOBA CodeGen project exists' `
    -Condition (Test-Path -LiteralPath $mobaProject -PathType Leaf) `
    -Failure $mobaProject
Test-Contract -Name 'Dedicated MOBA CodeGen package DLL exists' `
    -Condition (Test-Path -LiteralPath $mobaDll -PathType Leaf) `
    -Failure $mobaDll

$metaText = if (Test-Path -LiteralPath $mobaDllMeta -PathType Leaf) {
    Get-Content -LiteralPath $mobaDllMeta -Raw
}
else {
    ''
}
Test-Contract -Name 'MOBA DLL is imported as a RoslynAnalyzer' `
    -Condition ($metaText -match '(?m)^\s*-\s*RoslynAnalyzer\s*$') `
    -Failure "RoslynAnalyzer label is missing from $mobaDllMeta"

$mobaPackageJson = Get-Content -LiteralPath $mobaPackage -Raw | ConvertFrom-Json
$runtimePackageJson = Get-Content -LiteralPath $runtimePackage -Raw | ConvertFrom-Json
Test-Contract -Name 'MOBA CodeGen package identity is stable' `
    -Condition ([string]$mobaPackageJson.name -eq 'com.abilitykit.demo.moba.codegen') `
    -Failure "Unexpected package name '$($mobaPackageJson.name)'"
Test-Contract -Name 'MOBA runtime declares the CodeGen package dependency' `
    -Condition ([string]$runtimePackageJson.dependencies.'com.abilitykit.demo.moba.codegen' -eq [string]$mobaPackageJson.version) `
    -Failure 'Runtime package dependency is missing or does not match the CodeGen package version.'

$mobaCoreProjectText = Get-Content -LiteralPath (Resolve-RepoPath 'src/AbilityKit.Demo.Moba.Core/AbilityKit.Demo.Moba.Core.csproj') -Raw
Test-Contract -Name 'Moba.Core consumes the dedicated Roslyn project' `
    -Condition ($mobaCoreProjectText -match 'com\.abilitykit\.demo\.moba\.codegen' -and
        $mobaCoreProjectText -notmatch 'com\.abilitykit\.codegen[\\/]DotNet~[\\/]AbilityKit\.SourceGenerator') `
    -Failure 'Moba.Core must use the dedicated MOBA analyzer reference and not the generic SourceGenerator project.'

if ($failures.Count -gt 0) {
    Write-Error ("MOBA CodeGen ownership validation failed:`n - " + ($failures -join "`n - "))
    exit 1
}

Write-Host 'MOBA CodeGen ownership validation passed.' -ForegroundColor Cyan
exit 0

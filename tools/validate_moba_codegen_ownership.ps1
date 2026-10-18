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
$mobaTestsProject = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/DotNet~/AbilityKit.Demo.Moba.CodeGen.Tests/AbilityKit.Demo.Moba.CodeGen.Tests.csproj'
$mobaDll = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/AbilityKit.Demo.Moba.CodeGen.dll'
$mobaDllMeta = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/AbilityKit.Demo.Moba.CodeGen.dll.meta'
$mobaPackage = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.codegen/package.json'
$runtimePackage = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/package.json'
$planActionRegistry = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Core/PlanActionModuleRegistry.cs'
$eventRegistry = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Eventing/MobaEventSubscriptionRegistry.cs'
$planActionManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Core/MobaGeneratedPlanActionManifest.cs'
$eventManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Eventing/MobaGeneratedEventMappingManifest.cs'
$configManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Infrastructure/Config/Core/MobaGeneratedConfigTableManifest.cs'
$bootstrapRegistry = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/MobaBootstrapStageInitializer.cs'
$bootstrapManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/MobaGeneratedBootstrapStageManifest.cs'
$snapshotRegistry = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Snapshot/MobaSnapshotEmitterRegistry.cs'
$snapshotManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Snapshot/MobaGeneratedSnapshotEmitterManifest.cs'
$routeRegistry = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Routing/MobaBattleRouteRegistry.cs'
$routeManifest = Resolve-RepoPath 'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Routing/MobaGeneratedBattleRouteManifest.cs'

Test-Contract -Name 'Dedicated MOBA CodeGen project exists' `
    -Condition (Test-Path -LiteralPath $mobaProject -PathType Leaf) `
    -Failure $mobaProject
Test-Contract -Name 'MOBA CodeGen contract test project exists' `
    -Condition (Test-Path -LiteralPath $mobaTestsProject -PathType Leaf) `
    -Failure $mobaTestsProject
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

$planActionRegistryText = if (Test-Path -LiteralPath $planActionRegistry -PathType Leaf) { Get-Content -LiteralPath $planActionRegistry -Raw } else { '' }
$eventRegistryText = if (Test-Path -LiteralPath $eventRegistry -PathType Leaf) { Get-Content -LiteralPath $eventRegistry -Raw } else { '' }
$planActionManifestText = if (Test-Path -LiteralPath $planActionManifest -PathType Leaf) { Get-Content -LiteralPath $planActionManifest -Raw } else { '' }
$eventManifestText = if (Test-Path -LiteralPath $eventManifest -PathType Leaf) { Get-Content -LiteralPath $eventManifest -Raw } else { '' }
$configManifestText = if (Test-Path -LiteralPath $configManifest -PathType Leaf) { Get-Content -LiteralPath $configManifest -Raw } else { '' }
$bootstrapRegistryText = if (Test-Path -LiteralPath $bootstrapRegistry -PathType Leaf) { Get-Content -LiteralPath $bootstrapRegistry -Raw } else { '' }
$bootstrapManifestText = if (Test-Path -LiteralPath $bootstrapManifest -PathType Leaf) { Get-Content -LiteralPath $bootstrapManifest -Raw } else { '' }
$snapshotRegistryText = if (Test-Path -LiteralPath $snapshotRegistry -PathType Leaf) { Get-Content -LiteralPath $snapshotRegistry -Raw } else { '' }
$snapshotManifestText = if (Test-Path -LiteralPath $snapshotManifest -PathType Leaf) { Get-Content -LiteralPath $snapshotManifest -Raw } else { '' }
$routeRegistryText = if (Test-Path -LiteralPath $routeRegistry -PathType Leaf) { Get-Content -LiteralPath $routeRegistry -Raw } else { '' }
$routeManifestText = if (Test-Path -LiteralPath $routeManifest -PathType Leaf) { Get-Content -LiteralPath $routeManifest -Raw } else { '' }

Test-Contract -Name 'PlanAction registry exposes production fallback kill switch' `
    -Condition ($planActionRegistryText -match 'AbilityKit\.Moba\.DisablePlanActionReflectionFallback') `
    -Failure 'PlanActionModuleRegistry must support disabling reflection fallback in production.'
Test-Contract -Name 'Event mapping registry exposes production fallback kill switch' `
    -Condition ($eventRegistryText -match 'AbilityKit\.Moba\.DisableEventMappingReflectionFallback') `
    -Failure 'MobaEventSubscriptionRegistry must support disabling reflection fallback in production.'
Test-Contract -Name 'Generated PlanAction manifest has a generator hook' `
    -Condition ($planActionManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedPlanActionManifest must expose the generated registration hook.'
Test-Contract -Name 'Generated event mapping manifest has a generator hook' `
    -Condition ($eventManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedEventMappingManifest must expose the generated registration hook.'
Test-Contract -Name 'PlanAction fallback is disabled in non-development Unity players' `
    -Condition ($planActionRegistryText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'PlanAction reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Event mapping fallback is disabled in non-development Unity players' `
    -Condition ($eventRegistryText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'Event mapping reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Config table registry exposes production fallback kill switch' `
    -Condition ($configManifestText -match 'AbilityKit\.Moba\.DisableConfigTableReflectionFallback') `
    -Failure 'MobaGeneratedConfigTableManifest must support disabling reflection fallback in production.'
Test-Contract -Name 'Bootstrap stage registry exposes production fallback kill switch' `
    -Condition ($bootstrapRegistryText -match 'AbilityKit\.Moba\.DisableBootstrapStageReflectionFallback') `
    -Failure 'MobaBootstrapStageInitializer must support disabling reflection fallback in production.'
Test-Contract -Name 'Snapshot emitter registry exposes production fallback kill switch' `
    -Condition ($snapshotRegistryText -match 'AbilityKit\.Moba\.DisableSnapshotEmitterReflectionFallback') `
    -Failure 'MobaSnapshotEmitterRegistry must support disabling reflection fallback in production.'
Test-Contract -Name 'Battle route registry exposes production fallback kill switch' `
    -Condition ($routeRegistryText -match 'AbilityKit\.Moba\.DisableBattleRouteReflectionFallback') `
    -Failure 'MobaBattleRouteRegistry must support disabling reflection fallback in production.'
Test-Contract -Name 'Config table fallback is disabled in non-development Unity players' `
    -Condition ($configManifestText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'Config table reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Bootstrap stage fallback is disabled in non-development Unity players' `
    -Condition ($bootstrapRegistryText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'Bootstrap stage reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Snapshot emitter fallback is disabled in non-development Unity players' `
    -Condition ($snapshotRegistryText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'Snapshot emitter reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Battle route fallback is disabled in non-development Unity players' `
    -Condition ($routeRegistryText -match '#if !UNITY_EDITOR && !DEVELOPMENT_BUILD') `
    -Failure 'Battle route reflection fallback must be disabled in non-development Unity players.'
Test-Contract -Name 'Generated config table manifest has a generator hook' `
    -Condition ($configManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedConfigTableManifest must expose the generated registration hook.'
Test-Contract -Name 'Generated bootstrap stage manifest has a generator hook' `
    -Condition ($bootstrapManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedBootstrapStageManifest must expose the generated registration hook.'
Test-Contract -Name 'Generated snapshot emitter manifest has a generator hook' `
    -Condition ($snapshotManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedSnapshotEmitterManifest must expose the generated registration hook.'
Test-Contract -Name 'Generated battle route manifest has a generator hook' `
    -Condition ($routeManifestText -match 'static partial void AddGenerated') `
    -Failure 'MobaGeneratedBattleRouteManifest must expose the generated registration hook.'

if ($failures.Count -gt 0) {
    Write-Error ("MOBA CodeGen ownership validation failed:`n - " + ($failures -join "`n - "))
    exit 1
}

Write-Host 'MOBA CodeGen ownership validation passed.' -ForegroundColor Cyan
exit 0

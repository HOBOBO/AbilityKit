param()

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtimeRoot = Join-Path $repoRoot 'Unity\Packages\com.abilitykit.core\Runtime'
$foundationAsmdefPath = Join-Path $runtimeRoot 'com.abilitykit.core.asmdef'
$unityAsmdefPath = Join-Path $runtimeRoot 'Unity\AbilityKit.Core.Unity.asmdef'
$coreProjectPath = Join-Path $repoRoot 'src\AbilityKit.Core\AbilityKit.Core.csproj'
$shippedApiPath = Join-Path $repoRoot 'src\AbilityKit.Core\PublicAPI.Shipped.txt'
$unshippedApiPath = Join-Path $repoRoot 'src\AbilityKit.Core\PublicAPI.Unshipped.txt'
$packagesRoot = Join-Path $repoRoot 'Unity\Packages'
$continuousPackageRoot = Join-Path $packagesRoot 'com.abilitykit.continuous'
$continuousRuntimeRoot = Join-Path $continuousPackageRoot 'Runtime'
$continuousAsmdefPath = Join-Path $continuousRuntimeRoot 'AbilityKit.Continuous.asmdef'
$continuousProjectPath = Join-Path $repoRoot 'src\AbilityKit.Continuous\AbilityKit.Continuous.csproj'
$mobaRuntimePackageRoot = Join-Path $packagesRoot 'com.abilitykit.demo.moba.runtime'
$mobaViewRuntimePackageRoot = Join-Path $packagesRoot 'com.abilitykit.demo.moba.view.runtime'
$mobaEditorPackageRoot = Join-Path $packagesRoot 'com.abilitykit.demo.moba.editor'
$diagnosticsPackageRoot = Join-Path $packagesRoot 'com.abilitykit.diagnostics'
$diagnosticsRuntimeRoot = Join-Path $diagnosticsPackageRoot 'Runtime'
$unityCoreProjectPath = Join-Path $repoRoot 'Unity\AbilityKit.Core.csproj'

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-PrunedFiles {
    param(
        [string[]]$Roots,
        [string]$Pattern,
        [string[]]$ExcludedDirectoryNames = @()
    )

    $excludedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $ExcludedDirectoryNames) {
        $null = $excludedNames.Add($name)
    }

    $pendingDirectories = New-Object 'System.Collections.Generic.Stack[string]'
    foreach ($root in $Roots) {
        if ([System.IO.Directory]::Exists($root)) {
            $pendingDirectories.Push([System.IO.Path]::GetFullPath($root))
        }
    }

    while ($pendingDirectories.Count -gt 0) {
        $directory = $pendingDirectories.Pop()
        foreach ($file in [System.IO.Directory]::EnumerateFiles($directory, $Pattern, [System.IO.SearchOption]::TopDirectoryOnly)) {
            [System.IO.FileInfo]::new($file)
        }

        foreach ($childDirectory in [System.IO.Directory]::EnumerateDirectories($directory)) {
            if (-not $excludedNames.Contains([System.IO.Path]::GetFileName($childDirectory))) {
                $pendingDirectories.Push($childDirectory)
            }
        }
    }
}

$foundationAsmdef = Get-Content -LiteralPath $foundationAsmdefPath -Raw | ConvertFrom-Json
Assert-Contract ($foundationAsmdef.name -eq 'AbilityKit.Core') 'Foundation asmdef name must remain AbilityKit.Core.'
Assert-Contract ($foundationAsmdef.noEngineReferences -eq $true) 'AbilityKit.Core must keep noEngineReferences enabled.'
# Contract update (2026-08 fixed-point migration, phase P2): the ONLY Unity assembly reference
# Core may hold is the deterministic math kernel AbilityKit.Deterministic
# (MathUtil.Sqrt / Vec Magnitude / Quat normalization route through the fixed-point kernel).
# No other references may be added; Deterministic itself stays dependency-free
# (both ship in the same release batch).
$coreReferences = @($foundationAsmdef.references)
$coreReferencesOk = ($coreReferences.Count -eq 1 -and $coreReferences[0] -eq 'AbilityKit.Deterministic')
$coreReferencesMessage = 'AbilityKit.Core may reference only AbilityKit.Deterministic (deterministic math kernel, decision 2026-08). Found: [' + ($coreReferences -join ', ') + ']'
Assert-Contract $coreReferencesOk $coreReferencesMessage
Assert-Contract ($foundationAsmdef.allowUnsafeCode -eq $false) 'AbilityKit.Core must not enable unsafe code without a reviewed boundary change.'

$unityAsmdef = Get-Content -LiteralPath $unityAsmdefPath -Raw | ConvertFrom-Json
Assert-Contract ($unityAsmdef.name -eq 'AbilityKit.Core.Unity') 'Unity adapter asmdef name must remain AbilityKit.Core.Unity.'
Assert-Contract ($unityAsmdef.noEngineReferences -eq $false) 'AbilityKit.Core.Unity is the explicit engine adapter boundary.'
$unityReferences = @($unityAsmdef.references)
Assert-Contract ($unityReferences.Count -eq 1 -and $unityReferences[0] -eq 'AbilityKit.Core') 'AbilityKit.Core.Unity may reference only AbilityKit.Core.'

Assert-Contract (Test-Path -LiteralPath $continuousPackageRoot -PathType Container) 'The extracted com.abilitykit.continuous package is missing.'
$continuousAsmdef = Get-Content -LiteralPath $continuousAsmdefPath -Raw | ConvertFrom-Json
Assert-Contract ($continuousAsmdef.name -eq 'AbilityKit.Continuous') 'Continuous asmdef name must remain AbilityKit.Continuous.'
Assert-Contract ($continuousAsmdef.noEngineReferences -eq $true) 'AbilityKit.Continuous must keep noEngineReferences enabled.'
Assert-Contract (@($continuousAsmdef.references).Count -eq 0) 'AbilityKit.Continuous must remain independent from Core and other Unity assemblies.'

$continuousPackage = Get-Content -LiteralPath (Join-Path $continuousPackageRoot 'package.json') -Raw | ConvertFrom-Json
Assert-Contract ($continuousPackage.name -eq 'com.abilitykit.continuous') 'Continuous package name must remain com.abilitykit.continuous.'
Assert-Contract ($null -eq $continuousPackage.dependencies -or @($continuousPackage.dependencies.psobject.Properties).Count -eq 0) 'com.abilitykit.continuous must remain dependency-free.'
Assert-Contract (Test-Path -LiteralPath $continuousProjectPath -PathType Leaf) 'AbilityKit.Continuous .NET project is missing.'

$diagnosticsAsmdef = Get-Content -LiteralPath (Join-Path $diagnosticsRuntimeRoot 'com.abilitykit.diagnostics.runtime.asmdef') -Raw | ConvertFrom-Json
Assert-Contract ($diagnosticsAsmdef.name -eq 'AbilityKit.Diagnostics') 'Diagnostics runtime asmdef name must remain AbilityKit.Diagnostics.'
Assert-Contract (@($diagnosticsAsmdef.references) -contains 'AbilityKit.Core') 'Diagnostics debug drawing must explicitly reference AbilityKit.Core math contracts.'
$diagnosticsPackage = Get-Content -LiteralPath (Join-Path $diagnosticsPackageRoot 'package.json') -Raw | ConvertFrom-Json
Assert-Contract ($diagnosticsPackage.dependencies.'com.abilitykit.core' -eq '0.1.0') 'Diagnostics must declare its direct Core dependency.'
$mobaEditorAsmdef = Get-Content -LiteralPath (Join-Path $mobaEditorPackageRoot 'Editor\com.abilitykit.demo.moba.editor.asmdef') -Raw | ConvertFrom-Json
Assert-Contract (@($mobaEditorAsmdef.references) -contains 'AbilityKit.Diagnostics') 'MOBA Editor must reference Diagnostics runtime directly.'
Assert-Contract (@($mobaEditorAsmdef.references) -contains 'AbilityKit.Diagnostics.Editor') 'MOBA Editor must reference Diagnostics editor adapters directly.'
Assert-Contract (-not (@($mobaEditorAsmdef.references) -contains 'AbilityKit.Core.Editor')) 'MOBA Editor must not retain the legacy Core Editor debug-draw dependency.'
$mobaEditorPackage = Get-Content -LiteralPath (Join-Path $mobaEditorPackageRoot 'package.json') -Raw | ConvertFrom-Json
Assert-Contract ($mobaEditorPackage.dependencies.'com.abilitykit.diagnostics' -eq '0.1.0') 'MOBA Editor must declare its direct Diagnostics dependency.'
$legacyCoreUnityConsumers = New-Object System.Collections.Generic.List[string]
$packageAsmdefs = Get-PrunedFiles @($packagesRoot) '*.asmdef'
foreach ($asmdefPath in $packageAsmdefs) {
    if ($asmdefPath.FullName -eq $unityAsmdefPath) {
        continue
    }

    $asmdef = Get-Content -LiteralPath $asmdefPath.FullName -Raw | ConvertFrom-Json
    if (@($asmdef.references) -contains 'AbilityKit.Core.Unity') {
        $relativePath = $asmdefPath.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $legacyCoreUnityConsumers.Add("$relativePath references deprecated AbilityKit.Core.Unity; use an owner-local Unity adapter.")
    }
}
Assert-Contract ($legacyCoreUnityConsumers.Count -eq 0) ("Core.Unity consumer violations:`n" + ($legacyCoreUnityConsumers -join "`n"))

$packageRoots = @(
    Get-ChildItem -LiteralPath $packagesRoot -Directory -Filter 'com.abilitykit.*' |
        ForEach-Object { $_.FullName }
)
$packageSources = @(Get-PrunedFiles $packageRoots '*.cs')
$packageSourceTexts = @{}
foreach ($source in $packageSources) {
    $packageSourceTexts[$source.FullName] = [System.IO.File]::ReadAllText($source.FullName)
}

$forbiddenPatterns = @(
    '(?m)^\s*using\s+UnityEngine(?:\s*;|\.)',
    '(?m)^\s*using\s+Unity\.Collections(?:\s*;|\.)',
    '(?m)^\s*using\s+Unity\.Jobs(?:\s*;|\.)',
    '(?m)^\s*using\s+Unity\.Burst(?:\s*;|\.)',
    '\bglobal::UnityEngine\.',
    '\bglobal::Unity\.Collections\.',
    '\bglobal::Unity\.Jobs\.',
    '\bglobal::Unity\.Burst\.'
)
$auditRegexOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::Compiled
$coreNamespaceRegex = [regex]::new('(?m)^\s*namespace\s+(AbilityKit\.Core(?:\.[A-Za-z0-9_.]+)?)', $auditRegexOptions)
$continuousNamespaceRegex = [regex]::new('(?m)^\s*namespace\s+AbilityKit\.Continuous(?:\.[A-Za-z0-9_.]+)?', $auditRegexOptions)
$legacyContinuousRegex = [regex]::new('AbilityKit\.Core\.Continuous', $auditRegexOptions)
$legacyConfigurationRegex = [regex]::new('AbilityKit\.Core\.(Configuration|Reflection)', $auditRegexOptions)
$mobaNamespaceRegex = [regex]::new('(?m)^\s*namespace\s+(AbilityKit\.Demo\.Moba\.(?:View\.Settings|Bootstrap))(?:\s|$)', $auditRegexOptions)
$migratedInfrastructureUsingRegex = [regex]::new('(?m)^\s*using\s+(?:(?:static\s+)?AbilityKit\.Core\.(?:Debugging|Utilities)(?:\.|\s*;)|[A-Za-z_]\w*\s*=\s*AbilityKit\.Core\.(?:Debugging|Utilities)\s*;)', $auditRegexOptions)
$qualifiedMigratedInfrastructureRegex = [regex]::new('AbilityKit\.Core\.(?:Debugging|Utilities)\.', $auditRegexOptions)
$coreMarkersUsingRegex = [regex]::new('(?m)^\s*using\s+(?:AbilityKit\.Core\.Markers\s*;|[A-Za-z_]\w*\s*=\s*AbilityKit\.Core\.Markers\s*;)', $auditRegexOptions)
$frozenMarkerSymbolRegex = [regex]::new('\b(?:MarkerSystem|MarkerBootstrapper\s*<|KeyedMarkerBootstrapper\s*<|StaticMarkerBootstrapper\s*<)', $auditRegexOptions)
$qualifiedFrozenMarkerSymbolRegex = [regex]::new('AbilityKit\.Core\.Markers\.(?:MarkerSystem|MarkerBootstrapper|KeyedMarkerBootstrapper|StaticMarkerBootstrapper)', $auditRegexOptions)
$debugDrawNamespaceRegex = [regex]::new('(?m)^\s*namespace\s+AbilityKit\.Diagnostics\.DebugDraw(?:\s|\{|$)', $auditRegexOptions)
$violations = New-Object System.Collections.Generic.List[string]
$foundationSources = $packageSources |
    Where-Object {
        $_.FullName.StartsWith($runtimeRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        -not $_.FullName.StartsWith((Join-Path $runtimeRoot 'Unity') + '\', [StringComparison]::OrdinalIgnoreCase)
    }

foreach ($source in $foundationSources) {
    $text = $packageSourceTexts[$source.FullName]
    foreach ($pattern in $forbiddenPatterns) {
        if ($text -match $pattern) {
            $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
            $violations.Add("$relativePath matches forbidden pattern '$pattern'.")
        }
    }
}

Assert-Contract ($violations.Count -eq 0) ("Core foundation source boundary violations:`n" + ($violations -join "`n"))

$legacyNamespaceRules = @(
    [pscustomobject]@{
        Package = 'com.abilitykit.combat.collision.abstractions'
        Directory = 'Runtime\Math'
        NamespacePrefix = 'AbilityKit.Core.Mathematics'
        MaximumFiles = 5
        TargetNamespace = 'AbilityKit.Combat.Collision'
    },
    [pscustomobject]@{
        Package = 'com.abilitykit.combat.navigation'
        Directory = 'Runtime\Math'
        NamespacePrefix = 'AbilityKit.Core.Mathematics'
        MaximumFiles = 3
        TargetNamespace = 'AbilityKit.Combat.Navigation'
    },
    [pscustomobject]@{
        Package = 'com.abilitykit.record'
        Directory = 'Runtime\Record'
        NamespacePrefix = 'AbilityKit.Core.Recording'
        MaximumFiles = 56
        TargetNamespace = 'AbilityKit.Record'
    },
    [pscustomobject]@{
        Package = 'com.abilitykit.unity.pool'
        Directory = 'Runtime\Pool'
        NamespacePrefix = 'AbilityKit.Core.Pooling'
        MaximumFiles = 4
        TargetNamespace = 'AbilityKit.Unity.Pooling'
    },
    [pscustomobject]@{
        Package = 'com.abilitykit.world.snapshot'
        Directory = 'Runtime\SnapshotRouting'
        NamespacePrefix = 'AbilityKit.Core.Snapshots.Routing'
        MaximumFiles = 12
        TargetNamespace = 'AbilityKit.World.Snapshot.Routing'
    }
)

$namespaceViolations = New-Object System.Collections.Generic.List[string]
$legacyNamespaceCounts = @{}
foreach ($rule in $legacyNamespaceRules) {
    $legacyNamespaceCounts[$rule.Package] = 0
}

$namespaceDeclarations = New-Object System.Collections.Generic.List[object]
$continuousOwnershipViolations = New-Object System.Collections.Generic.List[string]
$configurationOwnershipViolations = New-Object System.Collections.Generic.List[string]
$extractedInfrastructureViolations = New-Object System.Collections.Generic.List[string]
$ripgrep = Get-Command 'rg' -ErrorAction SilentlyContinue
$corePackageRoot = Join-Path $packagesRoot 'com.abilitykit.core'

if ($null -eq $ripgrep) {
    foreach ($source in $packageSources) {
        $text = $packageSourceTexts[$source.FullName]
        if ([string]::IsNullOrEmpty($text)) {
            continue
        }

        $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $coreNamespaceMatch = $coreNamespaceRegex.Match($text)
        if ($coreNamespaceMatch.Success) {
            $namespaceDeclarations.Add([pscustomobject]@{
                Path = $source.FullName
                Namespace = $coreNamespaceMatch.Groups[1].Value
            })
        }

        if ($continuousNamespaceRegex.IsMatch($text) -and
            -not $source.FullName.StartsWith($continuousPackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $continuousOwnershipViolations.Add("$relativePath declares a namespace owned by com.abilitykit.continuous.")
        }

        if ($legacyContinuousRegex.IsMatch($text) -and
            -not $source.FullName.StartsWith($runtimeRoot + '\Continuous\', [StringComparison]::OrdinalIgnoreCase)) {
            $continuousOwnershipViolations.Add("$relativePath consumes deprecated AbilityKit.Core.Continuous; use AbilityKit.Continuous.")
        }

        if ($legacyConfigurationRegex.IsMatch($text) -and
            -not $source.FullName.StartsWith($runtimeRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $configurationOwnershipViolations.Add(
                "$relativePath consumes deprecated Core configuration/reflection infrastructure; use an owner-package API.")
        }

        $mobaNamespaceMatch = $mobaNamespaceRegex.Match($text)
        if ($mobaNamespaceMatch.Success) {
            $mobaNamespace = $mobaNamespaceMatch.Groups[1].Value
            $mobaOwnerRoot = if ($mobaNamespace -eq 'AbilityKit.Demo.Moba.View.Settings') {
                $mobaViewRuntimePackageRoot
            }
            else {
                $mobaRuntimePackageRoot
            }

            if (-not $source.FullName.StartsWith($mobaOwnerRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
                $configurationOwnershipViolations.Add("$relativePath declares '$mobaNamespace' outside its owner package.")
            }
        }

        $insideCore = $source.FullName.StartsWith($corePackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)
        $insideDiagnostics = $source.FullName.StartsWith($diagnosticsPackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)
        $usesMigratedInfrastructure = $migratedInfrastructureUsingRegex.IsMatch($text)
        if (-not $insideCore -and $usesMigratedInfrastructure) {
            $extractedInfrastructureViolations.Add("$relativePath consumes migrated Core debug-draw/disposal infrastructure.")
        }

        $usesCoreMarkers = $coreMarkersUsingRegex.IsMatch($text)
        $usesFrozenMarkerSymbol = $frozenMarkerSymbolRegex.IsMatch($text)
        $usesQualifiedFrozenMarkerSymbol = $qualifiedFrozenMarkerSymbolRegex.IsMatch($text)
        if (-not $insideCore -and (($usesCoreMarkers -and $usesFrozenMarkerSymbol) -or $usesQualifiedFrozenMarkerSymbol)) {
            $extractedInfrastructureViolations.Add("$relativePath consumes a frozen global Marker entry point.")
        }

        if (-not $insideDiagnostics -and $debugDrawNamespaceRegex.IsMatch($text)) {
            $extractedInfrastructureViolations.Add("$relativePath declares DebugDraw contracts outside com.abilitykit.diagnostics.")
        }
    }
}
else {
    $matches = @(& $ripgrep.Source -n --glob '*.cs' '^\s*namespace\s+AbilityKit\.Core(?:\.[A-Za-z0-9_.]+)?' $packagesRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing namespace ownership with exit code $LASTEXITCODE."
    }

    foreach ($matchLine in $matches) {
        $match = [regex]::Match(
            $matchLine,
            '^(?<path>.+?):\d+:\s*namespace\s+(?<namespace>AbilityKit\.Core(?:\.[A-Za-z0-9_.]+)?)')
        if (-not $match.Success) {
            continue
        }

        $namespaceDeclarations.Add([pscustomobject]@{
            Path = [System.IO.Path]::GetFullPath($match.Groups['path'].Value)
            Namespace = $match.Groups['namespace'].Value
        })
    }
}

foreach ($declaration in $namespaceDeclarations) {
    $relativeToPackages = $declaration.Path.Substring($packagesRoot.Length).TrimStart('\', '/')
    $pathParts = $relativeToPackages -split '[\\/]'
    if ($pathParts.Count -lt 2) {
        continue
    }

    $packageName = $pathParts[0]
    if ($packageName -eq 'com.abilitykit.core') {
        continue
    }

    $packageRoot = Join-Path $packagesRoot $packageName
    $relativeToPackage = $declaration.Path.Substring($packageRoot.Length).TrimStart('\', '/')
    $namespace = [string]$declaration.Namespace
    $matchingRule = $legacyNamespaceRules |
        Where-Object {
            $_.Package -eq $packageName -and
            $relativeToPackage.StartsWith($_.Directory + '\', [StringComparison]::OrdinalIgnoreCase) -and
            ($namespace -eq $_.NamespacePrefix -or $namespace.StartsWith($_.NamespacePrefix + '.', [StringComparison]::Ordinal))
        } |
        Select-Object -First 1

    if ($null -eq $matchingRule) {
        $relativePath = $declaration.Path.Substring($repoRoot.Length).TrimStart('\', '/')
        $namespaceViolations.Add("$relativePath declares namespace '$namespace', which is owned by com.abilitykit.core.")
        continue
    }

    $legacyNamespaceCounts[$matchingRule.Package]++
}

foreach ($rule in $legacyNamespaceRules) {
    $actualCount = [int]$legacyNamespaceCounts[$rule.Package]
    if ($actualCount -gt $rule.MaximumFiles) {
        $namespaceViolations.Add(
            "Legacy namespace allowance for $($rule.Package) grew from at most $($rule.MaximumFiles) to $actualCount files. " +
            "New code must use '$($rule.TargetNamespace)'.")
    }
}

Assert-Contract ($namespaceViolations.Count -eq 0) ("Core namespace ownership violations:`n" + ($namespaceViolations -join "`n"))

if ($null -ne $ripgrep) {
    $continuousDeclarations = @(& $ripgrep.Source -n --glob '*.cs' '^\s*namespace\s+AbilityKit\.Continuous(?:\.[A-Za-z0-9_.]+)?' $packagesRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing Continuous namespace ownership with exit code $LASTEXITCODE."
    }

    foreach ($line in $continuousDeclarations) {
        $path = ($line -split ':\d+:', 2)[0]
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($continuousPackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $relativePath = $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
            $continuousOwnershipViolations.Add("$relativePath declares a namespace owned by com.abilitykit.continuous.")
        }
    }

    $legacyContinuousUsages = @(& $ripgrep.Source -n --glob '*.cs' 'AbilityKit\.Core\.Continuous' $packagesRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing legacy Core Continuous usage with exit code $LASTEXITCODE."
    }

    foreach ($line in $legacyContinuousUsages) {
        $path = ($line -split ':\d+:', 2)[0]
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($runtimeRoot + '\Continuous\', [StringComparison]::OrdinalIgnoreCase)) {
            $relativePath = $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
            $continuousOwnershipViolations.Add("$relativePath consumes deprecated AbilityKit.Core.Continuous; use AbilityKit.Continuous.")
        }
    }
}

if ($null -ne $ripgrep) {
    $legacyInfrastructureUsages = @(& $ripgrep.Source -n --glob '*.cs' 'AbilityKit\.Core\.(Configuration|Reflection)' $packagesRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing legacy Core configuration/reflection usage with exit code $LASTEXITCODE."
    }

    foreach ($line in $legacyInfrastructureUsages) {
        $path = ($line -split ':\d+:', 2)[0]
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($runtimeRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $relativePath = $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
            $configurationOwnershipViolations.Add(
                "$relativePath consumes deprecated Core configuration/reflection infrastructure; use an owner-package API.")
        }
    }

    $mobaInfrastructureDeclarations = @(& $ripgrep.Source -n --glob '*.cs' '^\s*namespace\s+AbilityKit\.Demo\.Moba\.(View\.Settings|Bootstrap)(?:\s|$)' $packagesRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing MOBA infrastructure namespace ownership with exit code $LASTEXITCODE."
    }

    foreach ($line in $mobaInfrastructureDeclarations) {
        $match = [regex]::Match(
            $line,
            '^(?<path>.+?):\d+:\s*namespace\s+(?<namespace>AbilityKit\.Demo\.Moba\.(?:View\.Settings|Bootstrap))')
        if (-not $match.Success) {
            continue
        }

        $fullPath = [System.IO.Path]::GetFullPath($match.Groups['path'].Value)
        $namespace = $match.Groups['namespace'].Value
        $ownerRoot = if ($namespace -eq 'AbilityKit.Demo.Moba.View.Settings') {
            $mobaViewRuntimePackageRoot
        }
        else {
            $mobaRuntimePackageRoot
        }

        if (-not $fullPath.StartsWith($ownerRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $relativePath = $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
            $configurationOwnershipViolations.Add("$relativePath declares '$namespace' outside its owner package.")
        }
    }

    $directCoreSourceLinks = @(& $ripgrep.Source -n --glob '*.csproj' --glob '!**/obj/**' --glob '!**/bin/**' 'com\.abilitykit\.core[/\\]Runtime[/\\](Config|Reflection)' $repoRoot)
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed while auditing direct Core configuration/reflection source links with exit code $LASTEXITCODE."
    }

    foreach ($line in $directCoreSourceLinks) {
        $path = ($line -split ':\d+:', 2)[0]
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if ($fullPath -ne $coreProjectPath -and $fullPath -ne $unityCoreProjectPath) {
            $relativePath = $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
            $configurationOwnershipViolations.Add(
                "$relativePath directly links deprecated Core Config/Reflection sources; compile the owner-package implementation instead.")
        }
    }
}
else {
    $projects = Get-PrunedFiles @($repoRoot) '*.csproj' @('obj', 'bin', '.kilo', '.git')
    foreach ($project in $projects) {
        if ($project.FullName -eq $coreProjectPath -or $project.FullName -eq $unityCoreProjectPath) {
            continue
        }

        $text = [System.IO.File]::ReadAllText($project.FullName)
        if ($text -match 'com\.abilitykit\.core[/\\]Runtime[/\\](Config|Reflection)') {
            $relativePath = $project.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
            $configurationOwnershipViolations.Add(
                "$relativePath directly links deprecated Core Config/Reflection sources; compile the owner-package implementation instead.")
        }
    }
}

$managedProductionSources = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
$managedProductionSourceTexts = @{}
$managedSourceRoots = @(
    (Join-Path $repoRoot 'src'),
    (Join-Path $repoRoot 'Server')
)
$managedCompatibilityAllowRoots = @(
    (Join-Path $repoRoot 'src\AbilityKit.Core'),
    (Join-Path $repoRoot 'src\AbilityKit.Core.Tests')
)

$managedSources = Get-PrunedFiles $managedSourceRoots '*.cs' @('obj', 'bin')
foreach ($source in $managedSources) {
    $isAllowed = $false
    foreach ($allowRoot in $managedCompatibilityAllowRoots) {
        if ($source.FullName.StartsWith($allowRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $isAllowed = $true
            break
        }
    }
    if ($isAllowed) {
        continue
    }

    $managedProductionSources.Add($source)
    $text = [System.IO.File]::ReadAllText($source.FullName)
    $managedProductionSourceTexts[$source.FullName] = $text
    if ($legacyContinuousRegex.IsMatch($text)) {
        $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $continuousOwnershipViolations.Add(
            "$relativePath consumes deprecated AbilityKit.Core.Continuous; use AbilityKit.Continuous.")
    }
    if ($legacyConfigurationRegex.IsMatch($text)) {
        $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $configurationOwnershipViolations.Add(
            "$relativePath consumes deprecated Core configuration/reflection infrastructure; use an owner-package API.")
    }
}

Assert-Contract ($continuousOwnershipViolations.Count -eq 0) ("Continuous ownership violations:`n" + ($continuousOwnershipViolations -join "`n"))
Assert-Contract ($configurationOwnershipViolations.Count -eq 0) ("Configuration/reflection ownership violations:`n" + ($configurationOwnershipViolations -join "`n"))

if ($null -ne $ripgrep) {
    foreach ($source in $packageSources) {
        $text = $packageSourceTexts[$source.FullName]
        $insideCore = $source.FullName.StartsWith($corePackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)
        $insideDiagnostics = $source.FullName.StartsWith($diagnosticsPackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)
        $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')

        $usesMigratedInfrastructure = $migratedInfrastructureUsingRegex.IsMatch($text)
        if (-not $insideCore -and $usesMigratedInfrastructure) {
            $extractedInfrastructureViolations.Add("$relativePath consumes migrated Core debug-draw/disposal infrastructure.")
        }

        $usesCoreMarkers = $coreMarkersUsingRegex.IsMatch($text)
        $usesFrozenMarkerSymbol = $frozenMarkerSymbolRegex.IsMatch($text)
        $usesQualifiedFrozenMarkerSymbol = $qualifiedFrozenMarkerSymbolRegex.IsMatch($text)
        if (-not $insideCore -and (($usesCoreMarkers -and $usesFrozenMarkerSymbol) -or $usesQualifiedFrozenMarkerSymbol)) {
            $extractedInfrastructureViolations.Add("$relativePath consumes a frozen global Marker entry point.")
        }

        if (-not $insideDiagnostics -and $debugDrawNamespaceRegex.IsMatch($text)) {
            $extractedInfrastructureViolations.Add("$relativePath declares DebugDraw contracts outside com.abilitykit.diagnostics.")
        }
    }
}

foreach ($source in $managedProductionSources) {
    $text = $managedProductionSourceTexts[$source.FullName]
    $relativePath = $source.FullName.Substring($repoRoot.Length).TrimStart('\', '/')

    $usesMigratedInfrastructure =
        $migratedInfrastructureUsingRegex.IsMatch($text) -or
        $qualifiedMigratedInfrastructureRegex.IsMatch($text)
    if ($usesMigratedInfrastructure) {
        $extractedInfrastructureViolations.Add("$relativePath consumes migrated Core debug-draw/disposal infrastructure.")
    }

    $usesCoreMarkers = $coreMarkersUsingRegex.IsMatch($text)
    $usesFrozenMarkerSymbol = $frozenMarkerSymbolRegex.IsMatch($text)
    $usesQualifiedFrozenMarkerSymbol = $qualifiedFrozenMarkerSymbolRegex.IsMatch($text)
    if (($usesCoreMarkers -and $usesFrozenMarkerSymbol) -or $usesQualifiedFrozenMarkerSymbol) {
        $extractedInfrastructureViolations.Add("$relativePath consumes a frozen global Marker entry point.")
    }
}

Assert-Contract ($extractedInfrastructureViolations.Count -eq 0) ("Extracted infrastructure ownership violations:`n" + ($extractedInfrastructureViolations -join "`n"))

[xml]$coreProject = Get-Content -LiteralPath $coreProjectPath -Raw
$compileItems = @($coreProject.Project.ItemGroup.Compile)
$runtimeCompile = $compileItems | Where-Object { $_.Include -like '*com.abilitykit.core/Runtime/**/*.cs' } | Select-Object -First 1
Assert-Contract ($null -ne $runtimeCompile) 'AbilityKit.Core.csproj must compile the shared UPM runtime sources.'
Assert-Contract ([string]$runtimeCompile.Exclude -like '*Runtime/Unity/**/*.cs*') 'AbilityKit.Core.csproj must exclude Unity adapter sources.'

$packageReferences = @($coreProject.Project.ItemGroup.PackageReference)
$publicApiAnalyzer = $packageReferences | Where-Object { $_.Include -eq 'Microsoft.CodeAnalysis.PublicApiAnalyzers' } | Select-Object -First 1
Assert-Contract ($null -ne $publicApiAnalyzer) 'AbilityKit.Core.csproj must keep the Public API analyzer enabled.'
Assert-Contract (Test-Path -LiteralPath $shippedApiPath -PathType Leaf) 'Core shipped Public API baseline is missing.'
Assert-Contract ((Get-Item -LiteralPath $shippedApiPath).Length -gt 100) 'Core shipped Public API baseline is unexpectedly empty.'
Assert-Contract (Test-Path -LiteralPath $unshippedApiPath -PathType Leaf) 'Core unshipped Public API baseline is missing.'

$legacyNamespaceFileCount = ($legacyNamespaceCounts.Values | Measure-Object -Sum).Sum
Write-Output ("Core boundary audit passed: {0} foundation source files checked; {1} managed production source files checked; {2} legacy namespace files capped; Continuous, Diagnostics, Marker, and application infrastructure extraction enforced." -f @($foundationSources).Count, @($managedProductionSources).Count, $legacyNamespaceFileCount)

[CmdletBinding()]
param(
    [string]$ManifestPath = 'tools\moba-hero-acceptance-coverage.json',
    [string]$GateConfigPath = 'tools\test-gates.json',
    [string]$FixtureRootPath = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes',
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$allowedCoverageStates = @('covered', 'not-covered')

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Read-JsonFile {
    param([string]$Path, [string]$Label)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label was not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "$Label is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Get-TestMethodNames {
    param([string]$Source)

    # This is a source-reference check, not C# compilation or test discovery.
    # Keep string literals intact while removing comments so commented tests cannot count.
    $sourceWithoutComments = [regex]::Replace($Source, '(?s)@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*''|/\*.*?\*/|//[^\r\n]*', {
        param($Match)
        if ($Match.Value.StartsWith('/*') -or $Match.Value.StartsWith('//')) { return ' ' }
        return $Match.Value
    })
    $methodPattern = '(?m)(?<attributes>(?:\s*\[[^\]]+\])+\s*)public\s+(?:async\s+)?(?:void|IEnumerator|Task)\s+(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\('
    foreach ($methodMatch in [regex]::Matches($sourceWithoutComments, $methodPattern)) {
        if ($methodMatch.Groups['attributes'].Value -match '\[(?:NUnit\.Framework\.|UnityEngine\.TestTools\.)?(?:Test|TestCase|TestCaseSource|UnityTest)(?:Attribute)?\b') {
            $methodMatch.Groups['method'].Value
        }
    }
}

$fixtureRoot = Resolve-RepoPath $FixtureRootPath
$resolvedManifestPath = Resolve-RepoPath $ManifestPath
$resolvedGateConfigPath = Resolve-RepoPath $GateConfigPath
$manifest = Read-JsonFile -Path $resolvedManifestPath -Label 'Hero acceptance coverage manifest'
$gateConfig = Read-JsonFile -Path $resolvedGateConfigPath -Label 'Test gate config'

if ($manifest.schemaVersion -ne 1) {
    throw "Hero acceptance coverage manifest must use schemaVersion 1. Actual: $($manifest.schemaVersion)"
}

if ($manifest.manifest -ne 'moba-hero-acceptance-coverage') {
    throw "Hero acceptance coverage manifest has an unexpected manifest identifier: $($manifest.manifest)"
}

$heroes = @($manifest.heroes)
if ($heroes.Count -eq 0) {
    throw 'Hero acceptance coverage manifest must contain at least one hero.'
}

if (-not (Test-Path -LiteralPath $fixtureRoot -PathType Container)) {
    throw "Hero acceptance fixture root was not found: $fixtureRoot"
}

$fixtureFilesByClass = @{}
foreach ($fixtureFile in Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File -Filter '*SkillAcceptanceTests.cs') {
    $fixtureClass = [System.IO.Path]::GetFileNameWithoutExtension($fixtureFile.Name)
    if ($fixtureFilesByClass.ContainsKey($fixtureClass)) {
        throw "Multiple acceptance fixture files use the expected class name '$fixtureClass'."
    }
    $fixtureFilesByClass[$fixtureClass] = $fixtureFile
}

$gatesByName = @{}
foreach ($gate in @($gateConfig.gates)) {
    $gateName = [string]$gate.name
    if ([string]::IsNullOrWhiteSpace($gateName)) {
        throw 'Test gate config contains a gate without a name.'
    }
    if ($gatesByName.ContainsKey($gateName)) {
        throw "Test gate config defines gate '$gateName' more than once."
    }
    $gatesByName[$gateName] = $gate
}

$seenHeroIds = @{}
$seenHeroNames = @{}
$seenFixtures = @{}
$heroReports = @()
$coveredCount = 0
$linkedCount = 0
foreach ($hero in $heroes) {
    $heroId = 0
    if (-not [int]::TryParse([string]$hero.heroId, [ref]$heroId) -or $heroId -le 0) {
        throw "Hero acceptance coverage heroId must be a positive integer. Actual: '$($hero.heroId)'."
    }
    if ($seenHeroIds.ContainsKey($heroId)) {
        throw "Hero acceptance coverage manifest defines heroId $heroId more than once."
    }
    $seenHeroIds[$heroId] = $true

    $heroName = [string]$hero.heroName
    if ([string]::IsNullOrWhiteSpace($heroName)) {
        throw "heroId $heroId must declare heroName."
    }
    if ($seenHeroNames.ContainsKey($heroName)) {
        throw "Hero acceptance coverage manifest defines heroName '$heroName' more than once."
    }
    $seenHeroNames[$heroName] = $true

    $fixture = [string]$hero.fixture
    if ([string]::IsNullOrWhiteSpace($fixture) -or $fixture -notmatch '^AbilityKit\.Game\.Test\.UnitTest\.[A-Za-z0-9_]+$') {
        throw "heroId $heroId must declare a fully qualified Unity fixture name. Actual: '$fixture'."
    }
    if ($seenFixtures.ContainsKey($fixture)) {
        throw "Hero acceptance coverage manifest references fixture '$fixture' more than once."
    }
    $seenFixtures[$fixture] = $true

    $fixtureClass = $fixture.Split('.')[-1]
    if (-not $fixtureFilesByClass.ContainsKey($fixtureClass)) {
        throw "Fixture source file for heroId $heroId was not found under $fixtureRoot (expected class '$fixtureClass')."
    }
    $fixtureFile = $fixtureFilesByClass[$fixtureClass]
    $fixtureSource = Get-Content -LiteralPath $fixtureFile.FullName -Raw -Encoding UTF8
    if ($fixtureSource -notmatch 'namespace\s+AbilityKit\.Game\.Test\.UnitTest' -or $fixtureSource -notmatch ('class\s+' + [regex]::Escape($fixtureClass) + '\b')) {
        throw "Fixture '$fixture' does not match source file '$($fixtureFile.FullName)'."
    }

    if ([string]::IsNullOrWhiteSpace([string]$hero.sceneContract)) {
        throw "heroId $heroId must declare a sceneContract."
    }

    if ($null -eq $hero.coverage) {
        throw "heroId $heroId must declare coverage."
    }
    $coverageEntries = @($hero.coverage.PSObject.Properties)
    if ($coverageEntries.Count -eq 0) {
        throw "heroId $heroId must declare at least one coverage capability."
    }
    foreach ($coverageEntry in $coverageEntries) {
        $coverageKey = [string]$coverageEntry.Name
        if ($coverageKey -notmatch '^[a-z][A-Za-z0-9]*$') {
            throw "heroId $heroId coverage key '$coverageKey' must use lower camel case."
        }
        $coverageState = [string]$coverageEntry.Value
        if ($allowedCoverageStates -notcontains $coverageState) {
            throw "heroId $heroId coverage '$coverageKey' must be one of: $($allowedCoverageStates -join ', '). Actual: '$coverageState'."
        }
    }

    $gateName = [string]$hero.gate
    if (-not $gatesByName.ContainsKey($gateName)) {
        throw "heroId $heroId references undefined gate '$gateName'."
    }
    $gate = $gatesByName[$gateName]
    if ([string]::IsNullOrWhiteSpace([string]$gate.level) -or $null -eq $gate.ciPolicy) {
        throw "Gate '$gateName' must declare a level and ciPolicy."
    }
    $matchingFixtureStep = @($gate.steps | Where-Object {
        $_.kind -eq 'unity-editmode-test' -and
        (([string]$_.testFilter -eq $fixture) -or ([string]$_.testFilter).StartsWith($fixture + '.', [System.StringComparison]::Ordinal))
    })
    if ($matchingFixtureStep.Count -eq 0) {
        throw "Gate '$gateName' must contain a Unity EditMode step filtered by fixture '$fixture' or one of its test methods."
    }

    $testMethods = @(Get-TestMethodNames -Source $fixtureSource)
    $evidenceByCapability = @{}
    if ($null -ne $hero.coverageEvidence) {
        if ($hero.coverageEvidence -isnot [pscustomobject]) {
            throw "heroId $heroId coverageEvidence must be an object keyed by capability."
        }
        foreach ($evidence in $hero.coverageEvidence.PSObject.Properties) {
            $capability = [string]$evidence.Name
            if ($null -eq $hero.coverage.PSObject.Properties[$capability] -or $hero.coverage.$capability -cne 'covered') {
                throw "heroId $heroId evidence '$capability' must reference a covered capability."
            }
            if ($evidence.Value -isnot [array] -or $evidence.Value.Count -eq 0) {
                throw "heroId $heroId evidence '$capability' must be a non-empty array of test method names."
            }
            $seenMethods = @{}
            foreach ($method in $evidence.Value) {
                if ($method -isnot [string] -or $testMethods -cnotcontains $method) {
                    throw "heroId $heroId evidence '$capability' references missing NUnit test method '$method' in '$fixture'."
                }
                if ($seenMethods.ContainsKey($method)) {
                    throw "heroId $heroId evidence '$capability' repeats test method '$method'."
                }
                $seenMethods[$method] = $true
                $testName = "$fixture.$method"
                $executingSteps = @($matchingFixtureStep | Where-Object {
                    $_.testPlatform -ceq 'EditMode' -and
                    (([string]$_.testFilter -ceq $fixture) -or ([string]$_.testFilter -ceq $testName))
                })
                if ($executingSteps.Count -eq 0) {
                    throw "Gate '$gateName' does not execute evidence test '$testName' in EditMode."
                }
            }
            $evidenceByCapability[$capability] = @($evidence.Value)
        }
    }

    $capabilityReports = @()
    foreach ($coverageEntry in $coverageEntries) {
        $capability = [string]$coverageEntry.Name
        $state = [string]$coverageEntry.Value
        $methods = @()
        $evidenceStatus = 'not-covered'
        if ($state -eq 'covered') {
            $coveredCount++
            $evidenceStatus = 'missing-reference'
            if ($evidenceByCapability.ContainsKey($capability)) {
                $methods = @($evidenceByCapability[$capability])
                $evidenceStatus = 'source-linked'
                $linkedCount++
            }
        }
        $capabilityReports += [ordered]@{
            capability = $capability
            state = $state
            evidenceStatus = $evidenceStatus
            tests = @($methods | ForEach-Object { "$fixture.$_" })
        }
    }
    $heroReports += [ordered]@{
        heroId = $heroId
        heroName = $heroName
        fixture = $fixture
        gate = $gateName
        coverage = $capabilityReports
    }
}

foreach ($fixtureClass in $fixtureFilesByClass.Keys) {
    $fixtureName = "AbilityKit.Game.Test.UnitTest.$fixtureClass"
    if (-not $seenFixtures.ContainsKey($fixtureName)) {
        throw "Acceptance fixture '$fixtureName' is not registered in the hero coverage manifest."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $resolvedReportPath = Resolve-RepoPath $ReportPath
    foreach ($inputPath in @($resolvedManifestPath, $resolvedGateConfigPath) + @($fixtureFilesByClass.Values | ForEach-Object { $_.FullName })) {
        if ([System.IO.Path]::GetFullPath($resolvedReportPath) -eq [System.IO.Path]::GetFullPath($inputPath)) {
            throw 'ReportPath must not overwrite a validation input.'
        }
    }
    $report = [ordered]@{
        schemaVersion = 1
        report = 'moba-hero-acceptance-evidence'
        summary = [ordered]@{
            heroes = $heroes.Count
            coveredCapabilities = $coveredCount
            sourceLinkedCapabilities = $linkedCount
            missingReferences = $coveredCount - $linkedCount
        }
        heroes = $heroReports
    }
    $reportDirectory = Split-Path -Parent $resolvedReportPath
    [void][System.IO.Directory]::CreateDirectory($reportDirectory)
    [System.IO.File]::WriteAllText($resolvedReportPath, ($report | ConvertTo-Json -Depth 12), (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host "MOBA hero acceptance coverage manifest passed for $($heroes.Count) heroes." -ForegroundColor Green
Write-Host "Coverage evidence: $linkedCount/$coveredCount covered capabilities have source-linked tests; $($coveredCount - $linkedCount) need references. This does not certify test execution."

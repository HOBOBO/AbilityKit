[CmdletBinding()]
param(
    [string]$ManifestPath = 'tools\moba-hero-acceptance-coverage.json',
    [string]$GateConfigPath = 'tools\test-gates.json'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$requiredCoverageKeys = @('repeatedRelease', 'buff', 'projectile', 'area', 'presentation')
$allowedCoverageStates = @('covered', 'not-covered')
$expectedHeroes = @(
    @{ heroId = 1; heroName = 'LianPo'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\LianPo\LianPoSkillAcceptanceTests.cs' },
    @{ heroId = 1002; heroName = 'XiaoQiao'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\XiaoQiao\XiaoQiaoSkillAcceptanceTests.cs' },
    @{ heroId = 1003; heroName = 'ZhaoYun'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\ZhaoYun\ZhaoYunSkillAcceptanceTests.cs' },
    @{ heroId = 1004; heroName = 'Mozi'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\Mozi\MoziSkillAcceptanceTests.cs' },
    @{ heroId = 1005; heroName = 'Daji'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\Daji\DajiSkillAcceptanceTests.cs' },
    @{ heroId = 1006; heroName = 'YingZheng'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\YingZheng\YingZhengSkillAcceptanceTests.cs' },
    @{ heroId = 1007; heroName = 'SunShangxiang'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\SunShangxiang\SunShangxiangSkillAcceptanceTests.cs' },
    @{ heroId = 1008; heroName = 'Luban'; fixtureFile = 'Unity\Packages\com.abilitykit.demo.moba.view.runtime\Runtime\Game\Test\UnitTest\Acceptance\Heroes\Luban\LubanSkillAcceptanceTests.cs' }
)

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
if ($heroes.Count -ne $expectedHeroes.Count) {
    throw "Hero acceptance coverage manifest must contain exactly $($expectedHeroes.Count) heroes. Actual: $($heroes.Count)"
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
foreach ($expectedHero in $expectedHeroes) {
    $hero = @($heroes | Where-Object { [int]$_.heroId -eq [int]$expectedHero.heroId })
    if ($hero.Count -ne 1) {
        throw "Hero acceptance coverage manifest must contain exactly one hero with heroId $($expectedHero.heroId)."
    }

    $hero = $hero[0]
    $seenHeroIds[[int]$hero.heroId] = $true
    if ([string]$hero.heroName -ne [string]$expectedHero.heroName) {
        throw "heroId $($expectedHero.heroId) must use heroName '$($expectedHero.heroName)'. Actual: '$($hero.heroName)'."
    }

    $fixture = [string]$hero.fixture
    if ([string]::IsNullOrWhiteSpace($fixture) -or $fixture -notmatch '^AbilityKit\.Game\.Test\.UnitTest\.[A-Za-z0-9_]+$') {
        throw "heroId $($hero.heroId) must declare a fully qualified Unity fixture name. Actual: '$fixture'."
    }

    $fixturePath = Resolve-RepoPath $expectedHero.fixtureFile
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
        throw "Fixture source file for heroId $($hero.heroId) was not found: $fixturePath"
    }
    $fixtureSource = Get-Content -LiteralPath $fixturePath -Raw -Encoding UTF8
    $fixtureClass = $fixture.Split('.')[-1]
    if ($fixtureSource -notmatch 'namespace\s+AbilityKit\.Game\.Test\.UnitTest' -or $fixtureSource -notmatch ('class\s+' + [regex]::Escape($fixtureClass) + '\b')) {
        throw "Fixture '$fixture' does not match source file '$fixturePath'."
    }

    if ([string]::IsNullOrWhiteSpace([string]$hero.sceneContract)) {
        throw "heroId $($hero.heroId) must declare a sceneContract."
    }

    if ($null -eq $hero.coverage) {
        throw "heroId $($hero.heroId) must declare coverage."
    }
    foreach ($coverageKey in $requiredCoverageKeys) {
        if ($null -eq $hero.coverage.PSObject.Properties[$coverageKey]) {
            throw "heroId $($hero.heroId) coverage is missing '$coverageKey'."
        }
        $coverageState = [string]$hero.coverage.$coverageKey
        if ($allowedCoverageStates -notcontains $coverageState) {
            throw "heroId $($hero.heroId) coverage '$coverageKey' must be one of: $($allowedCoverageStates -join ', '). Actual: '$coverageState'."
        }
    }

    $gateName = [string]$hero.gate
    if (-not $gatesByName.ContainsKey($gateName)) {
        throw "heroId $($hero.heroId) references undefined gate '$gateName'."
    }
    $gate = $gatesByName[$gateName]
    $matchingFixtureStep = @($gate.steps | Where-Object {
        $_.kind -eq 'unity-editmode-test' -and
        (([string]$_.testFilter -eq $fixture) -or ([string]$_.testFilter).StartsWith($fixture + '.', [System.StringComparison]::Ordinal))
    })
    if ($matchingFixtureStep.Count -eq 0) {
        throw "Gate '$gateName' must contain a Unity EditMode step filtered by fixture '$fixture' or one of its test methods."
    }

    if ($hero.heroName -in @('ZhaoYun', 'Mozi', 'Daji', 'YingZheng', 'Luban')) {
        if ([string]$gate.level -ne 'P2') {
            throw "Gate '$gateName' for $($hero.heroName) must be P2. Actual: '$($gate.level)'."
        }
        if ($null -eq $gate.ciPolicy -or -not [bool]$gate.ciPolicy.runOnSchedule -or [bool]$gate.ciPolicy.runOnPullRequest -or [bool]$gate.ciPolicy.runOnPush) {
            throw "Gate '$gateName' for $($hero.heroName) must use the P2 scheduled-only CI policy."
        }
    }
}

foreach ($hero in $heroes) {
    if (-not $seenHeroIds.ContainsKey([int]$hero.heroId)) {
        throw "Hero acceptance coverage manifest contains unexpected heroId $($hero.heroId)."
    }
}

Write-Host "MOBA hero acceptance coverage manifest passed for $($heroes.Count) heroes." -ForegroundColor Green

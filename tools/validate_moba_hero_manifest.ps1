[CmdletBinding()]
param(
    [string]$ManifestPath = 'tools\moba-hero-manifest.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
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

function Require-PositiveInt {
    param([object]$Value, [string]$Label)

    $parsed = 0
    if (-not [int]::TryParse([string]$Value, [ref]$parsed) -or $parsed -le 0) {
        throw "$Label must be a positive integer. Actual: '$Value'."
    }

    return $parsed
}

function Require-ExistingFile {
    param([string]$Path, [string]$Label)

    $resolved = Resolve-RepoPath $Path
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Label was not found: $Path"
    }

    return $resolved
}

$resolvedManifestPath = Resolve-RepoPath $ManifestPath
$manifest = Read-JsonFile -Path $resolvedManifestPath -Label 'MOBA hero production manifest'

if ([int]$manifest.schemaVersion -ne 1) {
    throw "MOBA hero production manifest must use schemaVersion 1. Actual: $($manifest.schemaVersion)"
}
if ([string]$manifest.manifest -ne 'moba-hero-production') {
    throw "MOBA hero production manifest has an unexpected manifest identifier: '$($manifest.manifest)'."
}

$charactersPath = Require-ExistingFile -Path ([string]$manifest.charactersPath) -Label 'Characters config'
$characters = Read-JsonFile -Path $charactersPath -Label 'Characters config'
$charactersById = @{}
foreach ($character in $characters) {
    $characterIdValue = [string]$character.Id
    $characterId = Require-PositiveInt -Value $characterIdValue -Label 'characters.json Id'
    if ($charactersById.ContainsKey($characterId)) {
        throw "Characters config defines characterId $characterId more than once."
    }
    $charactersById[$characterId] = $character
}

foreach ($path in @($manifest.requiredConfigPaths)) {
    [void](Require-ExistingFile -Path ([string]$path) -Label 'Required hero config')
}
foreach ($path in @($manifest.requiredPresentationPaths)) {
    [void](Require-ExistingFile -Path ([string]$path) -Label 'Required hero presentation resource')
}

$coveragePath = Require-ExistingFile -Path ([string]$manifest.acceptanceCoveragePath) -Label 'Hero acceptance coverage manifest'
$coverageManifest = Read-JsonFile -Path $coveragePath -Label 'Hero acceptance coverage manifest'
if ([int]$coverageManifest.schemaVersion -ne 1 -or [string]$coverageManifest.manifest -ne 'moba-hero-acceptance-coverage') {
    throw "Hero acceptance coverage manifest is incompatible: $($manifest.acceptanceCoveragePath)"
}

$coverageByHeroId = @{}
foreach ($coverage in $coverageManifest.heroes) {
    $coverageIdValue = [string]$coverage.heroId
    $coverageId = Require-PositiveInt -Value $coverageIdValue -Label 'Acceptance coverage heroId'
    if ($coverageByHeroId.ContainsKey($coverageId)) {
        throw "Hero acceptance coverage manifest defines heroId $coverageId more than once."
    }
    $coverageByHeroId[$coverageId] = $coverage
}

$heroes = $manifest.heroes
if ($heroes.Count -eq 0) {
    throw 'MOBA hero production manifest must contain at least one hero.'
}

$characterIds = @{}
$acceptanceHeroIds = @{}
foreach ($hero in $heroes) {
    $characterIdValue = [string]$hero.characterId
    $acceptanceHeroIdValue = [string]$hero.acceptanceHeroId
    $characterId = Require-PositiveInt -Value $characterIdValue -Label 'Hero characterId'
    $acceptanceHeroId = Require-PositiveInt -Value $acceptanceHeroIdValue -Label 'Hero acceptanceHeroId'
    $heroName = [string]$hero.heroName
    $fixture = [string]$hero.fixture
    $fixturePath = [string]$hero.fixturePath

    if ([string]::IsNullOrWhiteSpace($heroName)) { throw "Hero characterId $characterId must declare heroName." }
    if ($characterIds.ContainsKey($characterId)) { throw "MOBA hero production manifest defines characterId $characterId more than once." }
    if ($acceptanceHeroIds.ContainsKey($acceptanceHeroId)) { throw "MOBA hero production manifest defines acceptanceHeroId $acceptanceHeroId more than once." }
    if (-not $charactersById.ContainsKey($characterId)) { throw "Hero '$heroName' references missing characterId $characterId in $($manifest.charactersPath)." }
    if (-not $coverageByHeroId.ContainsKey($acceptanceHeroId)) { throw "Hero '$heroName' references missing acceptanceHeroId $acceptanceHeroId in $($manifest.acceptanceCoveragePath)." }
    if ([string]$coverageByHeroId[$acceptanceHeroId].heroName -ne $heroName) { throw "Hero characterId $characterId has heroName '$heroName' but acceptance coverage declares '$($coverageByHeroId[$acceptanceHeroId].heroName)'." }
    if ([string]$coverageByHeroId[$acceptanceHeroId].fixture -ne $fixture) { throw "Hero '$heroName' fixture does not match acceptance coverage." }
    if ([string]::IsNullOrWhiteSpace($fixture) -or $fixture -notmatch '^AbilityKit\.Game\.Test\.UnitTest\.[A-Za-z0-9_]+$') { throw "Hero '$heroName' must declare a fully qualified Unity fixture name." }

    $resolvedFixturePath = Require-ExistingFile -Path $fixturePath -Label "Fixture source for hero '$heroName'"
    $fixtureSource = Get-Content -LiteralPath $resolvedFixturePath -Raw -Encoding UTF8
    $fixtureClass = $fixture.Split('.')[-1]
    if ($fixtureSource -notmatch 'namespace\s+AbilityKit\.Game\.Test\.UnitTest' -or $fixtureSource -notmatch ('class\s+' + [regex]::Escape($fixtureClass) + '\b')) {
        throw "Fixture '$fixture' does not match source file '$fixturePath'."
    }

    $characterIds[$characterId] = $true
    $acceptanceHeroIds[$acceptanceHeroId] = $true
}

Write-Host "MOBA hero production manifest passed for $($heroes.Count) heroes." -ForegroundColor Green

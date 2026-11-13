[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$toolDirectory = Join-Path $PSScriptRoot 'battlescenario-artifact-schema'
$nodeModules = Join-Path $toolDirectory 'node_modules'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is required. Install the repository-supported Node.js version and rerun this validator.'
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'npm is required to install the pinned schema validator dependencies.'
}
if (-not (Test-Path -LiteralPath (Join-Path $toolDirectory 'package-lock.json') -PathType Leaf)) {
    throw 'BattleScenario artifact schema lockfile is missing.'
}
if (-not (Test-Path -LiteralPath $nodeModules -PathType Container)) {
    throw "Pinned validator dependencies are not installed. Run: npm ci --prefix tools/battlescenario-artifact-schema"
}

Push-Location $repoRoot
try {
    & npm test --prefix tools/battlescenario-artifact-schema
    if ($LASTEXITCODE -ne 0) {
        throw "BattleScenario artifact schema tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

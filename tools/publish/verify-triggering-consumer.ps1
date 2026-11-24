param(
    [string]$EvidenceDirectory,
    [string]$UnityPath,
    [ValidateSet('Mono', 'IL2CPP')]
    [string]$PlayerBackend,
    [switch]$PrepareCandidate
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $EvidenceDirectory) {
    $EvidenceDirectory = Join-Path $repoRoot ('local/Logs/triggering-consumer/{0}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$arguments = @('-I', (Join-Path $PSScriptRoot 'verify_triggering_consumer.py'), '--output', $EvidenceDirectory)
if ($PrepareCandidate) {
    if ($UnityPath -or $PlayerBackend) {
        throw 'Use -PrepareCandidate without Unity or Player options.'
    }
    $arguments += '--prepare-candidate'
} else {
    if (-not $UnityPath) {
        $version = Select-String -LiteralPath (Join-Path $repoRoot 'Unity/ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (\S+)$'
        $UnityPath = Join-Path $env:ProgramFiles ('Unity/Hub/Editor/{0}/Editor/Unity.exe' -f $version.Matches[0].Groups[1].Value)
    }
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "Unity Editor not found: $UnityPath. Specify -UnityPath."
    }
    $arguments += @('--unity', $UnityPath)
    if ($PlayerBackend) { $arguments += @('--player-backend', $PlayerBackend) }
}
if (Get-Command py -ErrorAction SilentlyContinue) {
    & py -3 @arguments
} else {
    & python @arguments
}
exit $LASTEXITCODE

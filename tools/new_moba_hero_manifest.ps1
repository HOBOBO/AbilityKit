[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 2147483647)]
    [int]$CharacterId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string]$HeroName,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 2147483647)]
    [int]$AcceptanceHeroId,

    [string]$OutputDirectory = 'tools\moba-hero-manifests'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputDirectoryPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
$outputPath = Join-Path $outputDirectoryPath ("{0}-{1}.json" -f $CharacterId, $HeroName)

if (Test-Path -LiteralPath $outputPath) {
    throw "Hero manifest already exists: $outputPath"
}

$fixture = "AbilityKit.Game.Test.UnitTest.$($HeroName)SkillAcceptanceTests"
$fixturePath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/$HeroName/$($HeroName)SkillAcceptanceTests.cs"
$manifest = [ordered]@{
    schemaVersion = 1
    manifest = 'moba-hero-production-entry'
    characterId = $CharacterId
    acceptanceHeroId = $AcceptanceHeroId
    heroName = $HeroName
    fixture = $fixture
    fixturePath = $fixturePath
    requiredConfigPaths = @(
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/characters.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skills.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_flows.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_button_templates.json'
    )
    requiredPresentationPaths = @(
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/models.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/aoes.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/projectiles.json',
        'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/presentation_templates.json'
    )
}
$content = $manifest | ConvertTo-Json -Depth 4

if ($PSCmdlet.ShouldProcess($outputPath, 'Create MOBA hero manifest scaffold')) {
    New-Item -ItemType Directory -Path $outputDirectoryPath -Force | Out-Null
    [System.IO.File]::WriteAllText($outputPath, $content + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Created MOBA hero manifest scaffold: $outputPath" -ForegroundColor Green
}

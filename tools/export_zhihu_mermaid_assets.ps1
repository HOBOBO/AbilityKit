param(
    [string]$InputMarkdown = "",
    [string]$OutputDir = "Docs/zhihu-assets/abilitykit-intro",
    [int]$Scale = 2,
    [string[]]$DiagramNames = @(),
    [string]$ManifestTitle = "AbilityKit Zhihu Intro Mermaid Assets",
    [switch]$SkipRender
)

$ErrorActionPreference = "Stop"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if ([string]::IsNullOrWhiteSpace($InputMarkdown)) {
    $zhihu = [string]([char]0x77E5) + [string]([char]0x4E4E)
    $draft = Get-ChildItem -LiteralPath "Docs" -File -Filter "AbilityKit*.md" |
        Where-Object { $_.Name.Contains($zhihu) } |
        Select-Object -First 1

    if ($null -eq $draft) {
        throw "Input markdown not found. Pass -InputMarkdown explicitly."
    }

    $InputMarkdown = $draft.FullName
}

if (-not (Test-Path -LiteralPath $InputMarkdown)) {
    throw "Input markdown not found: $InputMarkdown"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$content = Get-Content -LiteralPath $InputMarkdown -Raw -Encoding UTF8
$pattern = '(?ms)^```mermaid\s*\r?\n(.*?)\r?\n```'
$matches = [regex]::Matches($content, $pattern)

$defaultNames = @(
    "01-positioning-scope",
    "02-repository-source-map",
    "03-capability-layers",
    "04-ecs-backends",
    "05-combat-action-flow",
    "06-battle-tick-loop",
    "07-actor-logic-view-boundary",
    "08-presentation-inputs",
    "09-skill-timeline-flow",
    "10-complex-skill-decomposition",
    "11-skill-composition-patterns",
    "12-trigger-runner-sequence",
    "13-effect-lifecycle",
    "14-continuous-behavior-composition",
    "15-effect-semantic-compositions",
    "16-damage-pipeline",
    "17-projectile-area-motion-boundary",
    "18-projectile-lifecycle",
    "19-area-capture-events",
    "20-natural-language-to-building-blocks",
    "21-trace-record-replay",
    "22-replay-contract",
    "23-sync-runtime-flow",
    "24-server-orleans-flow",
    "25-host-adapter"
)
$names = if ($DiagramNames.Count -gt 0) { $DiagramNames } else { $defaultNames }

if ($DiagramNames.Count -gt 0 -and $DiagramNames.Count -ne $matches.Count) {
    throw "DiagramNames count ($($DiagramNames.Count)) does not match Mermaid diagram count ($($matches.Count))."
}

$configPath = Join-Path $OutputDir "mermaid-config.json"
$configJson = @'
{
  "theme": "neutral",
  "themeVariables": {
    "fontFamily": "Microsoft YaHei, Segoe UI, Arial, sans-serif",
    "background": "#ffffff",
    "primaryColor": "#f8fafc",
    "primaryTextColor": "#111827",
    "primaryBorderColor": "#64748b",
    "lineColor": "#475569",
    "secondaryColor": "#eef2ff",
    "tertiaryColor": "#f1f5f9"
  },
  "flowchart": {
    "htmlLabels": true,
    "curve": "basis"
  },
  "sequence": {
    "showSequenceNumbers": false,
    "mirrorActors": false
  }
}
'@
[System.IO.File]::WriteAllText($configPath, $configJson, $Utf8NoBom)

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add("# $ManifestTitle")
$manifest.Add("")
$manifest.Add("Source markdown: ``$InputMarkdown``")
$manifest.Add("Generated at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$manifest.Add("")
$manifest.Add("| # | Mermaid | PNG |")
$manifest.Add("|---|---|---|")

for ($i = 0; $i -lt $matches.Count; $i++) {
    $baseName = if ($i -lt $names.Count) { $names[$i] } else { "{0:D2}-diagram" -f ($i + 1) }
    $mmdPath = Join-Path $OutputDir ($baseName + ".mmd")
    $pngPath = Join-Path $OutputDir ($baseName + ".png")
    $diagram = $matches[$i].Groups[1].Value.Trim()

    [System.IO.File]::WriteAllText($mmdPath, $diagram, $Utf8NoBom)

    if (-not $SkipRender) {
        $cmdArgs = @(
            "/c",
            "npx.cmd",
            "-y",
            "-p", "@mermaid-js/mermaid-cli",
            "mmdc",
            "-i", $mmdPath,
            "-o", $pngPath,
            "-b", "white",
            "-s", $Scale,
            "-c", $configPath
        )

        Write-Host "Rendering $pngPath"
        & cmd.exe @cmdArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Mermaid CLI failed for $mmdPath"
        }
        if (-not (Test-Path -LiteralPath $pngPath)) {
            throw "Mermaid CLI finished but PNG was not created: $pngPath"
        }
    }

    $pngLink = if (Test-Path -LiteralPath $pngPath) { "[$baseName.png]($baseName.png)" } else { "not generated" }
    $manifest.Add("| $($i + 1) | [$baseName.mmd]($baseName.mmd) | $pngLink |")
}

$manifestPath = Join-Path $OutputDir "README.md"
[System.IO.File]::WriteAllText($manifestPath, ($manifest -join [Environment]::NewLine), $Utf8NoBom)

Write-Host "Extracted $($matches.Count) Mermaid diagrams to $OutputDir"
if ($SkipRender) {
    Write-Host "PNG rendering skipped. Run without -SkipRender to generate PNG files."
}

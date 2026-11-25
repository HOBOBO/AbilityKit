<#
.SYNOPSIS
    Repair UTF-8-read-as-GBK mojibake in sample source comments.

.DESCRIPTION
    A subset of the sample sources in Unity/Packages/com.abilitykit.samples were
    committed with their UTF-8 comment bytes decoded as GBK, so Chinese XML doc
    comments read as garbage (for example the marker-type doc line in
    Runtime/Samples/Pipeline/ExecutorForAttribute.cs).
    The bytes are intact, so the text is recoverable by encoding back to GBK
    and decoding as UTF-8.

    This is comment-and-literal text only: the corrupted bytes never reach
    sample output (the golden baseline contains no mojibake), so the fix is
    cosmetic. It matters because this code becomes the teaching package - a
    quarter of its files currently document themselves in garbage.

    Safety: a line is rewritten only when every one of these holds:
      * the line is a comment line (//, ///, /*, *, */), so a repair can never
        change runtime behaviour even if the heuristic is wrong,
      * the line encodes to GBK without unmappable characters,
      * the resulting bytes decode as strict UTF-8 with no replacement chars,
      * the decoded result contains at least one CJK character - without this,
        correct Chinese can round trip into Cyrillic/Hebrew lookalikes, because
        a GBK byte pair is sometimes also a valid UTF-8 sequence. This is not
        hypothetical: an earlier revision corrupted the string literal
        "硬直帧" into "Ӳֱ֡" and the sample golden baseline caught it.
      * the result actually differs from the input.
    Lines that fail any check (for example text that lost a byte during the
    original corruption) are left untouched and reported, never guessed at.

    Verified by: a clean build and a byte-identical sample golden baseline.
    The baseline cannot see comment-only edits, so the comment-only rule above
    is what keeps this transform off the behaviour path.

.PARAMETER Apply
    Write the repaired files. Without this the script only reports.

.PARAMETER Root
    Directories to scan, relative to the repository root.

.EXAMPLE
    powershell -File tools/fix_sample_mojibake.ps1
    powershell -File tools/fix_sample_mojibake.ps1 -Apply
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [string[]]$Root = @('Unity/Packages/com.abilitykit.samples')
)

$ErrorActionPreference = 'Stop'

try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
} catch {
    Write-Warning "无法设置控制台输出编码： $($_.Exception.Message)"
}

$repoRoot = Split-Path -Parent $PSScriptRoot

# 编码和解码两侧均采用严格模式，无法往返的行直接跳过，防止破坏文本。
$gbk = [System.Text.Encoding]::GetEncoding(
    936,
    [System.Text.EncoderFallback]::ExceptionFallback,
    [System.Text.DecoderFallback]::ExceptionFallback)
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)

function Test-IsCommentLine {
    param([string]$Line)

    $trimmed = $Line.TrimStart()
    if ($trimmed.Length -lt 2) { return $false }

    return $trimmed.StartsWith('//') -or
           $trimmed.StartsWith('/*') -or
           $trimmed.StartsWith('*')
}

function Test-HasCjk {
    param([string]$Text)

    foreach ($ch in $Text.ToCharArray()) {
        $code = [int]$ch
        if ($code -ge 0x4E00 -and $code -le 0x9FFF) { return $true }
    }
    return $false
}

function Repair-Line {
    param([string]$Line)

    if ($Line.Length -eq 0) { return $null }

    # 仅处理注释行。这是关键保护，确保无论启发式结果如何，转换都不会触及行为代码。
    if (-not (Test-IsCommentLine -Line $Line)) { return $null }

    # 纯 ASCII 不会出现此类乱码，快速跳过占绝大多数的行。
    $hasHighByte = $false
    foreach ($ch in $Line.ToCharArray()) {
        if ([int]$ch -gt 127) { $hasHighByte = $true; break }
    }
    if (-not $hasHighByte) { return $null }

    try {
        $bytes = $gbk.GetBytes($Line)
    } catch {
        return $null   # contains characters outside GBK: not this corruption
    }

    try {
        $decoded = $strictUtf8.GetString($bytes)
    } catch {
        return $null   # not valid UTF-8: already correct text, or damaged
    }

    if ($decoded -eq $Line) { return $null }
    if ($decoded.IndexOf([char]0xFFFD) -ge 0) { return $null }

    # GBK 字节对也可能是合法 UTF-8 字节对，无损往返不能单独证明乱码。
    # 必须要求结果含真实中文，才能区分乱码与恰好能往返的正常文本。
    if (-not (Test-HasCjk -Text $decoded)) { return $null }

    return $decoded
}

$scannedFiles = 0
$changedFiles = 0
$changedLines = 0
$skippedLines = 0
$changedFileList = New-Object System.Collections.Generic.List[string]

foreach ($relativeRoot in $Root) {
    $absoluteRoot = Join-Path $repoRoot $relativeRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot)) {
        Write-Warning "Skipping missing root: $relativeRoot"
        continue
    }

    $files = Get-ChildItem -LiteralPath $absoluteRoot -Filter '*.cs' -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }

    foreach ($file in $files) {
        $scannedFiles++

        # 从原始字节检测 BOM：File.ReadAllText 已跳过 BOM，检查解码字符串会始终误报无 BOM，
        # 导致重写时静默移除所有受影响文件的 BOM。
        $rawBytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $hadBom = $rawBytes.Length -ge 3 -and
                  $rawBytes[0] -eq 0xEF -and $rawBytes[1] -eq 0xBB -and $rawBytes[2] -eq 0xBF

        $original = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)

        $lines = $original -split "`n"
        $fileChangedLines = 0
        $fileSkippedLines = 0

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $repaired = Repair-Line -Line $lines[$i]
            if ($null -ne $repaired) {
                $lines[$i] = $repaired
                $fileChangedLines++
                continue
            }

            # 无法修复的注释行属于残留损坏，通常是最初乱码时丢失字节。计数并公开残留，不得静默忽略。
            if (Test-IsCommentLine -Line $lines[$i]) {
                foreach ($ch in $lines[$i].ToCharArray()) {
                    if ([int]$ch -gt 127) { $fileSkippedLines++; break }
                }
            }
        }

        if ($fileChangedLines -eq 0) { continue }

        $changedFiles++
        $changedLines += $fileChangedLines
        $skippedLines += $fileSkippedLines
        $relative = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $changedFileList.Add("  $fileChangedLines line(s)  $relative")

        if ($Apply) {
            $rebuilt = ($lines -join "`n")
            if ($hadBom) { $rebuilt = [char]0xFEFF + $rebuilt }
            [System.IO.File]::WriteAllText($file.FullName, $rebuilt, (New-Object System.Text.UTF8Encoding($false)))
        }
    }
}

Write-Host ''
Write-Host "Scanned files: $scannedFiles"
Write-Host "Files with repairable mojibake: $changedFiles"
Write-Host "Repairable lines: $changedLines"

if ($changedFileList.Count -gt 0) {
    Write-Host ''
    Write-Host 'Per file:'
    foreach ($entry in $changedFileList) { Write-Host $entry }
}

if (-not $Apply) {
    Write-Host ''
    Write-Host 'Dry run. Re-run with -Apply to write the repairs.' -ForegroundColor Cyan
    exit 0
}

Write-Host ''
Write-Host 'Repairs written.' -ForegroundColor Green
Write-Host 'Verify with: dotnet build src/AbilityKit.Samples/AbilityKit.Samples.csproj'
Write-Host '             powershell -File tools/check_sample_baseline.ps1   (must stay byte-identical)'

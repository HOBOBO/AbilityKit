$ErrorActionPreference = 'Stop'

$root = 'd:\Workspace\gitProject\ability-kit\Unity\Assets\Scripts\Ability'
$files = Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs'

# High-confidence mojibake -> Chinese mappings (UI words)
$map = [ordered]@{
  '鏁存暟' = '整数'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '娴偣' = '浮点'
  '甯冨皵' = '布尔'
  '瀵硅薄' = '对象'
  '鍏朵粬' = '其他'
  '鍏ㄥ眬' = '全局'
  '灞€閮?' = '局部'
  '灞€閮' = '局部'
  '鏈€杩?' = '最近'
  '鏈€杩' = '最近'
  '琛屼负' = '行为'
  '璋冭瘯' = '调试'
}

# Ensure output is UTF-8 with BOM (Unity/VS stable)
$utf8bom = New-Object System.Text.UTF8Encoding($true)

$changed = 0
foreach ($f in $files) {
  $text = Get-Content -LiteralPath $f.FullName -Raw
  $orig = $text

  foreach ($k in $map.Keys) {
    $text = $text.Replace($k, $map[$k])
  }

  if ($text -ne $orig) {
    [System.IO.File]::WriteAllText($f.FullName, $text, $utf8bom)
    $changed++
    Write-Host ("Updated: " + $f.FullName)
  }
}

Write-Host ("Done. Updated files: " + $changed)

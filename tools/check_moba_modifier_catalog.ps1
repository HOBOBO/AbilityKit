$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

Push-Location (Join-Path $repoRoot 'LubanConfig/Moba')
try {
    & python -m unittest discover -s tests -p test_modifier_parameters.py
    if ($LASTEXITCODE -ne 0) {
        throw "MOBA modifier parameter tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

& python (Join-Path $repoRoot 'LubanConfig/Moba/export_pipeline.py') --check-published
if ($LASTEXITCODE -ne 0) {
    throw "MOBA Luban published configuration check failed with exit code $LASTEXITCODE."
}

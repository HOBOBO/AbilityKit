param(
    [switch]$Apply,
    [switch]$CheckPublished
)

$ErrorActionPreference = "Stop"
if ($Apply -and $CheckPublished) {
    throw "-Apply and -CheckPublished cannot be combined."
}

$exporter = Join-Path $PSScriptRoot "export_pipeline.py"
$arguments = @($exporter)
if ($Apply) { $arguments += "--apply" }
if ($CheckPublished) { $arguments += "--check-published" }
& python @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

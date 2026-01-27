param(
    [int[]]$Ports = @(4000, 5001)
)

$ErrorActionPreference = 'Stop'

$pids = foreach ($p in $Ports) {
    Get-NetTCPConnection -LocalPort $p -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
}

$pids = $pids | Where-Object { $_ -and $_ -gt 0 } | Select-Object -Unique

if (-not $pids) {
    Write-Host "No process found listening on ports $($Ports -join ',')."
    exit 0
}

Write-Host ("Killing PIDs: " + ($pids -join ','))
foreach ($procId in $pids) {
    Stop-Process -Id $procId -Force
}

Write-Host "Done."

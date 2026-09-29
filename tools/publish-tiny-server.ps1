[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$bundle = [System.IO.Path]::GetFullPath($OutputPath)
$unityRoot = Join-Path $repositoryRoot 'Unity'
if ($bundle.Equals($unityRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $bundle.StartsWith($unityRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be outside the source Unity project.'
}
if (Test-Path -LiteralPath $bundle) {
    if (Get-ChildItem -LiteralPath $bundle -Force | Select-Object -First 1) {
        throw "OutputPath already contains files: $bundle"
    }
}
[System.IO.Directory]::CreateDirectory($bundle) | Out-Null
foreach ($service in @(
    @{ Name = 'host'; Project = 'AbilityKit.Orleans.Host' },
    @{ Name = 'gateway'; Project = 'AbilityKit.Orleans.Gateway' }
)) {
    $project = Join-Path $repositoryRoot (
        'Server/Orleans/src/' + $service.Project + '/' + $service.Project + '.csproj')
    $destination = Join-Path $bundle $service.Name
    & dotnet publish $project --configuration $Configuration --output $destination `
        --self-contained false -p:UseAppHost=false --nologo -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Failed to publish $($service.Project)." }
    foreach ($extension in @('dll', 'deps.json', 'runtimeconfig.json')) {
        $artifact = Join-Path $destination ($service.Project + '.' + $extension)
        if (-not (Test-Path -LiteralPath $artifact)) { throw "Missing published artifact: $artifact" }
    }
}
$hostProtocol = Join-Path $bundle 'host/AbilityKit.Protocol.Room.dll'
$gatewayProtocol = Join-Path $bundle 'gateway/AbilityKit.Protocol.Room.dll'
$hostProtocolHash = (Get-FileHash -LiteralPath $hostProtocol -Algorithm SHA256).Hash
$gatewayProtocolHash = (Get-FileHash -LiteralPath $gatewayProtocol -Algorithm SHA256).Hash
if ($hostProtocolHash -ne $gatewayProtocolHash) {
    throw 'Host and Gateway published different Room protocol assemblies.'
}
$manifest = [ordered]@{
    schemaVersion = 1
    configuration = $Configuration
    assemblies = [ordered]@{
        roomProtocol = $hostProtocolHash
        tinyRules = (Get-FileHash -LiteralPath (Join-Path $bundle 'host/AbilityKit.Demo.Tiny.Core.dll') -Algorithm SHA256).Hash
        turnRules = (Get-FileHash -LiteralPath (Join-Path $bundle 'host/AbilityKit.Demo.Tiny.Turn.Core.dll') -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $bundle 'bundle.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tiny-server-template/README.md') `
    -Destination (Join-Path $bundle 'README.md')
$compositionSource = Join-Path $PSScriptRoot 'tiny-server-template/composition'
$composition = Join-Path $bundle 'composition'
[System.IO.Directory]::CreateDirectory($composition) | Out-Null
foreach ($file in @('TinyCustomHost.csproj', 'Program.cs', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $compositionSource $file) -Destination $composition
}
& dotnet build (Join-Path $composition 'TinyCustomHost.csproj') --nologo -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Published Tiny custom Host composition failed to build.' }
Write-Host "Tiny server bundle: $bundle"

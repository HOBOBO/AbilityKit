[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$compilerProject = Join-Path $repositoryRoot 'tools/AbilityKit.Protocol.CatalogCompiler/AbilityKit.Protocol.CatalogCompiler.csproj'
$catalogInput = Join-Path $repositoryRoot 'Protocols/Catalogs'
$manifestOutput = Join-Path $repositoryRoot 'Protocols/Generated/protocol-manifest.json'
$csharpOutput = Join-Path $repositoryRoot 'Unity/Packages/com.abilitykit.protocol/Runtime/Generated/BuiltInProtocolCatalogs.g.cs'

$compilerArguments = @(
    'run',
    '--project', $compilerProject,
    '--',
    '--input', $catalogInput,
    '--manifest', $manifestOutput,
    '--csharp', $csharpOutput
)

if ($Check) {
    $compilerArguments += '--check'
}

& dotnet @compilerArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

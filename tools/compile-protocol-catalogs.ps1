<#
.SYNOPSIS
    协议 catalog 治理统一入口：编译 Protocols/Catalogs/*.protocol.yaml 生成
    Protocols/Generated/protocol-manifest.json 与 Unity 运行时 BuiltInProtocolCatalogs.g.cs。

.DESCRIPTION
    这是协议 catalog 的唯一生成/校验入口。默认（无参数）重新生成并写回两个生成产物；
    -Check 为只读校验模式：重新编译后与已提交产物做字节级比较，不一致则退出码 3（stale）。

    生成是确定性的：源文件按序递归发现并排序，JSON 与 C# 输出顺序跟随源顺序。
    修改任一 catalog 后必须先运行本脚本（不带 -Check）把生成产物一并提交；
    CI（.github/workflows/abilitykit-test-gates.yml 的 protocol-catalogs job）在 PR/push
    门禁用 -Check 拦截陈旧产物。

.EXAMPLE
    ./tools/compile-protocol-catalogs.ps1            # 重新生成并写回生成产物
    ./tools/compile-protocol-catalogs.ps1 -Check     # 只读校验，stale 时非零退出
#>
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
$metadataOutput = Join-Path $repositoryRoot 'Unity/Packages/com.abilitykit.protocol/Runtime/Generated/BuiltInProtocolMetadata.g.cs'

$compilerArguments = @(
    'run',
    '--project', $compilerProject,
    '--',
    '--input', $catalogInput,
    '--manifest', $manifestOutput,
    '--csharp', $csharpOutput,
    '--metadata', $metadataOutput
)

if ($Check) {
    $compilerArguments += '--check'
}

& dotnet @compilerArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

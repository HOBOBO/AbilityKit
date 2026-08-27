<#
.SYNOPSIS
    协议 Wire 正式导出统一入口：按项目把 Protocols/WireSchemas/*.wire.yaml 确定性导出为
    Unity 协议包内已提交的 MemoryPack 生成产物。

.DESCRIPTION
    这是协议 Wire 正式导出的唯一生成/校验入口，Shooter/MOBA 通过同一注册表复用。
    默认（无参数）重新生成并写回 shooter 的导出产物；-Check 为只读校验模式：编译器在内存中
    重新执行确定性导出，与已提交产物逐文件比较（比较前统一 CRLF/LF，仓库没有 .gitattributes，
    core.autocrlf 会改写检出文件，字节级比较必须忽略行尾差异），不一致则退出码 3（stale）。

    比较范围（受管文件）：导出目录内全部 *.g.cs 与 protocol-export.json；Unity .meta 等其它
    文件不参与比较。缺失 schema 默认仅警告（渐进迁移）；-Strict 将其升级为失败（退出码 4）。

    修改任一 *.protocol.yaml / *.wire.yaml 后必须先运行本脚本（不带 -Check）把生成产物一并
    提交；CI（.github/workflows/abilitykit-test-gates.yml 的 protocol-catalogs job）在
    PR/push 门禁用 -Check 拦截陈旧产物。

    项目注册表：新项目接入 = 编写该项目的 *.wire.yaml（projectId 声明归属）并把输出目录
    登记到下方 $projectRegistry。Shooter 与 MOBA 均已接入默认确定性导出；MOBA 仍处于渐进迁移，
    因而默认只校验已接管的类型，不能在全项目 schema 补齐前启用 -Strict。

.PARAMETER Projects
    要导出/校验的项目键（注册表键），逗号分隔。默认 shooter,moba。

.PARAMETER Check
    只读校验模式；stale 时退出码 3，不写任何文件。

.PARAMETER Strict
    把缺失 wire schema 的警告升级为失败（退出码 4）。

.PARAMETER RepositoryRoot
    仓库根目录；默认取脚本所在目录的上一级（测试脚本用临时夹具仓库覆盖）。

.EXAMPLE
    ./tools/export-protocol-wire.ps1                        # 重新生成写回 shooter 与 moba 导出产物
    ./tools/export-protocol-wire.ps1 -Check                 # 只读校验，stale 时非零退出
    ./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check
#>
[CmdletBinding()]
param(
    [string[]]$Projects = @('shooter', 'moba'),
    [switch]$Check,
    [switch]$Strict,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = if ($RepositoryRoot) { (Resolve-Path $RepositoryRoot).Path } else { Split-Path -Parent $PSScriptRoot }
$compilerProject = Join-Path $PSScriptRoot 'AbilityKit.Protocol.CatalogCompiler/AbilityKit.Protocol.CatalogCompiler.csproj'
$catalogInput = Join-Path $repoRoot 'Protocols/Catalogs'
$wireInput = Join-Path $repoRoot 'Protocols/WireSchemas'

# 项目注册表：项目键 -> 协议 projectId 与已提交导出目录（相对仓库根）。
$projectRegistry = [ordered]@{
    shooter = @{
        ProjectId = 'abilitykit.shooter'
        ExportDirectory = 'Unity/Packages/com.abilitykit.protocol.shooter/Runtime/Generated'
    }
    moba = @{
        ProjectId = 'abilitykit.moba'
        ExportDirectory = 'Unity/Packages/com.abilitykit.protocol.moba/Runtime/Generated/MemoryPack'
    }
    room = @{
        ProjectId = 'abilitykit.shared'
        ExportDirectory = 'Unity/Packages/com.abilitykit.protocol.room/Runtime/Generated/MemoryPack'
    }
}

$wireSchemaSearchPaths = @()
if (Test-Path $wireInput) {
    $wireSchemaSearchPaths = Get-ChildItem -Path $wireInput -Filter '*.wire.yaml' -Recurse -File | Select-Object -ExpandProperty FullName
}

$firstFailureExitCode = 0
foreach ($projectKey in $Projects) {
    $entry = $projectRegistry[$projectKey]
    if ($null -eq $entry) {
        Write-Error ("Unknown protocol wire project '{0}'. Known projects: {1}." -f $projectKey, ($projectRegistry.Keys -join ', '))
        exit 1
    }

    $projectId = $entry.ProjectId
    $exportDirectory = Join-Path $repoRoot $entry.ExportDirectory

    $ownsWireSchema = $false
    foreach ($schemaPath in $wireSchemaSearchPaths) {
        $match = Select-String -LiteralPath $schemaPath -Pattern ('projectId:\s*' + [regex]::Escape($projectId)) -SimpleMatch:$false
        if ($null -ne $match) {
            $ownsWireSchema = $true
            break
        }
    }
    if (-not $ownsWireSchema) {
        Write-Error ("Project '{0}' ({1}) owns no *.wire.yaml under {2}; author its wire schemas before enabling the wire export gate." -f $projectKey, $projectId, $wireInput)
        if ($firstFailureExitCode -eq 0) { $firstFailureExitCode = 1 }
        continue
    }

    Write-Host ("=== Protocol wire export [{0}] projectId={1}" -f $projectKey, $projectId)
    $compilerArguments = @(
        'run',
        '--project', $compilerProject,
        '--',
        '--input', $catalogInput,
        '--wire-input', $wireInput,
        '--export-memorypack', $exportDirectory,
        '--project', $projectId
    )
    if ($Strict) { $compilerArguments += '--strict' }
    if ($Check) { $compilerArguments += '--check' }

    & dotnet @compilerArguments
    if ($LASTEXITCODE -ne 0 -and $firstFailureExitCode -eq 0) {
        $firstFailureExitCode = $LASTEXITCODE
    }
}

exit $firstFailureExitCode

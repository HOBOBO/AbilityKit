# shooter StateSync multiprocess 测试

## 关键工具

- `Server/Orleans/tools/restart_shooter_state_sync.bat`（启动本地 shooter state-sync 服务器）
- `Server/Orleans/tools/restart_shooter_state_sync.ps1`（PowerShell 实现）
- `Server/Orleans/tools/run_shooter_multiprocess_smoke.ps1`（多进程 smoke runner）
- `Server/Orleans/tools/test_shooter_multiprocess_ownership_cleanup.ps1`（TEST-01C 进程清理探针）
- `tools/run_shooter_aoi_lod_gate.ps1`（AOI/LOD 性能门控）

## 通过 gate 跑（推荐）

shooter 测试全部在 `tools/test-gates.json` 注册：

| Gate | 入口脚本 | 产物 ArtifactRoot |
|------|---------|-------------------|
| `shooter-fast` | dotnet test（4 个项目）| gate runner 默认 |
| `shooter-integration` | dotnet test（3 个 Orleans 项目）| gate runner 默认 |
| `shooter-unity-playmode` | Unity PlayMode | gate runner 默认 unity-results/ |
| `shooter-multiprocess` (TEST-01B) | `run_shooter_multiprocess_smoke.ps1 -Profile minimal` | `{GateOutputDirectory}/test-01b/` |
| `shooter-multiprocess-compatibility` | `run_shooter_multiprocess_smoke.ps1 -Profile compatibility` | `{GateOutputDirectory}/compatibility/` |
| `shooter-multiprocess-ownership-cleanup` (TEST-01C) | `test_shooter_multiprocess_ownership_cleanup.ps1` | `{GateOutputDirectory}/test-01c/` |
| `shooter-performance` | `run_shooter_aoi_lod_gate.ps1 -Profile {smoke/full}` | `{GateOutputDirectory}/performance/{smoke,full}.json` |

`{GateOutputDirectory}` 由 `run_test_gate.ps1` 自动替换为 `artifacts/test-gates/{ts}-{gate}/`。

## TEST-01B / TEST-01C 的产物结构

`run_shooter_multiprocess_smoke.ps1 -ArtifactRoot {path}` 会生成：

```
{ArtifactRoot}/
├── manifest.json                          ← 矩阵配置
├── processes/                             ← 每个进程的输出
│   ├── server.log
│   ├── silo.log
│   ├── client-1.log
│   └── client-2.log
├── network/                               ← 网络捕获
│   └── *.pcap
├── results/
│   ├── scenario-result.json               ← 场景结果
│   └── compatibility-matrix.json          ← 矩阵结果（仅 compatibility profile）
└── diagnostics/
    └── fault-recovery.json                ← 故障恢复诊断
```

TEST-01C 还会生成：

```
{ArtifactRoot}/
├── ownership-report.json                  ← 所有权清理报告
├── port-release-evidence.json             ← 端口释放证据
└── preserved-timeout-failure.json         ← 保留的超时失败证据
```

## restart_shooter_state_sync.bat（本地服务器）

用途：启动本地 Orleans state-sync 服务器（默认 `127.0.0.1:41001`），供 PlayMode 客户端连接测试。

调用方式：双击 `.bat` 或 PowerShell 执行。

产物：服务器进程日志（默认在 `Server/Orleans/logs/` 下，已被 `**/*.log` 忽略）。

## 直接调（绕过 gate）

不推荐，但需要时：

```powershell
# 跑 minimal profile
./Server/Orleans/tools/run_shooter_multiprocess_smoke.ps1 `
    -Configuration Release `
    -Profile minimal `
    -TcpPort 44101 -SiloPort 15111 -OrleansGatewayPort 34101 `
    -ArtifactRoot artifacts/shooter-manual/{yyyyMMdd-HHmmss} `
    -GlobalTimeoutSeconds 240

# 启动 state-sync 服务器（前台）
./Server/Orleans/tools/restart_shooter_state_sync.bat
```

## CI 集成

`.github/workflows/abilitykit-test-gates.yml` 定义多个 job：

- `shooter-multiprocess`（push + schedule）
- `shooter-multiprocess-compatibility`（schedule）
- `shooter-multiprocess-ownership-cleanup`（schedule）
- `shooter-performance-smoke` + `shooter-performance-full`

每个 job 都用 `actions/upload-artifact@v4 with if: always()` 上传整个 `artifacts/test-gates/{ts}-{gate}/`。

## 端口约定（避免冲突）

| Gate | TcpPort | SiloPort | OrleansGatewayPort |
|------|---------|----------|--------------------|
| `shooter-multiprocess` | 44101 | 15111 | 34101 |
| `shooter-multiprocess-compatibility` | 44201 | 15211 | 34201 |
| `shooter-multiprocess-ownership-cleanup` | 44301 | 15311 | 34301 |
| 本地 PlayMode（restart_shooter_state_sync） | — | — | 41001 |

**重要**：手动跑时必须遵守端口约定，避免与 CI 并行任务冲突。

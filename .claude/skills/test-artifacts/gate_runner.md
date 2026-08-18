# Gate Runner（推荐入口）

**所有跨项目测试统一走 gate 体系**，由 `tools/run_test_gate.ps1` + `tools/test-gates.json` 驱动。

## 工具文件

- `tools/run_test_gate.ps1` — 主执行器（约 744 行）
- `tools/test-gates.json` — gate 定义（schemaVersion 6，29 个 gate：P0 3、P1 18、P2 8）
- `tools/validate_shooter_test_gates.ps1` — shooter gate 契约验证器
- `.github/workflows/abilitykit-test-gates.yml` — CI 工作流
- `Docs/AbilityKit测试门禁与批量回归规范.md` — 团队约定文档

## 用法

```powershell
# 列出所有 gate
./tools/run_test_gate.ps1 -List

# 跑默认 gate（precheck）
./tools/run_test_gate.ps1

# 跑指定 gate
./tools/run_test_gate.ps1 -Gate moba-console-smoke

# 跑指定 step（调试用）
./tools/run_test_gate.ps1 -Gate regression -StepName "AOI LOD smoke"

# 指定结果目录
./tools/run_test_gate.ps1 -ResultsDirectory local/Logs/test-gates-custom

# CI 模式（无进度条、nologo）
./tools/run_test_gate.ps1 -Gate precheck -CI
```

## 产物路径

本地默认 `-ResultsDirectory local/Logs/test-gates`，每次运行生成：

```
local/Logs/test-gates/{yyyyMMdd-HHmmss}-{Gate}/
├── gate-summary.json                    ← 总结（status/startedAt/endedAt/steps[]）
├── 01-{StepName}.log                    ← 每个 step 的控制台输出
├── 01-{StepName}.error.log              ← stderr（仅 powershell-script step）
├── test-results/                        ← dotnet test step
│   └── 01-{StepName}.trx
└── unity-results/                       ← unity-*-test step
    ├── {StepName}.xml                   ← NUnit XML
    ├── {StepName}.log                   ← Unity Editor log
    └── {StepName}.command.txt           ← 完整命令行
```

nested gate 路径嵌套：`local/Logs/test-gates/{ts}-regression/{ts}-moba-zhaoyun-unity/...`

## 29 个 Gate 清单

### P0（Development Blocker）

| Gate | 用途 |
|------|------|
| `precheck`（默认） | 本地快速验证：build moba console + smoke |
| `moba-console-smoke` | moba console smoke gate（`Gate=MobaConsoleSmoke`）|
| `shooter-multiprocess-soak` | 计划任务/手工 16/64 observer PureState soak、网络阶段、恢复与资源趋势 |

### P1（Contract Blocker）

| Gate | 用途 |
|------|------|
| `moba-codegen` | 构建 framework/MOBA Generator 与 Analyzer，并运行 CodeGen 契约测试 |
| `runtime-contracts` | 网络 runtime / World DI / GameView runtime 契约 |
| `moba-network-options` | MOBA 战斗数据面选项装配契约（room-gateway 预设 / 输入序列化 / 重试 / 可靠事件 cursor）|
| `moba-acceptance-dotnet` | 无 Unity 的 MOBA 白盒验收判定（trace 基线 `src/AbilityKit.Demo.Moba.Acceptance.Tests/`）|
| `network-sdk` | 网络 SDK / 传输 / battle data-plane / transport loopback 契约 |
| `core-stability` | core 正确性、稳定包警告基线、命名空间所有权、确定性数学、分配契约 |
| `foundation-units` | 基础叶子包单元契约：timer / gameplaytags / diagnostics / flow / protocol |
| `moba-content-contracts` | 资源所有权 / 业务 ID / 触发聚合 / 表现资源 / 生成时机 |
| `moba-xiaoqiao-unity` | 小乔 Unity EditMode 验收（6 个 step）|
| `moba-lianpo-unity` | 廉颇 Unity EditMode 验收（2 个 step）|
| `shooter-fast` | shooter 协议/策略/隔离/网络/录像/基准 |
| `shooter-integration` | Gateway / Grains / 客户端集成 / 协议兼容 |
| `shooter-unity-playmode` | Unity PlayMode 客户端同步 smoke |
| `shooter-multiprocess` | TEST-01B 多进程故障恢复（minimal profile）|
| `shooter-multiprocess-ownership-cleanup` | TEST-01C 进程所有权清理探针 |
| `shooter-performance` | AOI/LOD smoke + full 性能 profile |
| `moba-complete-battle-journey` | World 生命周期与 Unity 技能、弹道、buff、伤害、死亡、重生、结算验收 |
| `moba-smoke` | 双客户端 TCP Gateway 登录、房间、选将、开战与权威帧 smoke |

### P2（Regression Baseline）

| Gate | 用途 |
|------|------|
| `regression`（manual/scheduled 默认） | 全量回归（含 moba 4 英雄 Unity 验收嵌套 gate）|
| `moba-zhaoyun-unity` | 赵云 Unity EditMode 验收（基线）|
| `moba-mozi-unity` | 墨子 Unity EditMode 验收（基线）|
| `moba-daji-unity` | 妲己 Unity EditMode 验收（基线）|
| `moba-yingzheng-unity` | 嬴政 Unity EditMode 验收（基线）|
| `shooter-multiprocess-compatibility` | 兼容性矩阵（Packed/PureState × 1-2 client × fault/reconnect）|
| `runtime-performance-measurement` | Pipeline/Triggering 性能契约与 JSON 测量产物 |
| `moba-multiprocess` | MOBA host-only silo 生命周期与端口隔离 smoke |

## 6 种 Step Kind

| Kind | 用法 | 产物 |
|------|------|------|
| `dotnet-build` | `dotnet build {project} -c {Configuration}` | `{idx}-{name}.log` |
| `dotnet-test` | `dotnet test {project} --logger trx;LogFileName={idx}-{name}.trx` | `{idx}-{name}.log` + `test-results/{idx}-{name}.trx` |
| `unity-editmode-test` | Unity batchmode `-runTests -testPlatform EditMode -testFilter {filter}` | `unity-results/{name}.xml` + `.log` + `.command.txt` |
| `unity-playmode-test` | 同上但 `-testPlatform PlayMode` | 同上 |
| `unity-execute-method` | Unity batchmode `-executeMethod {Method}` | 自定义 resultsFile |
| `powershell-script` | 委托给子脚本，`{GateOutputDirectory}` 占位符替换 | `{idx}-{name}.log` + `.error.log` |

## Gate 之间依赖（nested gate）

gate 的 step 可以是另一个 gate：

```json
{
  "name": "regression",
  "steps": [
    { "name": "Zhao Yun Unity acceptance", "kind": "gate", "gate": "moba-zhaoyun-unity" }
  ]
}
```

runner 检测循环依赖（`HashSet<string> Visiting`），抛 `"Circular gate dependency detected"`。

## gate-summary.json 结构

```json
{
  "gate": "precheck",
  "gatePath": ["precheck"],
  "level": "P0",
  "owner": "Core/MOBA Runtime",
  "status": "Passed",
  "startedAt": "2026-07-20T...",
  "endedAt": "2026-07-20T...",
  "elapsedSeconds": 12.345,
  "outputDirectory": "local/Logs/test-gates/20260720-...",
  "summaryPath": "local/Logs/test-gates/20260720-.../gate-summary.json",
  "failureMessage": null,
  "steps": [
    { "name": "...", "kind": "dotnet-test", "status": "Passed", "exitCode": 0, ... }
  ]
}
```

CI 工作流会显式传入 `-ResultsDirectory artifacts\\test-gates\\{job}`，再用 `if: always() + actions/upload-artifact@v4` 上传对应的 `artifacts/test-gates/{job}/`。本地运行不应复用该路径。

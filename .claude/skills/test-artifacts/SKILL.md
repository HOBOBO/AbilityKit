---
name: test-artifacts
description: AbilityKit 测试产物管理规范——统一规划 moba headless / shooter StateSync multiprocess / dotnet test / Unity EditMode+PlayMode 四类测试的输出路径、命名、生命周期与清理。涵盖 tools/run_test_gate.ps1 + tools/test-gates.json 的 29 个 gate 体系（P0 3 / P1 18 / P2 8，含 foundation-units 基础叶子包契约与 moba-acceptance-dotnet 白盒验收）、本地 local/Logs/ 与 CI 显式 artifacts/test-gates/ 目录约定、.gitignore 覆盖范围、绕过 gate 体系直接跑命令的注意事项。触发场景：跑测试、写新测试、加 test gate、排查测试产物位置、清理 TestResults、MultiplayerHeadlessHeroReplacement 产物、shooter multiprocess artifact、TRX 文件、NUnit XML、上传 CI artifact、CI workflow 上传测试产物。
---

# test-artifacts skill

基于源码核校（2026-08-04）。本 skill 规范 AbilityKit 所有测试产物的输出路径、命名、生命周期、清理与 gitignore 覆盖，覆盖 4 类测试：

1. **moba headless**（`MultiplayerHeadlessHeroReplacementCommand` 等 Unity executeMethod 无头测试）
2. **shooter StateSync multiprocess**（`run_shooter_multiprocess_smoke.ps1` + Orleans 服务器）
3. **dotnet test**（`AbilityKit.Demo.Moba.Tests` / `NetworkCondition.Tests` / `Shooter.Runtime.Tests` 等）
4. **Unity EditMode / PlayMode**（`com.abilitykit.demo.moba.editor/Tests` 与 `view.runtime/Runtime/Game/Test/`）

## 核心原则

**本地测试产物必须落在 `local/Logs/` 下，永远不要写在项目根或源码目录中。** `tools/run_test_gate.ps1` 的本地默认值为 `local/Logs/test-gates`；直接调用的测试命令也必须显式指定该根目录下的结果路径。

CI 是唯一例外：`.github/workflows/abilitykit-test-gates.yml` 显式传入 `-ResultsDirectory artifacts\\test-gates\\...`，以便 `actions/upload-artifact` 上传。该覆盖仅适用于短生命周期的 CI 工作区，不能作为本地默认值。

历史教训：2026-07-20 之前，`MultiplayerHeadlessHeroReplacementCommand` 默认 resultPath 是 `../MultiplayerHeadlessHeroReplacement.xml`（相对 `Unity/` 即项目根），导致根目录累积了 24 个带 `-fix/-rerun/-probe/-diagnostic` 后缀的调试 XML 快照。

## 本地产物目录总览（全部被 .gitignore 覆盖）

```
local/Logs/
├── test-gates/                          ← gate 体系输出（推荐入口）
│   └── {yyyyMMdd-HHmmss}-{Gate}/        ← 每次运行一个目录
│       ├── gate-summary.json            ← gate 总结
│       ├── 01-{StepName}.log            ← 每个 step 的日志
│       ├── test-results/                ← dotnet test TRX
│       │   └── 01-{StepName}.trx
│       └── unity-results/               ← Unity 测试 NUnit XML + log + command.txt
│           ├── {StepName}.xml
│           ├── {StepName}.log
│           └── {StepName}.command.txt
├── headless/                            ← 不走 gate 的 ad-hoc 无头测试
│   └── MultiplayerHeadlessHeroReplacement-{yyyyMMdd-HHmmss}.xml
├── moba-acceptance/                     ← Unity MOBA 验收 trace / summary
├── moba-console-smoke/                  ← Console smoke trace / summary
├── dotnet/                              ← 直接 dotnet test 的 TRX
├── unity-manual/                        ← 直接 Unity batchmode 的 XML / log
└── shooter-manual/                      ← 直接 shooter multiprocess 的进程证据
```

## .gitignore 覆盖

```
/local/
**/*.log
**/TestResults/
```

`/local/` 覆盖所有本地产物；`**/*.log` 与 `**/TestResults/` 仅用于拦截未遵守显式输出约定的工具默认值。

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [gate_runner.md](gate_runner.md) — `run_test_gate.ps1` + `test-gates.json` 29 gate 体系（推荐入口；含 foundation-units 基础叶子包契约 + moba-acceptance-dotnet 白盒验收判定 gate，trace 基线在 `src/AbilityKit.Demo.Moba.Acceptance.Tests/{Fixtures,Traces}/`，真实基线经 `tools/capture_moba_acceptance_traces.ps1` 刷新）
- [dotnet_test.md](dotnet_test.md) — dotnet test 直接跑的产物（TRX + local/Logs/dotnet/）
- [unity_editmode_playmode.md](unity_editmode_playmode.md) — Unity EditMode/PlayMode 测试产物（NUnit XML + Editor Tests）
- [moba_headless.md](moba_headless.md) — MultiplayerHeadlessHeroReplacementCommand 等无头测试
- [shooter_state_sync.md](shooter_state_sync.md) — shooter multiprocess + StateSync + restart_shooter_state_sync.bat
- [ad_hoc_workflows.md](ad_hoc_workflows.md) — 绕过 gate 体系的注意事项 + 清理策略
- [ci_integration.md](ci_integration.md) — .github/workflows/abilitykit-test-gates.yml + actions/upload-artifact

## 相关 skill

- moba demo 测试体系 → [moba-demo](../moba-demo/SKILL.md)（含 5 套测试清单）
- shooter demo 测试体系 → [shooter-demo](../shooter-demo/SKILL.md)（含 AcceptanceSpecs / PlayMode）
- 帧同步测试 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)

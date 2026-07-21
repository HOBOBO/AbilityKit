---
name: test-artifacts
description: AbilityKit 测试产物管理规范——统一规划 moba headless / shooter StateSync multiprocess / dotnet test / Unity EditMode+PlayMode 四类测试的输出路径、命名、生命周期与清理。涵盖 tools/run_test_gate.ps1 + tools/test-gates.json 的 17 个 gate 体系、artifacts/headless/ 与 artifacts/test-gates/ 目录约定、.gitignore 覆盖范围、绕过 gate 体系直接跑命令的注意事项。触发场景：跑测试、写新测试、加 test gate、排查测试产物位置、清理 TestResults、MultiplayerHeadlessHeroReplacement 产物、shooter multiprocess artifact、TRX 文件、NUnit XML、上传 CI artifact、CI workflow 上传测试产物。
---

# test-artifacts skill

基于源码核校（2026-07-20）。本 skill 规范 AbilityKit 所有测试产物的输出路径、命名、生命周期、清理与 gitignore 覆盖，覆盖 4 类测试：

1. **moba headless**（`MultiplayerHeadlessHeroReplacementCommand` 等 Unity executeMethod 无头测试）
2. **shooter StateSync multiprocess**（`run_shooter_multiprocess_smoke.ps1` + Orleans 服务器）
3. **dotnet test**（`AbilityKit.Demo.Moba.Tests` / `NetworkCondition.Tests` / `Shooter.Runtime.Tests` 等）
4. **Unity EditMode / PlayMode**（`com.abilitykit.demo.moba.editor/Tests` 与 `view.runtime/Runtime/Game/Test/`）

## 核心原则

**所有测试产物必须落在 `artifacts/` 下，永远不要写在项目根**。

历史教训：2026-07-20 之前，`MultiplayerHeadlessHeroReplacementCommand` 默认 resultPath 是 `../MultiplayerHeadlessHeroReplacement.xml`（相对 `Unity/` 即项目根），导致根目录累积了 24 个带 `-fix/-rerun/-probe/-diagnostic` 后缀的调试 XML 快照。已在 `artifacts/headless-archive/` 归档。

## 产物目录总览（全部被 .gitignore 覆盖）

```
artifacts/
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
├── headless-archive/                    ← 历史归档（2026-07-20 前根目录残留）
│   └── MultiplayerHeadlessHeroReplacement-*.xml (24 个)
├── test-gates/contract-validation/      ← validate_shooter_test_gates.ps1 输出
│   └── report.json
└── gateway-build-validation/            ← Server/Orleans 子项目构建产物
```

## .gitignore 覆盖

```
**/*.log
**/artifacts/
**/TestResults/
```

这三条覆盖了所有测试产物路径（dotnet 默认 `TestResults/` + 项目自定义 `artifacts/`）。

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [gate_runner.md](gate_runner.md) — `run_test_gate.ps1` + `test-gates.json` 17 gate 体系（推荐入口）
- [dotnet_test.md](dotnet_test.md) — dotnet test 直接跑的产物（TRX + TestResults/）
- [unity_editmode_playmode.md](unity_editmode_playmode.md) — Unity EditMode/PlayMode 测试产物（NUnit XML + Editor Tests）
- [moba_headless.md](moba_headless.md) — MultiplayerHeadlessHeroReplacementCommand 等无头测试
- [shooter_state_sync.md](shooter_state_sync.md) — shooter multiprocess + StateSync + restart_shooter_state_sync.bat
- [ad_hoc_workflows.md](ad_hoc_workflows.md) — 绕过 gate 体系的注意事项 + 清理策略
- [ci_integration.md](ci_integration.md) — .github/workflows/abilitykit-test-gates.yml + actions/upload-artifact

## 相关 skill

- moba demo 测试体系 → [moba-demo](../moba-demo/SKILL.md)（含 4 套测试清单）
- shooter demo 测试体系 → [shooter-demo](../shooter-demo/SKILL.md)（含 AcceptanceSpecs / PlayMode）
- 帧同步测试 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)

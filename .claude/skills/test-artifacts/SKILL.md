---
name: test-artifacts
description: AbilityKit 运行产物与测试门禁指引。用于运行测试、构建或 Unity 批处理前指定输出路径，定位本地或 CI 证据、检查暂存区，以及区分运行报告与测试基线。
---

# 测试产物指引

基于源码核校（2026-08-04）。本 skill 规范 AbilityKit 所有测试产物的输出路径、命名、生命周期、清理与 gitignore 覆盖，覆盖 4 类测试：

1. **moba headless**（`MultiplayerHeadlessHeroReplacementCommand` 等 Unity executeMethod 无头测试）
2. **shooter StateSync multiprocess**（`run_shooter_multiprocess_smoke.ps1` + Orleans 服务器）
3. **dotnet test**（`AbilityKit.Demo.Moba.Tests` / `NetworkCondition.Tests` / `Shooter.Runtime.Tests` 等）
4. **Unity EditMode / PlayMode**（`com.abilitykit.demo.moba.editor/Tests` 与 `view.runtime/Runtime/Game/Test/`）

## 核心原则

**本地运行报告统一放 `local/Logs/`，不要写在仓库根、`Unity/` 根或源码目录。** 已有测试 gate 优先用 `tools/run_test_gate.ps1`（默认 `local/Logs/test-gates`）。直接执行测试、构建或 Unity 批处理时，启动前建好带时间戳的运行目录，显式指定日志和结果路径。正式发布物、构建缓存与被测试消费的夹具不随日志迁移。

提交前用 `python -X utf8 tools/check_repo_structure.py --strict --staged-outputs` 检查暂存内容。报告不应暂存；发现问题时只撤销对应文件的暂存，再按[归档规则](../repo-structure-governance/SKILL.md)处理。未改动的历史日志只提示，不因这次检查删除。

CI 如有接线，应显式指定并上传 `artifacts/test-gates/` 下对应的任务目录。是否已接线、如何校验参见[测试门禁与批量回归规范](../../../Docs/AbilityKit测试门禁与批量回归规范.md#8-ci-接入与静态校验)，不要把 CI 目录作为本地默认路径。

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

## 分节索引

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [gate_runner.md](gate_runner.md) — 现有 gate 的发现、执行与产物定位
- [dotnet_test.md](dotnet_test.md) — dotnet test 直接跑的产物（TRX + local/Logs/dotnet/）
- [unity_editmode_playmode.md](unity_editmode_playmode.md) — Unity EditMode/PlayMode 测试产物（NUnit XML + Editor Tests）
- [moba_headless.md](moba_headless.md) — MultiplayerHeadlessHeroReplacementCommand 等无头测试
- [shooter_state_sync.md](shooter_state_sync.md) — shooter multiprocess + StateSync + restart_shooter_state_sync.bat
- [ad_hoc_workflows.md](ad_hoc_workflows.md) — 绕过 gate 体系的注意事项 + 清理策略
- [ci_integration.md](ci_integration.md) — CI 接入约定入口，不代表当前已接线

## 相关 skill

- moba demo 测试体系 → [moba-demo](../moba-demo/SKILL.md)（含 5 套测试清单）
- shooter demo 测试体系 → [shooter-demo](../shooter-demo/SKILL.md)（含 AcceptanceSpecs / PlayMode）
- 帧同步测试 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)

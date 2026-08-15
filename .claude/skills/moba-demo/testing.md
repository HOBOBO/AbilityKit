# 5 套测试体系

## 1. MOBA .NET xUnit（`src/AbilityKit.Demo.Moba.Tests/`）

按业务分目录：

- `AI/`：AiTrainingEnvironment / AiTrainingRunnerOutput
- `Buff/`：BuffStackingPolicyApplier
- **`Collision/`**：Grid/naive 等价、OBB sweep、GridBroadphase、Moba motion adapter
- `Context/`：ContextBridge / ContinuousExecutionContext
- `Continuous/`：ContinuousLifecycle
- **`Motion/`**：穿墙投影、blink/block、墙滑、召唤物行为树 smoke
- **`Navigation/`**：绕障、不可达、确定性、直线化简、walkable
- `Passive/`：PassiveLifecycleService
- `Skill/`：SkillCastRuntimeService / SkillInputHandleResult
- `Smoke/`：Console smoke、trace artifact、battle script、首帧快照、配置预热与 snapshot buffer；**LiveSim 系列**（`LiveSimTraceSource`/`LiveSimSetupActionExecutor`/`LiveSimTimelineRunner` + 三个测试，见 §6）
- `Trace/`：TraceRegistrySmoke
- `Triggering/`：OwnerBoundTriggerGate / PresentationCueRuntime / TriggerExecutionGateway / ProjectileAreaTriggerConfig

## 2. MOBA 网络条件 .NET xUnit（`src/AbilityKit.Demo.Moba.NetworkCondition.Tests/`）

`NetworkConditionControllerTests`（单文件引用 `view.runtime` 的 `NetworkConditionController.cs`）

## 3. CodeGen / Analyzer .NET xUnit（`src/AbilityKit.CodeGen.Tests/`）

验证框架 Source Generator、框架 Analyzer、MOBA Generator/Analyzer、生成 Manifest 契约和诊断稳定性。修改编译期逻辑时优先运行 `moba-codegen` P1 gate。

## 4. Unity Edit-mode（`com.abilitykit.demo.moba.editor/Tests/`）

asmdef `AbilityKit.Demo.Moba.Diagnostics.Core.Tests`，覆盖：

- BattleDebug 各部分
- Diagnostic 各 Producer（Area/Buff/Effect/Exception/Heal/Projectile/Summon/Sync/Trace）
- Diagnostic State / Store / ViewModel
- `MobaDiagnosticSystemOrderTests`

## 5. Unity 内联测试（`view.runtime/Runtime/Game/Test/`）

asmdef `AbilityKit.Game.UnitTests`（`UNITY_INCLUDE_TESTS`）：

- 6 英雄验收测试：`UnitTest/Acceptance/Heroes/{Daji, LianPo, Mozi, XiaoQiao, YingZheng, ZhaoYun}`
- `FrameSyncTestHarness`
- `TriggerRunnerSmokeTests`
- `AttributeModifierStorageTests`
- `BattleFlowOnGUITest`
- `TimelineLogicRunner`
- `UnitTest/Acceptance/Common/` / `UnitTest/Acceptance/Infrastructure/`
- `Expectations/` / `FrameSync/`

## 6. 白盒验收 dotnet 判定层（`src/AbilityKit.Demo.Moba.Acceptance` + `.Tests`，2026-08-14 新增）

设计见 `Docs/AbilityKit白盒测试DSL与测试平台设计.md`（含 seam 路线 §14.1）。**无 Unity 的验收判定管线**：

- **lib**（零 ProjectReference）：链接真实 `MobaAcceptanceModels.cs`（Unity 包内原文件）+ `AcceptanceJsonCodec`（System.Text.Json 替 `UnityEngine.JsonUtility`）+ `AcceptanceVerifier`（从 `MobaAcceptanceTraceExporter.BuildCoverage` 忠实移植、harness-free）+ `ITraceSource`（`FileTraceSource`/`NullTraceSource`/`CompositeTraceSource`）+ `AcceptanceBatchRunner`（扫期望目录 → 每用例取 trace → 判定 → 生产同款 `batch_summary.json`；needs-trace 不计 failed）。
- **gate**：`moba-acceptance-dotnet`（P1，`tools/test-gates.json` + GH workflow job；`Gate=MobaAcceptanceDotnet` trait 过滤）。
- **trace 来源三层**（`CompositeTraceSource` 按序取，真实优先）：
  1. 合成 `Tests/Fixtures/`（兜底，永远过）；
  2. 真实 `Tests/Traces/` —— `tools/capture_moba_acceptance_traces.ps1` 经 Unity 批量入口 `MobaAcceptanceWebCommand.RunDirectoryFromCommandLine` 捕获（需 Unity 2022.3.62f1，本机手动跑一次 + 提交）；
  3. **`LiveSimTraceSource`**（`Moba.Tests/Smoke/`）—— 跑真实 console sim 产 trace，**纯 dotnet 无需 Unity**；配套 `LiveSimSetupActionExecutor`（setupActions 五动词移植 + alias 注册表 + `DisableActorBrain`/`HasCollider` 辅助）、`LiveSimTimelineRunner`（timeline 动词 → 富 `SkillInputEvent` 经 `IMobaInputCoordinator` 提交）与 `LiveSimAcceptanceScenarioRunner`（收敛件：期望→actors 装配[本地玩家 seed + spawn_actor]+**锚点平移**(-15,0,0) 适配 console 原型图+setup+timeline→live trace→`AcceptanceVerifier`；真实 `skill_10010101_scenario` 完整 verdict 已跑通）。**关键坑**：console `Configs/ability/triggers/skills/` 是 `trigger_source_manifest.json` 的 `skills` 类别加载源，可能陈旧于 Unity `Resources/ability/triggers/skills/`——同步方向是 Unity 权威→console 副本。**批量同步工具**：`tools/sync_moba_ability_triggers.ps1`（字节级 SHA256 比对，`-Apply` 落盘、`-FailOnDrift` 供 CI；EXTRA 只报告不删）。**已 apply（2026-08-14）**：55 文件同步（31 DIFFERENT + 24 MISSING），验证 console smoke 9/9 + Skill/Triggering/Buff 102/102 全绿无回归；剩 6 个 EXTRA（console 独有技能触发器，Unity 包无，待人工确认是否 console 专属）——`trigger_10060112/10060212/10060312/10060314/10060315/10060316.json`。5 个端到端测试（`Gate=MobaConsoleSmoke`，5/5 绿）。**覆盖扫描**（`LiveSimAcceptanceBatchTests`，诊断型恒绿）：遍历全部 11 期望做 live 判定——**2/11 已完整 verdict**（skill_10010101 + _scenario，都是 hero 1001 skill 1）；9 失败归因两类：**(A) 施法者英雄不匹配**（1002 英雄技能 SkillCast=0，因 `AssembleActors` 把施法者 seed 到 console 默认本地玩家=廉颇 1001，而 console `BattleStartConfig.Players` 已内置 3 英雄 player_1=1001/player_2=1002/player_3=1003——需 heroId→playerId 映射），**(B) 复杂技能效果**（1001 的 2/3 号技能 area/jump/多段）。
- **注意**：修改 `MobaAcceptanceModels.cs`/期望 schema 前必须过 `moba-acceptance-dotnet`；行为有意变更后重跑捕获脚本刷新 `Traces/` 基线。

## 共享测试基础设施

运行时侧 `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Testing/`：

- `BattleTestScript` / `BattleTestScriptRunner`
- `MobaRuntimeTestEnvironment`
- `MobaTestConfigBuilder`

被 Console AutoTest 与 .NET Smoke 测试复用。

注意：`com.abilitykit.demo.moba.runtime` 的 csproj（`AbilityKit.Demo.Moba.Core`）**排除 `Testing/` 目录**，测试脚本只在 Unity 侧与 Console 侧用。

## 推荐验证入口

```powershell
# 修改 CodeGen/Analyzer/Manifest 时
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate moba-codegen

# 修改 MOBA runtime 或 Console smoke 相关逻辑时
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

# 4 套测试体系

## 1. .NET xUnit（`src/AbilityKit.Demo.Moba.Tests/`，179 个，全部通过 ✅）

按业务分目录：

- `AI/`（2）：AiTrainingEnvironment / AiTrainingRunnerOutput
- `Buff/`（1）：BuffStackingPolicyApplier
- **`Collision/`（8）**：`GridCollisionWorldTests`（4，grid/naive 等价）+ `CollisionCorrectnessFixTests`（3，OBB sweep+GridBroadphase）+ `MobaMotionCollisionWorldAdapterTests`（3）— NEW
- `Context/`（2）：ContextBridge / ContinuousExecutionContext
- `Continuous/`（1）：ContinuousLifecycle
- **`Motion/`（9）**：`MotionWallInteractionTests`（5，穿墙投影/blink/block）+ `ConfigurableMotionSolverSlideTests`（2，墙滑）+ `MobaSummonBTreeSkillSmokeTests`（已修复）— NEW
- **`Navigation/`（5）**：`GridPathfinderTests`（5，绕障/不可达/确定性/直线化简/walkable）— NEW
- `Passive/`（1）：PassiveLifecycleService
- `Skill/`（2）：SkillCastRuntimeService / SkillInputHandleResult
- `Smoke/`（10）：ConsoleMobaSmokeFlowTests(+TestBase) / ConsoleSmokeTraceArtifactExporter / BattleTestScenarioLibrary / BattleTestScriptRunner / MobaRuntimeFirstFrameSnapshotAcceptance / MobaRuntimeLog / MobaRuntimeValidationReport / MobaSkillPipelinePrewarm / MobaSmokeEntryContract / MobaSnapshotBufferConsumption
- `Trace/`（1）：TraceRegistrySmoke
- `Triggering/`（4）：OwnerBoundTriggerGate / PresentationCueRuntime / TriggerExecutionGateway / ProjectileAreaTriggerConfig

## 2. .NET xUnit（`src/AbilityKit.Demo.Moba.NetworkCondition.Tests/`，1 个）

`NetworkConditionControllerTests`（单文件引用 `view.runtime` 的 `NetworkConditionController.cs`）

## 3. Unity Edit-mode（`com.abilitykit.demo.moba.editor/Tests/`，23 个）

asmdef `AbilityKit.Demo.Moba.Diagnostics.Core.Tests`，覆盖：

- BattleDebug 各部分
- Diagnostic 各 Producer（Area/Buff/Effect/Exception/Heal/Projectile/Summon/Sync/Trace）
- Diagnostic State / Store / ViewModel
- `MobaDiagnosticSystemOrderTests`

## 4. Unity 内联测试（`view.runtime/Runtime/Game/Test/`）

asmdef `AbilityKit.Game.UnitTests`（`UNITY_INCLUDE_TESTS`）：

- 6 英雄验收测试：`UnitTest/Acceptance/Heroes/{Daji, LianPo, Mozi, XiaoQiao, YingZheng, ZhaoYun}`
- `FrameSyncTestHarness`
- `TriggerRunnerSmokeTests`
- `AttributeModifierStorageTests`
- `BattleFlowOnGUITest`
- `TimelineLogicRunner`
- `UnitTest/Acceptance/Common/` / `UnitTest/Acceptance/Infrastructure/`
- `Expectations/` / `FrameSync/`

## 共享测试基础设施

运行时侧 `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Testing/`：

- `BattleTestScript` / `BattleTestScriptRunner`
- `MobaRuntimeTestEnvironment`
- `MobaTestConfigBuilder`

被 Console AutoTest 与 .NET Smoke 测试复用。

注意：`com.abilitykit.demo.moba.runtime` 的 csproj（`AbilityKit.Demo.Moba.Core`）**排除 `Testing/` 目录**，测试脚本只在 Unity 侧与 Console 侧用。

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
- `Smoke/`：Console smoke、trace artifact、battle script、首帧快照、配置预热与 snapshot buffer
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

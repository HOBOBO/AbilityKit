---
name: battle-flow
description: 战斗流程编辑器（BattleFlow）——玩法无关的「拖积木→编译→headless跑→verdict+trace」体系。框架三包 com.abilitykit.scenario（中立 IR）/com.abilitykit.battleflow（积木+编译+运行钩子+编辑器窗口）/com.abilitykit.environment（环境 Profile），加 MOBA 项目扩展（binder/runner/shell-out/断言积木）。触发场景：战斗流程、battle flow、积木、场景预览、技能预览、拖积木、断言积木、EnvironmentProfile、TestScenario、BattleBlock、headless runner、battleflow。
---

# battle-flow skill（战斗流程编辑器）

目标：**拖积木 → 编译 → headless 跑 → verdict + trace**（降测试代码成本 + 流程可视化），玩法无关的框架地基 + MOBA 项目扩展。核心闭环已端到端验证（`verdict=PASSED`）。

## 架构（框架给机制、项目给扩展）

| 包 | 职责 |
|----|------|
| `com.abilitykit.scenario` | 中立 DSL IR：`TestScenario`/`TestActor`/`TestTimelineStep`/`TestVector3` + `TestScenarioValidator` + `ScenarioCodec`（**Json.NET**，Unity 无 STJ）。`Expectations` 是 opaque `object?`（断言插件化）。 |
| `com.abilitykit.battleflow` | 积木模型（`BattleBlock`/`BattleAtomicBlock`/`BattleCompositeBlock`）+ `BattleFlowCompiler`（积木树→TestScenario）+ `BattleBlockPalette`（按类别分组的调色板注册表）+ `IBattleFlowRunner`/`BattleFlowRunnerRegistry`（运行钩子）+ `BattleFlowWindow`（Editor 三栏 IMGUI）。 |
| `com.abilitykit.environment` + `demo.moba.environment` | 环境 Profile 机制（concern/原语/profile + expander）+ MOBA taxonomy + `MobaBattleFlowAssertions`（断言 DTO，纯 C# .NET+Unity 共用）。 |
| MOBA 项目扩展 | `MobaEnvironmentProfileBinder`（原语→实体）、`MobaBattleFlowScenarioRunner`（**独立 .NET headless 工程** `src/AbilityKit.Demo.Moba.BattleFlow.Runner`：boot→spawn→cast→trace→verdict）、`MobaBattleFlowRunner`（MOBA editor shell-out）、`MobaAssertionBlocks`（断言积木）。 |

## 红线（必守）

1. **积木组合节点只做「宏」（Sequence），不做「控制流」**——别加 Selector/Parallel/Loop/Condition，否则退化成行为树克隆。控制流/反应式归 `com.abilitykit.behaviortree`（经 `BehaviorProfileId` 挂角色）。
2. **IR 是唯一真相**：积木树编译到 `TestScenario`，测试与预览共用，不造第三套 IR。
3. **框架 Runtime 包保持纯 C#（`noEngineReferences`）**：Odin/Unity 只在 Editor 层用。断言 DTO 这类「编辑器+runner 共用」的类型放 `demo.moba.environment`（有 .NET 镜像）。
4. **编辑器 shell-out 而非进程内跑**：Unity 编辑器不能进程内 boot console 世界（`ConsoleBattleBootstrapper` 是 .NET 工程）。

## 扩展点（怎么加东西）

- **加积木**：继承 `BattleAtomicBlock`（或 `BattleCompositeBlock`），`Compile(BattleFlowBuilder)` 编译到 IR；`BattleBlockPalette.Register(category, template)` 注册进调色板（MOBA 侧用 `[InitializeOnLoad]` 注册，见 `MobaBattleFlowBlocks.cs`）。
- **加项目 runner**：实现 `IBattleFlowRunner`，`BattleFlowRunnerRegistry.Runner = ...` 注册；MOBA 用 shell-out 到 .NET runner。
- **加断言**：继承 `MobaAssertionBlock`，`Apply(MobaBattleFlowAssertions)` 累积断言；runner 侧 `ConvertToExpectation` 映射到 `MobaAcceptanceExpectation` 走 `AcceptanceVerifier`。

## 关键位置

- 编辑器窗口：`com.abilitykit.battleflow/Editor/BattleFlowWindow.cs`
- .NET headless runner：`src/AbilityKit.Demo.Moba.BattleFlow.Runner/`（`Program.cs` 命令行入口 + `MobaBattleFlowScenarioRunner.cs` 世界执行核心）
- 断言积木：`com.abilitykit.demo.moba.editor/Editor/BattleFlow/MobaAssertionBlocks.cs` + `MobaBattleFlowAssertions.cs`（在 demo.moba.environment）
- headless 验证入口：`BattleFlowHeadlessVerify.RunVerify`（`-executeMethod`）
- 测试：`src/AbilityKit.BattleFlow.Tests`、`src/AbilityKit.Demo.Moba.Tests/Smoke/MobaBattleFlowScenarioRunnerTests.cs`

## 当前状态 + 路线图

- **已完成**：核心闭环（拖积木→编译→headless→环境构建→施放→trace→verdict）、断言积木、调色板分组、断言字段反射编辑。
- **P0（下一步）持久化 + 流程库**：保存/加载 `.battleflow`（JSON 序列化积木树）+ 流程库面板。
- **P1 批量化 + CI**：批量跑 `.battleflow` → 汇总 + 挂 gate（复用 `AcceptanceBatchRunner`）。
- **P2 可视化 + 编辑器完善**：接 `TraceTreeWindow`、断言下拉/必填、复合积木作者化、拖拽+undo。

**已知限制**：runner 是 smoke 级（spawn 裸骨架英雄，无属性/技能 loadout），断言只能判"必然存在/不存在"的 trace kind，还不能针对真实技能效果（伤害数值）。补完整需 `InitializeFromAttributeTemplate` + `InitializeFromLoadout`。

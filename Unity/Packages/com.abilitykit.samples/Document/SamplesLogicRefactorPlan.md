# AbilityKit.Samples.Logic Refactor Plan

> 目标：把 `AbilityKit.Samples.Logic` 从“能运行的一组示例”重构为“可持续扩展、可作为正式框架示例包交付、能支撑从零到一学习复杂玩法链路”的纯逻辑示例框架包。

## 1. 当前判断

`AbilityKit.Samples.Logic` 已经具备正式示例包的雏形：

- 有独立的纯逻辑定位，不绑定 Unity、MonoGame、Web 或服务器框架。
- 有 `AbilityKit.Samples.Abstractions` 提供宿主边界、日志、环境、执行服务和目录模型。
- 有 `sample-manifest.json` 提供稳定 id、排序、标题和标签。
- 有 Onboarding、Foundation、Tags、Pipeline、Flow、Triggering、HFSM、Modifiers、World、Combat、Demo 等教学分区。
- 第一批 combat samples 已经覆盖实体、技能库、目标、伤害、碰撞、区域、投射物和命中伤害闭环。

但它还没有达到“正式示例框架包”的成熟度：

- 示例数量多，但只有少数进入 manifest，稳定入口和实际目录不一致。
- 一批旧示例仍偏概念说明或输出 API 名称，没有真实接入框架包能力。
- 旧输出存在乱码、说明文风格、非结构化日志，不适合作为正式文档和 Web 展示。
- 新增复杂示例时缺少统一模板、夹具、命名、层级、清单、验收标准。
- 当前学习路径偏模块罗列，缺少“从一个需求逐步长成完整系统”的主线。
- Demo 示例与框架包能力之间缺少中间层，完整项目过大，小 sample 又不够连续。

## 2. 重构目标

### 2.1 产品目标

把该包定位为 AbilityKit 的“官方纯逻辑样例中心”：

- 新人可以从 `onboarding/*` 开始，按稳定 id 一路学习到战斗、World、同步、回放。
- 框架维护者可以用它验证 package API 是否好用、是否可组合、是否纯逻辑可运行。
- 宿主项目可以复用 catalog / executor / logger，把同一批 sample 挂到控制台、静态 Web、Unity、MonoGame、测试页面。
- 后续新增任意复杂示例时，先补小 sample，再组合成 scenario，最后才进入完整 demo。

### 2.2 工程目标

- 所有正式示例必须进入 `sample-manifest.json`，并使用稳定 id。
- 所有正式示例必须可通过 `SampleExecutionService` 独立运行。
- 所有 Web 标签示例必须确定性、短流程、纯内存、无外部进程、无真实网络、无 UnityEngine 依赖。
- 所有跨帧示例必须由 `ISampleEnvironment` 或明确的 fixed step 驱动，不直接阻塞线程。
- 所有复杂示例必须拆成 fixture、scenario、sample 三层，避免一个文件堆完整游戏。
- 所有旧示例必须进入“保留、重写、迁移、废弃”四类之一。

## 3. 目标架构

### 3.1 包分层

```text
AbilityKit.Samples.Logic/
├── SampleAttribute.cs
├── SampleCatalogProvider.cs
├── SampleRegistry.cs
├── sample-manifest.json
├── Document/
│   ├── SamplesLogicRefactorPlan.md
│   └── CombatSampleCoveragePlan.md
├── Infrastructure/
│   ├── Runtime/                 # sample 专用运行时工具，不进入框架正式 API
│   ├── Fixtures/                # 跨 sample 复用夹具
│   ├── Scenarios/               # 可组合业务场景
│   ├── Assertions/              # 示例级断言与输出校验辅助
│   └── Config/                  # 示例配置、资源、注册表辅助
└── Samples/
    ├── 00_Onboarding/
    ├── 01_Foundation/
    ├── 02_TagsConfig/
    ├── 03_Execution/
    ├── 04_ReactiveState/
    ├── 05_CombatBasics/
    ├── 06_CombatSkills/
    ├── 07_WorldHost/
    ├── 08_BattleSync/
    └── 09_DemoScenarios/
```

说明：目录名称可以保留当前 `Samples/Onboarding` 这类短名，不强制一次性重命名；但文档、manifest order 和后续新增内容应按上面的阶段模型组织。

### 3.2 三层示例模型

正式 sample 不应直接把所有逻辑写在 `OnRun()` 里，而应按复杂度分层：

| 层级 | 作用 | 放置位置 | 示例 |
| --- | --- | --- | --- |
| Fixture | 构造稳定、可复用的测试世界和数据 | `Infrastructure/Fixtures` | `SampleBattleWorld`、`SampleSkillLibraryFixture` |
| Scenario | 表达一个业务过程，可被多个 sample 复用 | `Infrastructure/Scenarios` | `FireballHitScenario`、`AreaDotScenario` |
| Sample | 负责教学叙事、调用 scenario、输出结构化日志 | `Samples/*` | `CombatProjectileHitDamage` |

简单示例可以只有 sample；一旦跨越 2 个以上模块，必须拆出 fixture / scenario。

### 3.3 示例生命周期

每个正式示例应有明确状态：

| 状态 | 含义 | 允许出现在 manifest | 允许打 web 标签 |
| --- | --- | --- | --- |
| Draft | 草稿，占位或探索 API | 否 | 否 |
| Candidate | 可运行，但教学结构未最终整理 | 可选 | 否 |
| Stable | 推荐入口，已结构化输出并通过验收 | 是 | 可选 |
| Legacy | 旧示例，仅保留参考 | 否 | 否 |
| Deprecated | 待删除或迁移 | 否 | 否 |

建议在 manifest item 增加 `status`、`level`、`modules`、`next`、`source` 字段，先由工具忽略也可以，后续再让 catalog 暴露给 UI。

建议 manifest 形态：

```json
{
  "id": "combat/projectile-hit-damage",
  "type": "AbilityKit.Samples.Logic.Samples.Combat.CombatProjectileHitDamage",
  "order": 707,
  "title": "Combat Projectile Hit Damage",
  "status": "Stable",
  "level": "Intermediate",
  "modules": [ "Combat.Projectile", "Combat.Damage", "Dataflow" ],
  "tags": [ "combat", "projectile", "damage", "package-api", "web" ],
  "next": [ "combat/skill-projectile" ]
}
```

## 4. 学习路径重构

### 4.1 主学习线

重构后的主线不再按包名平铺，而按能力成长：

```text
00 Onboarding
  -> 01 Foundation
  -> 02 Tags / Config
  -> 03 Pipeline / Flow
  -> 04 Triggering / HFSM / Modifiers / Continuous
  -> 05 Combat Basics
  -> 06 Combat Skills
  -> 07 World / Host
  -> 08 Sync / Replay
  -> 09 Demo Scenarios
```

每一层都应回答三个问题：

1. 这个层解决什么问题？
2. 它最小可运行 API 是什么？
3. 它如何连接下一层？

### 4.2 从零到一复杂示例路线

复杂玩法不应直接写成一个大 demo，而应沿着下面路线扩展：

```text
Concept
  -> Data Vocabulary
  -> Runtime Context
  -> Single Operation
  -> Cross-frame Process
  -> Event Reaction
  -> State / Modifier Integration
  -> Combat Closure
  -> World Hosted Closure
  -> Sync / Replay Closure
```

以“火球术”为例：

| 阶段 | 示例 id | 目标 |
| --- | --- | --- |
| Concept | `onboarding/skill-slice` | 明确标签、检查、执行、持续效果和事件 |
| Data | `tags/requirements-fireball`、`config/skill-table` | 用标签和配置描述火球术 |
| Runtime | `pipeline/skill-cast-check` | 前置检查、消耗、冷却 |
| Cross-frame | `flow/skill-cast-timing` | 前摇、等待、取消 |
| Event | `triggering/on-hit-burn` | 命中后触发燃烧 |
| Modifier | `modifiers/burning-dot` | 持续扣血、叠层、衰减 |
| Combat | `combat/skill-projectile` | Targeting -> Projectile -> Damage |
| World | `battle/world-skill-cast` | World 中接收输入并推进技能 |
| Sync | `battle/replay-fireball` | 输入录制、状态快照、回放验证 |

## 5. 目录与分类规划

### 5.1 保留但规范化的分类

| 当前分类 | 目标分类 | 处理建议 |
| --- | --- | --- |
| Onboarding | 00 Onboarding | 保留，作为入口，补 next 链接 |
| Foundation | 01 Foundation | 修复乱码，确认是否接入 Core.Common 真实 API |
| Tags | 02 TagsConfig | 保留并补配置驱动示例 |
| Config | 02 TagsConfig | 从 Foundation 分类独立出来 |
| Pipeline | 03 Execution | 保留，统一成技能执行阶段示例 |
| Flow | 03 Execution | 重写说明型示例为真实节点运行 |
| Triggering | 04 ReactiveState | 优先对齐真实 Triggering API |
| Continuous | 04 ReactiveState | 与 Modifiers / Triggering 串起来 |
| StateMachine | 04 ReactiveState | 保留 HFSM 主线，减少纯概念长文 |
| Modifiers | 04 ReactiveState | 保留，增加与 Damage/Skill 的连接 |
| Targeting | 05 CombatBasics | 迁入 combat 主线或作为兼容入口 |
| Combat | 05/06 Combat | 扩展为基础能力 + 技能组合两段 |
| World | 07 WorldHost | 修复问题后再进入正式 manifest |
| Demo | 09 DemoScenarios | 旧 demo 降级，新 demo 必须由前面 sample 组合而来 |

### 5.2 order 区间建议

| 区间 | 分类 | 说明 |
| --- | --- | --- |
| 0-99 | Onboarding | 初始导览和宿主边界 |
| 100-199 | Foundation | 日志、环境、事件、对象池、注册表 |
| 200-299 | Tags / Config | 标签、配置、类型映射 |
| 300-399 | Pipeline / Flow | 阶段执行和跨帧流程 |
| 400-599 | Triggering / HFSM / Modifiers / Continuous | 事件、状态、持续效果、数值变化 |
| 600-699 | World / Host | 生命周期、DI、Host、服务组合 |
| 700-849 | Combat Basics | 实体、目标、伤害、碰撞、区域、投射物 |
| 850-949 | Combat Skills | 技能释放、命中、DOT、Buff、引导、状态门控 |
| 950-1099 | Battle Sync | 输入帧、快照、状态哈希、回放、传输抽象 |
| 1100+ | Demo Scenarios | 端到端小场景和完整 demo |

## 6. 旧示例治理

### 6.1 立即处理清单

| 示例 | 当前问题 | 建议动作 |
| --- | --- | --- |
| `Foundation/EventSystem.cs` | 输出乱码，可能未接入真实 Core API | 重写为 `foundation/event-dispatcher` |
| `Foundation/ObjectPool.cs` | 输出乱码，概念偏旧 | 重写为 `foundation/object-pool` |
| `Foundation/MarkerRegistry.cs` | 输出乱码，与 Config 注册表关系不清 | 迁移到 `config/type-registry` |
| `Triggering/BasicTrigger.cs` | 输出乱码，说明型 | 按真实 API 重写为 `triggering/basic-event-trigger` |
| `Flow/SequenceAndRace.cs` | 偏图解说明 | 改为真实 `SequenceNode` / Race 节点运行 |
| `Flow/TimedFlow.cs` | 偏模拟说明 | 改为 `ISampleEnvironment` 驱动 |
| `Flow/FlowAdvancedExample.cs` | API 名称可能过期，说明过长 | 拆成 2-3 个小 sample |
| `Demo/TowerDefense.cs` | 输出乱码，占位 demo | 降级 Legacy，迁移为 combat skill 系列 |
| `Demo/TimedTowerDefense.cs` | 输出乱码，占位 demo | 降级 Legacy，迁移为 fixed-frame area/projectile 示例 |
| `Demo/RPGBattle.cs` | 输出乱码，占位 demo | 降级 Legacy，迁移为 damage/modifier/skill 示例 |
| `WorldServicesDeepDive.cs` | 已知可能栈溢出 | 修复前不得进入 web / stable |
| `Ability/`（整棵子树，27 文件 / 2308 行） | 不在 manifest、无任何 Attribute 标注（`MarkerScanner` 不会注册）、外部零引用、内部 `*Tests.RunAll()` 从未被调用 | **归类 Legacy（仅保留参考）**，见 §6.3 |

### 6.2 Legacy 隔离策略

建议新增 `Samples/Legacy` 或通过 manifest `status=Legacy` 隐藏旧示例。控制台默认 `--list` 只显示 Stable/Candidate；需要时再提供 `--include-legacy`。

短期如果不改宿主参数，也应做到：

- Legacy 不进入 `sample-manifest.json`。
- Legacy 不带 `web` 标签。
- README 不把 Legacy 放进推荐学习路径。

### 6.3 `Ability/` 归类裁定（2026-09-10）

`Ability/` 是一棵自包含子树：`Ability/Core/`（一套教学用的迷你框架接口：`IActionFactory` / `IWorldModule` / `IPipelineContext`）、`Ability/Samples/1.Bootstrap`~`3.Action`（渐进式示例）、`Ability/Tests/`（从未被调用的自检类）。

它满足 Legacy 的全部特征：不在 `sample-manifest.json`、**无任何 Attribute 标注**、外部零引用（`Samples/Config/SamplePipelinePhases.cs` 的 `ExecutePhase` 与框架的 `WorldBlueprint` 都是**同名不同类**）、内部 `RunAll()` 无调用点。

**裁定：归类 Legacy（仅保留参考）**，理由是该子树展示了"如果自己从零搭一套技能/管线骨架会长什么样"，对读源码的人仍有对照价值，但已不是推荐入口。据此：不进 manifest、不打 web 标签、不进 README 学习路径、不做进一步改造。

**已知代价**：`Ability/` 下 8 个文件直接 `Console.WriteLine`（共 31 处），绕过 `ILogger` 宿主边界。这是保留参考状态的既有形态——`Console.WriteLine` 在 Unity 下可正常编译，故不影响包的可编译性；若日后决定迁入正式示例，应连同 `ILogger` 改造一并做。

## 7. Manifest 与 Catalog 治理

### 7.1 当前问题

当前注册表会扫描所有 `[Sample]` 类型，而 manifest 只覆盖正式入口的一部分。这样会造成：

- 未进入 manifest 的旧示例仍可能出现在目录。
- 菜单序号和 README 推荐路径容易漂移。
- UI 宿主无法知道哪些是 Stable，哪些只是 Draft。
- 未来新增复杂示例时缺少质量门禁。

### 7.2 推荐演进

分两步推进：

1. 保持自动扫描能力，但新增 manifest 校验工具，列出：
   - 带 `[Sample]` 但未进入 manifest 的类型。
   - manifest 指向不存在类型的条目。
   - id 重复、order 重复、缺少 tags/status/modules 的条目。
2. 让正式 catalog 默认只展示 manifest 中 status 为 Stable/Candidate 的条目，自动扫描仅用于开发期发现。

### 7.3 建议新增校验输出

```text
Sample Manifest Validation
- Stable entries: 16
- Attribute-only samples: 57
- Missing type entries: 0
- Duplicate ids: 0
- Web entries without deterministic tag: 0
```

该工具可以先作为控制台参数 `--validate-manifest`，也可以放进测试项目。

## 8. 示例编写标准

### 8.1 单个示例模板

每个 Stable 示例建议固定输出结构：

```text
Title
Description

Input / Setup
- 展示输入、配置、实体、标签、上下文

Run
- 展示实际调用的框架 API 或 scenario 步骤

Result
- 展示可验证结果、状态变化、事件、日志

What this sample proves
- 说明它证明了哪个包能力
- 说明下一步应该看哪个 sample
```

### 8.2 代码标准

- `OnRun()` 只负责教学流程和输出，不堆复杂业务逻辑。
- 复杂对象构造进入 fixture。
- 多步骤业务进入 scenario。
- 输出统一使用 `Section`、`KeyValue`、`Bullet`、`Divider`。
- 禁止 `Thread.Sleep`、真实时间等待、真实网络连接、Unity API。
- 随机数必须可注入 seed，或固定输入。
- 示例不要吞掉关键异常；失败要能被宿主显示。
- 同一个模块的 sample 命名保持动词和名词一致。

### 8.3 标签标准

建议 tags 分为几类：

| 类型 | 示例 | 用途 |
| --- | --- | --- |
| 分类 | `combat`、`flow`、`triggering` | UI 分组和搜索 |
| 能力 | `projectile`、`damage`、`state-machine` | 能力过滤 |
| 质量 | `package-api`、`web`、`deterministic` | 展示和自动验证 |
| 难度 | `beginner`、`intermediate`、`advanced` | 学习路径 |
| 形态 | `single-step`、`fixed-frame`、`scenario` | 宿主渲染方式 |

## 9. 扩展路线图

### Phase 0：治理底座

目标：让示例包先能被管理。

- 修复 README 中学习路径与 manifest 的关系说明。
- 给 manifest 增加 `status`、`level`、`modules`、`next` 字段。
- 增加 manifest 校验命令或测试。
- 列出所有 attribute-only sample，形成治理表。
- 明确 Legacy 不进入默认展示。

验收：默认目录只展示正式入口；未治理旧示例不会混入 Web 导出。

### Phase 1：修复 Foundation / Flow / Triggering 旧入口

目标：把最容易影响第一印象的乱码和说明型示例清掉。

- 重写 Foundation 三个乱码示例。
- 重写 `BasicTrigger`。
- 将 Flow 说明型示例改为真实节点运行。
- 保留现有 Onboarding 作为入口，并补充 next 关系。

验收：新人从 Onboarding 到 Flow/Triggering 不再遇到乱码或纯概念占位。

### Phase 2：完善 Combat Basics

目标：以现有第一批 combat samples 为主线继续补齐基础能力。

- 补 `combat/entity-registry`、`combat/entity-multikey-index`。
- 补 `combat/targeting-pipeline`、`combat/targeting-shapes`、`combat/targeting-nearest-topk`。
- 补 `combat/damage-basic`、`combat/damage-defense`、`combat/damage-apply-to-entity`。
- 补 `combat/collision-overlap`、`combat/area-dot`。
- 若 Motion 包可用，补 `combat/motion-trajectory`。

验收：用户能从实体数据一路理解到目标、伤害、碰撞、区域和投射物。

### Phase 3：建设 Combat Skills 主线

目标：支撑从零到一实现复杂技能。

- 新增 `combat/skill-cast-check`。
- 新增 `combat/skill-target-damage`。
- 新增 `combat/skill-projectile`。
- 新增 `combat/skill-area-dot`。
- 新增 `combat/skill-buff-modifier`。
- 新增 `combat/skill-flow-channel`。
- 新增 `combat/skill-state-gate`。

验收：`ProgressiveSkill` 不再只是框架概念演进，而能串到真实战斗包能力。

### Phase 4：World / Battle / Sync 教学层

目标：在不引入真实网络复杂度的前提下，解释真实项目如何宿主纯逻辑。

- 修复 World deep dive 问题。
- 新增 `battle/world-composition`。
- 新增 `battle/frame-input`。
- 新增 `battle/frame-simulation`。
- 新增 `battle/snapshot-dispatch`。
- 新增 `battle/state-hash`。
- 新增 `battle/transport-contract`。
- 新增 `battle/sync-adapter-fake`。

验收：示例可以解释完整工程中的 World、输入、帧推进、快照和传输边界，但不要求真实联网。

### Phase 5：Demo Scenarios 收束

目标：让完整 demo 成为前面能力的组合结果，而不是孤立大工程。

- 废弃或迁移旧 `TowerDefense`、`TimedTowerDefense`、`RPGBattle`。
- 新增 `demo/fireball-combat-loop`。
- 新增 `demo/area-control-loop`。
- 新增 `demo/projectile-replay-loop`。
- 从 Demo Moba / Shooter 抽取小切片，而不是直接复制完整项目。

验收：Demo 能清楚标注由哪些 sample 能力组合而来，并可回链到基础示例。

## 10. 验收标准

### 10.1 示例级验收

每个 Stable 示例必须满足：

- 有稳定 id、title、description、order、tags。
- 能通过 `--id` 单独运行。
- 输出包含 Setup / Run / Result 或等价结构。
- 至少展示一个真实框架包 API 或明确说明它只演示 sample 宿主基础设施。
- 结果可人工检查，关键状态用 `KeyValue` 输出。
- 不依赖真实时间、真实网络、UnityEngine、外部进程。
- 如果带 `web` 标签，必须可短时间执行并确定性输出。

### 10.2 分类级验收

每个分类必须满足：

- 有 README 或总文档中的目标说明。
- 有 beginner -> intermediate -> advanced 的顺序。
- 至少一个 sample 指向下一分类。
- 不混入明显 Legacy 示例。

### 10.3 包级验收

示例包整体必须满足：

- `sample-manifest.json` 无重复 id。
- manifest 指向类型全部存在。
- 默认 catalog 不展示 Deprecated / Legacy。
- README 推荐路径全部能通过 `--id` 运行。
- `--web` 只导出带 `web` 且满足确定性约束的示例。
- 文档中每个规划阶段都有可验证完成条件。

## 11. 建议优先改动清单

第一轮建议只做治理，不急着重命名大目录：

1. 增加 manifest metadata 字段和校验工具。
2. 默认隐藏未进入 manifest 的旧示例，或至少在 README 明确“manifest 是正式入口”。
3. 修复 Foundation / Triggering / Demo 中的乱码输出。
4. 把 Flow 的三个说明型示例拆成真实运行 API 示例。
5. 以 `CombatSampleCoveragePlan.md` 为子计划继续补 Combat Skills。
6. 为复杂技能新增 fixture / scenario 文件夹，避免继续把大逻辑塞进 sample 文件。
7. 最后再考虑目录重命名和大规模迁移，降低风险。

## 12. 与 Combat 专项规划的关系

`Document/CombatSampleCoveragePlan.md` 继续作为战斗能力覆盖的专项计划。本文件负责更高层的示例框架治理，包括：

- 示例包定位。
- 生命周期和质量门禁。
- 学习路径。
- manifest / catalog 治理。
- 旧示例迁移。
- 从基础能力到复杂 demo 的扩展方法。

Combat 专项规划中的 Phase B/C/D/E 可以并入本计划的 Phase 2/3/4。后续新增战斗示例时，优先遵守本文件的三层模型和验收标准。
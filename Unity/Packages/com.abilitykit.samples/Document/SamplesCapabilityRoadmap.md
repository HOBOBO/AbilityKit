# AbilityKit Samples Capability Roadmap

> 目标：基于当前 AbilityKit 已提供的包能力，规划一条从最简单接入到复杂战斗、World、同步、回放、完整 Demo 的示例建设路线。该文档不是替代 `SamplesLogicRefactorPlan.md`，而是作为后续扩充 `sample-manifest.json` 和重写旧示例的能力矩阵。

## 1. 总体判断

当前 `AbilityKit.Samples.Logic` 已经有两类资产：

- 正式入口：`sample-manifest.json` 中已有 16 个 Stable 示例，覆盖 Onboarding、HelloWorld、Flow、Triggering 配置组合、Combat 第一波能力。
- 历史材料：目录下还有 Foundation、Tags、Config、Pipeline、Flow、Triggering、HFSM、Modifiers、Continuous、World、Demo、ProgressiveSkill 等 57 个 attribute-only 示例，可作为迁移和重写素材。

当前最大缺口不是“没有示例”，而是：

1. 能力覆盖没有形成完整阶梯。
2. 旧示例没有按 manifest 生命周期治理。
3. 从单模块到复杂系统之间缺少中间层 scenario。
4. Web 展示已经具备时间轴壳，但缺少高价值样例的结构化帧快照。
5. 网络、同步、回放、完整 Demo 还没有进入纯逻辑样例主线。

后续规划应从“模块罗列”转为“能力成长路径”：每一层都回答一个真实问题，并自然引出下一层。

## 2. 能力分层

### 2.1 Layer 0：宿主与示例框架

目标：让使用者知道 AbilityKit 不强制绑定 Unity、Web 或服务器，而是一组可由宿主组合的纯逻辑能力。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| Catalog / manifest | 已有 `SampleCatalogProvider`、manifest 治理 | 需要展示 Stable/Candidate/Legacy 差异 | `onboarding/catalog-governance` |
| Executor / logger | 已有 `SampleExecutionService`、`BufferedSampleLogger` | 需要展示 UI host、Web host 如何接入 | `onboarding/ui-host` 已有，后续加强 |
| Environment / tick | 已有 `ISampleEnvironment`、`SampleRunHandle` | 需要一个最小持续驱动示例 | `foundation/environment-tick` |
| Web observer | 已有 Canvas timeline 壳 | 缺少帧快照契约 | `onboarding/web-timeline-snapshot` |

### 2.2 Layer 1：基础设施与数据词汇

目标：建立最小运行基础：日志、事件、对象池、注册表、标签、配置。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| 日志与结构化输出 | `Foundation/HelloWorld.cs` | 已稳定但偏基础 | `foundation/hello-world` 已有 |
| 事件派发 | `Foundation/EventSystem.cs` | 旧示例需确认是否接真实 Core API | `foundation/event-dispatcher` |
| 对象池 | `Foundation/ObjectPool.cs` | 旧示例需重写 | `foundation/object-pool` |
| 类型/资源注册 | `Foundation/MarkerRegistry.cs`、`Config/ConfigRegistryBasics.cs` | 需要统一到 Config 主线 | `config/type-registry` |
| GameplayTags | `Tags/*` | 未进入 manifest | `tags/basic-tags`、`tags/requirements`、`tags/stack` |
| 配置模型 | `Config/Sample*.cs` | 缺少“配置 -> 类型 -> 运行时”的正式样例 | `config/skill-table` |

### 2.3 Layer 2：执行编排

目标：解释一次技能或行为如何从“一个动作”变成“阶段化、可等待、可取消、可组合”的执行链。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| Pipeline 阶段执行 | `Pipeline/*`、`ProgressiveSkill_Phase4` | 需要稳定化并接入 manifest | `pipeline/basic-phases`、`pipeline/skill-cast-check` |
| Flow 跨帧流程 | `FlowBasics` 已稳定，`SequenceAndRace`、`TimedFlow` 待重写 | 缺少真实 Race/Timeout/Cancel 教学 | `flow/sequence-race`、`flow/skill-cast-timing` |
| Behavior / BTCore | `Triggering/BehaviorTreeExample`、`StateMachine/HFSMBehaviorTree` | 没有独立基础层 | `behavior/basic-tree` |
| ActionSchema / CodeGen | 相关项目存在，样例未覆盖 | 需要后置到配置驱动阶段 | `action-schema/basic-action` |

### 2.4 Layer 3：响应式状态与数值变化

目标：把执行链连接到事件、状态和数值，使能力从“能执行”变为“能响应战斗变化”。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| Triggering 基础事件 | `TriggerConfigCompositionAndTemplate` 已稳定，其他 Triggering 样例待重写 | 需要从最小事件到模板组合分层 | `triggering/basic-event-trigger`、`triggering/condition-blackboard` |
| Scheduler / RPN / ExecCtx | `Triggering/*` 已有旧材料 | 需要判断哪些是正式 API，哪些降级 Legacy | `triggering/scheduler`、`triggering/expression-condition` |
| HFSM | `StateMachine/*` | 没有进入 manifest，部分说明型 | `hfsm/basic-state`、`hfsm/hierarchy`、`hfsm/trigger-bridge` |
| Modifiers | `Modifiers/*` | 未进入 manifest，缺少和伤害/技能结合 | `modifiers/attribute-basic`、`modifiers/stacking-dot` |
| Continuous | `ContinuousBasics`、`ProgressiveSkill_Phase1` | 需要和环境 tick、modifier 结合 | `continuous/dot-lifecycle` |

### 2.5 Layer 4：World 与 Host

目标：展示 AbilityKit 在真实宿主中的组织方式：生命周期、服务容器、Host 边界、多客户端/多世界。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| World DI | `WorldDIOverview.cs` | 未治理进入 manifest | `world/di-basics` |
| World 生命周期 | `WorldLifecycle.cs` | 需验证稳定性 | `world/lifecycle` |
| Host 概念 | `WorldHostOverview.cs` | 需要和 `AbilityKit.Host` 真实 API 对齐 | `world/host-overview` |
| 服务组合 | `WorldServicesDeepDive.cs` | 曾标记可能有风险，需修复 | `world/services` |
| 多客户端/通信 | `HostClientManagement.cs`、`WorldCrossCommunication.cs` | 需要拆小 | `world/host-client`、`world/cross-message` |
| Snapshot / FrameSync / StateSync | 项目存在，samples 未覆盖 | 是从零到一主线的重要缺口 | `sync/frame-input`、`sync/state-snapshot`、`sync/replay-basic` |

### 2.6 Layer 5：Combat 基础能力

目标：让使用者理解战斗不是一个黑盒，而是由实体、目标、伤害、碰撞、运动、投射物、技能库组合出来。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| 实体索引 | `combat/entity-keyed-index` 已稳定 | 后续可补实体生命周期 | `combat/entity-lifecycle` |
| 技能库 | `combat/skill-library-index` 已稳定 | 需要配置驱动技能库 | `combat/skill-library-config` |
| 目标搜索 | `combat/targeting-index-provider` 已稳定，`TargetingBasics` 旧入口 | 需要从基础 targeting 迁入 combat 主线 | `combat/targeting-shape-filter` |
| 伤害管线 | `combat/damage-pipeline` 已稳定 | 需要 modifier、抗性、暴击 | `combat/damage-with-modifiers` |
| 碰撞/区域 | `combat/collision-raycast`、`combat/area-enter-stay-exit` 已稳定 | 需要持续区域 DOT | `combat/area-dot` |
| 投射物 | `combat/projectile-basic-hit`、`combat/projectile-hit-damage` 已稳定 | 需要运动/生命周期/多目标 | `combat/projectile-pierce-chain` |

### 2.7 Layer 6：Combat 技能组合

目标：从基础能力组合出完整技能：施法条件、消耗、前摇、目标、弹道、命中、伤害、持续效果、冷却、取消。

| 阶段 | 推荐示例 | 覆盖模块 |
| --- | --- | --- |
| 技能配置 | `skill/fireball-config` | Tags、Config、SkillLibrary |
| 施法检查 | `skill/cast-validation` | Pipeline、Tags、World service |
| 前摇与取消 | `skill/channel-cancel` | Flow、Environment、HFSM |
| 投射物命中 | `skill/projectile-hit` | Targeting、Projectile、Collision |
| 伤害结算 | `skill/hit-damage` | Damage、Dataflow、Modifiers |
| 命中触发 | `skill/on-hit-trigger` | Triggering、ExecCtx |
| DOT / Buff | `skill/burning-dot` | Continuous、Modifiers |
| 状态门控 | `skill/hfsm-gated-cast` | HFSM、Pipeline |
| 完整火球术 | `skill/fireball-complete` | 上述全部 |

### 2.8 Layer 7：同步、回放与完整场景

目标：让样例从“本地纯逻辑可运行”升级为“可验证、可回放、可接真实 Demo”。

| 能力 | 当前材料 | 缺口 | 建议示例 |
| --- | --- | --- | --- |
| 帧输入 | `World.FrameSync`、Demo 项目引用 | Samples 未覆盖 | `sync/input-frame` |
| 状态快照 | `World.Snapshot` | Samples 未覆盖 | `sync/world-snapshot` |
| 状态同步 | `World.StateSync` | Samples 未覆盖 | `sync/state-diff-apply` |
| 回放 | `Record`、`Record.MemoryPack` | Samples 未覆盖 | `replay/fireball-cast` |
| 网络传输抽象 | `Network.Runtime`、`Protocol.*` | Samples 不应直接真实联网，应用内存传输模拟 | `network/in-memory-transport` |
| Battle runtime | `Game.Battle.Runtime` | 只被 samples 引用，缺少正式样例 | `battle/runtime-loop` |
| 端到端 Demo | `Demo/ProgressiveSkill`、Moba/Shooter/ET demos | 需要从样例链组合成可读小场景 | `demo/minimal-battle-room` |

## 3. 从零到一主线规划

### 3.1 主线一：最小接入到可视化观察

| 顺序 | 示例 id | 状态 | 目标 |
| --- | --- | --- | --- |
| 0 | `onboarding/orientation` | 已稳定 | 知道 AbilityKit 是按需组合工具集 |
| 1 | `onboarding/host-boundary` | 已稳定 | 理解纯逻辑和宿主边界 |
| 2 | `foundation/hello-world` | 已稳定 | 运行第一个 sample |
| 3 | `foundation/environment-tick` | 待新增 | 理解时间由宿主推进 |
| 4 | `onboarding/web-timeline-snapshot` | 待新增 | 理解 Web 观察器和 timeline 数据 |

### 3.2 主线二：从数据词汇到技能执行

| 顺序 | 示例 id | 状态 | 目标 |
| --- | --- | --- | --- |
| 10 | `tags/basic-tags` | 迁移 Tags 旧样例 | 定义 GameplayTags |
| 11 | `tags/requirements` | 迁移 Tags 旧样例 | 使用 tag requirement 表达可释放条件 |
| 12 | `config/skill-table` | 待新增 | 用配置描述技能 |
| 13 | `pipeline/basic-phases` | 迁移 Pipeline 旧样例 | 将技能拆成阶段 |
| 14 | `flow/skill-cast-timing` | 重写 Flow 旧样例 | 处理前摇、等待、超时、取消 |

### 3.3 主线三：从事件响应到状态系统

| 顺序 | 示例 id | 状态 | 目标 |
| --- | --- | --- | --- |
| 20 | `triggering/basic-event-trigger` | 重写旧样例 | 事件触发动作 |
| 21 | `triggering/condition-blackboard` | 重写旧样例 | 条件和上下文 |
| 22 | `modifiers/attribute-basic` | 迁移旧样例 | 修改角色属性 |
| 23 | `continuous/dot-lifecycle` | 迁移旧样例 | DOT 生命周期 |
| 24 | `hfsm/basic-state` | 迁移旧样例 | 管理角色 Idle / Casting / Dead |
| 25 | `hfsm/trigger-bridge` | 迁移旧样例 | 事件驱动状态变化 |

### 3.4 主线四：从战斗能力到完整技能

| 顺序 | 示例 id | 状态 | 目标 |
| --- | --- | --- | --- |
| 30 | `combat/entity-keyed-index` | 已稳定 | 建立战斗实体索引 |
| 31 | `combat/skill-library-index` | 已稳定 | 查找技能定义 |
| 32 | `combat/targeting-index-provider` | 已稳定 | 从实体索引中找目标 |
| 33 | `combat/damage-pipeline` | 已稳定 | 结算伤害 |
| 34 | `combat/collision-raycast` | 已稳定 | 查询碰撞 |
| 35 | `combat/area-enter-stay-exit` | 已稳定 | 区域进入/停留/离开 |
| 36 | `combat/projectile-basic-hit` | 已稳定 | 投射物命中 |
| 37 | `combat/projectile-hit-damage` | 已稳定 | 命中后伤害闭环 |
| 38 | `skill/fireball-complete` | 待新增 | 完整火球术组合 |

### 3.5 主线五：从本地逻辑到 World / Sync / Replay

| 顺序 | 示例 id | 状态 | 目标 |
| --- | --- | --- | --- |
| 40 | `world/di-basics` | 迁移旧样例 | 服务注册和解析 |
| 41 | `world/lifecycle` | 迁移旧样例 | World 创建、启动、Tick、销毁 |
| 42 | `world/host-client` | 迁移旧样例 | Host 和客户端边界 |
| 43 | `battle/runtime-loop` | 待新增 | Battle runtime fixed update |
| 44 | `sync/input-frame` | 待新增 | 帧输入建模 |
| 45 | `sync/world-snapshot` | 待新增 | 状态快照 |
| 46 | `sync/state-diff-apply` | 待新增 | 状态同步 |
| 47 | `replay/fireball-cast` | 待新增 | 输入录制和回放验证 |
| 48 | `demo/minimal-battle-room` | 待新增 | 小型端到端战斗房间 |

## 4. 覆盖缺口清单

### 4.1 已有但未治理进入 manifest

优先迁移这些已有 `[Sample]`：

1. `Tags/*`：作为数据词汇主线入口。
2. `Pipeline/PipelineBasics.cs`、`Pipeline/PipelinePhaseExecutorExample.cs`：作为执行编排入口。
3. `Flow/SequenceAndRace.cs`、`Flow/TimedFlow.cs`：重写后进入跨帧主线。
4. `Modifiers/*`、`Continuous/ContinuousBasics.cs`：作为数值变化和 DOT/Buff 主线。
5. `StateMachine/*`：作为 HFSM 主线。
6. `World/*`：修复并拆分后进入 World 主线。
7. `Demo/ProgressiveSkill/*`：不要直接全部进入 Stable，应作为“完整技能系统演进”的候选链条重写输出。

### 4.2 当前缺少的新示例

必须新增的关键断点：

1. `foundation/environment-tick`：最小持续驱动。
2. `onboarding/web-timeline-snapshot`：Web 观察器快照协议。
3. `config/skill-table`：配置到运行时对象。
4. `skill/fireball-complete`：完整技能组合。
5. `battle/runtime-loop`：宿主 fixed update。
6. `sync/input-frame`：帧输入。
7. `sync/world-snapshot`：状态快照。
8. `sync/state-diff-apply`：状态同步。
9. `replay/fireball-cast`：回放验证。
10. `demo/minimal-battle-room`：最小完整房间。

### 4.3 应降级或隔离的旧示例

这些不应直接进入 Stable：

- 只打印概念、不调用真实 API 的 Triggering / Flow / Demo 旧样例。
- 输出乱码或过度说明型的 Foundation / Demo 样例。
- 无法短流程、确定性、纯内存运行的 World 深挖样例。
- 依赖真实网络、UnityEngine、外部进程或长时间循环的样例。

## 5. 推荐推进顺序

### Phase A：补齐学习断点

1. 新增 `foundation/environment-tick`。
2. 新增 `onboarding/web-timeline-snapshot`。
3. 迁移 `tags/basic-tags`、`tags/requirements`。
4. 新增 `config/skill-table`。

目标：让新人从“能运行”过渡到“能表达技能数据”。

### Phase B：重写执行和响应主线

1. 稳定 `pipeline/basic-phases`。
2. 重写 `flow/sequence-race` 和 `flow/skill-cast-timing`。
3. 重写 `triggering/basic-event-trigger` 和 `triggering/condition-blackboard`。
4. 迁移 `modifiers/attribute-basic`、`continuous/dot-lifecycle`。
5. 迁移 `hfsm/basic-state`。

目标：形成技能释放前半段：检查、等待、取消、事件、状态、数值。

### Phase C：组合完整技能

1. 保持已有 8 个 combat Stable 示例。
2. 新增 `skill/fireball-complete`。
3. 为 fireball 示例输出结构化 timeline snapshot。
4. 让 Web 观察器能画出施法者、目标、投射物、命中、DOT tick。

目标：让 web 页面第一次真正体现框架价值。

### Phase D：进入 World / Sync / Replay

1. 迁移 `world/di-basics`、`world/lifecycle`、`world/host-client`。
2. 新增 `battle/runtime-loop`。
3. 新增 `sync/input-frame`、`sync/world-snapshot`、`sync/state-diff-apply`。
4. 新增 `replay/fireball-cast`。

目标：从本地技能逻辑进入可验证、可同步、可回放的战斗运行时。

### Phase E：最小完整 Demo

1. 新增 `demo/minimal-battle-room`。
2. 从前面 Stable 示例复用 fixture / scenario。
3. 接入 Canvas timeline snapshot，展示多人输入、技能释放、命中、状态同步、回放校验结果。

目标：形成从零到一闭环：新人能从 HelloWorld 一路走到完整小战斗。

## 6. Manifest 扩容规则

新增正式示例时必须满足：

- 有稳定 id，按本文主线命名。
- `status` 至少为 `Candidate`，推荐入口必须为 `Stable`。
- 有 `level`、`modules`、`tags`、`next`。
- Web 示例必须 `deterministic`、短流程、纯内存。
- 跨帧示例必须由 `ISampleEnvironment`、fixed step 或明确 scenario driver 推进。
- 多模块示例必须拆出 fixture / scenario，sample 只负责教学输出。

建议 order 区间：

| 区间 | 主线 |
| --- | --- |
| 0-99 | Onboarding / Host |
| 100-199 | Foundation |
| 200-299 | Tags / Config |
| 300-399 | Pipeline / Flow |
| 400-599 | Triggering / Modifiers / Continuous / HFSM |
| 600-699 | World / Host |
| 700-849 | Combat Basics |
| 850-949 | Skill Composition |
| 950-1099 | Sync / Replay |
| 1100+ | Demo Scenarios |

## 7. Web 观察器后续演进

当前 Web 已支持基于日志的通用 timeline。下一步应支持更有语义的帧快照：

```text
Sample timeline event
  -> entities: position / hp / state / tags
  -> projectiles: position / velocity / owner / target
  -> areas: center / radius / active time
  -> events: cast / hit / damage / modifier-added / modifier-tick
  -> assertions: expected hp / expected state / replay hash
```

建议不要一开始引入 Phaser/Pixi。先让 `skill/fireball-complete` 输出稳定快照，验证 Canvas 观察器足够表达能力后，再决定是否替换渲染后端。

## 8. 验收标准

这条路线完成后，应达到：

1. `sample-manifest.json` 至少覆盖 45-55 个正式示例。
2. 每个主线阶段至少有 2 个 Stable 示例。
3. 至少 1 条完整技能链从数据、检查、释放、命中、伤害、DOT、状态、World、同步、回放贯通。
4. Web 页面能可视化至少一个复杂技能场景，而不只是日志。
5. 旧示例全部进入 Stable / Candidate / Legacy / Deprecated 之一。
6. 新人可以只按 `next` 字段顺序学习，不需要先理解整个仓库。

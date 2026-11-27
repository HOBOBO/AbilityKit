# MOBA 技能开发的 Battle Scenario 闭环验收流程

> 状态：开发流程基线。用于解决“Headless/配置测试通过，但 Unity 单机运行仍有问题”的协作和验收脱节。
>
> 适用范围：烬及后续七名拥有完整模型、特效和动作资源的英雄：Jinx、Veigar、Gragas、Karthus、Lux、Jhin、Annie。
>
> 权威配置、发布顺序、HFSM/Pipeline/Trigger 边界和生成物规则以 [`moba-skill-authoring`](../../../../.claude/skills/moba-skill-authoring/SKILL.md) 与 [`27-MobaHeroSkillIntegrationBaseline.md`](27-MobaHeroSkillIntegrationBaseline.md) 为准。

## 1. 结论

应把 Battle Scenario 提升为“技能玩法闭环”的主验收层，但不能让它取代 Unity 单机运行和录像复核。四类证据各自负责不同问题：

| 层级 | 负责回答的问题 | 通过证据 | 不能单独证明的内容 |
|---|---|---|---|
| P0/P1 配置与发布 | 权威源、ID、Flow、Trigger、资源是否能被运行时加载 | Luban/Pipeline/Trigger/StateMachine 发布检查和版本一致性 | 玩家输入、屏幕坐标、模型动作、VFX |
| P2 Battle Scenario | 在确定的战斗环境中，技能是否产生正确的玩法结果 | 场景驱动、trace/state/context/relationship 断言、确定性指纹 | Unity HUD 是否真的收到 Pointer 事件，表现挂点和清理是否正确 |
| P3 Unity 单机输入与表现 | 玩家操作是否能抵达同一技能闭环，指示器、动作、特效和清理是否正确 | Unity 场景运行、HUD 输入链、Presentation Cue、模型/VFX 观察、截图或录像 | 录像画面本身不能证明伤害、Buff 或目标规则正确 |
| P4 回放与批次复核 | 同一输入能否重放，多个技能是否能在一次审阅中比较 | 输入记录、快照/trace 回放、`MobaReplayValidator` 报告、批次录像索引 | 回放不能替代原场景的权威断言 |

因此，“Battle Scenario + 结果断言 + Unity 单机录像”比把多个分散 Headless 测试结果直接当作完成更合理。推荐的完成定义是：**同一场景先通过玩法断言，再由 Unity 使用同一输入契约运行，最后以回放和录像检查表现差异。**

## 2. 当前仓库已经具备什么

### 2.1 Battle Scenario 侧

`MobaBattleScenarioWorldRunner` 已经执行以下链路：

```text
TestScenario
  -> TestScenarioValidator
  -> ConsoleBattleBootstrapper
  -> 环境 profile / 障碍物
  -> spawn_actor 与 loadout
  -> timeline 或 virtual network 输入
  -> LiveSimSetupActionExecutor / LiveSimTimelineRunner
  -> MobaTraceRegistry
  -> AcceptanceVerifier.VerifyWithObservations
  -> BattleScenarioRunResult + determinism fingerprint
```

可直接使用的场景能力包括：

- `SpawnActorBlock`：英雄、属性模板、队伍、玩家、初始位置和朝向；
- `SetEnvironmentBlock` / `PlaceObstacleBlock`：战斗环境和碰撞障碍；
- `TimelineStepBlock`：在确定时间施放技能、指定槽位、目标、位置、方向和持续时间；
- `ExecutionSettingsBlock`：tick rate、最大时长、settle 窗口和结束条件；
- `MobaBattleScenarioAssertions`：`MustContain`、`MustNotContain`、`State`、`Context`、`Relationships`、`Prediction`；
- `RunDetailed`：trace 节点、网络 trace、预测结果和确定性指纹；
- DSL 和 `.battlescenario` 批量回归：可放入 `src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows`，由 `BattleScenarioRegressionTests` 扫描。

这已经足以验证“真实英雄 loadout 被装配、真实技能被提交、Flow/Trigger/Projectile/Area 结果出现、状态和关系满足预期”。

### 2.2 Unity 输入侧

真实单机输入链是：

```text
SkillButtonView
  -> BattleHudInputEventBridge
  -> BattleHudInputEventDispatcher
  -> BattleContext / BattleInputRuntime
  -> BattleInputFeature
  -> SkillAimRelease / SkillClick PlayerInputCommand
  -> SkillCastCoordinator
```

`BattleHudInputEventDispatcher` 已覆盖 Click、AimStart、AimUpdate、AimCancel、AimEnd，并通过 `BattleAimProjectionService` 把输入偏移转换为世界位置、方向和释放快照。已有 HUD 测试证明按钮事件层和部分指示器参数可消费，但烬 E/W 的生产验收仍有直接调用 `TryCastBySlot` 的路径，因此没有证明完整的 `SkillButtonView -> BattleInputRuntime -> 权威技能结果` 闭环。

### 2.3 追踪、摘要和回放侧

- `MobaAcceptanceTraceExporter` 可输出 JSON/JSONL、详细 trace 文本和批次摘要；
- `MobaAcceptanceSummary` 已包含 `skillCastTraceFound`、`effectExecutionTraceFound`、`projectileLaunched`、`areaSpawned`、`buffApplied`、状态/关系覆盖率等结果；
- `MobaReplayValidator` 可按帧比较 Actor 的位置、旋转、HP、速度、队伍和实体集合；
- `FrameReplayDriver`/`BattleReplaySessionOwner` 已有输入重放和 session 生命周期测试。

这些能力可以组成验收证据包，但目前还没有统一的“场景 ID—输入记录—Unity 录像—回放报告”索引规范，也没有把真实 HUD 输入作为 Battle Scenario 结果的必需入口。

### 2.4 真实运行问题包的收益与现有支持度

将玩家在 Unity 本地模式中发现的问题保存成可审查的问题包，主要收益是把“我看见技能没生效”拆成可定位的事实：输入有没有到 HUD、释放时冻结了什么瞄准数据、模拟有没有接受施法、投射物是否生成/命中、表现层是否消费了对应事件。这样可以减少反复口述操作步骤、猜测运行配置和重启 Unity 的时间；修复后也能把同一份输入转成稳定回归场景，避免只修到某一台机器上的偶发现象。

当前已有足够的观测点支持分阶段落地，但还没有一键式 Unity 实机问题包：

| 证据 | 已有能力 | 缺口 |
|---|---|---|
| 画面 | 可用 Unity Game 视图录屏或系统录屏；场景工作流已定义录像和时间段索引 | 没有仓库内置的录像控制器，也没有录像时间码到战斗帧的索引 |
| HUD 手势 | `BattleHudInputEventDispatcher` 已集中分发 Move、AimStart、AimUpdate、AimCancel、AimEnd；`BattleInputFeature` 把最终 HUD 输入变为命令 | 没有通用的只读 observer 将原始屏幕坐标、投影后的世界方向、释放快照与帧号关联保存 |
| 输入回放 | Unity Battle 有 `IFrameRecordWriter` 和 `FrameReplayDriver`；Console 有独立 `.akrec` 录制格式 | 两种录制不是同一协议；当前不能假设它们保存了 Unity HUD 的完整手势轨迹、指示器状态和本地资源版本 |
| 权威结果 | Battle Scenario trace、世界快照、Projectile event snapshot、状态断言可覆盖施法、投射物、命中和伤害 | 尚无统一 run manifest 把它们与一次人工运行绑定 |
| 表现结果 | Presentation Cue 和现有 VFX 消费测试可检查表现消费；录像可供人工观察 | 没有统一的表现事件日志/句柄生命周期与录屏帧对照工具 |

因此首期不应承诺“给一段 MP4 就能自动复现”。合理承诺是：录像解释视觉现象；随包的输入与权威事件把问题定位到责任边界；只有输入命令和场景上下文完整时才尝试确定性重放。录像本身是非权威证据，不作为伤害、命中或技能成功的判定来源。

### 2.5 低侵入架构与边界

问题采集应作为开发/验收侧的可选旁路组件，依赖方向为：

```text
Unity 输入/UI ──┬──> 现有 Battle HUD / InputRuntime ──> 权威战斗逻辑
               └──> 只读 Capture Observer ──> input.jsonl
世界快照/事件 ─────> 只读 Capture Observer ──> trace/snapshots/cues.jsonl
屏幕画面 ──────────> 外部录屏或可选 Unity Capture ──> recording + 时间索引
上述产物 ──────────> run manifest / 问题包
```

采集器可以订阅或包装现有输入 sink，并消费已有快照/事件；它不能替换输入、延迟提交、改变施法参数、吞掉异常、调用技能 API、修改 HP/状态或驱动表现。记录失败、磁盘变慢、缓冲区满、停止采集时都必须 fail-open：丢弃/截断诊断记录并报告采集不完整，绝不能反压或改变战斗 Tick。采集默认关闭，仅本地开发/验收构建可用，输出到 `local/Logs/` 或明确指定的 `artifacts/`，不能进入生产战斗配置或存档。

建议按侵入程度分三层推进：

1. **L0 手工证据包（零战斗代码改动）**：用户用系统/Unity 录屏，创建 manifest，附 Unity Player.log、复现步骤、提交号和配置版本。先验证团队是否能靠统一命名和步骤更快定位问题；缺点是没有可执行的精确输入重放。
2. **L1 只读运行采集（薄适配层）**：在 HUD dispatcher 边界记录原始操作和投影后的 Aim 值，在命令提交边界记录帧号/slot/command result；订阅现有 Projectile、Presentation Cue 和快照数据，落 JSONL 与 manifest。以 decorator/observer 方式接入，不改命令语义和权威系统。录屏可继续外置，通过一次 Start/Stop 和时间戳索引关联。
3. **L2 受控重放（单独评审）**：把规范化的 HUD 手势或最终命令输入交给现有 replay/input queue 重放，再比较 trace、快照和表现事件。重放要明确选择“重放原始手势”还是“重放最终命令”：前者覆盖 HUD 投影，后者定位战斗逻辑；不可把二者混为一个结果。只在确认现有 FrameRecord 对输入类型、顺序、目标、帧号和上下文有足够表达能力后扩展格式。

低侵入的验收条件：关闭采集时无行为差异；打开采集时事件只读且顺序不变；录制器异常不影响战斗；同一 run 的事件可按单调序号和帧号对齐；采集包清楚标记缺失数据和 schema 版本。新增代码应留在 view/runtime 的开发工具或 diagnostics 边界，核心技能、HFSM、Trigger、Projectile 和伤害链不得依赖采集组件。

### 2.6 七英雄技能表现用例

用例位于 [`Flows/SkillPresentation`](../../../../src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation)。七个英雄均有独立的 Q/W/E/R 和单发普攻场景。独立场景只验收一个主题，但保留真实触发前置，例如烬 R 的架枪与四次射击、古拉加斯 W 的强化普攻和卡尔萨斯 E 的开关。预览始终以正式 HeroId 解析技能栏，不绕过槽位替换。

| 英雄 | 独立普攻 / 被动 | 多单位场景 |
|---|---|---|
| 烬 | `jhin-basic-attack` / `jhin-passive-magazine`（第四发与换弹） | `jhin-q-four-hits` 四个对手；`jhin-q-kill-chain` 降低部分目标血量 |
| 金克丝 | `jinx-basic-attack`、`jinx-basic-attack-fishbones`；`jinx-q-toggle-only` 单看切枪。正式配置尚无击杀加速，武器路由不算击杀被动验收 | 火箭普攻有主、次目标；`jinx-e` 三点落位 |
| 安妮 | `annie-basic-attack` / `annie-passive-pyromania`（叠层后以 W 眩晕） | `annie-w`、`annie-r-two-targets`；`annie-e-ally` 和 `annie-e-tibbers` 区分友军与熊 |
| 维迦 | `veigar-basic-attack` / `veigar-passive-growth`（Q 命中叠层） | `veigar-q-two-targets` 用第三名敌人作为对照 |
| 古拉加斯 | `gragas-basic-attack` / `gragas-passive-happy-hour`（低血量施法回血） | `gragas-w` 溅射；`gragas-r-two-targets` 双目标 |
| 卡尔萨斯 | `karthus-basic-attack` / `karthus-passive-respawn`（陪练击杀、死后施法、复活） | `karthus-q` / `karthus-q-two-targets` 对照孤立条件；`karthus-r-multi-target` 带友军对照 |
| 拉克丝 | `lux-basic-attack` / `lux-passive-illumination`（Q 标记、普攻消耗） | `lux-q-two-targets`；`lux-w-two-allies` 带范围外友军和同线敌人对照 |

打开 `Window > AbilityKit > 战斗测试用例编辑器`，从左侧用例库选择场景。它会索引 MOBA 仓库用例及项目的 `Assets/BattleScenarios`，例如输入 `金克丝 W skill-presentation` 就能定位 `jinx-w`。也可用「浏览用例…」加载其他目录。选中后不需要重新选文件：

1. **纯预览**：点击「技能预览」，在 Game 视图观看。它使用正式地图、模型、动作和特效，不装配战斗 HUD、玩家输入采集和战斗调试覆盖层；时间线及结尾等待走完后自动暂停。停止 Play Mode 后保留选中用例，再点「技能预览」会新建战斗重播。
2. **交互播放**：从「播放」下拉菜单选「交互播放」，保留 HUD 与手动输入，播完可继续操作。两种播放都通过正常本地输入绑定按逻辑帧提交脚本输入。原 MOBA 菜单仍可作为文件选择入口。
3. **玩法断言**：点「运行断言」查看判定与 Trace，或用「批量运行」选择 `SkillPresentation` 目录。Unity 播放仍只核对已配置的 `SkillCast` 次数，不替代完整断言。

「配置校验」检查当前编辑内容；技能预览和单例断言都执行选中的已保存用例及其引用场景。编辑未保存时先保存；如果文件在外部改过，请重新加载用例后再执行。

画面验收单独记录模型、起手/释放/后摇动作、出手/飞行/命中/持续特效、挂点、方向、透明度和退场。自动停在完成帧不代表特效此时还存活；短暂特效应在播放期间暂停逐帧观察。烬 W 的发射在施法提交即出现，拉克丝 R 在提交后约 1 秒出束，不应只截施法开始帧。

烬 W/E、金克丝 R、拉克丝 R 可复用 EditMode Test Runner 的 `SkillPresentationCapture` 类别批量取证（Unity 批处理时不要加 `-nographics`）。测试启动同一正式本地预览，以实际 `SkillCast` 为零帧，按阶段保存相机 PNG、实际逻辑帧、Animator Clip 和活动 VFX 位置到 `local/Logs/moba-presentation-scenarios/`。截图不含 HUD Overlay，VFX 条目也不代表像素可见；测试通过后仍需查看画面。添加同类场景时先选不被地图障碍遮挡的站位，再从 Headless trace 选取命中前、命中后和清理后的采样帧。

预览使用正式 Demo 的本地地图。一个角色负责本地镜头和手动输入归属，可布置多名友军和敌军陪练。时间线开始前可用 `set_attr` 设置已布置角色的属性（例如低血量、无蓝）；时间线可对已布置角色提交技能输入、移动或等待。纯预览仍关闭手动输入；不支持的自定义环境、障碍或网络命令会在预检时报错。

补充复现用例沿用同一目录和播放入口：

| 文件 | 看点与断言边界 |
|---|---|
| `jhin-q-four-hits.battlescenario` | 只锁定首目标，不额外传入瞄准落点。Headless 断言一次施法、四次碰撞和伤害；Unity 消费用例分别断言同弹体四次命中特效、重复快照去重、退出释放飞行特效、命中特效按独立寿命回收。附带瞄准落点会触发当前发射链的距离裁剪，不用该输入替代四跳表现基线。 |
| `jhin-w-marked-hero.battlescenario` / `jhin-w.battlescenario` | 前者用 Q 实际命中授标，再以 W 命中同一英雄，断言伤害和一次禁锢。后者直接 W，断言有伤害而无禁锢。已标记小兵、野怪仍受穿线伤害但不禁锢，由 `SevenHeroAlignmentRuntimeTests` 构造正式分类验证；当前纯预览用例不模拟该单位分类。 |
| `annie-r-tibbers-timeout.battlescenario` | 一次召唤，无后续输入，播放保留到自然退场窗口之后。场景断言出生；被击杀与到期的实际清理分别使用 `Tibbers_DeathShouldReleaseSummonAndBehaviorRuntime` 和 `R_ShouldApplyConfiguredImpactDamageAndSpawnConfiguredTibbersSummon`。未配置专用退场特效时不播占位。 |
| `karthus-w-rank-one-edge.battlescenario` | 墙中心两侧留出可对照的距离；一级只作用一个目标，墙按期退场。五级边界用 `Wall_rank_changes_the_authoritative_edge` 从正式技能槽设置等级并走真实 Cast 验证。预览文件不覆盖五级墙体的画面尺寸。 |
| `karthus-w.battlescenario` | 敌我对照角色从墙外起步。Headless 断言墙生成与到期；Unity 用例 `KarthusW_FromScenario_AllowsCrossingAndKeepsDebuffAfterWallExpires` 加入移动输入，验证双方能穿墙、只有敌方受减速和减魔抗、敌方折返不重复施加、墙退场后既有减益独立到期。单独打开场景时只播放 W，不会自动驱动对照角色走位。 |

## 3. 推荐的单一场景工作流

每个技能或技能批次都使用同一个 `caseId`，按以下顺序推进：

```text
需求契约
  -> 权威源检查与发布
  -> Battle Scenario 建立战斗环境
  -> 场景驱动技能输入
  -> 玩法结果断言
  -> Unity 单机复现同一输入
  -> 自动采集 trace / snapshot / input
  -> 按需录制视频
  -> Replay 重放并比较
  -> 修复后重跑同一 caseId
```

### 第一步：先写技能闭环契约

技能文档必须先填完下列字段，不能只写“能命中”或“有特效”：

| 契约字段 | 必须明确的内容 |
|---|---|
| 输入 | Click、Hold、Drag、Release、Cancel、Toggle 或多次独立输入；对应槽位和 `SkillId` |
| 瞄准 | 单位目标、地面落点、方向、扇区、全图目标或自身；输入偏移如何投影到世界 |
| Commit 快照 | Release/Commit 时冻结的坐标、方向、目标、施法者位置、等级和资源 |
| 玩法结果 | SkillCast、Flow phase、Trigger/Effect、Projectile/Area、Damage/Buff、状态和清理 |
| 取消/失败 | 无效目标、越界、移动打断、死亡、冷却、资源不足时的结果和回滚 |
| 表现 | 指示器、模型动作、挂点、VFX 生命周期、打断和销毁；表现不拥有玩法结果 |
| 证据 | 最小 Battle Scenario、Unity 输入场景、断言、录像步骤和回放比较范围 |

### 第二步：建立最小 Battle Scenario

场景至少包括：

1. 一个带真实英雄 loadout 的 `caster`；
2. 一个或多个有明确队伍和属性模板的目标；
3. 会影响机制的墙体、障碍、视野或环境 profile；
4. 技能提交前的准备动作（站位、血量、Buff、冷却和资源）；
5. 技能施法、等待命中/延迟、后续输入和 settle 窗口；
6. 玩法结果断言和禁止出现的错误结果。

场景不要只断言 `SkillCast`。至少要根据技能形态覆盖以下链路：

```text
SkillCast
  -> Flow/Phase
  -> Trigger/Effect
  -> ProjectileLaunch 或 AreaSpawn
  -> Hit/Damage/Buff/State
  -> 生命周期结束与清理
```

对于“没有命中”的场景，应同时写 `MustNotContain` 或状态不变断言，避免只因为技能成功提交就误判通过。

### 第三步：断言要覆盖四种结果

建议每个场景至少有一组 trace、state、context、relationship 断言：

- **Trace**：技能、Flow、Projectile、Area、Damage、Buff、终结/取消节点的存在和次数；
- **State**：目标 HP、施法者资源、位置、Buff 数量、模式参数或状态结果；
- **Context**：释放快照中的 `AimPosition`、`AimDirection`、source/target、阶段、结束原因；
- **Relationship**：父技能与子 Projectile/Area/Effect 的归属关系，保证不是无关节点碰巧出现。

断言的最小模板：

```csharp
new MobaBattleScenarioAssertions
{
    MustContain =
    {
        new MobaTraceAssertion { Kind = "SkillCast", ConfigId = skillId },
        new MobaTraceAssertion { Kind = "DamageApply", ConfigId = effectId },
    },
    MustNotContain =
    {
        new MobaTraceAssertion { Kind = "DamageApply", ConfigId = forbiddenEffectId },
    },
    State =
    {
        new MobaStateAssertion { Alias = "target", Property = "hp", Comparator = "lt", ExpectedValue = "1000" },
    },
    Context =
    {
        new MobaContextAssertion { Alias = "caster", Kind = "SkillCast", Property = "commit_snapshot", Comparator = "exists" },
    },
    Relationships =
    {
        new MobaRelationshipAssertion
        {
            ParentKind = "SkillCast", ParentConfigId = skillId,
            ChildKind = "ProjectileLaunch", ChildConfigId = projectileId,
        },
    },
}
```

上面的字段名需以当前 runtime trace dictionary 为准；如果某种结果尚未进入 trace，不应在测试里伪造字段，而应先补充权威观测点或降低断言到已有的 state/context。

### 第四步：Unity 运行必须复用同一个输入契约

Unity 场景不能重新手工“看着差不多”地操作。每次验收都要记录：

- `caseId`、英雄、槽位、SkillId、版本和资源包；
- `PointerDown/AimStart`、每次 Drag/AimUpdate、Release/AimEnd、Cancel；
- 输入时刻、帧号、屏幕坐标、世界坐标、世界方向；
- Commit 前后技能按钮状态、指示器状态和提交命令；
- 最终 trace、状态快照、Presentation Cue、模型动作和 VFX 句柄。

第一阶段可以用测试驱动的 HUD bridge 调用来保证事件顺序；第二阶段再接入真实 Unity Test Runner 或本地单机录制。无论哪一种，都必须经过 `BattleHudInputEventDispatcher` 和 `BattleInputRuntime`，不能用 `SkillCastCoordinator.TryCastBySlot` 代替输入闭环。

### 第五步：按需录像，但自动保留机器证据

录像是人工发现视觉和交互问题的工具，不是唯一通过条件。每个批次应至少保留：

```text
<artifactRoot>/moba/<hero>/<caseId>/<runId>/
  scenario.json                 # 场景和断言
  input.jsonl                   # Pointer/HUD/PlayerInputCommand 输入
  acceptance-summary.json       # passed、覆盖率、结果节点
  trace.jsonl                   # 权威 trace
  snapshots.jsonl               # 可回放的世界快照（若场景开启）
  replay-report.json            # MobaReplayValidator 报告
  presentation-cues.jsonl       # 模型/VFX/音效 cue
  recording.mp4                 # 按需生成
  review-index.json              # 技能批次和视频时间段索引
```

录像命名建议使用 `caseId/runId`，不要使用“最终版”“最新视频”等不可追踪名称。多个技能可以放在同一批次录像中，但每段必须有起止帧和技能标签，便于一次回放审阅多个技能。

### 第六步：回放验证分两类

1. **玩法回放**：输入记录重新驱动同一场景，比较 trace、快照和 determinism fingerprint；失败时优先定位输入、发布漂移或确定性问题。
2. **表现回放**：加载同一输入和快照，检查指示器、动作、特效、挂点和清理。表现回放不能反向修改权威状态，也不能用画面存在替代 Damage/Buff 断言。

`MobaReplayValidator` 的位置/旋转/HP/速度容差应按场景类型配置。技能逻辑验收使用严格或接近严格的状态比较；模型动画和 VFX 的视觉检查允许独立的表现容差，不应把表现浮点误差混入玩法失败。

## 4. 烬第一批闭环

### 4.1 烬 E：地面落点到 Area

最小场景需要：

- 烬在固定位置，E 可用，目标位于落点附近和落点外各一个；
- HUD 通过 Press/Drag/Release 提交地面位置；
- 至少等待“投放完成—延迟武装—Area 触发—清理”的完整时间；
- 断言 Release 快照的世界落点、Area 中心、延迟前无伤害、延迟后伤害/减速、Area 结束；
- 取消、越界、施法者死亡和重复释放各有一个负向场景；
- Unity 录像观察指示器中心、落点 ghost、模型动作、VFX 挂点和延迟期间的状态。

通过条件：Battle Scenario 断言 Area 和效果闭环；Unity 证据证明 HUD 输入的落点与权威 Area 中心一致；录像没有残留指示器或提前爆炸。指示器的施法距离和 Area 作用半径必须分别取各自权威字段，不能只比较圆形半径。

### 4.2 烬 W：拖拽方向到 Projectile

最小场景需要：

- 施法者、首个有效目标、后方目标和遮挡/无效目标；
- Drag 方向超过最大射程、Release 时改变方向、释放后移动三种输入；
- 断言 Release 冻结方向和射程裁剪、Projectile 宽度、首个有效命中、后方目标不命中、命中控制/伤害和生命周期结束；
- Unity 录像观察方向线、线宽、拖拽更新、释放后指示器关闭、Projectile/VFX 和命中反馈。

通过条件：目标筛选和 Projectile 碰撞结果来自权威查询/碰撞链；HUD 仅消费预览结果。不能用“方向线看起来指向目标”代替首个有效目标和后方目标断言。

### 4.3 烬 R 及后续

R 的架枪是跨多次输入的长期模式：架枪入口可以是短 Pipeline，但架枪阶段、射击次数、终结射击、移动打断和退出必须由 HFSM/Modifier 管理；每一发 Release 都是独立短 Pipeline。场景应把多次射击作为一个 case 的连续输入，并在录像索引中标记每一发。不能用一个长 `Repeat` Flow 伪装成整段架枪。

## 5. 从烬到金克丝的推进顺序

在烬 E/W 输入闭环稳定后，按以下顺序推进金克丝；每一步都先做一个 Battle Scenario，再补 Unity 输入和录像：

1. **Jinx Q**：武器模式切换和普通攻击参数。先验证 HFSM/Modifier、攻击槽位和模式恢复，再验证表现；不把 Q 误做成地面指示器。
2. **Jinx W**：方向拖拽 Projectile。复用烬 W 的输入闭环，新增蓄力/释放快照、射程、首个有效目标和穿透规则的断言。
3. **Jinx E**：多枚陷阱落点。先固定多点布置的输入协议、每枚 Area 的归属/去重/生命周期，再决定指示器是否需要多 ghost；不能只用一个 TargetCircle 表示最终玩法。
4. **Jinx R**：超远距离方向 Projectile。验证全图范围、方向和边界、目标筛选、终结伤害与表现；把遮挡/视野规则作为独立场景。
5. 再依次推进 Veigar、Gragas、Karthus、Lux、Annie，优先复用已经被真实英雄闭环证明的输入和指示器契约。

只有当这些真实场景证明公共字段缺失时，才扩展 `SkillButtonTemplate`、指示器枚举或 HUD 输入协议。扩展时必须同时补 DTO、Editor 校验、运行时消费、Unity/Console 发布一致性和对应场景；不要为了某个英雄先加一个硬编码英雄分支。

## 6. 失败定位矩阵

| 现象 | 首先查看 | 常见根因 | 修复归属 |
|---|---|---|---|
| 没有 SkillCast | input.jsonl、HUD bridge、PlayerInputCommand | 按钮事件没提交、玩家映射失效、槽位错误 | HUD/Input/Session |
| 有 SkillCast 没有 Flow/Effect | acceptance summary、Flow 发布产物、Trigger runtime snapshot | Flow/Trigger 未发布或 ID 漂移 | Pipeline/Trigger/发布 |
| 有 Effect 没有 Projectile/Area | trace relationship、spawn/launch 日志 | Payload、目标查询、创建事务或生命周期清理错误 | Projectile/Area/Trigger |
| trace 正确但 HP/Buff 错 | state/context、属性和 Buff 快照 | 目标筛选、来源属性、重复执行或时序错误 | Trigger/Damage/Buff |
| Battle Scenario 正确，Unity 不正确 | input 坐标、Commit 快照、HUD 指示器、单机日志 | 屏幕到世界投影、Release 快照、按钮状态或 Session 装配差异 | HUD/Input/Unity 装配 |
| Unity 画面正确但断言失败 | trace 与录像时间线 | 表现提前/滞后、表现回调拥有玩法结果、配置漂移 | 表现层或权威链路 |
| 第一次回放正确，第二次不同 | determinism fingerprint、replay-report、ActorId/快照 | 非确定性排序、随机种子、实体 ID 或清理未收敛 | 战斗确定性/回放 |
| 单个技能通过，连续技能失败 | 同一 case 的完整 timeline、HFSM 快照 | 状态污染、冷却/资源未恢复、长期模式错误归属 Pipeline | HFSM/Modifier/清理 |

排查顺序固定为：**权威源 -> 发布产物 -> 运行时消费者 -> 输入/场景 -> trace/state -> 表现/录像**。不应直接修改生成 JSON 来“让测试通过”。

## 7. 本地与 CI 门禁

### 开发者本地

1. 先运行受影响的配置、Pipeline、Trigger、StateMachine 检查；
2. 运行单个 `.battlescenario` 或对应 Battle Scenario 测试；
3. 运行同一 `caseId` 的 Unity HUD/单机验收；
4. 只在需要观察表现时开启录像；
5. 若输入或状态变更，重跑回放和 determinism 检查。

### Console 批跑与离线排查

从仓库根目录运行现有 .NET Runner，不启动 Unity，也不加载 Unity 场景或战斗预制体：

```powershell
dotnet run --project src/AbilityKit.Demo.Moba.BattleScenario.Runner -- `
  --batch src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation `
  --output-dir local/Logs/moba-scenario-runs --evidence all `
  --case jhin-q --case annie-q
```

不指定 `--case` 时，运行该目录下所有 `.battlescenario`；`--case` 可重复，值为不带扩展名的文件名。每次运行发布一个独立目录，用 `batch_summary_v1.json` 汇总断言通过、断言失败、执行错误和无断言。仍可去掉 `--output-dir` 和其他新增选项，使用原有文本批跑方式。

`--evidence none` 不采集离线诊断快照，只存验收结果；`failed`（指定输出目录时的默认值）只保留失败证据，但仍启用快照采集；`all` 保留所有成功捕获的证据。证据来自同一次战斗，不用于重演或判定；场景无法加载时通常没有证据。

打开 Unity 的 **Tools → AbilityKit → Demos → Moba → Battle → 战斗调试 → 批次**。工作台分别记住「预设目录」和「分析库」，Windows 默认库为 `%LOCALAPPDATA%/AbilityKit/BattleAnalysis/MOBA/runs/`。也可选择其他 `--output-dir`，包括旧的临时留档根；不自动迁移、覆盖或删除旧批次。查看历史不执行场景；没有证据仍可查验收。

从面板批跑时，选全部预设或勾选用例，设定留证策略后点击「批量执行」。编辑器在后台调用同一个 `.NET` Runner，不启动 Unity Headless。Console 发布批次后通过 `--receipt <新文件>` 返回实际批次 ID，窗口刷新并定位本次结果，判定失败也不影响查看。运行日志和回执仍在仓库的 `local/Logs/moba-scenario-editor/`；长期留档只放所选分析库。需要排查时，再打开单场证据，或返回所选批次。

同一份留档也能在 [Admin Console](../../../../Server/AdminConsole/README.md#英雄技能批次排查) 的「技能分析」中查看。网页可以选择整个输出根目录或单个批次目录；文件只在浏览器本机读取。

### CI

- `BattleScenarioRegressionTests`：所有已提交场景必须通过；
- 受影响英雄的 Unity acceptance gate：必须确认 Test Runner 真正结束，并检查 NUnit XML 的 `total/passed/failed/skipped`；
- `moba-content-contracts`、`moba-console-smoke`、`moba-complete-battle-journey`：按 Skill 规范运行；
- 录像不是 CI 通过条件，但 CI 应发布 `acceptance-summary.json`、trace、输入和回放报告，供需要时人工录制或复核；
- 当同一场景连续失败三次以上时，保留首个失败 artifact，不用“最后一次截图”覆盖诊断上下文。

## 8. 协作方式和完成定义

每个技能由“需求、场景、实现、Unity 验收、录像复核”围绕同一个 `caseId` 协作。开发者提交时至少附上：

- 技能契约和权威 ID；
- Battle Scenario 文件或测试入口；
- 断言覆盖的 trace/state/context/relationship；
- Unity 输入路径和已知未覆盖项；
- acceptance summary、trace 和回放报告；
- 需要人工查看时的录像和时间段索引。

一个技能只有同时满足以下条件才标记为“完成”：

1. 权威源和发布检查通过；
2. Battle Scenario 在真实战斗环境中通过正向和至少一个负向断言；
3. Unity 输入经过 HUD bridge，单机运行结果与场景契约一致；
4. 指示器、动作、VFX、打断和清理经过人工或自动表现检查；
5. 同一输入可回放，玩法状态和 trace 没有不可解释的分叉。

这个定义允许快速的 Headless 回归继续存在，但不会再把“调用了技能入口且 trace 出现”误认为 Unity 中的完整可用技能。

### 8.1 有原始展示资源的技能：增加源预览对照

同一 `caseId` 保留两条证据：**`game-res` 原展示回答「原资产如何播放」；Battle Scenario 回答「权威玩法何时、何地、对谁播放」**。

| 步骤 | 必查项 | 记录内容 |
|---|---|---|
| 打开原展示 | 优先看 `game-res/Unity/Assets/<英雄展示>.unity` 与实际绑定的 Animator、特效组及激活段，不凭资源文件名猜阶段 | 原场景路径、动作 Clip、阶段、锚点、可见部件与停播点 |
| 核对独立资源 | 对照 `game-res` 的 `ExtractedPrefabs` 与当前发布的 VFX/动作绑定；确认模型、材质、粒子、拖尾及局部动画在独立实例上可播放 | 源节点 → 独立 prefab/clip → 发布 ID；注明替代、缺失及是否实际播放 |
| 按玩法事件采样 | 复用 Battle Scenario 预览与 `MobaSkillScenarioPlaybackTests`，按施法、飞行、到达/命中、持续、退场等实际阶段截图，比较动作、世界锚点、挂点和播放顺序 | 同一 `caseId` 的 trace、Cue、关键帧截图；可隔离 renderer 检查是否真的贡献像素 |
| 分别判定 | 玩法断言通过、资源可见、接入后表现对齐分别记结果；自动通过不代表艺术效果逐帧相同 | 写明已对齐、已知差异、待人工对照或没有采样的阶段 |

原展示的 HUD、示范靶、脚本定时、根位移与自动爆炸不进入战斗执行链。片段内的骨骼、材质和粒子节奏可用作参照；命中、落点、目标、开始与清理由玩法规则和对应 Cue 驱动。无需复制 `game-res` 的播放模块，也不把它加入战斗运行时依赖。

| 场景 | 原展示对照 | 实战验收 |
|---|---|---|
| `veigar-basic-attack` | `09.Veigar_Hextech/Imported/AutoAttack.anim` 的 `Fx_AA_MainMissile` 在 0.25 秒启用。 | 用原有逻辑时间轴在 250 毫秒发射，跟踪弹道仍在命中时结算；验证模型动作、出弹、命中及回收。 |
| `lux-basic-attack`、`lux-passive-illumination` | `12.Lux_DarkStar/Animations/Lux_DarkStar_AA.anim` 的 `AA_Missile` 以及提取后的 `AA_Hit`、`Passive_Mark`、`Passive_Hit`。 | 对照无标记和有标记的普攻：标记持续到普攻命中，随后只在原目标引爆一次；预览尾段覆盖命中特效和被动引爆的自然回收。 |
| `karthus-basic-attack` | `11.Karthus_Project.unity` 的 `AutoAttack_1` 对应 `KarthusProject_AutoAttack.anim`；`AA_Missile` 在 0.542 秒启用，源出弹局部高度为 1.525。 | 独立弹道 prefab 仅抬高 `AA_Missile` 子节点，不复制源动画的示范轨迹，不改变权威投射物位置、时序或碰撞。验证模型动作、弹道可见且移动、单次命中/伤害、命中特效及两段表现回收。 |
| `gragas-basic-attack`、`gragas-w` | `10.Gragas_StarGuardian.unity` 的原演示 `AutoAttack_1` 使用 `AutoAttack_2.anim` 展示 `FX_AA_Impact`；正式接入区分 `AA_Hit` 和强化攻击的 `AA_Impact`，不把原展示动作当作两种实战攻击的共同绑定。 | 普攻独立播放普通挥击与单次 `AA_Hit`；W 后强化普攻才播强化动作和 `AA_Impact`，可命中多个目标，但不会为每个目标重复播放强化冲击。 |
| `karthus-passive-respawn` | `11.Karthus_Project/Animations/KarthusProject_Death.anim` 的 `Fx_Death`，对应独立 `Karthus_Project_Death` prefab。 | 灵体特效用角色初始挂点定位，此后不跟随复活位移；仍由死亡许可 Buff 管理寿命。实拍覆盖死亡开场、死后 Q、Q 结束后仍可见、复活清除灵体和 E 光环；源演示的固定退场时间不接入玩法。 |

# MOBA 同步闭环与英雄实现推进计划

> 文档类型：MOBA 项目实现推进与验收计划
> 事实基线：2026-11-10
> 适用范围：Jinx、Annie、Jhin 以及后续四名完整资源英雄
>
> 本文用于在继续扩展英雄机制前确认实现方向。它把同步模式、逻辑状态、临时实体和表现恢复拆成独立责任，并按可验收的批次安排实现顺序。文中的“已有入口”只代表源码或测试入口存在；只有列出的验收证据通过，才把能力标记为已闭环。

## 1. 当前结论

当前更适合先跑通一条真实 MOBA 内容的同步闭环，再批量推进剩余英雄。理由不是暂停英雄内容，而是先用已经具备较完整资源和机制的 Annie、Jinx 建立代表性基线：它们分别覆盖被动计数、Buff、技能等级、指示器、投射物、区域、召唤物行为树、武器模式和 HFSM 状态变化。同步链路如果此时仍有边界缺口，继续增加四名英雄会把同一个问题复制到更多技能、Buff、临时实体和表现事件上。

建议顺序如下：

1. 冻结同步配置和状态边界，确认单机、Lockstep、预测回滚、权威恢复各自的责任。
2. 用 Annie 和 Jinx 的真实技能内容完成 Lockstep 双端一致性验收。
3. 在同一批内容上完成预测、状态 Hash、回滚和重演验收。
4. 完成权威快照恢复、远端插值、表现重建和输入解锁验收。
5. 完成断线/恢复会话流程后，再按统一能力矩阵实现剩余四名英雄。
6. Jhin 的 HFSM 改动稳定并通过专属门禁后，再把 Jhin 纳入同步基线；在此之前只使用已经稳定的技能路径作为对照，不把正在修改的状态机作为第一轮同步阻断项。

这不是把英雄实现永久后置。P0 至 P4 应优先补齐同步闭环所需的通用能力；P5 开始可以并行推进剩余英雄，但每个英雄必须先经过内容级单机验收，再进入同步矩阵。

## 1.1 一个工程展示两种同步方式

后续可以在同一个 Unity MOBA 示例工程的入口 HUD/大厅面板中提供“MOBA 帧同步”和“MOBA 状态同步”两个选项。当前代码已经具备低成本复用的基础：`BattleSyncMode` 定义了 `Lockstep`、`SnapshotAuthority` 和 `HybridPredictReconcile`；`BattleStartConfig`/`BattleStartPresetSO` 保存模式；`BattleStartPlanAssembler` 将模式转换为启动计划；`SessionMobaWorldBootstrapFactory` 再将启动计划接入逻辑世界驱动。两种模式可以共享英雄、技能、Buff、召唤物、行为树、模型、VFX 和 HUD 技能栏。

推荐采用“入场选择，整局固定”的方式：

```text
入口 HUD / 大厅
  -> 选择 FrameSyncPreset 或 StateSyncPreset
  -> 生成本次 battle 的 BattleStartPlan
  -> 进入同一套 MobaBattleFlow / BattleSessionFeature
  -> 只替换 sync adapter、authority/recovery 和表现数据源
  -> 战斗结束后回到入口 HUD，下一局可以重新选择
```

第一阶段不应在战斗进行中直接切换 adapter。运行中切换会同时影响输入所有权、逻辑推进、权威帧、快照水位、回滚历史和表现订阅，成本和风险都明显高于“重新开局选择模式”。

入口实现可复用现有 `GameEntryRuntimeGuiBridge` 或大厅 UI：

| 入口选项 | 预设内容 | 允许的主机边界 |
|---|---|---|
| MOBA 帧同步 | `SyncMode=Lockstep`、正式 `frame-sync-authority`、同一英雄/地图/输入配置 | Local、GatewayRemote；正式多人优先使用此项 |
| MOBA 状态同步 | `SyncMode=SnapshotAuthority`、快照/恢复/远端表现参数 | 先用于 Local/Console/恢复演示；GatewayRemote 只有在服务端明确提供 state-sync 模板后才启用 |

选择器不能直接修改场景中的共享 `ScriptableObject`。应保存“当前选择的 preset/profile id”，进入战斗时生成本次启动使用的运行时配置或选择对应的只读 preset；这样两种模式不会互相污染，也便于录制和复现。

需要补的最小能力是：

1. 两个可验证的启动预设（FrameSync、StateSync），共享 `PlayersSO`、`EnterGameSO`、资源 manifest 和技能配置。
2. 入口 HUD 的模式按钮/下拉框、当前模式标签和不支持组合的禁用提示。
3. `BattleStartPlan` 构建测试，确认选择项正确映射到 `FrameSync`/`StateSync`，且不改变 roster、tick、随机种子和技能输入。
4. 模式门禁：当 `HostMode=GatewayRemote` 而服务端能力只声明 Lockstep 时，状态同步按钮必须禁用或明确显示“仅本地/恢复演示”，不能让 UI 产生可用错觉。
5. 一条 Annie R/Tibbers 和一条 Jinx Q/E 的双模式演示脚本：同一输入录制分别运行两种模式，比较最终 HP、Buff、Counter、技能冷却、临时实体集合和表现事件。

这样做的边界是“同一工程、同一内容、两种启动方式”，不是承诺正式 MOBA 房间已经支持两个网络协议模板。正式房间目前仍以 `frame-sync-authority` 为唯一生产路线；状态同步展示应先作为本地/恢复路径，待服务端能力声明、协议和 E4 artifact 完成后再开放 GatewayRemote。

## 2. 现状与证据等级

### 2.1 已有的基础设施

仓库已经具备以下运行时入口或组合测试：

| 能力 | 当前入口/证据 | 目前能证明的范围 |
|---|---|---|
| 帧同步路由 | `BattleSyncVirtualRouteTests`、`BattleWorldWithFrameSync`、`frame-sync-authority` | 帧同步路由、断线 catch-up 和确认帧相关的通用行为有测试入口 |
| 同步组合 | `MobaSynchronizationCompositionTests` | Hash calculator、rollback provider 注册、恢复诊断事件和 provider 生命周期有组合级断言 |
| 权威恢复 | `AuthoritativeStateRecoveryRuntimeTests` | full baseline、增量快照、失败后保持输入阻断、重连 generation 和可靠事件重试有运行时测试 |
| 远端表现 | `MobaRemoteInterpolationAcceptanceTests` | 远端采样和插值表现有验收入口 |
| 预测运行时 | `RemoteDrivenRuntimeModuleFactory` | 预测历史、ahead、capture interval 和 rollback 开关已有配置路径 |
| Annie 内容 | `AnnieSkillAcceptanceTests` | 被动计数/眩晕、Q/W/E/R、提伯斯生命周期和行为树单机路径已有验收入口 |
| Jinx 内容 | `JinxSkillAcceptanceTests`、`HeroSkillIndicatorAcceptanceTests` | Q 武器模式、W/E/R、普攻 Projectile、HUD 指示器和本地输入路径已有验收入口 |

### 2.2 尚未证明的端到端能力

以下事项不能仅由上述测试名称或源码分支推断为完成：

- Annie 召唤物、Buff、GameplayCounter、冷却、HFSM/行为树状态在双端 Lockstep 下长期运行后仍保持相同状态。
- Jinx Q 武器模式切换、Modifier 贡献、射程/攻速/资源和表现状态在双端以及回滚重演后保持一致。
- 真实技能内容发生 Hash mismatch 后，回滚能够覆盖所有影响战斗结果的状态，并且重演不会重复应用伤害、Buff 或表现 Cue。
- 权威快照能够恢复临时召唤物的存活集合、Owner 关系、行为树运行时、Buff 和表现；当前设计更适合“实体集合裁剪 + 重执行创建”，不能把普通状态字段导入误认为召唤物恢复完成。
- 远端快照、确认帧和表现事件的组合能在断线恢复后重建 HUD、VFX、Animator 和技能栏，同时不会重复播放一次性事件。
- MOBA 生产房间可以在战斗中任意热切换同步模板。当前正式协议固定为 `frame-sync-authority`/`Lockstep`；`SnapshotAuthority` 和 `HybridPredictReconcile` 是 Console/客户端组合路径或恢复辅助能力，不能直接宣传为生产房间可热切换模式。

证据等级采用以下约定：

| 等级 | 含义 |
|---|---|
| E0 | 需求或设计文档，未找到可执行证据 |
| E1 | 源码入口或配置存在，未形成直接测试 |
| E2 | 单元/组合测试通过，覆盖局部契约 |
| E3 | MOBA Console 或 Unity 单机/离线 Acceptance 通过 |
| E4 | 两个独立客户端或服务进程的真实同步、弱网、恢复 artifact 通过 |
| E5 | CI/发布门禁可重复执行并保留 artifact |

本计划的目标是把 Annie/Jinx 的关键同步项从 E2/E3 推到 E4，再把同一套门禁纳入 E5。没有 E4 证据时，不把“有适配器”写成“联机已完成”。

## 3. 同步责任边界

### 3.1 三类运行模式的实际边界

| 模式 | 主要职责 | 当前 MOBA 使用边界 |
|---|---|---|
| `Lockstep` | 广播输入和帧号，各端以同一逻辑顺序推进 | MOBA 正式房间的主路线；英雄技能和召唤物正常对局应首先在此闭环 |
| `HybridPredictReconcile` | 本地预测、状态 Hash、权威帧校正、回滚重演 | 作为客户端预测/恢复组合路径验证；必须用真实 Annie/Jinx 内容证明，而不是只测通用 driver |
| `SnapshotAuthority` | 权威端输出状态，客户端导入并驱动远端表现 | 用于权威恢复和远端表现验收；当前不等同于 MOBA 房间可替换的正式同步模板 |

同步模式由 `BattleStartConfig`、`BattleStartPresetSO`、`BattleStartPlanBuilder` 和相应 adapter 选择。模式选择发生在会话启动阶段；“切换状态同步”应被实现成明确的恢复流程，而不是运行中直接替换 adapter：

```text
Lockstep 或预测运行
  -> 检测断线、Hash mismatch 或恢复请求
  -> 停止接受会改变玩法的输入
  -> 建立新的 recovery generation
  -> 导入完整权威 baseline / 增量快照
  -> 裁剪预测临时实体并按确认输入重建
  -> 重建 Buff、召唤物关系、HFSM/行为树和表现投影
  -> 校验 frame/hash/可靠事件水位
  -> 解锁输入并恢复正常推进
```

权威恢复期间，表现层只消费快照、确认事件和状态投影；表现层不能反向修改权威逻辑，也不能因为重新显示一个 Cue 就再次执行伤害或 Buff。

### 3.2 需要进入逻辑状态的对象

以下对象只要能改变未来战斗结果，就必须有确定性更新、Hash 参与和回滚/恢复策略：

- Actor 的位置、朝向、生命、资源、死亡/复活和阵营关系。
- 技能槽、技能等级、冷却、施法阶段、GameplayCounter 和被动监听状态。
- Buff 实例、层数、剩余时间、来源、Modifier 贡献和护盾吸收状态。
- Projectile、Area、Motion 等持续运行时的阶段、计时、命中去重和来源上下文。
- 召唤物的实体 ID 集合、Owner/RootOwner、模板 Code、行为树黑板和当前节点；召唤物创建/销毁必须使用确定性命令和可重复的 ID 分配。
- Jinx 的 `WeaponMode` 及其 Modifier；Annie 的被动计数、眩晕消费和 Tibbers 的生命周期/跟随状态。

### 3.3 只进入表现投影的对象

以下数据可以由确认状态重建，不应独立成为权威玩法状态：

- Animator 参数、动作播放时间、模型挂点、技能按钮高亮和 HUD 进度条。
- VFX、SFX、相机抖动、指示器绘制和一次性 Presentation Cue。
- 远端插值缓存和渲染时间线。

表现事件需要稳定的 `event key + frame/generation + source` 去重规则。回滚或恢复后，表现从确认状态重建；已确认过的伤害、Buff、召唤和销毁事件不能因为重演再次产生副作用。

## 4. 分批实施顺序

### P0：同步基线与配置冻结

**目标：** 让每次同步验收使用同一份可追溯配置。

工作项：

1. 固定 Annie/Jinx 的英雄、技能、Buff、Counter、召唤物、行为树、模型/VFX 软链接和技能指示器 Code；发布前执行配置引用和资源存在性校验。
2. 固定 `BattleStartConfig` 的帧率、初始帧、玩家/英雄 roster、同步模式、预测 ahead/history、Hash 和快照 schema 版本。
3. 记录每个场景的配置版本、资源清单和技能输入序列，保证 Console、Unity 单机和双端测试使用同一来源。
4. 把 Jhin HFSM 变更标记为独立阻断项，不让其未完成的状态图污染 Annie/Jinx 的同步基线。

**通过门槛：** 同一配置在 Console 与 Unity 导出结果一致；不存在未发布的引用、占位资源或英雄编号冲突；测试 artifact 能记录 schema/config revision。

### P1：Lockstep 真实 MOBA 内容验收

**目标：** 证明正常对局下两个逻辑端执行同样输入会得到相同战斗结果。

优先场景：

| 场景 | 必须观察的状态 |
|---|---|
| Annie 被动 | Q/W/E/R 成功施放计数，第四次技能伤害触发眩晕，计数消费和冷却一致；普通攻击不应误计数 |
| Annie R | 指示器位置、召唤位置、Tibbers 模板和 Owner 关系一致；无敌人时跟随，进入水平跟随距离后停止；有敌人时行为树切换到追击/攻击 |
| Annie Buff/护盾 | E 的护盾、持续时间、移速和消耗在两端一致；护盾吸收顺序和剩余值一致 |
| Jinx Q | 武器模式互斥切换、Modifier 叠加/移除、射程/攻速/资源和普攻 Projectile 结果一致 |
| Jinx E/R | E 陷阱区域生成/命中去重，R 目标筛选和首次英雄命中爆炸只发生一次 |

**通过门槛：** 每个关键帧的 frame、输入顺序、技能阶段、HP/资源、冷却、Buff、Counter、Projectile/Area/召唤物实体集合和最终状态 Hash 一致；不要求渲染帧完全相同，但要求表现所需的确认事件顺序一致。

### P2：预测、Hash、回滚与重演

**目标：** 在 P1 场景中主动制造延迟、丢包或 Hash mismatch，验证本地预测不会改变最终权威结果。

工作项：

1. 对 Annie 的 R 召唤、被动眩晕、护盾和 Tibbers 行为树分别制造预测与权威分歧。
2. 对 Jinx 的 Q 模式切换和 E 区域生成制造分歧，检查 Modifier、技能槽和临时实体是否回滚。
3. 回滚前删除预测端多出的 Projectile/Area/召唤物；按确认输入重放创建命令，使用受回滚约束的 Actor ID 分配器，避免 Owner link、target 和 NetId 漂移。
4. 验证命中、伤害、Buff、召唤和一次性 Cue 的去重，确保重演只恢复状态而不重复副作用。

**通过门槛：** mismatch 被报告一次，回滚开始/完成各报告一次；回滚后状态 Hash、实体 ID 集合、Owner 关系、Buff/护盾/Counter、技能冷却和 HFSM/行为树节点与权威端一致；表现层没有重复伤害数字、眩晕特效或召唤特效。

### P3：权威状态恢复与远端表现

**目标：** 验证从 full baseline 和增量快照恢复时，客户端可以重新建立可玩的 Annie/Jinx。

恢复内容至少包括：

- Actor roster、位置/朝向、HP/资源、技能冷却和当前施法阶段。
- Buff 实例、层数、剩余时间、Modifier、护盾剩余值和 GameplayCounter。
- Projectile/Area 的存活集合和阶段；对无法安全导入的预测临时实体执行裁剪并重建。
- Tibbers 的实体 ID、Owner/RootOwner、模板 Code、当前位置、行为树黑板和跟随/追击状态。
- Jinx 的 WeaponMode、Modifier 来源和 HUD 技能栏状态。

**通过门槛：** baseline 导入成功后输入保持阻断直到所有必需状态和可靠事件水位完成；导入失败继续请求 full state；恢复完成后 frame/hash 与权威一致，HUD/VFX/Animator 从确认状态重建，重复快照不会重复播放一次性 Cue。

### P4：会话断线、恢复与输入解锁

**目标：** 把 P2/P3 组合成可重复的会话流程。

工作项：

1. 断线或恢复请求进入单独 generation，取消旧 generation 的异步回调。
2. 清理旧预测输入和远端插值缓存，保留需要确认的本地输入序列。
3. 导入 baseline/增量快照，校验 schema、配置 revision、frame、Hash 和可靠事件 watermark。
4. 先重建逻辑 world 与临时实体，再重建表现，最后解锁玩法输入。
5. 记录 recovery artifact：断线帧、恢复帧、恢复前后 Hash、删除/重建实体、重放输入和表现去重结果。

**通过门槛：** 重连后 Annie/Jinx 可以继续释放技能；Tibbers 不重复生成且 Owner 关系正确；Jinx 不因恢复丢失或重复切换 WeaponMode；旧 generation 的回调不能改变新会话状态。

### P5：剩余四名英雄机制实现

每名英雄按以下顺序接入，不直接复制现有英雄的专用分支：

1. 先完成技能机制文档和能力缺口分类，明确指示器、Buff、Projectile、Area、Counter、召唤物、HFSM/行为树和表现需求。
2. 只补通用 action/predicate/schema 或配置表；英雄差异留在技能、触发器、Buff、资源和行为树配置中。
3. 完成单机 Battle Scenario：空放、命中、未命中、边界、资源/冷却、死亡/复活和表现资源路径。
4. 加入 P1 Lockstep 内容矩阵，再按需要加入 P2/P3 的特有恢复场景。
5. 通过同步门禁后才把英雄标记为“一比一复刻完成”。

## 5. Annie 与 Jinx 同步验收矩阵

| 维度 | Annie | Jinx | 证据要求 |
|---|---|---|---|
| 输入 | Q/W/E/R 指示器、技能栏提交、施法朝向 | Q/W/E/R 指示器、武器模式按钮、普攻槽位 | 两端收到相同 `PlayerInputCommand`，frame/order 一致 |
| 计数/模式 | GameplayCounter 层数、第四次眩晕、消费 | `WeaponMode`、互斥 Buff/Modifier | 每次变更进入确定性状态和 Hash |
| 持续效果 | E 护盾/移速、眩晕 Buff | Q 模式 Modifier、E 陷阱状态 | 剩余时间、来源、层数和结束顺序一致 |
| 临时实体 | Tibbers 生成、跟随、追击、死亡、Owner 死亡销毁 | Projectile、E 三个陷阱区域、R 命中爆炸 | 实体集合、ID、Owner、阶段和销毁原因一致 |
| 行为树/HFSM | Tibbers brain 节点和黑板 | Jinx 角色 HFSM/攻击状态 | 节点/状态变化由逻辑状态恢复，表现只消费投影 |
| 表现 | 召唤、眩晕、护盾、指示器、HUD | 武器动画、Projectile、陷阱、爆炸、HUD | Cue 稳定去重，恢复后从确认状态重建 |
| 预测/恢复 | R 预测生成与裁剪重建 | Q 模式预测切换与 Modifier 恢复 | mismatch、rollback、baseline、增量恢复均有 artifact |

## 6. 不应提前做的事情

- 不在生产 MOBA 房间中把 `SnapshotAuthority` 当成可以随时切换的第二套模板；先完成明确的恢复协议。
- 不为了单个英雄在共享 Cast、Pipeline、Counter、Summon 或行为树执行器里增加英雄 ID 分支。
- 不把模型、VFX、Animator 或 HUD 的表现状态当作权威战斗状态写回逻辑 world。
- 不把召唤物只当成一个 Transform。至少要保留实体集合、Owner、模板 Code、行为树运行时和销毁原因；恢复时按“裁剪预测实体 + 重执行创建”处理。
- 不因通用 Hash、RollbackRegistry 或 StateSyncAdapter 有测试，就宣称 Annie/Jinx 的完整技能链已经完成同步验收。

## 7. 建议的测试与门禁入口

### 7.1 现有测试作为基础门禁

- .NET：`dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj`
- Unity View Runtime：执行 `MobaSynchronizationCompositionTests`、`BattleSyncVirtualRouteTests`、`AuthoritativeStateRecoveryRuntimeTests`、`MobaRemoteInterpolationAcceptanceTests`。
- Annie：执行 `AnnieSkillAcceptanceTests` 中被动、Q/W/E/R、Tibbers 生命周期和行为树用例。
- Jinx：执行 `JinxSkillAcceptanceTests` 与 `HeroSkillIndicatorAcceptanceTests` 的 Q/W/E/R、普攻和 HUD 输入用例。

### 7.2 需要新增的 E4/E5 门禁

1. 两个独立客户端使用同一发布配置和输入录制，逐帧比较输入顺序、Hash、实体集合和关键事件。
2. 虚拟网络注入延迟、丢包、乱序和 Hash mismatch，输出 rollback/replay artifact。
3. 断线后请求 full baseline 与增量恢复，验证输入阻断、generation、临时实体重建和表现去重。
4. Unity Battle Scenario 录制 Annie R/Tibbers、Jinx Q/E/R 的逻辑轨迹和 Presentation Cue，和单机金样本比较。
5. 发布门禁检查配置 revision、资源 manifest、schema 版本和 artifact 完整性；失败时阻断继续扩展英雄。

## 8. 完成定义

一名英雄只有同时满足以下条件，才进入“可玩且可同步”的完成状态：

1. 设计文档、技能/触发器/HFSM/行为树和资源软链接均有权威来源。
2. 单机 Battle Scenario 覆盖正常、边界、失败、死亡/复活和表现路径。
3. Lockstep 双端在代表技能场景中逐帧收敛。
4. 预测回滚后逻辑状态、临时实体、Buff、冷却和表现 Cue 正确。
5. 权威恢复后可以继续输入，HUD/VFX/Animator 从确认状态重建且无重复副作用。
6. 发布流程、配置校验、Unity/Console 测试和同步 artifact 可重复执行。

在达到上述定义前，文档应使用“已具备基础设施”“单机已通过”“同步待验收”等精确状态，不使用笼统的“已完成”。

## 9. 相关文档与源码

- [04-快照、表现层与预测回滚](04-SnapshotPresentationPrediction.md)
- [15-联机会话与协议契约](15-OnlineSessionAndProtocolContract.md)
- [16-领域连续运行时与临时实体生命周期](16-DomainContinuousRuntimeAndTemporaryEntityLifecycle.md)
- [21-技能能力缺口矩阵](21-SkillCapabilityGapMatrix.md)
- [31-MOBA Battle Scenario 技能验收流程](31-MobaBattleScenarioSkillAcceptanceWorkflow.md)
- [35-MOBA 召唤物行为树生命周期基础](35-MobaSummonBrainLifecycleFoundation.md)
- [36-MOBA 召唤物回滚与生命周期基础](36-MobaRollbackBrainLifecycleFoundation.md)
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Battle/Networking/Replication/`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/Annie/AnnieSkillAcceptanceTests.cs`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/Jinx/JinxSkillAcceptanceTests.cs`

---

*文档版本：v1.0 | 最后更新：2026-11-10 | 本文是实现顺序与验收门禁，不替代具体英雄技能设计文档。*

## 本轮实现状态（2026-11-10）

- 新增 `MobaBattleSyncModePolicy`，统一校验 HostMode/SyncMode/网关传输组合，并为入口提供显示名称和禁用原因。
- `BattleStartPlanAssembler` 与 `BattleSessionConnectionRuntime` 共用该策略；GatewayRemote 仍明确只允许 Lockstep/frame-sync-authority。
- `BattleStartConfig.EnableSyncModeSelection` 作为正式开关；关闭时保留本地自动进入，开启后在 Demo Lobby 中显示帧同步和状态同步选项。
- 入口只从本地 `BattleStartPresetSO` 选择；当前状态同步选项会明确显示“本地 authority transport 尚未完成”，不会伪造可用状态同步会话。
- 新增 EditMode 覆盖策略边界、状态同步禁用原因和自动进入开关的测试。下一批应补齐本地 authority world、snapshot producer/transport/import 后，再增加 StateSync preset 与 Annie/Jinx 双模式验收。

## 本轮补充（2026-11-10）

- 新增 `MobaBattleSyncModePolicy`，统一校验 HostMode/SyncMode/网关传输组合，并提供 Gateway profile 映射。
- MOBA 服务端目录新增 `moba-state-sync-authority`，运行模式使用 BattleWorld，并声明与客户端一致的 `Moba.AuthoritativeRemoteInterpolation` 能力；默认仍是 `frame-sync-authority`。
- GatewayRemote StateSync 只在房间能力声明匹配时启动；既有房间 bootstrap 不再把 StateSync 强制改成 Lockstep，LaunchSpec 也保留来源同步模式。Hybrid 仍因 MOBA 房间目录未声明而拒绝。
- 客户端已经具备 StateSync snapshot/materialization/import/recovery 基础；但本地 SnapshotAuthority authority transport 仍未完成，Demo Lobby 中的本地状态同步选项继续禁用。GatewayRemote 房间选择流程与 Annie/Jinx 双端实际技能验收仍待完成。
- MOBA 服务端 Grains 编译通过。Grains.Tests 当前因已有 `MobaCharacterHfsmPushPayloadTests` 调用不存在的 `MobaCharacterHfsmProfile.CreateDefinition` 而无法编译，需先修该独立测试阻断再运行目录与房间路由测试。

- HUD 的同步模式选择现在会在本地对应模式不可用时查找 GatewayRemote preset；新增远程状态同步 preset 并挂入 `MobaDemoRoot`，点击后进入既有远程大厅入口。该配置依赖房间服务器支持 `moba-state-sync-authority`。
- Smoke host 曾成功启动并监听 31101；独立 client-only 在 Login op 100 超时，集成 smoke 连接后 `GuestLogin` 失败，尚未走到创建 StateSync 房间阶段，因此不作为 StateSync 验收通过证据。端到端验证需先修复/确认 smoke 登录环境，并使用真实双端（可用 instance2）。

## 本轮 Smoke 联调记录（2026-11-10）

排查并修复了三处导致双同步验证无法完成的问题：

- MobaSmoke 的 RequestClient 原先使用 Raw 响应解码，但 Gateway TCP 响应有 4 字节 status envelope，GuestLogin 响应因此被错误解释。Smoke 现在明确使用 `TcpGatewayRequestResponseEnvelopeCodec`，网关错误能显示实际 status 与 message。
- MobaSmoke 漏注册 `ServerGameplayModuleCatalog`，RoomGrain 激活时 DI 失败；同时房间创建未提供 MOBA 必需的 tag bundle 版本。Smoke 现在复用正式目录配置，并为测试房间提供明确的 smoke bundle version。
- 原 Smoke 将两种同步模式都经由 StateSync 订阅和 `SubmitBattleInput` 验收，导致帧同步模式在等待英雄移动时超时。现在 frame-sync-authority 分支走正式的 SubscribeFrameSync / SubmitFrameInput 协议，并验证两个客户端收到相同 world/frame 和提交的玩家输入。

验证结果：`dotnet build AbilityKit.Orleans.MobaSmoke.csproj --no-restore` 成功；`moba-state-sync-authority` 单机 host 上两条独立 TCP 客户端完成建房、战斗启动、权威移动快照收敛、Full StateSync 恢复及可靠事件 ACK；`frame-sync-authority` 完成建房、战斗启动、两个客户端订阅帧流、服务端接受输入并向两端广播相同输入帧。以上验证使用 Smoke 内两个独立客户端连接，尚未替代 Unity 双实例（instance2）表现验收。

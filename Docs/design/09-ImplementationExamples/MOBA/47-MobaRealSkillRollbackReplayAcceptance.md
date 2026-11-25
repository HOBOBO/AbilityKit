# MOBA 真实技能回滚与重演验收

更新时间：2026-11-10

## 目的

本文定义同步计划 P2 的可验收边界。当前已存在的预测协调器测试和 MOBA rollback provider 测试分别验证通用预测流程与各状态 provider 契约；它们还没有证明真实 MOBA 技能世界经过 rollback/replay 后能得到相同结果。

## 当前证据及其边界

`MobaBattleScenarioWorldRunnerTests.PredictionDsl_ReconnectHashMismatchRollsBackAndReplaysDeterministically` 通过 `MobaBattleScenarioPredictionRunner` 运行一个只含整数 `State` 的 `PredictionWorld`。该测试可以验证合成世界收到 Hash mismatch 后会回滚并重演，但不执行 Annie/Jinx 技能，也不比较战斗实体、技能运行时、Buff、召唤物或表现事件。

`BattleScenarioRealSkillTests` 会在正式 Console MOBA 逻辑世界运行技能输入并收集 trace 与结果状态；它目前没有把这个世界接入 `RollbackCoordinator`。`MobaRollbackRegistryBuilder` 则已登记 MOBA 世界中的核心 rollback provider，包括实体与组件、帧时钟、技能执行与流水线、技能经济与冷却、Buff、护盾、GameplayCounter、投射物/区域、召唤物所有权与行为状态等。provider 单测证明各自能导出和导入，不代表完整世界已具备可重演的驱动闭环。

## 接入前必须处理的帧时钟边界

Console 驱动帧时会依据 `ConsoleBattleContext.LastFrame` 调用 `FrameTime.StepTo(...)`。MOBA registry 目前可以回滚 `FrameTime`，但 `RollbackCoordinator` 不拥有或恢复 `ConsoleBattleContext.LastFrame`。如果测试只恢复 registry snapshot，随后继续调用 Console `Tick`，帧时钟可能被外层 context 帧号重新覆盖。真实技能 replay harness 必须让单一的 simulation frame authority 同时驱动 context、`FrameTime`、输入命令帧和 World tick；不能通过只恢复 `FrameTime` 来宣称重演闭环通过。

这也是先做 harness 接线审查、再加英雄场景的原因。若必须调整 Console 帧驱动，应把恢复入口放在通用帧同步/World 驱动层，并保证普通单机、帧同步和测试驱动使用同一条帧推进路径。

## 推荐实现顺序

1. 为真实 MOBA World 建立可控制的固定帧驱动入口：可在指定帧捕获 `MobaRollbackRegistryBuilder` 生成的 registry，可恢复 snapshot，并可在恢复后把 context 帧、`IFrameTime` 和输入帧对齐。
2. 建立可重放的 BattleScenario 输入记录：保留 `PlayerInputCommand` 的 frame、player、opcode 和 payload；重演时复用同一份已确认输入序列，而不是重新生成不同帧号的命令。
3. 先用最小真实技能场景接通闭环。优先选稳定、可观察且不依赖复杂临时实体的 Annie Q；若技能数据/命中时序使其不适合，则选 Jinx Q 的模式切换。之后再覆盖 Annie R/Tibbers 与 Jinx E 的临时实体场景。
4. 对比 rollback 前后并重演后的确定性逻辑状态：actor roster/ID、Owner/RootOwner、HP/资源、Buff/层数/剩余时间、冷却、GameplayCounter、护盾值、技能阶段、Projectile/Area/召唤物及 HFSM/行为树状态。忽略纯视图插值差异，但单次伤害、Buff 应用、召唤和 presentation cue 的可靠事件必须有明确去重策略。
5. 输出可复查 artifact，至少含 checkpoint frame、rollback target、输入序列、恢复前后 hash、实体集合差异、事件去重结果和最终 verdict。

## 通过标准

- 真实 Console MOBA World 执行真实 Annie/Jinx 技能，而非合成 `PredictionWorld`。
- 在技能效果产生前后各捕获 checkpoint；注入一次确定的预测偏差后恢复并重放相同输入。
- replay 后逻辑状态 Hash 与未扰动的同输入基线一致，实体 ID、所有权、Buff/Counter/冷却及技能运行时一致。
- 伤害、Buff、召唤和 presentation cue 不因 replay 重复提交；恢复期间的预测实体不会残留。
- 断言能在同一配置版本上重复运行，并保存输入与差异 artifact。

## 当前状态与下一步

截至本轮，P2 仍未通过；其战斗状态同输入重演部分已通过。[49 号联合恢复](49-MobaSkillLifecycleRollbackPlanProposal.md) 与 [50 号配套时钟/战斗记录恢复](50-MobaCombatActivityRollbackProposal.md) 已实现。两个不同 source/target ID 组合各从同 roster checkpoint 原样重演两次，全部已登记 provider 字节/hash、伤害/资源、时钟和持续体均与基线一致；资源导入遗漏的属性上下文投影也已按既定契约修复。原始表现事件仍为基线 5 条、重演 8 条，Counter 阶段/序列不同，可靠事件生产/消费去重尚未验收。状态同步断线重连、Unity 表现和跨 Projectile actor 增删恢复均未因此通过。下文早期阻断记录是历史证据，以本节及末尾最新实施记录为当前状态。

相关实现：

- `src/AbilityKit.Demo.Moba.Acceptance.LiveSim/MobaBattleScenarioWorldRunner.cs`
- `src/AbilityKit.Demo.Moba.Acceptance.LiveSim/LiveSimTimelineRunner.cs`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Testing/MobaBattleScenarioPredictionRunner.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaRollbackRegistryBuilder.cs`
- `src/AbilityKit.Demo.Moba.Tests/Smoke/MobaEntitasRollbackTests.cs`
- `src/AbilityKit.Demo.Moba.Tests/Smoke/BattleScenarioRealSkillTests.cs`

## 本轮推进（2026-11-10）

- `ConsoleBattleBootstrapper.TryRestoreSimulationFrame` 现在把 rollback coordinator 恢复与 Console `LastFrame`、逻辑时间及 `FrameTime` 对齐组合成一个正式入口。真实 MOBA World 回归覆盖恢复 checkpoint 后继续 tick 一帧；context 帧与 World 帧同步前进。
- 新增 Annie Q 真实技能阻断回归：确认 `SkillCast` trace、目标 HP 下降和 Annie 资源消耗都发生；恢复施法前 checkpoint 时，Buff provider 以 `ProviderFailed` 拒绝导入，明确报告 Buff 实例 membership 已变化。测试目前将这个已知边界作为预期结果，不将其算作 P2 通过。
- 额外探测发现，在 Annie Q 已开始但仍处于运行中的时间点捕获完整 World snapshot，也可能因 `Skill child execution context ... does not match its runtime child reference` 而无法保存。正式处理应同时检查 Skill Runtime/Pipeline/Context provider 的父子关系与 Buff 生命周期恢复顺序。

验证：`Rollback_restore_realigns_console_and_world_frame_clocks_before_next_tick` 与 `Annie_q_real_skill_reports_buff_lifecycle_rollback_gap` 均通过。当前 P2 仍未通过；下一批优先处理可重建 Buff 生命周期实例的 rollback provider，并修复/界定技能运行中 child context 快照一致性，再把预期阻断测试升级为“rollback + 重演后状态相等”。

## 2026-11-10 增量进展：施法中快照

Annie Q 在飞行中捕获 rollback snapshot 已纳入真实技能回归测试。此前快照失败来自 `ProjectileLauncher` 子引用的配置 ID 与执行上下文配置 ID属于不同配置域：前者标识 launcher 行，后者标识 launcher 生成的 projectile 行。`MobaSkillRuntimeRollbackProvider` 现在按 child kind 校验 launcher 上下文，不再错误比较这两个 ID；普通 projectile 子引用仍严格校验 projectile 配置 ID。

这一阶段曾覆盖恢复时 Buff 实例数量变化被 `MobaBuffTimerRollbackProvider` 拒绝。后续接入 lifecycle provider 后，当前测试覆盖施法前捕获、Annie Q 施法中捕获、命中后的 HP/法力变化，以及恢复被 owner Blackboard 预检拒绝。前述快照校验问题已修复，但生命周期恢复缺口仍阻断 P2；真实 rollback + replay 的等价性尚未验收。

## 后续发现：Buff 生命周期重建已接入，预检转而暴露 Blackboard 成员问题

rollback registry 现在通过 `MobaBuffLifecycleRollbackProvider` 复用 staged Buff state recovery，并放在 execution context 与 skill runtime 恢复之后，使 Buff 实例、Continuous 绑定和技能 retain 有机会按完整生命周期重建。真实 Annie Q 场景已证明施法中快照可捕获，但从施法后恢复到施法前 checkpoint 时，当前更早的 `MobaOwnerBlackboardRollbackProvider` 在预检阶段拒绝：施法期间创建的 actor owner Blackboard 不存在于 checkpoint 的 owner 集合中。由于预检在任何 provider 导入前统一执行，Buff provider 尚未参与这次恢复；因此不能据此宣称 Buff 重建或完整回放已经通过。

下一步应让 owner Blackboard 的创建/销毁成员也能按 checkpoint 安全恢复，再重新运行 Annie Q 回滚与同输入重放比较。P2 仍未通过。

## 前一批查证与机制准入（2026-11-10，历史记录）

已核查 `OwnerBlackboardStore.TryValidateSnapshotSet`、MOBA provider、owner 来源、效果懒创建入口、订阅持有的 resolver 引用，以及 coordinator 预检/导入边界。当前严格集合校验是明确契约；支持成员重建会新增恢复语义，因此按 `Docs/MobaMechanismGovernance.md` 先提交 [48 号方案](48-MobaOwnerBlackboardRollbackProposal.md)，等待用户明确确认。本批只更新设计与进度文档，没有实施依赖该方案的 runtime、配置或发布物变更。

基于已有构建产物复跑定向测试：5/5 通过（帧时钟恢复 1 项、Annie Q 施法中捕获及 owner Blackboard 阻断 1 项、旧 Buff timer 预检 3 项）。这是已有行为复核；没有重新编译，也不代表新增成员恢复、Buff lifecycle 全链或 P2 已通过。

## 本批结果：限定 actor Blackboard 成员恢复（2026-11-10）

用户确认 48 号方案后，已增加显式 store 成员恢复计划和带 roster/来源分类的 MOBA 本地快照。相同 actor roster、相同非 actor owner/订阅集合是硬边界；幸存引用保持、非变异预检和提交失败局部补偿已有测试。既有严格恢复接口及旧 payload 继续要求成员相同。

真实 Annie Q 回归先完成正常 passive 初始化 tick，再捕获施法前 checkpoint，不预创建 actor Blackboard。Annie 的 actor owner 在施法后确实懒创建；独立导入该 provider 的前/后快照可以撤回、恢复该成员，导出字节分别与快照一致。当前生产 owner board 为空，字段和引用验证由使用非空模板的通用/provider 测试覆盖。

完整恢复统一预检已通过 Blackboard，随后在 `MobaContinuousModifierCaptureRollbackProvider`（key `10031`）因修饰器实例成员变化拒绝。本场景观测到三个新 BuffContinuousRuntime，配置 `10010000`、source ID 4/5/6；场景包含默认其他英雄，不能将它们视为 Annie Q 的专属效果。导入数为 0，帧号、HP、资源及 Blackboard 未被部分改写。Buff lifecycle provider 尚未执行导入，完整 rollback/replay 仍未通过。

本轮重新编译的验证结果：Triggering 全套 26/26；MOBA 定向回归 150/150（其中新 provider 测试 18 项）。定向范围含 `MobaOwnerBlackboardMembershipRollbackTests`、`MobaLogicWorldStateImportSmokeTests`、`MobaBuffTimerRollbackPreflightTests`、`MobaBuffContextRecoveryTests`、`MobaModifierAttributeSourceTests`、`MobaEntitasRollbackTests`、`MobaSkillContextRollbackTests`。日志与边界见 48 号实施记录。

49 号联合恢复方案已获确认，范围不扩大召唤物、订阅重建或全局恢复事务。后续按该方案统一解决 Buff/Continuous 来源身份及 Context/父技能 retain 时序，再继续同输入重演及状态对比。

## 联合恢复第一批：捕获和只读准备（2026-11-10，历史记录）

[49 号方案](49-MobaSkillLifecycleRollbackPlanProposal.md) 已进入实施。本批新增版本化联合 checkpoint 和只读 `Prepare`：Buff 本地信息保存来源 ID、retain、来源视图、定点计时及暂停状态，父技能和引用按目标快照交叉校验。构造目标 Continuous 仅发生在隔离对象中；不存在 live 注册、context 创建、source 分配和正常生命周期事件。独立 provider 的 strict 路径不变。

新预检测试 30/30；连同前批范围的定向回归 180/180，均重新编译通过。真实 Annie Q 的施法前目标在命中后的 World 中通过联合预检；飞行中目标在捕获时通过，前后导出状态相同。额外查明飞行中暂存两个 Projectile actor（9/10），命中后消失，因而该目标在命中后按同 roster 边界拒绝。

联合提交、失败补偿及正式 registry 单一所有权尚未接入；当前完整恢复仍在 key `10031` 拒绝，P2 仍未通过。下一批先实现同 roster 的联合提交/补偿及 registry 切换，再升级真实 Q 恢复断言并继续同输入重演。跨 roster 的飞行中目标不属于本批扩大范围。完整命令、证据和边界见 49 号实施记录。

## 联合恢复第二批：完整恢复前置条件通过（2026-11-10）

正式 registry 已切换到 `MobaSkillLifecycleRollbackProvider`（key `10011`），统一拥有 Context、SkillRuntime、Buff/Continuous、TriggerModifier、Pipeline 和 owner Blackboard 的本地恢复。版本 2 envelope 共十个必需 section；旧 Context payload 及第一批版本 1 checkpoint 拒绝混用。准备、提交和失败补偿的实际顺序、来源身份与 retain、订阅引用和投影恢复详见 [49 号实施记录](49-MobaSkillLifecycleRollbackPlanProposal.md)。

真实场景 `MobaLogicWorldStateImportSmokeTests.Annie_q_precast_checkpoint_restores_complete_lifecycle_through_formal_registry` 已由预期失败升级为成功：正常初始化、捕获施法前检查点、执行 Q 并验证伤害/消耗，然后经正式 coordinator 完整恢复；帧号、HP/法力、roster、Blackboard 和联合十个 section 与 checkpoint 一致。重复恢复及下一帧检查通过，恢复期间没有额外 execution Ended，旧 Q runner 不残留。修复了 Entitas 组件替换被误作 group 真移除、从而错误结束被动执行根的问题。

本次重新编译的 MOBA 定向回归 **205/205**（含联合准备/提交/补偿 46 项），Context **32/32**，Continuous **3/3**。六阶段失败注入覆盖局部补偿，补偿失败明确报告停止 replay。飞行中 checkpoint 在捕获时有效；命中后因 Projectile actor 集合变化仍拒绝恢复，未扩大同 roster/同非 actor owner 与订阅集合的边界。

**P2 仍未通过。** 本批证明完整恢复这一前置条件；尚未证明重放同一份 `PlayerInputCommand` 后的确定性状态/hash 相等，也未完成可靠事件去重及差异 artifact。下一批依次记录输入并原样重放、比较终态并输出差异、验证既有可靠事件消费去重。Unity 表现与状态同步断线重连均不计入本批结果。

## 同输入重放批次：真实差异与新增状态缺口（2026-11-10，历史记录）

新增 `MobaRecordedInputReplayTests.Annie_q_same_input_replay_reports_unrestored_combat_activity_as_p2_failure`，保留默认完整 World、真实安妮 Q 与目标。checkpoint frame 19，保存的输入 frame 23，命中 frame 25，终点 frame 79。输入文件包含原始 payload、玩家、操作码与原始帧，以及同 prediction key 的重复包；两次重演都从同一文件读取，使用现有 `IWorldInputReplaySink` 的正式生命周期，不重新生成另一套命令。每次先扰动 HP，再用正式恢复入口回退，单次 Q 伤害、终点 HP/法力均与基线一致。

**正向状态等价检查实际失败。** 两次比较均只在已登记 registry 的 key `10011` 下发现 `continuous` / `continuous-local` 差异：目标的脱战 TriggerIntervalContinuous 缺失。最近战斗时间未恢复，独立 `IWorldClock` 也未回退；第一次恢复后 World clock 仍为 `2.6306655`，checkpoint 值为 `0.63066673`。它与已回退的 Context/FrameTime 是不同的时钟，不能混为一谈。

基线与重演的原始事件数分别为 5 与 8，Counter cue Started 变为 StackChanged，新增三条 Tag Started，cue 序列继续累加。网络可靠事件的 battle/epoch/sequence 重传去重与模拟重新生成事件的去重是不同验收；本批没有以网络单测替代后者。

当前新增用例是缺口复现回归，精确断言遗漏状态和差异，**不将它通过记作 P2 通过**。报告输出 `StateMismatch`、`stateEqual=false` 和 `reliableEventDeduplicationVerified=false`。待 50 号范围明确确认并实现后，升级为完整字节/hash 相等断言，再处理仍存在的事件差异。

落盘检查还修复了已有 `WorldRollbackSnapshotCodec` 的条目未声明 MemoryPackable 的问题；没有新增字段/版本。真实 checkpoint/baseline/perturbed/replay 的 `.bin` 均验证解码重编码字节相同。

artifact 默认输出到测试程序集目录的 `artifacts/moba-recorded-input-replay`，可通过 `MOBA_REPLAY_ARTIFACT_ROOT` 指定目录。本轮输出为 `local/Logs/moba-recorded-input-replay/`：`inputs.json`、`checkpoint.bin`、`baseline.bin`、`perturbed-1/2.bin`、`replay-1/2.bin`、各联合 section 的原始字节、`baseline-events.json` 与 `report-1/2.json`。报告包括输入 SHA-256、registry SHA-256、roster、缺失持续体、时钟/战斗时间及原始事件，不丢弃有差异字段。

重新编译的验证：MOBA 定向回归 **206/206**（205 项前批回归＋1 项缺口复现），帧同步包 **21/21**（含新 codec 往返用例），既有 `MobaSynchronizationFaultMatrixTests` **7/7**（网络可靠游标/基线故障矩阵，不能替代模拟重演去重）。日志为 `%TEMP%/moba-replay-regression-tests.log`、`%TEMP%/moba-replay-framesync-tests.log`、`%TEMP%/moba-replay-delivery-tests.log`。P2、Unity 表现和服务端恢复均未验收通过。50 号新增领域状态恢复需按机制准入确认后继续。

## 50 号实施：同输入战斗状态一致（2026-11-10，最新）

用户确认配套方案后，正式 registry 新增 `10045` 最近战斗记录与 `10046` WorldClock 的本地回滚。coordinator 对声明的 required-state 在任何导入前检查条目完整性，旧快照缺失对应状态直接拒绝。`WorldClock.RestoreState` 保留 float 位模式；战斗记录整表恢复保持零时间与未记录的区别。全部改动使用通用状态，不依赖英雄/技能身份，未修改英雄生产配置或网络协议。

本批还修复既定资源投影契约的遗漏：资源 map 恢复后调用现有 `MobaResourceAttributeContextProjector.Refresh`，防止资源依赖 Modifier 仍读取上一轮的 `resource.rage.ratio`。补齐两项 provider 后曾实际出现 Q 同帧伤害 `56.666668→56.478405`，通过原始伤害与解码资源记录定位并修复；未忽略 HP/资源差异。

`MobaRecordedInputReplayTests.Annie_q_same_input_replay_restores_exact_registered_state` 现为两组正向真实 World 测试，分别 `7→8` 和 `8→7`。每组保留默认六个 actor，再正常初始化额外 actor；checkpoint 19、输入 23、命中 25、终点 79。重复输入包走既有 journal，每次只产生一次 Q 伤害；连续恢复和重演两次。恢复后立即重捕获的完整 snapshot hash、重演终态的全部 provider payload、联合十个 section、伤害帧/数值、HP/法力、时钟和战斗时间均与自己的基线一致，未缺失持续体。每组单独保留原始输入和报告，避免不同 actor 组合覆盖 artifact。

四份新报告均为 `StateEqual_EventDeliveryPending`，`stateEqual=true`、`reliableEventDeduplicationVerified=false`。表现事件仍为基线 5 条、重演 8 条；Counter Started/StackChanged、Tag 通知及序列差异保留原样。完整已登记战斗状态相等不代表所有运行时状态已覆盖，更不代表可靠事件或 Unity VFX 验收通过。

| 重新编译的验证范围 | 结果 | 日志 |
|---|---|---|
| MOBA 原定向回归＋新时钟/战斗状态边界＋两组真实重演 | **236/236** | `%TEMP%/moba-clock-regression-tests.log` |
| 资源/Modifier 真实对象冒烟 | **12/12** | `%TEMP%/moba-clock-resource-smoke-tests.log` |
| FrameSync 包，含 required-state 和部分快照兼容性 | **25/25** | `%TEMP%/moba-clock-framesync-tests.log` |
| World DI 包 | **39/39** | `%TEMP%/moba-clock-world-di-tests.log` |
| 既有网络故障矩阵 | **7/7** | `%TEMP%/moba-clock-delivery-tests.log` |

复核命令（顺序执行，避免共享构建目录争用）：

```powershell
$env:MOBA_REPLAY_ARTIFACT_ROOT = 'C:\Workspace\gitProject\AbilityKit\local\Logs\moba-recorded-input-replay'
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --no-restore -v:q --filter 'FullyQualifiedName~MobaCombatClockRollbackTests|FullyQualifiedName~MobaRecordedInputReplayTests|FullyQualifiedName~MobaSkillLifecycleRollbackPreparationTests|FullyQualifiedName~MobaOwnerBlackboardMembershipRollbackTests|FullyQualifiedName~MobaLogicWorldStateImportSmokeTests|FullyQualifiedName~MobaBuffTimerRollbackPreflightTests|FullyQualifiedName~MobaBuffContextRecoveryTests|FullyQualifiedName~MobaModifierAttributeSourceTests|FullyQualifiedName~MobaEntitasRollbackTests|FullyQualifiedName~MobaSkillContextRollbackTests|FullyQualifiedName~MobaPassiveSkillLifecycleServiceTests|FullyQualifiedName~MobaSkillPipelineProviderSmokeTests'
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --no-restore -v:q --filter 'FullyQualifiedName~MobaModifierRealObjectSmokeTests'
dotnet test src/AbilityKit.World.FrameSync.Tests/AbilityKit.World.FrameSync.Tests.csproj --no-restore -v:q
dotnet test src/AbilityKit.World.DI.Tests/AbilityKit.World.DI.Tests.csproj --no-restore -v:q
dotnet test src/AbilityKit.Demo.Moba.View.Runtime.Tests/AbilityKit.Demo.Moba.View.Runtime.Tests.csproj --no-restore -v:q --filter 'FullyQualifiedName~MobaSynchronizationFaultMatrixTests'
```

最新 artifact 位于 `local/Logs/moba-recorded-input-replay/source-7-target-8/` 和 `source-8-target-7/`。每组保存 `inputs.json`、`checkpoint.bin`、`baseline.bin`、`perturbed-1/2.bin`、`replay-1/2.bin`、十个 section 的原始字节、各快照解码 HP/资源 JSON、`baseline-events.json` 及 `report-1/2.json`。旧根目录的 `StateMismatch` 报告是上一批历史证据，不能混作本批结果。

下一步依次处理表现生产端的可回退状态、Tag 比较基线与通知时序、持久 cue 对账和瞬时事件稳定身份/提交，再验证既有网络游标下的实际消费次数。50 号末尾列出了只读查证和待制定的通用契约；新增表现恢复/重演语义须单独确认。**P2 整体仍未通过；本批未运行 Unity、服务端重连或跨 roster 恢复验收。**

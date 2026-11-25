# MOBA 技能生命周期联合回滚方案

状态：已确认；联合捕获、预检、提交、组内补偿及正式 registry 迁移已完成。50 号配套时钟/最近战斗记录恢复已确认并完成，两组 Annie Q 同输入重演各两次的完整 registry 字节/hash 一致。表现事件仍有差异，P2 未通过。日期：2026-11-10。

确认依据：用户在上一轮收到本文具体方案及确认问题后，回复“按推荐的下一批继续”。批准范围为本文的 MOBA 局部联合生命周期恢复组、本地 envelope/registry 所有权迁移、组内预检和补偿；同 roster、同非 actor Blackboard owner/订阅集合边界继续生效，不扩大网络恢复协议或全局事务。

## 需求与已验证事实（方案提出时的历史基线）

本方案继续处理帧同步 P2 的真实技能 rollback/replay，不是状态同步断线重连。48 号 actor Blackboard 恢复已在确认范围内实现，不需要重复确认。

真实用例 `MobaLogicWorldStateImportSmokeTests.Annie_q_blackboard_preflight_passes_but_continuous_membership_blocks_full_restore` 证明：Annie Q 正常施放、飞行中快照捕获、伤害和法力消耗均发生；恢复施法前 checkpoint 已通过 Blackboard 预检，但在 key `10031` 拒绝，错误为 `Continuous modifier capture membership changed since snapshot.`，导入数为 0。

本场景含默认其他英雄；观察到的新增对象为三个 `BuffContinuousRuntime`，Buff 配置 `10010000`，Owner/Target 分别为 1、4、8，ModifierSourceId 为 4、5、6。这里记录的是具体复现证据，不将这些 ID 用作运行时分支，也不把它们全归因于 Annie Q 自身机制。

## 为什么需要联合处理

| 消费者 | 当前契约/缺口 | 影响 |
|---|---|---|
| `RollbackCoordinator.TryRestoreCore` | 所有 provider 先独立预检，之后结构导入、普通导入；预检拿不到其他 provider 的目标状态 | 不能以“稍后 Buff 会恢复”为由取消当前集合校验 |
| `MobaContinuousModifierCaptureRollbackProvider.ReadAndValidate` | 一般 Continuous 身份必须与 live 集合相同；仅已支持的 TriggerInterval 有自身计划路径 | Buff 在预测期间增加/结束时，会合法地改变该集合 |
| `MobaBuffStateRecoveryProvider.ParseAndValidate` | 父技能 handle 要在 live runtime 中存在 | checkpoint 父技能需要恢复、当前已经结束时，独立预检无法证明目标引用合法 |
| `MobaBuffStateRecoveryProvider.ApplyPrepared/RestoreEntry` | 清除 Buff 后重建 Continuous，再重新 RetainChild | 目标 retain 已在 SkillRuntime snapshot 中时，需明确复用/重绑定，避免重复 retain |
| `MobaBuffStateRecoveryEntry` | 仅保存 HasContinuous；不保存 Continuous ModifierSourceId、stable captures、定点计时与暂停状态 | 单独重建会重新分配 source ID，可能无法匹配 key `10031` 的 checkpoint capture |
| `MobaContinuousLifecycleBinder` | 注册时分配来源 ID，来源 ID 在生命周期内不可任意重赋；allocator 不能回退到活跃来源之前 | 必须在注册前确定恢复身份；不能先随机注册再改 ID 或忽略碰撞 |
| `MobaRuntimeContextService.RestoreRollbackEntityCursor` | 先裁剪预测 context/binding；Buff 的清理/重建在其他 provider 中执行 | 清理旧对象与恢复新引用的顺序需要一起定义，不能靠调整一两个 key 假定安全 |
| `SkillCastCoordinator` runner rollback | runner 恢复需先有父 runtime、phase execution context；旧 runner 需要正常 detach | Pipeline 也属于该生命周期的引用持有方，不能遗漏 |

其中 key `10031` 的 membership 拒绝是已实际复现的阻断；其余是已查明的字段/调用依赖，尚未经过本场景成功全链导入验证，不能写成已复现失败。

## 推荐机制：MOBA 局部生命周期恢复组

不删除校验，不在技能或 Buff 执行链中识别英雄，也不为 Annie 单独编写恢复算法。

### 所有权与兼容

新增 MOBA 的联合生命周期 rollback provider，复用现有领域服务，将 RuntimeContext、SkillRuntime/ExecutionContext、Buff、Continuous captures/allocator、TriggerModifierContinuous 和 SkillPipeline runner 的本地恢复组织为同一个版本化恢复组。现有 provider 保留其独立用途和严格路径，正式 MOBA rollback registry 对这些状态只登记一份所有权，禁止独立和联合 provider 重复导入。

候选落点为 `MobaSkillLifecycleRollbackProvider` 与内部只读 `MobaSkillLifecycleRestorePlan`。联合 provider 可复用当前 Context 的装配位置/key `10011`，但 payload 必须有新的明确版本和组类型；这不是沿用旧 Context 字节格式。registry 的 required-state 检查同步改为验证该组及其必需 sections，旧本地历史不能自动解释为新组，切换配置时清空/拒绝旧历史。准确类型命名服从代码风格，边界不能缩减成“只跳过 membership”。

本组仅用于本地 rollback。状态同步现有网络 payload、重连协议和服务端恢复入口不在此次改动范围；可复用解析/构建函数，但不能静默改变它们的契约。

### 准备阶段：以完整目标状态验证

1. 捕获显式 named sections 及版本，不依赖 live 对象枚举顺序。关联使用已有完整语义键：Buff 的 Target/BuffId/SourceActorId/SourceContextId，技能的 runtime ID/generation/root context，Continuous 的 Owner/ModifierSource/Target。
2. 本地 Buff rollback 补充其 Continuous 来源 ID、stable captures、定点计时及暂停/生命周期状态；按模板解析 Modifier 定义，不把运行时引用序列化进去。新字段归本地组 envelope，避免误改状态同步 DTO。
3. 非变异地交叉验证目标 actor roster、配置/schema、context 拓扑、父技能及其 child/retain、runner phase、来源唯一性/allocator 上界和各 projection 的来源归属。父技能在目标快照中存在且契约匹配即可，不要求它仍活在当前世界。
4. 只允许通过明确 Buff/TriggerInterval/TriggerModifier 生命周期来源恢复的实例增删。其他普通 Continuous 仍要求成员不变；非 actor Blackboard owner/订阅仍沿用 48 号限制，变化时拒绝。
5. 生成隔离的准备对象与清理/重建计划，校验过程不创建 live context、不注册 Continuous、不分配新 source、不发布玩法或表现事件。

### 提交阶段：先清理旧引用，再恢复身份和绑定

1. 在旧上下文和父技能仍有效时 detach 旧 runner、解除旧 Buff/Continuous/retains；使用恢复专用的既定清理路径，不发送正常施放/移除效果及可靠表现事件。
2. 裁剪预测 context，恢复目标 RuntimeContext/ExecutionContext、父技能与 allocator。目标 retain 表与 Buff/runner 的重新绑定必须一一对应，复用目标身份，不能额外递增 retain 游标。
3. 用目标 source ID 在注册前构建 Buff/Continuous，恢复计时、captures 与暂停状态，再绑定 context 和父技能，最后接回 runner。
4. 重建来源投影并校验其与目标 ledger 一致；清除已撤回来源的数值、标签、槽位候选和 HFSM 参数贡献。恢复过程中不 Tick、不触发正常新增/结束事件。
5. 该组需要进入既有结构恢复阶段：先由 actor 结构 provider 确认相同 roster，再执行生命周期组，之后由普通 actor/经济/参数等 provider 恢复值。必须用实际 registry 顺序验证组件恢复不会覆盖重建绑定，不能只凭 key 大小证明正确。
6. 组内提交失败补偿该组持有的引用、成员、allocator 和投影；补偿失败明确报告不可继续。其他 provider 之前/之后的恢复仍可能失败，保留 coordinator 的部分恢复状态并停止 replay，不承诺整 World 原子事务。

这是目标契约，不能直接把现有 `ClearAllBuffs + EnsureActive` 拼接成实现就认定满足。若实现查证发现组外还有必要的生命周期持有方，先补交新增范围，而非暗中扩大组。

## 范围及替代方案

本批仍限定相同 actor roster、相同非 actor Blackboard owner/订阅集合。召唤物创建销毁、任意订阅重建、其他普通 Continuous 的工厂恢复、全局 rollback 事务、网络重连和视图表现恢复不在批准请求中。

| 方案 | 取舍 |
|---|---|
| 推荐：MOBA 局部联合恢复组 | 依赖在组内可见，保留通用 coordinator 契约；需要本地 envelope/registry 所有权迁移和组内补偿 |
| 扩展通用 coordinator，增加跨 provider 的目标快照与计划依赖协议 | 更适合多 demo 统一需求，但本次影响更广，暂不作为默认路线 |
| 只绕过 Continuous 校验，等待 Buff provider 修复 | 没有来源身份/retain/allocator 保证，不采纳 |
| 移除默认英雄或 Buff，只测无生命周期变化的场景 | 可以作为额外最小基线，不能替代本场景，也不能作为本缺口已解决的证据 |

## 实施与验收顺序

1. 先定义组内 snapshot/plan 契约及 registry 单一所有权，再写捕获和无副作用预检；验证缺 section、版本错误、损坏 parent/retain、source 冲突和模板变化均在导入前拒绝。
2. 接入有/无目标 Buff、原 Buff 结束、新 Buff 增加、暂停/恢复、多个同类不同来源的恢复；比较 ID、generation、定点计时、captures、allocator 与引用绑定，重复导入不得增加 retain 或副作用。
3. 对清理、构建、注册、绑定、投影阶段注入失败，证明组内补偿；独立测试另一组不同配置/actor ID，证明通用语义。组外依赖失败保持明确拒绝。
4. 将现有真实场景的预期 key `10031` 拒绝升级为成功恢复：HP/法力、actor roster、Blackboard、Buff/Continuous、runtime/runner 和帧时钟均与 checkpoint 一致，再 Tick 检查是否被旧对象覆盖。
5. 在完整恢复成功后，重放同一份记录的 `PlayerInputCommand`，比较确定性状态和可靠事件去重；产出 checkpoint、输入和状态差异 artifact。这一步通过之前，P2 仍标记未通过。

## 已确认决策

采用上述 **MOBA 局部联合生命周期恢复组**，允许扩展本地 rollback envelope、registry 组所有权及组内恢复/补偿，覆盖 Context、SkillRuntime、Buff/Continuous 来源身份、TriggerModifier 与 Pipeline 的既定关联；同时保留同 roster/同非 actor owner 集合边界，先通过真实 Annie Q 恢复，再继续同输入 replay 验收。

依据 `Docs/MobaMechanismGovernance.md` 和 `.claude/skills/moba-skill-authoring/SKILL.md`，这新增了跨域恢复语义，超出 48 号已批准的存储层成员恢复；现已取得对此方案和范围的明确确认，按以上边界实施。

## 第一批实施结果：联合捕获与只读预检（2026-11-10，历史记录）

本批完成捕获和准备入口，**尚未实现联合提交/补偿，也未将新组登记到正式 registry**。`MobaSkillLifecycleRollbackPreparation` 不是 import provider；返回的 `MobaSkillLifecycleRestorePlan` 只持有隔离后的 checkpoint 和计数，不代表已恢复 World。后续提交必须重新检查 live 前置条件。既有独立 provider 的校验和登记继续生效，因此完整恢复仍在 key `10031` 拒绝，P2 未通过。

### 实现与数据契约

- `Rollback/MobaSkillLifecycleRollbackCheckpoint.cs`：版本 1 的 `moba-skill-lifecycle` 本地 envelope，保存 frame、完整 actor roster 和九个 named sections：`context-entities`、`execution-contexts`、`skill-runtime`、`buffs`、`buff-local`、`continuous`、`trigger-modifiers`、`pipelines`、`owner-blackboards`。拒绝未知/重复/遗漏 section、必需字段缺失、重复 JSON 属性、尾随数据及超出 16 MiB 的 checkpoint。
- 执行节点复用既有 MemoryPack DTO；Buff 来源使用显式本地 DTO，避免 JSON 默认构造只读结构后把节点/来源还原成零值。网络 Buff DTO、版本和恢复入口不变。
- `Services/Buffs/MobaBuffLocalRollbackPreparation.cs`：补齐 Buff 的 retain ID、ContextSource、Continuous source ID、总时长/elapsed/interval 的 Q32.32 raw、Active/Paused 状态及配置指纹。检查 runtime/Continuous/repository 的双向所有权，孤立注册对象不能由恢复组自动认领。目标 Buff 按目标 parent/child/retain 验证；当前父技能已结束或 Buff runtime context 已销毁不再是该准备路径的拒绝依据。未持有 retain 的技能句柄只保留溯源含义。
- `Rollback/MobaSkillLifecycleRollbackPreparation.cs`：交叉检查执行拓扑、幸存节点的稳定身份、runtime/retain 游标、技能 root/child、局部节点 section 的完整作用域、Buff context、runner parent/phase 及 Blackboard。仅为目标 Buff 构造隔离的 Continuous；不注册 live 对象、不分配 ID、不创建 context、不 Tick，不触发正常新增/结束事件。plan 保存和取出的字节均复制。
- `MobaContinuousModifierCaptureRollbackProvider.ValidateLifecycleImport`：以目标 Buff 替换计划集合中的当前 Buff，再验证 captures 的 stable definition key、source 唯一性/归属及 allocator 上界；普通 Continuous 成员仍执行原来的严格集合校验。独立 `ValidateImport` 不使用这一目标集合路径。

本批没有按英雄或技能身份添加执行分支。测试中的 Annie/技能/Buff ID 仅用于场景装配和真实结果定位；两个不同 source/target actor 组合走相同预检算法。

### 重新编译的验证

| 范围 | 结果与边界 |
|---|---|
| 新 `MobaSkillLifecycleRollbackPreparationTests` | 30/30；含 Active/Paused、定点计时与来源 roundtrip、目标 Buff 新增/当前 Buff 已结束、目标父技能已结束/context 已销毁、plan 字节隔离、无事件/无状态变异、损坏依赖/非法 envelope、普通 Continuous 严格集合与孤立 Buff 所有权保护 |
| MOBA 定向回归合计 | 180/180，含新 30 项及前批 150 项。重新编译，未使用 `--no-build` |
| 真实 Annie Q | 施法前 checkpoint 在命中后的 World 中通过联合只读预检；飞行中 checkpoint 在捕获时通过联合只读预检。两处预检前后导出字节一致；Q 的伤害与法力消耗正常发生 |
| 真实 World 完整恢复 | 仍由独立 provider 执行，预期在 key `10031` 的 Continuous membership 预检失败；导入数 0，HP/法力/帧号/Blackboard 不变 |
| Unity / 同输入重演 / 可靠事件去重 | 本批未验收，不计作 P2 完成 |

命令：

```powershell
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --no-restore -v:q --filter 'FullyQualifiedName~MobaSkillLifecycleRollbackPreparationTests|FullyQualifiedName~MobaOwnerBlackboardMembershipRollbackTests|FullyQualifiedName~MobaLogicWorldStateImportSmokeTests|FullyQualifiedName~MobaBuffTimerRollbackPreflightTests|FullyQualifiedName~MobaBuffContextRecoveryTests|FullyQualifiedName~MobaModifierAttributeSourceTests|FullyQualifiedName~MobaEntitasRollbackTests|FullyQualifiedName~MobaSkillContextRollbackTests' --logger 'console;verbosity=normal'
```

日志：`%TEMP%/moba-lifecycle-regression-tests.log`。真实 Console World 初始化涉及共享注册表/池，新测试 collection 禁止并行初始化，避免测试进程中的集合并发写；此安排不改变 World 或同步机制。

### 新确认的真实场景边界

飞行中 checkpoint 的 roster 为 1–10，命中后为 1–8。测试逐一检查新增 actor 9/10 的 `EntityMainType`，两者均为 Projectile；场景包含默认其他英雄，不能认定两者都来自 Annie Q。飞行中快照在捕获时可通过预检，但命中后准备该快照必须因 roster 改变而拒绝。这不是解除同 roster 限制的授权，也不能通过少记两个 actor 或跳过集合校验解决。

施法前 checkpoint 与命中后 World 的 roster 相同，仍可在既定范围内优先推进联合提交与恢复验收。跨投射物 actor 增删的恢复属于后续边界；若要扩大，应先审查已有 actor/projectile provider 的结构恢复责任，再补具体方案。

### 当时的下一批顺序（已由下述第二批落实）

1. 接入旧 runner/Buff 的恢复专用 detach、目标执行身份/allocator 恢复、精确 retain 重绑定和 Buff/Continuous 重建；对非 actor 订阅幸存引用保持及 projection ledger 做完整提交校验，不能直接拼接网络 `ClearAllBuffs + EnsureActive`。
2. 加入组内阶段失败注入与补偿，再把正式 registry 切为单一组所有权；覆盖结构恢复、组件恢复与投影恢复的实际顺序，禁止新旧 provider 同时导入同一状态。
3. 先把同 roster 的真实 Annie Q 施法前恢复从“预期拒绝”升级为成功，再接记录输入重演和状态差异 artifact。跨 roster 的飞行中恢复保持显式拒绝，未经新方案确认不扩展。

## 第二批实施结果：联合提交、补偿与正式登记（2026-11-10）

已新增 `MobaSkillLifecycleRollbackProvider`（key `10011`）和 `MobaSkillLifecycleRollbackCommit`。正式 MOBA registry 对组内状态只登记联合 provider，不再同时登记独立 Context、SkillRuntime、Buff、Continuous、TriggerModifier、Pipeline 和 owner Blackboard provider；缺少完整服务的最小测试 World 保留独立路径，正式战斗的 required-state 校验要求联合组并拒绝重复所有权。独立 provider 的网络用途保持原契约。

### 当前本地快照契约

envelope 升为 **版本 2**，保留前批九个 section，增加必需的 `continuous-local`，共 **十个 section**。新 section 保存 Continuous 的类型/Owner/Id 身份、注册顺序、Active/Paused/Inactive、elapsed/interval raw、标签规则暂停归属及配置指纹；Pipeline 的配置由 runner checkpoint 校验。版本 1 和旧 Context payload 明确拒绝，不能混用旧本地历史。此修改不新增网络恢复 DTO。

准备阶段构造隔离的 Buff、TriggerInterval 和 TriggerModifier，交叉检查目标来源、父技能/retain、执行上下文、模板与配置、投影来源及 allocator；普通 Continuous 仍只恢复现存实例，不提供通用重建工厂。Commit 紧邻变更前再次预检。

### 提交与失败语义

提交静默 detach runner、撤回旧投影，恢复 Context/ExecutionContext、SkillRuntime、精确 Buff retain、repository 和服务索引，再恢复 Continuous 注册顺序、runner、幸存订阅绑定、Blackboard、capture/projection ledger 与标签暂停归属。幸存订阅持有的 Blackboard 引用保持，门控重新绑定到恢复后的实例。恢复不执行正常 Buff 移除效果、retain 释放、Continuous 新增/结束事件；标签有效值缓存失效并重设表现 cue 比较基线，避免下一次事件与撤回前的缓存比较。

Detached、Built、Registered、Bound、逐实例 Projected 和 Reconciled 阶段具有内部失败注入点。失败补偿该组先前的实例/引用、context 结构和快照历史、runtime/retain、索引、顺序、allocator、Blackboard 及投影；投影测试覆盖部分投影已提交的失败。补偿也失败时明确抛出聚合错误并要求停止 replay。组外 provider 的失败仍由 coordinator 报告部分恢复，不承诺整个 World 的原子性。

实际全链验收还发现：Entitas 替换组件时会在实体仍属于 group 的情况下发送 Removed/Added，旧 `ReactiveWorldSystemBase` 因而把恢复 SkillLoadout 的替换误作生命周期移除，结束被动执行根。现按既定 membership 契约检查 `group.ContainsEntity(entity)`，仍在组内的替换交给已有 `OnComponentReplaced`；真实移除继续清理。真实安妮场景验证恢复期间没有额外 execution Ended，十个 section 字节均与 checkpoint 一致。

### 重新编译的验收证据

| 范围 | 结果与边界 |
|---|---|
| `MobaSkillLifecycleRollbackPreparationTests` | 46/46；含前批预检、两个 actor/source 组合的 Buff 恢复、精确 retain/raw timing、存活订阅引用及门控重绑、运行中 Pipeline、TriggerModifier 参数投影/allocator、TriggerInterval 来源/计时/暂停/顺序/索引、六阶段失败补偿、补偿失败与 Context 快照历史补偿 |
| MOBA 定向回归合计 | **205/205**；含上述 46 项及 Blackboard、真实 World、Buff、投影、Entitas、SkillContext、被动生命周期与 Pipeline 回归 |
| Context / Continuous 通用包 | **32/32、3/3**，顺序重新编译执行 |
| 真实 Annie Q | `Annie_q_precast_checkpoint_restores_complete_lifecycle_through_formal_registry`：正常施放并命中后，经正式 coordinator 恢复施法前 checkpoint；HP/法力、roster、Blackboard、帧号和联合十个 section 一致；重复恢复、下一帧对齐通过，无旧 Q runner 残留，无恢复期间 execution Ended |
| 飞行中 checkpoint | 捕获有效；命中后因 Projectile actor 集合改变仍明确拒绝，未放宽同 roster 边界 |
| Unity / 同输入重演 / 可靠事件去重 | 本批未验收；P2 仍未通过 |

复核命令：

```powershell
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --no-restore -v:q --filter 'FullyQualifiedName~MobaSkillLifecycleRollbackPreparationTests|FullyQualifiedName~MobaOwnerBlackboardMembershipRollbackTests|FullyQualifiedName~MobaLogicWorldStateImportSmokeTests|FullyQualifiedName~MobaBuffTimerRollbackPreflightTests|FullyQualifiedName~MobaBuffContextRecoveryTests|FullyQualifiedName~MobaModifierAttributeSourceTests|FullyQualifiedName~MobaEntitasRollbackTests|FullyQualifiedName~MobaSkillContextRollbackTests|FullyQualifiedName~MobaPassiveSkillLifecycleServiceTests|FullyQualifiedName~MobaSkillPipelineProviderSmokeTests' --logger 'console;verbosity=normal'
dotnet test src/AbilityKit.Context.Tests/AbilityKit.Context.Tests.csproj --no-restore -v:q
dotnet test src/AbilityKit.Continuous.Tests/AbilityKit.Continuous.Tests.csproj --no-restore -v:q
```

本次日志：`%TEMP%/moba-lifecycle-final-regression.log`、`%TEMP%/moba-lifecycle-context-regression.log`、`%TEMP%/moba-lifecycle-continuous-regression.log`。涉及 Console World 静态注册表/池的测试放在禁止并行的同一 collection。所有结果均重新编译，没有使用 `--no-build`；未修改英雄生产配置或发布产物以规避验收。

### 推荐下一批：真实同输入重放及差异证据

1. 从同 roster 的施法前 checkpoint 开始，记录并重用同一份 `PlayerInputCommand`（frame/player/opcode/payload），走正式帧推进和输入入口；对照未扰动的同输入基线。
2. 比较重放终点的确定性状态/hash，输出 checkpoint 帧、输入、恢复前后状态、section/实体差异与 verdict；差异必须可定位，不能仅报告恢复成功。
3. 核查既有可靠事件标识和消费去重路径，验证重放不会重复提交不可逆事件；若需要超出已确认范围的新事件/协议语义，先查证并提方案，不借测试静默扩展机制。

上述同输入、状态和事件证据通过后才能把 P2 标记通过；服务端权威快照恢复、断线重连及跨 roster 恢复仍按各自范围推进。

## 下一批实际结果：同输入重放已接通，发现组外状态遗漏（2026-11-10，历史记录）

按上述顺序执行了真实安妮 Q 输入落盘、固定帧重演两次、registry 原始字节/hash 比较和事件采集。正常伤害/资源结果相同，完整 registry 比较仍失败：本组 `continuous` / `continuous-local` 的目标脱战持续体缺失。查明 `MobaCombatActivityService` 的最近战斗记录和独立 `IWorldClock.Time/DeltaTime` 尚未登记本地回滚，恢复后仍沿用上一段模拟的时间事实。它们属于组外新增恢复所有权，未擅自加入本组或放宽比较。

表现原始事件也存在 5→8 条及 Counter cue 阶段/序列差异；未将网络重传去重误记为模拟重演去重。已完成范围、原始 artifact、复现回归和后续确认边界见 [47 号当前记录](47-MobaRealSkillRollbackReplayAcceptance.md) 与 [50 号配套时钟/战斗状态恢复方案](50-MobaCombatActivityRollbackProposal.md)。当前 MOBA 206/206、FrameSync 21/21 测试通过包含**已知缺口复现测试**，不构成 P2 通过。

## 配套恢复后的结果（2026-11-10，最新）

用户批准的 50 号已完成。世界时钟与最近战斗记录使用独立 provider，本组不新增 section 或扩大生命周期范围。真实重演进一步暴露资源 map 回退后遗漏派生属性上下文投影，本批通过既有资源投影器恢复既定契约。两组 source/target 组合各重演两次，全部已登记 provider payload 和联合十个 section 与基线字节相同，时钟、伤害与资源相同，持续体不再缺失。

重新编译的 MOBA 定向 **236/236**、资源/Modifier 冒烟 **12/12**、FrameSync **25/25**、World DI **39/39**、网络故障矩阵 **7/7**。最新原始证据和命令见 [47 号最新记录](47-MobaRealSkillRollbackReplayAcceptance.md)。事件阶段/数量/序列仍有差异，报告为 `StateEqual_EventDeliveryPending`，P2 整体未通过。下一步需明确表现生产/提交的重演契约，不回退已对外提交的可靠投递游标来掩盖差异。

## 后续已批准的受管 Motion 恢复（2026-11-17）

用户确认的 `plans/MobaManagedMotionRecoveryDecision.2026-11-17.md` D1–D4 已实施。生命周期 envelope 为 v4，并拒绝 v3；`continuous-local` 为存活的 Motion Continuous 记录来源类型、现有可回滚 ModifierSourceId 及 FixedDelta 精确值。没有 Modifier 的 Motion 也保留来源号，完全相同业务身份的新实例不能冒充旧实例。预检要求原始来源仍活动并留在创建时的 owner Pipeline，比较不可变来源配置；提交只恢复 raw 剩余时间与活动位，不触发 Activate、碰撞、落地或结束回调。来源值跟随联合生命周期提交和组内补偿。

验收包括 raw 精度、重复恢复、终点最后一步、优先级竞争与显式撤销、无效晚条目零导入、移除/结束/替换/不支持来源拒绝，以及七阶段注入失败后的状态和绑定补偿。详细证据与限制见 `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`。正式 Gragas R 的整世界 8 帧重演仍在被动连续体重同步时差异化结束三条战斗外门控间隔状态；这不属于上述确认范围，需单独确认 [被动 Continuous 重绑定方案](../../../../plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md)。

# MOBA actor Blackboard 生命周期回滚方案

状态：已确认，限定范围已实现并通过定向验收；完整 P2 尚未通过。日期：2026-11-10。

确认依据：用户在询问本方案属于帧同步回滚还是状态同步重连、得到“先完成帧同步 P2，后推进 P3/P4”的说明后，回复“先按推荐的顺序进行完善”。本次确认覆盖本文的同 roster、同非 actor owner/订阅集合的 actor Blackboard 成员恢复能力；不扩展到召唤物增删、订阅重建或全局恢复事务。

## 目标与本批边界

让真实 MOBA World 在 actor roster 不变、被动/Continuous/订阅 owner 集合不变时，可以从施法后的 checkpoint 恢复到施法前：撤回预测期间懒创建的 actor Blackboard，恢复已释放的 actor Blackboard，并恢复其字段值。用于继续推进 Annie Q 的真实 rollback/replay 验收，算法不识别英雄或技能身份。

这是新增恢复语义，不是配置修正。按上面的用户确认范围实施；此前泛化的“继续”不单独作为机制批准依据。

## 实施前事实与冲突

| 权威消费者/契约 | 已查证行为 | 对恢复的影响 |
|---|---|---|
| `Unity/Packages/com.abilitykit.triggering/Runtime/Blackboard/Resolvers/IBlackboardResolver.cs`：`OwnerBlackboardStore` | `GetOrCreate` 懒创建 owner；`TryValidateSnapshotSet` 要求当前与快照 owner 集合完全相同；恢复只向既有 resolver 写值 | 当前没有“恢复 owner 成员”的接口，不能直接改成忽略集合差异 |
| `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/Execution/MobaEffectExecutionService.cs`：`BeginExecutionSession` | 外层效果执行无显式 Blackboard 时，以 `SourceActorId` 获取或创建 actor Blackboard | 正常施法就可能改变 owner 集合，不是 Annie 专属行为 |
| `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaOwnerBlackboardRollbackProvider.cs` | 注释和实现均明确不推断 owner 创建/销毁；预检用当前生命周期源校验待恢复快照 | 将过去快照与现在集合比较会拒绝正常的预测成员变化 |
| `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaRollbackRegistryBuilder.cs` | owner 来源包括 actors、passive、subscriptions、continuous；actor 来源只报告已经存在于 store 中的 actor ID | ownerKey 仅是数值，不能仅凭正负、大小或恰好等于 actor ID就决定所有权 |
| `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/MobaTriggerPlanSubscriptionService.cs` | owner-bound 注册持有 resolver；`Stop` 等路径释放 owner Blackboard | 全量替换 store 内全部 resolver 会使存活订阅持有旧引用 |
| `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackCoordinator.cs` | 先对全部 provider 预检，再执行结构与普通恢复；导入失败报告 world 可能部分恢复 | 不能将依赖未来恢复状态的校验直接搬到 Import 后就宣称安全或原子 |

实施前用例曾验证真实施法、HP/法力变化和施法中快照捕获，恢复在 key `10008` 预检处拒绝。本文实现后的真实结果见下文；旧阻断描述仅记录问题来源。

## 推荐方案：显式的 actor 成员恢复能力

### 数据和接口

1. 保留通用 `TryRestoreSnapshotSet` 的“相同成员、只恢复值”语义，避免影响其他 demo 和调用方。新增显式 opt-in 的成员恢复能力：先构建并验证恢复计划，再应用计划；具体命名在实现中遵循仓库风格。
2. MOBA provider 使用带版本的本地 rollback envelope，携带 Blackboard snapshot set、checkpoint actor roster 与 owner 来源分类。只标记由 actor 独立持有的 owner 为本批可撤回/重建对象；来源重叠或不明时拒绝，不以 ownerKey 数值猜测分类。
3. actor roster 在预检时必须与 checkpoint 相同。本批不扩大 actor spawn/despawn 的恢复能力。非 actor owner 集合与分类也必须一致；不自动创建订阅、被动或 Continuous。
4. 旧格式只允许沿用严格恢复路径，不能被隐式解释成可重建格式；不承诺跨程序版本的 rollback 历史兼容，也不改变网络协议或服务端重连快照。

### 准备、提交和引用生命周期

1. checkpoint 捕获只读记录已有 owner，不为了让快照通过而预创建所有 actor Blackboard。
2. preflight 完整验证版本、唯一 owner/board、字段类型、初始化模板一致性、roster 与来源分类。缺失 owner 的 board 使用隔离 resolver 按现有初始化模板验证，不能调用 live `GetOrCreate` 产生副作用。
3. 对幸存 owner 原位恢复值，保留 resolver 和 board 引用；对 checkpoint 已有但当前缺失的 actor owner，按模板准备 resolver 后恢复值；对 checkpoint 没有、预测期间新增的 actor owner，撤回该 store 成员。
4. 只有可确认不被当前订阅/被动/Continuous 持有的 actor owner 才允许撤回或新建。不能以 store 改动代替订阅解绑，也不能让存活 callback 指向被替换的 resolver。
5. provider 内的提交失败应恢复其原成员和值；其他 provider 的整体事务性不在本批扩展范围，仍保留 coordinator 的失败状态和停止重演要求。预检无法保证安全的依赖必须继续拒绝，不能静默跳过。
6. 本批限定同 roster、同订阅集合，因而不需要通过调整 provider key 来掩盖目标 actor 尚未恢复的问题。完成后重新检查 Export 的生命周期一致性，确认实际 store 与来源记录一致。

## 为什么不选其他做法

| 选择 | 取舍 |
|---|---|
| checkpoint 前预创建所有 actor Blackboard | 改变正常懒创建生命周期，还不能覆盖释放、召唤物、订阅来源；只改验收初始化会隐藏真实缺口 |
| 删除 owner 集合检查，只恢复交集 | 预测 owner 与字段残留，不能保证下一次施法或重演一致 |
| 每次恢复全量替换 resolver | 存活订阅会持有旧引用；需要更广的订阅重建契约 |
| 一次完成所有 owner、Buff、召唤物及跨 provider 事务 | 范围显著扩大，不适合作为本批前置条件；先用明确限制的 actor 恢复能力验证路径 |

## 影响与机制映射

| 部分 | 落点与权威 | 判定 |
|---|---|---|
| owner 数据和 resolver 成员 | Triggering Blackboard store；只管理存储，不执行战斗效果 | 已确认并实现：显式成员恢复能力 |
| actor 来源分类、roster 和版本 | MOBA owner Blackboard rollback provider 与 registry 装配 | 已确认并实现：本地快照字段和恢复边界 |
| 施法与效果 | 现有 HFSM / Flow / Trigger；调用方继续使用既有 Blackboard 接口 | 不新增玩法算法，不按英雄身份分支 |
| 发布与资源 | 无 Excel、技能配置、Trigger Authoring 或 Unity/Console 发布物修改 | 本批无发布流程变更 |
| 验收 | 存储边界单测与真实 Console MOBA World 用例 | 本批存储/provider 验收通过；完整恢复仍有下游阻断 |

## 验收顺序

1. store：空集合、actor owner 增加/释放后恢复、值恢复、幸存 resolver/board 引用稳定、重复恢复；失败时成员和值不变。
2. 拒绝场景：重复/未知 owner、重复 board、字段类型不符、模板不符、来源冲突、roster 或非 actor owner 集合改变。预检不能改变 live store。
3. MOBA provider：完整捕获/恢复/再捕获，确认结果与 checkpoint 一致；未知格式继续拒绝；缺失依赖不能被“默认值”吞掉。
4. Annie Q：撤销本用例当前 key `10008` 的预期拒绝断言后，验证真实恢复是否通过，恢复后帧时钟、HP/资源、Blackboard 成员和值与 checkpoint 一致。若下一 provider 失败，保留具体失败事实，不将其算作通过。
5. 恢复全链通过后，重放同一组已记录的 `PlayerInputCommand`（含 frame/player/opcode/payload），而非重新生成命令；比较 HP/资源、Buff、Counter、冷却、skill runtime、临时实体和相关确定性状态，并输出差异 artifact。事件去重和临时实体覆盖仍属于 P2 完整验收要求。

## 已查到但本批不自动扩展的依赖

- Buff staged recovery 当前在 preflight 查询活跃 skill runtime，而目标 snapshot 的 runtime 可能需之后恢复。不能简单放宽该检查。
- runtime context provider 会先撤回预测 context；较晚的 Buff 重建仍需审查清理旧 runtime、技能 retain 复用及 Continuous 绑定的先后关系。
- owner-bound callback 的订阅集合恢复、actor/召唤物生命周期和整体恢复事务需要独立契约。本批不把这些缺口默认批准为新增机制；若实际运行被其阻断，提交证据和新增决策。

以上是代码风险核查，尚未用成功的全链恢复证明其必然触发。

## 实施结果与验证

实现落点：

- `OwnerBlackboardMembershipRestore.cs` 新增 `IOwnerBlackboardMembershipSnapshotStore.TryPrepareMembershipRestore` 和一次性 `IOwnerBlackboardMembershipRestorePlan.TryApply`；旧严格接口保持原契约。准备阶段隔离验证模板、成员和字段，保留 DTO 副本；提交检查成员引用及模板是否改变，幸存 resolver/board 原位写值，失败时恢复提交前成员和值。
- `IMobaActorOwnerKeySource.CopyActorIds` 提供显式 roster；registry 的 actor owner 源实现该接口。版本 1 本地 envelope 携带 actor roster、owner 来源和 Blackboard snapshot；旧 JSON 沿严格路径读取。非 actor 来源不变，重叠来源的 owner 只允许原位恢复，不允许按 actor-only 重建。
- 提交后的生命周期检查仍保留。共享算法不识别 Annie 或任意技能 ID；英雄 ID 只用于真实场景装配及验收定位。

实际测试：

| 验收层 | 结果 |
|---|---|
| `AbilityKit.Triggering.Tests` 全套 | 26/26，其中新成员恢复测试 13 项；包含空 checkpoint、增删和值恢复、引用保持、损坏快照、模板变化和中途失败补偿 |
| `MobaOwnerBlackboardMembershipRollbackTests` | 18/18；包含来源/roster/重叠保护、非法 envelope、旧格式与重复恢复 |
| MOBA 定向回归合计（包含上行） | 150/150；覆盖真实 World、帧时钟、Buff context/旧 timer、Modifier 来源、Entitas 和技能上下文 |
| 真实 Annie Q | 施法前与飞行中捕获通过；命中、HP/法力变化通过；Blackboard 预检通过；独立导入前快照确实撤回 Annie 的懒创建 actor owner，再导入后快照恢复该 owner，导出字节分别相同 |
| 完整 World 恢复与重演 | 尚未通过；在 key `10031` 的 Continuous membership 预检拒绝，导入数为 0；帧号、HP、法力和 Blackboard 保持恢复尝试前的值 |

真实场景在 spawn 后先走一次正常 tick，让 passive 订阅完成初始化；没有预创建 actor Blackboard。当前生产模板的 owner boards 为空，因此真实场景证明成员生命周期，字段值和 board 引用由非空模板的存储/provider 测试覆盖。

本批使用重新编译的 .NET 测试，不是 `--no-build` 复跑。日志：`%TEMP%/triggering-membership-full-tests.log`、`%TEMP%/moba-membership-final-tests.log`。Unity Editor 表现、同输入 replay、可靠事件去重不属于本次已通过结论。

## 新暴露的后续边界

用例现名为 `Annie_q_blackboard_preflight_passes_but_continuous_membership_blocks_full_restore`。捕获后出现三个带 Modifier 的 `BuffContinuousRuntime`（本场景 source ID 为 4/5/6，Buff 配置 `10010000`），与 checkpoint 的实例集合不同。该场景还含默认 roster，不能将这些 Buff 都归因于 Annie 自身技能。非 actor Blackboard owner 集合不变，与 Continuous 修饰器实例集合不变是两种不同约束。

本批没有放宽 key `10031` 的校验，也没有证明 key `10030` 的 Buff lifecycle 全链导入成功。后续联合处理 Buff/Continuous 与上下文、父技能 retain 和来源 ID 的目标快照依赖，具体已确认方案及首批捕获/只读预检结果见 [49 号方案](49-MobaSkillLifecycleRollbackPlanProposal.md)。本文原批准保持有效，不重复请求 actor Blackboard 方案确认。

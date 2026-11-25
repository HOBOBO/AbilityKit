# 状态同步包阶段评审：Source SDK 与 Colyseus

**结论：保留现有分层，先减少接入方需要理解和组合的概念，再决定是否扩大公开 API。** `world.statesync` 已足够支撑本地状态历史、实体检查点与离线预测试点；它不是完整的复制会话，也还没有生产级预测回滚。本文承接[分层审查](14-StateSyncLayeringReview.md)、[能力补全路线](15-StateSyncCapabilityRoadmap.md)和 [D2 预测提案](13-StateSyncReplicationAndPredictionProposal.md#d2只预测本地受控移动另行确认)，不更改既有批准范围。

## 外部对照

| 参考 | 值得借鉴 | 不照搬 |
| --- | --- | --- |
| [Valve Source SDK 2013：客户端预测](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/client/prediction.cpp)、[实体插值](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/client/c_baseentity.cpp) | 本地预测按命令保存和重演，权威更新后以确认进度校正；`IsFirstTimePredicted` 区分首次执行和重演。远端实体的时间插值与本地输入重演分开。AbilityKit 应复用普通移动规则，但分离预测步进和外部效果提交。 | 不移植 SDK、FPS 命中延迟补偿或整世界回滚。Source 的命令确认是其自身合同，不证明 AbilityKit 的输入已终结。 |
| Colyseus：[State Sync](https://docs.colyseus.io/state)、[StateView](https://docs.colyseus.io/state/view)、[Client Prediction](https://docs.colyseus.io/netcode/client-prediction) | 用清晰的服务器写入边界和客户端观察入口降低接入成本；初始全量、后续补丁、发送周期、按观察者可见视图各有职责。固定步长预测、权威纠正和待确认输入重演是显式能力，不等于用了 Room/Schema 就具备预测回滚。 | 不引入 Room/Schema 依赖，不用累计补丁替换 `Delta(B, F)`。可见性先做纯投影，不把离开视野等同销毁，也不默认每个观察者持有独立基线。 |

两者可分别作为**预测命令生命周期**和**状态复制的接入体验**参考。只读公开源码与文档即可，本阶段不需要克隆；不测试参考项目，不比较网络流量或载荷。

## 当前设计的优缺点

| 范围 | 已有优势 | 当前限制 |
| --- | --- | --- |
| 框架包运行时 | [程序集](../../../Unity/Packages/com.abilitykit.world.statesync/Runtime/com.abilitykit.world.statesync.asmdef)不引用 Unity Engine；`SnapshotBuffer` 有界且复制读写对象，可以复用存储后端。`StateManager` 把本地逐实体检查点与复制快照分开。 | `TryRestore` 找到检查点不等于所有实体都恢复；按现有 ID 查注册实体，不验证生命周期代际。`GetFullState` 不是完整权威玩法状态。`InputBuffer` 保存命令引用，调用方仍负有不可变约束。 |
| 复制与重建 | 固定基线按 `Delta(B, F)` 从已提交全量 B 重建候选，不依赖上一个增量。准入、重建、玩法导入和表现分工清楚。 | 新接入方仍要理解通用管线与 MOBA 适配各自的准入和恢复策略。会话层两个提交点都完成后才可交付已提交结果；当前联动夹具不是生产事务，也不是可复用产品入口。 |
| 对账与预测 | P0 `LocalMovementReconciler` 显式区分无输入 Tick 和覆盖缺失，绑定作用域、提交修订与终结证明；重演失败不撤销权威结果。 | `PredictionCoordinator`、`ClientPredictionModule`、P0 易被误认为是可互换入口。旧协调器不能用空批次区分缺失和无输入；实体模块不重演。P0 尚无产品侧同截面终结来源和最小移动投影接线。 |
| 帧同步与发布 | 帧同步和状态同步可以共享业务规则、固定步长实现和可复制值，不必维护两套移动规则。包已有文档、示例与 .NET 验收。 | 帧同步命令确认与世界检查点不能充当状态同步快照游标或输入终结证明。公开 API 基线、隔离安装和 Unity Player 仍须作为独立发布门禁。 |

证据边界：本地状态提交结果测试覆盖双提交、部分提交失败、全量门禁和会话切换；这是单线类型化验证，不是生产并发、线上复制或实际 MOBA 回滚验收。[据点投影](15-StateSyncCapabilityRoadmap.md#框架包第二种类型化投影接入验收)是复制管线的第二种接入证据，不是 P0 的第二个消费者。包级细节见[稳定性评审](../../../Unity/Packages/com.abilitykit.world.statesync/Documentation~/StabilityReview.md)与[离线提交契约](../../../Unity/Packages/com.abilitykit.world.statesync/Documentation~/LocalCommittedStateResult.md)。

## 推荐的抽象调整顺序

1. **先做一个窄的离线参考接入，不增公开类型。** 以普通移动和第二种类型化状态分别演示 `权威投影 → 准入与重建 → 导入与提交 → 已提交结果`。预测是可选消费者，输入终结证明独立传入；无证明仍可消费权威值。先复用已有类型和测试替身，验证新接入不需要依赖 MOBA、帧同步驱动或 `StateManager`。若两种接入确实重复，才考虑抽取测试内帮助类型；不从单一实现提前推出 `IStateSyncManager` 一类大接口。
2. **再收敛读写边界，不强行合并三个预测入口。** 兼容类型与签名保留；新示例推荐现有快照历史、检查点和显式对账三条路径。等第二个真实需求出现，再评审是否把「只读已提交结果」提炼成窄的值契约。候选、已提交结果、终结证明及预测结果不能互换；复制层拥有恢复意图，会话负责执行，对账器不写回世界或触发全量请求。暂不调整历史默认 128 帧及 P0 的 32 Tick / 64 条输入上限，模拟 Tick、快照间隔、全量周期和预测容量分别配置。
3. **按瓶颈决定扩展方向。** D1.2 从纯对象可见视图及共享基线成本对照开始，区分不可见、退场与销毁，不先引入每人基线。D2 先落实同一权威截面的终结证明、隔离移动状态和可复算依赖，再评审接入本地受控移动；其他玩法、整世界回滚和线上联调不随之启用。这两条路线不互为前置。

## 离线参考接入

[参考测试](../../../src/AbilityKit.World.StateSync.Tests/Samples/LocalProjectionReferenceTests.cs)用现有 `ClientSnapshotSyncPipeline<T>` 走真实准入与游标提交，用测试内的值投影分别模拟移动和据点。重建使用已提交的原始全量值，不能拿上一增量或导入后调整过的值当基线；只有管线应用成功后才交付导入结果。普通移动可选择把该结果交给 P0，终结证明另传；据点只读取提交结果，不装配预测。导入失败和旧代际均不交付。

本轮有实际复用证据的是测试内的接入顺序：投影各自提供重建和导入，剩余步骤共用；尚无第二个生产消费方，不提取公开 API。测试没有接入 MOBA、FrameSync 驱动或检查点，也不覆盖生产端部分写入、异步提交或线上链路。任何生产接线、恢复语义变更或 MOBA 新机制另行确认。本地验收不抓包、不解析网络载荷、不启动网络服务、不检查项目外进程或对象。

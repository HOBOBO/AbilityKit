# 状态同步下一批优化：可恢复增量与局部预测

> 2026-11-21 · 用户回复“按推荐的下一批继续”，确认 **仅实施 D1**。D2 仍待确认。本文补充 [03.2 帧同步与状态同步预测回滚](03.2-FrameStatePredictionRollback.md)。

## 借鉴哪些设计

- Colyseus 0.18 的 [State Sync](https://docs.colyseus.io/state) 将权威状态、首次全量、字段级补丁分开；[StateView](https://docs.colyseus.io/state/view) 为客户端过滤可见状态。借鉴投影和编码的边界，但不照搬逐补丁消费方式：AbilityKit 会合并待发增量。首批不改可见范围，未来为不同视图维护各自基线，并区分离开视野与销毁。
- Colyseus 的 [Client Prediction](https://docs.colyseus.io/netcode/client-prediction) 用固定步长、权威覆盖、重演待确认输入和远端插值。借鉴流程，不引入 SDK，不复用 FrameSync 整 World 回滚，也不混入服务端命中延迟补偿。Unity Netcode for Entities 的 [Prediction edge cases](https://docs.unity3d.com/Packages/com.unity.netcode@1.5/manual/prediction-details.html) 指出部分实体回滚可能跨 Tick 读状态；首例限制为不依赖动态实体的本地移动。

## 源码风险

- `MobaBattleProtocolMapper.CreateStateSyncPushFromProjection` 在**构建**时推进上一份投影缓存，定向全量同样经过该共享缓存；`BattleLogicHostGrain.CoalescePendingSnapshot` 和 `StateSyncObserverGrain` 可替换待发增量。
- `StateSyncPush` 没有明确的基线 ID；`MobaSnapshotAdmission` 的帧差门槛不是基线校验。例：F100 位置为 0，F101 变成 1，F102 不变。如果 F101 被跳过，F102 可能仍能应用，却不包含这次变化。此为源码推导，尚未端到端复现。
- `SubmitBattleInputHandler` 入口成功不代表命令已执行并反映在快照里；可靠事件水位也不是输入确认。`PredictionCoordinator` 可复用克隆和重演，`ClientPredictionModule` 不是网络对账器，见 03.2 §4.1。

## D1：让增量可跳包（推荐下一批）

- **协议：** 显式协商新版本。以 `(WorldId, StateStreamGeneration, SnapshotId)` 标识发布；`Frame` 表示模拟时间，增量带 `BaseSnapshotId`。保留实体级 Upsert/Remove，逐项核查玩法扩展负载可否跳包。旧客户端走旧协议，不改默认 FrameSync 或可靠事件路由。
- **发布：** 广播流保留不可变全量 B；同周期每包都是 Delta(B, F)，而非 Delta(F−1, F)。定向全量请求合并、限频，在下一允许的发布点轮换共享全量，不用只发给一个人的全量改写共享基线。累计增量接近全量时可提前轮换，超预算不截断为可导入包。
- **应用：** 分开保存基线和最近应用的权威投影。校验流身份、版本、精确基线和发布顺序，从 B 重建目标后一次提交。覆盖上一帧会漏掉回归基线值、以及基线后创建又消失的对象。缺基线不部分应用，合并请求全量；用全量自身的事件水位恢复，不用增量跳过可靠事件。
- **取舍：** 全量-only 作正确性对照与降级，代价是带宽。链式增量仅加基线 ID 能检测缺口，却仍可能频繁重同步。先不做字段掩码、量化、AOI 或逐客户端快照 ACK；共享基线成本过高时再评估按观察者的已确认基线编码。

## D2：只预测本地受控移动（另行确认）

- **确认：** 按控制者和输入 epoch 提供与权威状态同 Tick 的终结水位；仅跨过连续的已生效或明确终局拒绝的输入，不跨过入队、临时失败或缺口。若高序号可先反映在权威状态，单一连续水位不足，需另议稀疏结果方案。
- **资格：** 两端同一固定步长及普通移动规则；核查位置、速度、朝向、移动约束及环境依赖可否独立导入复算。动态碰撞、控制或受管位移依赖不完整时走权威；不预测远端、最终命中、伤害、Buff 和技能结算，不按英雄/技能 ID 特判。
- **对账：** 静默覆盖权威移动状态，裁剪已终结输入，从权威 Tick 后重演剩余命令。保留空 Tick 和稳定顺序；已过原预测 Tick 的待确认命令不得直接丢弃，也不得重演到基线之前。无法安全重排、历史越界或控制权变更时退回权威。重演不重复发包、结算或派发 Cue。

## 验收与决策

- **D1：** 端到端走 Orleans → Gateway → 客户端；任意跳过中间增量，重建仍与同 Tick 全量逐 Actor、逐字段等价。覆盖两客户端定向重同步、删除/回归基线、丢全量、乱序、换流、断线和事件水位。健康网络/弱网分别对比旧链、固定基线和全量-only 的字节率、构建/应用 P95、分配量及重同步耗时。
- **D2：** 覆盖入队未执行、终局拒绝、空 Tick、重复乱序、历史越界、控制权切换、客户端停止预测后恢复；记录纠偏幅度、重演 Tick/耗时和降级原因，无重复副作用。

**待确认：** D2 等 D1 链路证据和移动状态账本齐备后单独确认。

## D1 实施记录（2026-11-21）

后续审查与待确认的收敛方案见 [状态同步分层审查](14-StateSyncLayeringReview.md)。

- 新客户端仅在房间声明外层 schema 4 时经 123 显式订阅；旧 103 及快照外壳不变。固定头与原玩法负载同包，保留原 schema 和 payload opcode，避免把 Character HFSM 的内层版本与流版本混用。
- 服务端在正常 Tick 按房间广播固定基线，定向重同步请求合并，距离上一次全量至少约四分之一秒时可提前轮换；例行全量或高变更量的全量回退不受请求节流限制。客户端先验证、完整重建，再应用；恢复窗口清除基线但保留已提交发布游标。
- `AbilityKit.Orleans.MobaSmoke --sync-template moba-state-sync-authority --probe-fixed-baseline` 已在真实 TCP 双客户端通过：基线 8、跳过 9 后收到 10、共同轮换到 15；随后旧 schema 1 与固定基线订阅并存。客户端测试另外验证了缺基线、旧流、回归基线和短命实体清理。该烟测不是弱网带宽/分配量对照矩阵；Unity EditMode 因现有工程锁未运行。

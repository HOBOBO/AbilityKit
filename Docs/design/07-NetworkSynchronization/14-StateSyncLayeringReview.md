# 状态同步审查：参数、复制与重建

审查范围：本仓库当前源码、协议目录与本地可控测试。本文审查 MOBA 固定基线状态流（D1），不改变旧状态流、FrameSync、可靠事件或玩法机制。两处提交顺序修复经确认实施，见文末；其他改造仍待确认，不授权 D2。

## 结论

保留 D1 的基本分层：权威世界提取当前投影，复制流用固定全量 B 生成可跳包的 `Delta(B, F)`，接收端先重建 F，再由玩法层导入。该算法适合共享视图和待发增量合并，但生成、可发布、已应用是三种不同的状态，目前没有在提交边界上完全区分。优先补齐这些契约，再调整归属；不建议把预测放进解码器、重建器或快照队列。

## 参数从哪里生效

| 所有者 | 当前有效值与单位 | 消费点与审查意见 |
| --- | --- | --- |
| 房间/战斗时钟 | 房间传入 `TickRate`，非正值回退 30 Hz。 | [房间启动映射](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Rooms/OrleansRoomBattleStartMapper.cs#L55)、[Host 初始化](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L171)。`Frame` 是模拟时间，不是快照编号或输入确认。 |
| 玩法同步模板 | MOBA 状态流每 1 模拟帧发快照、每 30 模拟帧例行发全量。模板与策略构造器都对无效输入回退 1/30。 | [模板](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplay/ServerGameplayModuleCatalog.cs#L417)、[发布策略](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/Server/BattleHost/BattleSnapshotSyncPolicy.cs#L5)、[调用处](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L852)。两个条件是独立的绝对帧取模；例如 4/30 的例行全量落于 60 的倍数，而非间隔 30 帧。当前 1/30 不受该问题影响，不能把它当任意可组合配置。 |
| 共享基线轮换 | 请求全量时合并请求；阈值在 Host 写死为 `max(1, TickRate / 4)`，整数除法，30 Hz 时 7 帧；还需等到发布点。例行全量、首包和体积启发式触发不受这条限频控制。 | [调用处](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L1079)、[轮换逻辑](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/FixedBaselineSnapshotStream.cs#L20)。`13 × Upsert 数 + Remove 数` 是字段数权重，不是实际序列化字节数；不能作为硬预算。 |
| 每观察者发送策略 | `AbilityKit:StateSyncObserver`：16,384 B/s、32 KiB 桶、32 项、500 ms 队龄、10 ms drain。空环境及 `limitedbw` 保留配置；其他非空环境禁用此处限速。 | [默认配置](../../../Server/Orleans/src/AbilityKit.Orleans.Host/appsettings.json#L25)、[选项映射](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/StateSyncObserverOptions.cs#L57)、[出队](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Runtime/Sync/SnapshotSendQueue.cs#L229)。`BurstBytes` 是出队令牌桶参数，不是消息上限；超桶大小项并非因此拒绝。环境名在这里隐式覆盖限速，新环境容易意外继承旁路行为。 |
| 消息预算 | 本仓库 room 协议对全量/增量消息体均限制 4 MiB。固定头为 36 B；协议 helper 用同一数值约束内嵌 `Payload`，Observer 再用相同数值约束完整序列化消息体。 | [协议目录](../../../Protocols/Catalogs/room.protocol.yaml#L308)、[固定头](../../../Unity/Packages/com.abilitykit.protocol.room/Runtime/Room/FixedBaselineStateSyncProtocol.cs#L21)、[完整消息预算](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/StateSyncObserverGrain.cs#L147)。两者范围不同，内层通过不代表外层通过。 |
| 接收准入 | 旧链默认最大增量间隔 120 模拟帧；固定基线链以流、快照 ID 和精确基线为准，不用 120 帧判断可否重建，仍拒绝帧倒退。 | [MOBA 准入](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Networking/Replication/MobaSnapshotAdmission.cs#L42)、[通用准入](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Runtime/Sync/AuthoritativeSnapshotAdmission.cs#L89)。间隔是旧复制策略参数，不是未来预测历史窗口。 |

## 分层与优先问题（修复前）

当前路径是 `MobaBattleRuntimeAdapter` → `FixedBaselineSnapshotStream` → `StateSyncObserverGrain` → `GatewayStateSyncSnapshotMapper` → `MobaAuthoritativeSnapshotState` → `AuthoritativeStateRecoveryRuntime`。保留投影与复制、线格式与玩法导入的分离：固定基线投影捕获不推进旧链缓存；Mapper 解开外层流 schema 4 后传下内层玩法 schema，客户端玩法准入校验的是后者（当前支持 0–3）。两者现有 `projectionSchemaVersion < SchemaVersion` 的数值关系只是当前实现的耦合点，不应被当作新玩法版本的演进规则。见 [投影捕获](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplays/Moba/Battle/MobaBattleRuntimeAdapter.cs#L430)、[解码](../../../Unity/Packages/com.abilitykit.demo.moba.share/Runtime/Game/Flow/Battle/Presentation/ProtocolAdapter/GatewayStateSyncSnapshotMapper.cs#L43)、[内层版本校验](../../../Unity/Packages/com.abilitykit.protocol.room/Runtime/Room/FixedBaselineStateSyncProtocol.cs#L56)。

1. **P1 · 完整消息预算检查晚于共享基线提交。** [`Encode`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/FixedBaselineSnapshotStream.cs#L43) 已推进基线、全量帧/ID 并清掉全量请求；[`OnSnapshotPushedAsync`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/StateSyncObserverGrain.cs#L147) 才能因完整消息超预算而拒绝，Host 对该结果[只记录](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L1338)。据此可推导：新 B 未能发布，而随后体积较小的增量仍可能引用 B。建议在发布方明确“构建候选 → 用当前序列化器确认完整消息预算 → 提交可发布快照”；Observer 仍拥有各自队列、丢包及补全策略，不要求所有观察者送达才提交。不合尺寸的全量不能偷偷切片或标为有效，后续分片需另行设计。
2. **P1 · 准入游标先于业务导入和复制提交。** [`HandleSnapshot`](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/StateSync/AuthoritativeStateRecoveryRuntime.cs) 对固定基线先 `PrepareFixed`，再 `Admit` 推进 `LastAcceptedFrame`，最后才导入/应用世界、采用可靠事件基线和 `CommitFixed`。`RequireFullBaseline` 只撤销 baseline 标记而不回退该帧。应用失败时，较早但比最近成功应用更新的全量可能被失败帧的游标挡住；恢复成功和解锁也早于 `CommitFixed`。这不是已证实的世界不可恢复：现有失败路径会请求新全量。建议明确拆开可拒绝的检查与已应用提交，在世界导入和必要事件基线采用成功后统一推进已应用/复制游标，再对外发布恢复与插值；失败保持恢复中，并验证部分应用后的重置，不承诺通用 World 事务回滚。
3. **P2 · 超突发桶快照的限速契约不一致。** [队列](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Runtime/Sync/SnapshotSendQueue.cs#L229) 允许超桶大包在令牌满桶时发送，但按实际字节扣减；通用队列测试要求偿还全部欠额，[Observer 测试](../../../Server/Orleans/src/AbilityKit.Orleans.Grains.Tests/Battle/StateSyncObserverDrainPolicyTests.cs#L157) 却预期扣减成本以桶容量封顶。在当前测试配置下，一次 100 KiB 发送后，下一份 32 KiB 全量需等约 6.25 秒才能出队，而用例预期 2 秒。先明确限速精度与恢复延迟的取舍，再对齐测试与实现；不能靠调大桶掩盖合同差异。

## 改造建议

- **先补边界和测试，不改协议（已实施）。** 修正上述两处提交时机，区分不可编码、整包超限、观察者队列拒绝、缺基线和导入失败；保留旧订阅与可靠事件语义。保留全量/增量启发式作软判断，最终预算只认当前 codec 输出；是否共享已编码结果，以本地分配与 CPU 数据决定。
- **再收敛配置权威。** 发布节奏、例行全量与请求合并由战斗复制策略拥有；Observer 只拥有交付预算。分别标注模拟帧、墙钟毫秒和字节。例行全量建议定义为距上次可发布全量满 N 帧后的首个发布点；替代方案是保留绝对帧取模并限制两个周期整除。两者行为不同，不在此次审查里代选实现。
- **小步提取复制状态机，不抽玩法。** 当前服务端编码器硬编码 Actor 字段等价，客户端重建器同时保存 Actor 集合和流游标；新增字段、其他投影或多个可见视图都会扩大改动面。先在现有实现内分开流身份/顺序/基线与 Actor 差分/物化，稳定后有第二个真实消费者再考虑上移到 `com.abilitykit.network.runtime`。采用显式的流版本、玩法投影版本和语义验证；HFSM、世界导入、可靠事件应用仍归玩法适配层。不要假定所有新增玩法负载都可跳包，也不要把同房间共享基线直接复用于不同的可见集合。
- **D2 单独推进。** 预测消费成功提交的权威移动状态与同 Tick 的输入终结信息，在独立的对账/重演层运行。不要拿 `SnapshotId`、可靠事件水位或仅入队成功代替输入确认。现有 Actor 展示投影也不是可直接恢复的预测账本，详见 [D2 方案](13-StateSyncReplicationAndPredictionProposal.md)。

## 下一批验收用例

- 用当前 codec 在本地生成临界大小的全量与增量，区分内嵌负载合法但整包超限的情形；被拒绝的候选 B 不得成为后续增量的引用。两观察者中一方队列丢基线，另一方仍可应用，且补全路径可观察。
- 注入世界应用失败、导入失败及事件基线采用失败，确认不推进已应用游标，不报告恢复、不解锁、不发布插值；后续可接受全量能恢复。验证同帧更高发布 ID、旧流晚到与任意跳过中间增量。
- 用非整除周期（如 4/30）、不同 TickRate、突发补全与限速/不限速环境验证实际节奏。分别记录整包字节、编码/应用 P95、分配量与补全耗时；未跑本地弱网对照前，不对最优参数或吞吐作结论。

首次审查用当前源码、禁用还原重跑聚焦测试：通用准入/队列 22 项通过，固定基线客户端重建 3 项通过，服务端固定基线/Observer 参数与出队 23 项通过、1 项失败（上述 `OversizedSnapshot_DoesNotCreateDebtBeyondBurstBudget`）。

## 本地故障注入验证

- **服务端已复现。** [固定基线发布用例](../../../Server/Orleans/src/AbilityKit.Orleans.Grains.Tests/Battle/FixedBaselineSnapshotStreamTests.cs) 先将第 10 帧全量交给 Observer，再用当前编码器生成第 17 帧候选全量，使内嵌负载恰好为 4 MiB，但整包超限。Observer 返回 `Failed`；第 18 帧的小增量可被接收进队列，却引用被拒绝的第 17 帧基线，而不是第 10 帧。用例直接调用实际编码器和 Observer 入口，不启动 Orleans 集群或传输。
- **客户端准入与重建冲突已复现。** [无头组件用例](../../../src/AbilityKit.Demo.Moba.View.Runtime.Tests/FixedBaselineSnapshotReconstructionTests.cs) 用当前 codec 生成测试消息，覆盖已有基线的增量应用失败和无已提交基线的全量导入失败对应的游标路径。第 12 帧通过准入但不提交重建，进入恢复后 `LastAcceptedFrame` 仍为 12；第 11 帧全量通过 `PrepareFixed`，却被 `Admit` 判过期；第 13 帧全量可以准入并提交。
- **验证范围。** 服务端固定基线 3 项、客户端组件 5 项通过；新增用例刻画当前行为，修复时应改成断言目标契约。另补了 [Unity 编排层用例](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Battle/Networking/Replication/AuthoritativeStateRecoveryRuntimeTests.cs)，覆盖世界应用失败、导入失败和事件基线拒绝。禁用包管理的 EditMode 运行在工程编译阶段遇到缺失的包引用，三个编排层场景尚未执行。未做多观察者故障注入、弱网性能矩阵或限速契约调整。未修改运行时、协议或参数。

## 提交顺序修复记录

同步模式与推送能力是两回事。`BattleLogicHostGrain` 在不同帧时钟入口复用同一个 `_runtimeSession.Tick` 契约；`SupportsStateSyncPush` 也可覆盖由帧同步驱动的世界。正常 MOBA 客户端只在非帧同步输入路线启用固定基线订阅。因此，下述修复归属快照发布和权威状态导入，不改变帧同步的输入批次、时钟或检查点重演；反过来，帧同步的序号也不能替代快照基线或状态同步的输入终结水位。

- **发布端。** Host 先补全事件水位等元数据，再由固定基线流用与 Observer 相同的序列化器校验完整消息。超限候选不推进发布 ID、基线、最近全量帧，也不清除已有刷新请求。无刷新请求时，后续可用增量仍引用最后一个可发布的基线；有请求时，下一个满足条件且通过预算的全量可以完成轮换。Observer 保留交付端防线；发布预检增加每个固定基线候选一次序列化，不共享缓冲区，不等待各观察者送达。
- **接收端。** 固定基线先重建、再只读检查准入；世界应用（或导入及必要的事件基线采用）成功后，才提交准入游标和复制状态，再报告恢复、解锁与发布插值。失败后仍可用晚于最后成功帧、早于失败帧的有效全量恢复。旧路径仍用原 `Admit` 语义；不改变可靠事件的消费规则，也不承诺通用世界事务。
- **验收。** 本地源码聚焦测试：通用准入/队列 23 项、客户端重建 5 项、服务端固定基线/Observer 27 项通过。回环 TCP 双客户端烟测通过，覆盖跳过增量、共享全量轮换和旧 schema 1 并存。Unity EditMode 编排用例已更新，受前次离线工程包引用阻塞，本批未验证；未测弱网性能，不处理 P2 的桶欠额契约或 D2 预测。

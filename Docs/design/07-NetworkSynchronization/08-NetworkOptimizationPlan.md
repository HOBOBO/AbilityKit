# 08 · 多人联网模块优化计划

> 基于 2026-08-09/10 的全层核校与 P2.2 迁移整理。列出剩余优化项，按 **价值/成本/阻塞** 排序，便于选择性推进。每项标注前置依赖与测试安全网。
>
> 相关：接入配方见 [07-MultiplayerSdkIntegrationGuide](07-MultiplayerSdkIntegrationGuide.md)；能力地图见 [00-SynchronizationCapabilityMap](00-SynchronizationCapabilityMap.md)。

---

## 0. 当前状态（本轮已完成）

| 项 | 状态 |
|---|---|
| Tier-1 清理（死代码、文档订正、4 个新网络测试工程挂入 `network-sdk` 门禁） | ✅ |
| Tier-2 `NetworkTransport` 契约测试（10 个，含 `RawServerPushReceived` 先于类型化解码、per-submit 重试） | ✅ |
| **P2.2 shooter 数据面 → 统一 `NetworkTransport`**（两连接拓扑；multiprocess smoke 含 recoverable-retry 全绿、`diffStatus=Identical`） | ✅ |
| WIP room-flow 稳定化（`MobaSmoke` CS8801 修、moba-smoke 全绿、staged-restore `RestoreAsync` 单测 4 个） | ✅ |
| `GatewayMultiplayerSession` arg-validation 测试 | ✅ |
| **SDK 稳定化轮（2026-08-10，详见各节）**：门面 dogfood（P-A）、GC 两轮（P-B）、Console 合并（P-C）、正确性收尾（P-D）、双订阅隐患（P-H） | ✅ |
| **胶水收敛**：`NetworkBattleConfig.UseRoomGatewayStateSyncInput` 预设（WireSubmitBattleInputReq/Res 映射 + CommandSequence + 可插拔重试策略），三 demo 各 ~45 行相同映射收敛为一处 + 5 个契约测试 | ✅ |
| **GC**：`NetworkSession` 收包/分发闭包内联快路径（InlineDispatcher 宿主零闭包）；发包 Encode 池化**评审后否决**（队列 dispatcher 会在 Send 返回后读取缓冲，池化会损坏数据——见 P-I 注记） | ✅ |
| **Hook 补强**：`NetworkTransport.AuthenticationFailed` / `SubmitInputFailed` 事件（P2.2 排障期"异常被吞"痛点） | ✅ |
| **两连接宿主 `GatewayBattleClientHost`（新包 `com.abilitykit.network.client`）**：房间连接+战斗连接+凭证传递+绑定纪律收进 SDK（attachBattle 时自动跳过房间侧订阅 = P-H 纪律结构化）；门面新增 `subscribeStateSync`/`joinFallbackToCreate` 选项（覆盖 join 失败的两种形态：Success=false 与 wire 层抛异常）；Console 已迁移并 e2e 验证 | ✅ |
| **shooter 迁入宿主（2026-08-10）**：宿主新增**原语构造路径**（`NetworkSdkClient` + `GatewaySessionResult`，服务非门面的自定义房间栈）+ `AttachBattle(connect: false)` 延迟建连（推送消费者先订阅再握手）+ `NetworkBattleConfig.WithRawDownlinkOnly()`（raw 下行消费者清空类型化反序列化器）。shooter `BuildBattleTransport` 迁至宿主，`ShooterNetworkTransportOptionsFactory` 删除（唯一消费者已迁）；launcher 的 Tick/Dispose 生命周期委托宿主。验证：33 契约测试 + multiprocess smoke（故障注入+重连）全绿 | ✅ |
| **MOBA 增量回归主线（2026-08-10）**：MOBA 的 `NetworkTransportOptionsFactory` 改走宿主的静态 `BuildBattleOptions`（共享网关/会话/协议预填——单一协议装配源），保留 MOBA 特有的 framesync/statesync 双分支输入 + 快照/帧反序列化 + 双 dispatcher 自管生命周期（其会话模型与宿主单 dispatcher 生命周期不匹配，故只共享装配步、不接 AttachBattle）。三 demo 协议预填收敛到一处。验证：MOBA view runtime 84 测试 + moba-smoke 全绿 | ✅ |

三个旗舰 demo 现在都跑在统一战斗引擎上 —— "契约中立数据面"论点全面证成。

---

## 1. 剩余优化项（按推进优先序）

### P-A · `GatewayMultiplayerSession` 接入（高层门面 dogfood）
- **价值**：高。关闭"零消费者"信誉缺口；每个 demo 退役 ~200 行手拼组装。
- **已推进（2026-08-10）**：门面已**灵活化 + 可注入 + 可扩展**：
  - `CreateAsync` 新增可选 `waitForBattleStart`（false 跳过 `WaitForBattleStart` 轮询，适配无正式开战环节的流程）。
  - 抽取 **`RunRoomFlowAsync` 编排 seam**（public static，可注入任意 `IRoomGatewaySessionClientBase` 构建的 flow）—— 外部可定制/可单测。
  - 新增 **`afterJoinAndBeforeReady` 钩子**（hero-pick/staged-loading 扩展点，供 MOBA 类流程用）。
  - 测试：`network.room` 编译通过、13 Room.Tests 全绿（含 `RunRoomFlowAsync` happy-path 完成+订阅、钩子先于 SetReady 触发、staged-restore 4 个）。
- **✅ Console demo 已 dogfood（2026-08-10，首个真实消费者）**：`StateSyncAdapter` 的 login 后房间组装（join-or-create → ready → subscribe，~60 行手写 wire 请求）切换到 `RunRoomFlowAsync` 编排 seam，保留自有连接/重连外壳与 join→create 回退语义。**同日完成完整开局链路**：facade 新增 `afterReadyAndBeforeBattleStart` 钩子（BeginLoading/ReportAssetsLoaded 扩展点），Console 以 `waitForBattleStart: true` + hero-pick/loading 双钩子驱动完整开战流程，**端到端收到服务端权威快照流（1189 帧推送）**。
- **过程中修复的阻断性存量问题**（均非门面引入）：① `Connect()` 无参重载空 identity（GuestId="" → login 400，回退 `_config.WorldId`/`PlayerId`）；② `battle_start.json` 缺 loadout 必填字段（Level/AttributeTemplateId/BasicAttackSkillId/SkillIds）；③ 房间缺 `syncTemplateId=state-sync-authority` tag（默认起帧同步战斗，永不推快照）；④ **Console 全速空转游戏循环（WaitOne 于常置位事件）以数千 Hz 喂 `Tick(0.033)`，SDK 心跳假时间 ~150ms 就判定超时断连** —— 适配器改喂 `Stopwatch` 实测墙钟（已在 `network.sdk` README 记录该契约陷阱）。
- **验证**：本地 Orleans 网关端到端（login → join 409 回退 create → pick → ready → loading → InBattle → subscribe → 快照流）；`moba-console-smoke` 门禁 4/4；Room.Tests 14/14（含新钩子编排时序单测）。
- **剩余**：① ~~MOBA 接入~~（评估结论：不接，MOBA 已在 `RoomGatewaySessionFlow` 分阶段 API 上，门面定位线性/最小流程）；② `CreateAsync` 完整 happy-path 端到端测试需 in-process gateway fixture；③ Console e2e 中快照 delta 恒空（服务端世界未收到操作输入）—— 因 auto-test 脚本在连接建立前已失败退出，未产生联网后的输入；同模板（state-sync-authority）的输入→快照链路已由 `moba-smoke` 的 `AUTHORITATIVE_INPUT_VERIFIED` 背书。
- **成本**：中-高（MOBA 段）。~~**依赖**：room-flow WIP 收尾~~（已提交 `24100399`，阻塞解除）。

### P-B · 热路径分配卫生（GC 优化）— **第二轮已完成（2026-08-10）**
- **价值**：中。降低帧热路径 GC 压力。
- **第一轮已做**（verified：11 framesync 测试全过 + moba-smoke 全绿）：
  - `FrameCommandBuffer.TrimBeforeLocked`：`new List<int>()` 每 trim → 复用字段缓冲 `_trimRemovals`（锁内单线程，安全）。
  - `RollbackCoordinator.TryCaptureAndStore`（MOBA client 每帧回滚 capture）：改为直接传池化捕获 list 作 span 给 `RollbackSnapshotRingBuffer.Store`（新增 span 重载），**消除每帧 `ToArray` 数组分配**；保留 `Capture+Store` 结果发布序列与公共 `Capture` 的 ToArray（外部调用者契约）。`CloneEntries`/`CountPayloadBytes` 改 span 版。
- **第二轮已做（2026-08-10）**（verified：`core-stability` 门禁全绿 = 8 测试工程 + UPM 依赖审计；moba-smoke 全绿）：
  - `FrameCommandBuffer.SubmitCommand` 每新帧 `new Dictionary` → **实例级字典池**（`_frameCommandPool`，trim/clear 退役时清空回收；全部访问在 `_sync` 锁内；公共读取只出防御拷贝， pooled 实例不外泄）。
  - `RollbackCoordinator.TryRestoreCore` 每 restore `new IRollbackStateProvider[]` → **`ArrayPool.Shared.Rent/Return`**（finally 归还 + clearArray，resolve→import 两阶段语义不变）。
- **刻意未动**（核校后判断）：
  - `StateManager`/`StateSlots`/`EntityPredictionState`/`PredictionCoordinator`/`StateDiffProvider` 等 **dormant**（无 demo-runtime 引用）——优化无价值。
  - `FrameCommandBuffer.GetFrameCommandsOrEmpty`/`TryGetFrameCommands` 的 Dictionary 拷贝是**刻意的 detached-snapshot 防御拷贝**（有测试 pin）——不动。
  - `CloneEntries` 里 `payload.Clone()`（每 entry 字节克隆）是**必要的所有权拷贝**（provider Export 可能复用缓冲）——不动。
- **P-B 靶点清零**，后续只在 profiling 证据出现时重启。

### P-C · Console demo `StateSyncAdapter` 合并 — **已完成（2026-08-10）**
- **价值**：中。消除 319 行并行手写 SnapshotAuthority 路径，收敛到统一 `NetworkTransport`（类似 P2.2，但针对 Console）。
- **完成内容**：房间组装段此前已收敛到 `GatewayMultiplayerSession.RunRoomFlowAsync`（见 P-A）。本轮数据面迁移：Console 改为**两连接拓扑**（与 shooter/MOBA 同构）——房间控制面留在 room 连接，战斗数据面独立 `NetworkTransport`（自有 TcpTransport + `NetworkBattleConfig` room-gateway 预设 + `WithInputSerializer`/`WithSnapshotDeserializer` 两个玩法回调）。引擎接管：连接即 RenewSession→SubscribeStateSync 握手（推送绑定 last-writer-wins 移到 battle 连接）、输入提交请求-响应 + 权威帧重试、类型化 `StateSyncSnapshotPushed` 事件。删除：raw `SendRawRequestAsync(SubmitBattleInput)` + 手写 push opcode 分发 + 手动反序列化。
- **验证**：本地 Orleans 网关 e2e —— 完整房间流程 + battle 数据面建连 + **1103 帧类型化快照推送**（经 `StateSyncSnapshotPushed` 事件）；`moba-console-smoke` 门禁 4/4。
- 至此三个 demo（MOBA / shooter / Console）全部跑在统一 `NetworkTransport` 战斗引擎上。

### P-D · 潜在正确性收尾（同步核心重构遗留）— **已完成（2026-08-10）**
- **价值**：中。
- **结论与处置**：
  1. `StateManager.RestoreSnapshot` 忽略 `WorldStateSnapshot` —— **确认非 bug，是设计**：恢复路径的权威来源是逐实体回滚数据（`IRollbackable`），`WorldStateSnapshot` 服务 diff/哈希/网络面。处置：删掉死参数（`TryRestore` 的 TryGet 保留为存在性检查），doc 注明分工。
  2. `BinarySerializerImpl` 活反射 —— **加类型级 `FieldInfo[]` 静态缓存**（`ConcurrentDictionary.GetOrAdd`），消除每对象节点重复 `GetFields`。property 支持**刻意不扩**：当前零生产消费者（`WorldStateSnapshot` 走 `ToBytes` 快路径，仅 sample/test 触达），扩展会改变线格式；已在类 doc 注明"仅 public 实例字段"的边界。
- **验证**：StateSync.Tests 6/6。

### P-E · UDP/WebSocket 服务端
- **价值**：中。客户端 transport（LiteNet/WebSocket）已就绪；WebSocket Gateway 服务端已进入 canonical 注册链但默认关闭，LiteNet/UDP 网关仍未实现。剩余目标是启用非 TCP 生产部署。
- **WebSocket 已推进**：`WebSocketTransportServer` 由 `GatewayModuleExtensions` 绑定 `AbilityKit:Gateway:WebSocket` 并注册 hosted service；默认 `Enabled=false`，已有 Gateway 服务端路径/帧协议/Stop-Restart 契约测试。
- **剩余**：WebSocket 端到端 smoke（含真实 Gateway 启动配置、TLS/反向代理/断线恢复按部署需要验证）；LiteNet/UDP server listener、配置、smoke。
- **成本**：中。

### P-F · `DemoHarnessRunner` 迁出 `network.runtime`
- **价值**：低-中。架构整洁（test infra 不该在 runtime 包）。`network.runtime` README 已标注"计划迁出"。
- **范围**：多包重构（`network.runtime` → 新 test-infra 包 → MOBA/shooter carrier + ~8 测试文件依赖更新）。
- **成本**：中-高。

### P-G · 预测栈统一（v1.0）
- **价值**：中。3 套预测实现（`world.framesync/Rollback`、`host.extension/Client/FrameSync`、shooter adapter）去重，抽 `IClientPredictionDriver`。shooter adapter 有 `TODO(v1.0)`。
- **成本**：高。机制不同（framesync vs statesync），强统一有再次破裂风险（参考 coordinator 当初"过度统一被废弃"）。**部分可能不做**（P5 原则）。

### P-I · SDK 稳定化设计裁决（2026-08-10 评审记录）
- **核心层/扩展层拆分 —— 现状即合理，不动物理结构**：`network.runtime`（连接/传输）→ `network.sdk`（组装）→ `network.room`（房间栈+门面）/ `network.battle`（契约中立引擎）→ `network.battle.config`（room-gateway 预设=扩展层）。引擎内部 framesync/statesync 特性按 opcode 配置共存，属"配置即扩展"；物理拆包只会割裂统一引擎的价值。真正的越界项是 `DemoHarnessRunner` 在 runtime 包（= P-F，长线）。
- **胶水代码：参数化优于代码生成**。三 demo 重复的 `WireSubmitBattleInputReq/Res` 映射已由 `UseRoomGatewayStateSyncInput` 预设收敛；剩余 per-game 回调仅 1-2 行（快照/帧反序列化 + id 转换），源码生成不 buy 任何东西。线格式类型本身已由 MemoryPack 生成器产出——代码生成的正确位置在协议层，不在接入胶水层。
- **发包 Encode 缓冲池化 —— 否决**。`IFrameCodec.Encode` 每包 `new byte[]` 看似浪费，但其所有权契约（返回缓冲调用方永久持有）是安全的：四个传输全部同步消费 Send 缓冲，而**队列型 dispatcher 宿主（如 MOBA 的 DedicatedThreadDispatcher IO）会把接收缓冲捕获进闭包、在 Send 返回后才读**——池化会在该场景损坏数据。同理 `NetworkFrameReader.TryRead` 的 payload 拷贝（防御生命周期）。已实现的 GC 优化仅限零风险点：`NetworkSession` 收包/分发闭包对 InlineDispatcher 宿主的内联快路径。
- **MOBA `retryAtAuthoritativeFrame = wire.ShouldResync` 存疑映射**（`NetworkTransportOptionsFactory` statesync 分支，原样保留在预设调用里）：ShouldResync 语义是"客户端应全量重同步"，被当重试标志用；shooter 刻意把它当纯数据。MOBA 生产走 framesync 分支（该映射仅 SnapshotAuthority 模式触达），暂不动，记为待查项。

### P-H · 杂项清理
- **double-subscribe（2026-08-10 修复，实为正确性隐患而非仅带宽浪费）**：网关推送绑定按 observerKey（accountId:roomId）**单槽 last-writer-wins**（`StateSyncObserverGrain.StateSyncGatewayPushBinding`）。shooter 恢复路径曾在 room 连接上重跑 `SubscribeStateSync`，会把推送路由从 battle 连接抢回 room 连接（推送被丢弃 = 战斗黑屏直到 battle 重连）。修复：restore 的 `SubscribeStateSync` 分支改为不再在 room 连接上订阅（`subscribeOnRoomConnection: false`），订阅由 battle 数据面独占（其 NetworkTransport 重连自动 RenewSession→SubscribeStateSync）；初次 launch 的订阅保留以守住 `Flow.Subscribed` 契约。验证：shooter-multiprocess smoke（含 recoverable-retry 3 次注入故障 + 重连）全绿、双端 `diffStatus=Identical`、`reconnectPushesAfter=3`。
- **coordinator P4 复核**：已收缩为契约包（13 文件），`CoordinatorPayloadCodec` 存活（被 `EntityState`/`FrameSnapshotData` 用）；确认无残余死代码。

---

## 2. 推荐推进序

短中线项（P-A/P-B/P-C/P-D/P-H + SDK 稳定化轮）已于 2026-08-10 全部清零。剩余：
1. **P-E / P-F / P-G** —— 长线，按需或按用户 WIP 节奏推进；P-G 谨慎（可能部分不做）。
2. P-I 待查项（MOBA ShouldResync-as-retry 映射）随手确认。

---

## 3. 验证基线

推进任一项后，按其影响面回归：
- 引擎/同步核心（P-B/P-D）：`core-stability` 门禁（framesync + statesync + record + host + triggering）。
- shooter 路径（P2.2 后续、P-C 若涉及）：`shooter-fast` + `shooter-integration` + `shooter-multiprocess`（权威两连接门禁）+ `shooter-unity-playmode`（需 Unity Editor）。
- MOBA/房间（P-A/P-C）：`moba-smoke`（两客户端 TCP Gateway）。
- SDK/transport：`network-sdk` 门禁（Sdk/Room/Battle engine + Battle.Config + 3 transport loopback）。

---

本文档随源码与能力边界同步演进；完成项移入第 0 节，新发现项按价值/成本插入第 1 节。

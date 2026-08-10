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

两个旗舰 demo 现在都跑在统一战斗引擎上 —— "契约中立数据面"论点全面证成。

---

## 1. 剩余优化项（按推进优先序）

### P-A · `GatewayMultiplayerSession` 接入（高层门面 dogfood）
- **价值**：高。关闭"零消费者"信誉缺口；每个 demo 退役 ~200 行手拼组装代码。
- **阻塞**（已核校）：门面① 强依赖 `WaitForBattleStart`（不适配 Console 单 client SnapshotAuthority，无 formal battle start）；② 跳过 hero-pick + staged-loading（不适配 MOBA）；③ 自建 flow 不可注入（happy-path 测试需 in-process gateway fixture）。
- **前置改动**（门面灵活化，属用户 WIP）：`waitForBattleStart: bool = false` 可选；可选 hero-pick/loading 钩子；抽取可注入 flow 以便单测。
- **接入目标**：room-flow WIP 收尾后，切 MOBA `MultiplayerGatewayEntryModule`（由 `moba-smoke` 验证）；或新建一个完整流程的最小示例。
- **成本**：中-高。**依赖**：room-flow staged-restore WIP 收尾。

### P-B · 热路径分配卫生（GC 优化）
- **价值**：中。降低帧热路径 GC 压力。
- **靶点**（`a9e5e0b2` 同步核心重构遗留，选择性池化）：`StateSlots.Keys`（每次访问 `new List`）、`FrameCommandBuffer` 字典读（每次 `new Dictionary` 拷贝）、`StateManager.CaptureState` 每 capture 数组、`TrimRollbackBuffers` 临时 `HashSet`/`List`、`RollbackCoordinator.TryRestoreCore` 每次 `new IRollbackStateProvider[]`。
- **成本**：低-中（逐点）。**安全网**：framesync/statesync 契约测试已存在（`core-stability` 门禁）。**无 WIP 依赖，可立即逐点推进。**

### P-C · Console demo `StateSyncAdapter` 合并
- **价值**：中。消除 319 行并行手写 SnapshotAuthority 路径，收敛到统一 `NetworkTransport`（类似 P2.2，但针对 Console）。
- **成本**：中。**依赖**：与 P-A 协同（`StateSyncAdapter` 的 login/create/join/subscribe 可由 `GatewayMultiplayerSession` 替代，前提是 `WaitForBattleStart` 可选）。

### P-D · 潜在正确性收尾（同步核心重构遗留）
- **价值**：中。
- **项**：① `StateManager.RestoreSnapshot` 忽略已存的 `WorldStateSnapshot`（restore 路径上死存，仅 `ComputeDiff`/`GetFullState` 用）——确认是否应恢复；② `BinarySerializerImpl` 活反射（每次 `GetFields`、无缓存），只序列化 public **field**（非 property）——确认是否需缓存/扩到 property。
- **成本**：低-中。**安全网**：statesync 测试。

### P-E · UDP/WebSocket 服务端
- **价值**：中。客户端 transport（LiteNet/WebSocket）已就绪，gateway 仅 TCP；启用非 TCP 生产部署。
- **成本**：中。**依赖**：用户的 WS 服务端 WIP（`WebSocketTransportServer.cs`）。

### P-F · `DemoHarnessRunner` 迁出 `network.runtime`
- **价值**：低-中。架构整洁（test infra 不该在 runtime 包）。`network.runtime` README 已标注"计划迁出"。
- **范围**：多包重构（`network.runtime` → 新 test-infra 包 → MOBA/shooter carrier + ~8 测试文件依赖更新）。
- **成本**：中-高。

### P-G · 预测栈统一（v1.0）
- **价值**：中。3 套预测实现（`world.framesync/Rollback`、`host.extension/Client/FrameSync`、shooter adapter）去重，抽 `IClientPredictionDriver`。shooter adapter 有 `TODO(v1.0)`。
- **成本**：高。机制不同（framesync vs statesync），强统一有再次破裂风险（参考 coordinator 当初"过度统一被废弃"）。**部分可能不做**（P5 原则）。

### P-H · 杂项清理
- **double-subscribe 带宽浪费**：P2.2 两连接拓扑下，room flow 仍 `SubscribeStateSync`（room 连接）且 battle `NetworkTransport` 自动订阅（battle 连接）；room 连接的推送已被剥光丢弃。room flow 去掉 `SubscribeStateSync` 即可（低成本、低价值，省带宽）。
- **coordinator P4 复核**：已收缩为契约包（13 文件），`CoordinatorPayloadCodec` 存活（被 `EntityState`/`FrameSnapshotData` 用）；确认无残余死代码。

---

## 2. 推荐推进序

1. **P-B（热路径 GC）** —— 无 WIP 依赖、有测试安全网、可立即逐点推进；每点独立可验证。
2. **P-A（`GatewayMultiplayerSession` 门面灵活化 + 接入）** —— 价值最高，但依赖 room-flow WIP 收尾 + 门面改动（用户决策）。
3. **P-C（Console 合并）** —— 与 P-A 协同，门面灵活化后顺势做。
4. **P-D（正确性收尾）** —— 低成本，可随手穿插。
5. **P-E / P-F / P-G** —— 按需或按用户 WIP 节奏推进；P-G 谨慎（可能部分不做）。

---

## 3. 验证基线

推进任一项后，按其影响面回归：
- 引擎/同步核心（P-B/P-D）：`core-stability` 门禁（framesync + statesync + record + host + triggering）。
- shooter 路径（P2.2 后续、P-C 若涉及）：`shooter-fast` + `shooter-integration` + `shooter-multiprocess`（权威两连接门禁）+ `shooter-unity-playmode`（需 Unity Editor）。
- MOBA/房间（P-A/P-C）：`moba-smoke`（两客户端 TCP Gateway）。
- SDK/transport：`network-sdk` 门禁（Sdk/Room/Battle engine + Battle.Config + 3 transport loopback）。

---

本文档随源码与能力边界同步演进；完成项移入第 0 节，新发现项按价值/成本插入第 1 节。

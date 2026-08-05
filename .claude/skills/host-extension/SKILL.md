---
name: host-extension
description: AbilityKit Host 扩展包（com.abilitykit.host.extension，v0.1.0 Beta）——框架通用的 Host runtime 扩展（FrameSyncDriver/Rollback/CatchUp/Session/Client/Server/Time/WorldStart）。⚠ Moba 专属 host adapter（Runtime/Moba/ 子树，38 文件 + 3 asmdef）已于 0.1.0 提取到新的 demo 包 com.abilitykit.demo.moba.host；Moba 相关设计文档仍在本目录 moba_*.md 中但代码位置已变。触发场景：FrameSync 驱动、客户端预测/回滚/对账、BattleHost 生命周期、CatchUp、FixedStepTickRunner、WorldAutoStart。
---

# host-extension skill

基于源码核校（2026-07-31）。`com.abilitykit.host.extension` 包根：`Unity/Packages/com.abilitykit.host.extension/`，version `0.1.0`（Beta，`AbilityKitStable=true`）。⚠ Moba 专属子树（`Runtime/Moba/`）已于 0.1.0 提取到 `com.abilitykit.demo.moba.host`；以下包结构仅列出通用部分。

## 包结构（7 个 asmdef）

```
com.abilitykit.host.extension/
├── Runtime/
│   ├── com.abilitykit.host.extension.asmdef          [Host.Extensions]（根，autoReferenced=true）
│   ├── FrameSync/                                     共享层（Driver/Module/接口/CatchUp）
│   ├── Client/                                        [Host.Extensions.Client]
│   │   ├── FrameSync/                                 客户端预测 generic primitives
│   │   └── StateSync/                                 RemoteClientInputSubmitQueue
│   ├── Server/                                        [Host.Extensions.Server]
│   │   ├── BattleHost/                                BattleHost 生命周期 + tick/buffer/scheduler/snapshot/observer
│   │   └── FrameSync/CatchUp/                         InMemoryFrameSyncInputHistory
│   ├── Rollback/                                      ServerRollbackModule
│   ├── Time/                                          FixedStepTickRunner + ServerFrameTimeModule
│   ├── WorldStart/                                    WorldAutoStartModule + IWorldAutoStartHandler
│   ├── Session/                                       RoomGatewaySessionFlow（8 阶段）+ FramePacketNetAdapter
│   └── Moba/                                          三个 asmdef（Client/Server/Shared）
│       ├── Shared/                                    [Host.Extensions.Moba]（依赖 Protocol.Moba）
│       ├── Client/                                    [Host.Extensions.Moba.Client]
│       └── Server/                                    [Host.Extensions.Moba.Server]
├── Example/                                           LogicWorldServerExample（仅示例，不是框架类）
└── Document/                                          设计文档
```

## 5 个 IHostRuntimeModule（包核心）

| Module | 文件 | 关键依赖 |
|--------|------|---------|
| `FrameSyncDriverModule` | `Runtime/FrameSync/FrameSyncDriverModule.cs` | 服务端帧同步权威驱动 |
| `ClientPredictionDriverModule` | `Runtime/FrameSync/ClientPredictionDriverModule.cs` | 客户端预测 + 回滚 + 对账 |
| `ServerFrameTimeModule` | `Runtime/Time/ServerFrameTimeModule.cs` | 弱依赖 IFrameSyncDriverEvents |
| `WorldAutoStartModule` | `Runtime/WorldStart/WorldAutoStartModule.cs` | world 自启 |
| `ServerRollbackModule` | `Runtime/Rollback/ServerRollbackModule.cs` | **强依赖** IFrameSyncDriverEvents |

**安装顺序**（源码验证）：`FrameSync → ServerFrameTime → WorldAutoStart → Rollback`（`MobaHostRuntimeBuilder.CreateModules` 实现）。

## Sections

- [host_modules.md](host_modules.md) — 5 个 IHostRuntimeModule + 顺序约束
- [client_prediction.md](client_prediction.md) — ClientPredictionDriverModule + 配置接口（含新 IClientPredictionReconcileControl）
- [server_battlehost.md](server_battlehost.md) — BattleHost 生命周期 + tick/buffer/scheduler/snapshot/observer
- [catchup.md](catchup.md) — CatchUp 子系统 + WorldCatchUpDriver + BattleFrameSyncGrain 服务端集成（v0.1.0 新增）
- [client_helpers.md](client_helpers.md) — ClientPredictionInputHistory / ReconciliationCoordinator / RemoteClientInputSubmitQueue / FramePacketNetAdapter
- [time_worldstart.md](time_worldstart.md) — FixedStepTickRunner + ServerFrameTimeModule + WorldAutoStartModule
- [session_flow.md](session_flow.md) — RoomGatewaySessionFlow 8 阶段 + FramePacketNetAdapter
- [framesync_server.md](framesync_server.md) — 🆕 BattleFrameSyncGrain 完整能力：CatchUp / Recording / Metrics / BotAI / TickRate / BattleWorldWithFrameSync
- [spectator.md](spectator.md) — 🆕 观战模式：SpectatorWorldDriver（框架层）+ BattleSessionFeature.Spectator（Demo 层集成）
- [gateway_connection.md](gateway_connection.md) — 🆕 统一网关连接抽象：IGatewayConnection + GatewayConnection（请求/推送/seq匹配）
- [moba_runtime.md](moba_runtime.md) — MobaHostRuntimeBuilder + IMobaBattleRuntimePort + MobaGameStartPort
- [moba_startsource.md](moba_startsource.md) — GameStartSource 路由（3 Source + Router）
- [moba_room.md](moba_room.md) — MobaRoomState + MobaRoomOrchestrator + 命令应用
- [moba_roomsync.md](moba_roomsync.md) — Client/Server RoomSync + Outbox + Broadcaster
- [moba_startgame.md](moba_startgame.md) — MobaGameStartOrchestrator + IMobaServerGameLifecycle + WorldAutoStartHandler
- [moba_createworld.md](moba_createworld.md) — CreateWorld 数据模型（Spec/Plan/LaunchSpec/SimulationLaunchPlan）

## 相关 skill

- 完整技能/触发/BUFF 见 [ability-kit](../ability-kit/SKILL.md)
- 客户端预测深度排查见 [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 会话协调器见 [coordinator](../coordinator/SKILL.md)
- moba demo 见 [moba-demo](../moba-demo/SKILL.md)

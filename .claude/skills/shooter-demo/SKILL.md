---
name: shooter-demo
description: AbilityKit Shooter Demo（com.abilitykit.demo.shooter.*）——6 个包的服务端权威 StateSync + Svelto.ECS 演示示例。**刻意不复用 ability/combat 技能栈**，与 moba demo（Entitas + FrameSync + 全技能栈）形成对照。覆盖 ShooterBattleRuntimePort 9 端口、ShooterBattleSimulation 双 module 管线、三类快照（StateSnapshot/PackedSnapshot/PureStateSnapshot 含 AOI）、三种客户端同步策略（PredictRollback/AuthoritativeInterpolation/HybridHeroPrediction）、11 opcodes、6 种实体 Group、4 种 PlayMode、ShooterDemoWindow 三种 DriveMode、ShooterAcceptanceSpecs 纯 C# 验收基线。🆕 v0.1.0：多人烟雾测试通过、Unity 无头双实例脚本、暂停/恢复 GUI、IGatewayConnection 统一网关抽象。触发场景：StateSync 实现、Svelto ECS 适配、packed/pure-state 快照编码、AOI 兴趣裁剪、客户端预测回滚（不复用 ClientPredictionDriverModule）、Lag Compensation、Fast Reconnect、Drift Recovery、FrameRecordReplay、ML-Agents 训练环境。
---

# shooter-demo skill

## Protocol changes

For Shooter opcodes, payloads, MemoryPack fields, codecs, or generated DTOs, follow [`protocol-wire`](../protocol-wire/SKILL.md). Protocol fields must originate from grouped Wire Schema v2; handwritten protocol files retain behavior only.

基于源码核校（2026-08-03）。Shooter demo 实际存在，约 215 个 .cs 文件，分 6 个包。

## 核心定位（必读）

**Shooter demo = 服务端权威 StateSync + Svelto.ECS 的网络同步演示示例**。

**刻意不复用** ability/combat/triggering/modifiers/attributes 技能栈——这是与 moba demo（Entitas + FrameSync + 全技能栈复用）的核心对照点。memory 3 天前的判断"shooter = 网络同步（Svelto-ECS + statesync）/shooter 未复用技能栈"**当前代码仍然完全吻合**。

## 与 moba demo 的核心差异

| 维度 | Shooter | MOBA |
|------|---------|------|
| ECS 框架 | **Svelto.ECS**（`world.svelto` + `thirdparty.svelto`）；struct `IEntityComponent`、`ExclusiveGroup`、`QueryEntities` | **Entitas**（`world.entitas`）；代码生成 `Actor*Component` |
| 同步方式 | **StateSync**（服务端权威，packed/pure 快照下推） | **FrameSync** + 客户端预测回滚（`ClientPredictionDriverModule`） |
| 技能栈复用 | **不复用**。runtime deps 只含 core/host/world.di/world.svelto/share/protocol.shooter | 全面复用 ability/combat.*/triggering 等 |
| 战斗内核 | 自写极简：`ShooterBattleSimulation` + 双 module + 空间网格命中 | `SkillCastCoordinator`/`MobaBuffService` 完整玩法生产线 |
| 客户端预测 | 自己的 `ShooterClientPredictionRuntimeAdapter` + `ShooterPackedSnapshotRollbackProvider`（**不复用** `ClientPredictionDriverModule`） | 复用 `ClientPredictionDriverModule` |
| 配置 | `ShooterSveltoGameplayScenarioConfig`（内置 `WaveSurvival`）+ `ShooterRuleSet` 硬编码 | Luban + JSON 表 |
| 玩法 | 俯视圆形竞技场、波次生存、射击 + 命中 + 爆炸 + 穿透；3 种攻击槽 | MOBA 四英雄技能/被动/Buff |
| AI | `com.abilitykit.demo.shooter.ai`（ML-Agents）+ `ShooterBotAiRuntime` 规则 Bot | 无独立 AI 包 |

## 6 个包

| 包 | 文件数 | 职责 |
|----|-------|------|
| `com.abilitykit.demo.shooter.runtime` | ~80 | 逻辑世界 + Svelto 战斗模拟 + 同步导出 |
| `com.abilitykit.demo.shooter.share` | 1 | 共享常量（`ShooterGameplay`：RoomType/WorldType/GameplayId/TickRate=30/MaxPlayers=4/PlayerHp=1000） |
| `com.abilitykit.demo.shooter.view.runtime` | 121 | 客户端表现/同步/会话/PlayMode |
| `com.abilitykit.demo.shooter.editor` | 6 | `Tools/AbilityKit/Shooter Demo` 窗口 + SceneView 渲染 |
| `com.abilitykit.demo.shooter.ai` | 1 | ML-Agents 训练环境适配 |
| `com.abilitykit.protocol.shooter` | 7 | 线协议（MemoryPack 结构体 + Codec） |

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [packages_overview.md](packages_overview.md) — 6 包依赖图 + 职责
- [runtime_simulation.md](runtime_simulation.md) — ShooterBattleRuntimePort 9 端口 + ShooterBattleSimulation 双 module 管线
- [svelto_ecs.md](svelto_ecs.md) — Svelto 适配 + 5 个 ExclusiveGroup + struct 组件
- [snapshots_hash.md](snapshots_hash.md) — 三类快照 + StateHasher + AOI 兴趣裁剪 + 定点量化
- [client_sync.md](client_sync.md) — 三种客户端同步策略 + Drift Recovery + Fast Reconnect + Lag Compensation
- [network_protocol.md](network_protocol.md) — 11 opcode + MemoryPack Codec
- [presentation.md](presentation.md) — PresentationSession + ViewProjection + DotsBinder + ViewEventSink
- [playmode_editor.md](playmode_editor.md) — 4 种 PlayMode + 3 种 DriveMode + SceneView 渲染
- [acceptance_testing.md](acceptance_testing.md) — ShooterAcceptanceSpecs 纯 C# 验收基线 + DeterminismSpecRunner + Benchmark
- [multiplayer_verification.md](multiplayer_verification.md) — 🆕 **v0.1.0** 多人同步验证：烟雾测试结果 / Unity 无头双实例 / 暂停恢复 GUI

## 相关 skill

- 完整技能/触发/BUFF（**shooter 不复用**，但可作对照）→ [ability-kit](../ability-kit/SKILL.md)
- 客户端预测（**shooter 不复用** `ClientPredictionDriverModule`）→ [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- shooter 多人联网接入（`NetworkSdkBuilder` → `NetworkSdkClient` → `RoomGatewaySessionFlow`，**不经 coordinator**）→ 见 [coordinator/integration_recipes.md](../coordinator/integration_recipes.md) 的"shooter 真实路径"
- moba demo 对照 → [moba-demo](../moba-demo/SKILL.md)

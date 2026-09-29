---
name: tiny-demo
description: AbilityKit Tiny 示例（com.abilitykit.demo.tiny / .logic / .turn）——最小的双人联机战斗接入参考，用尽量少的项目代码串起 Room + State/Frame/Hybrid 三种同步模式 + 断线恢复 + Unity 表现。覆盖确定性规则（TinyBattle/TinyInput/TinyBattleStateCodec/FNV-1a 哈希，零框架依赖）、服务端玩法模块（TinyServerGameplayModule 注册进 ServerGameplayModuleCatalog）、同步模板映射与能力协商、会话状态机 TinyBattleSession/TinyGameplayRoot、帧预测与回滚适配 TinyFrameReplication、逐章教程 00-10、以及 17 阶段验收入口 tools/verify-tiny-starter.ps1。触发场景：改 Tiny 战斗规则或攻击伤害、加同步模式、改房间/玩家槽位策略、RulesKey 与启动清单哈希、Tiny 章节样例（Samples~）、Room+State/Frame/Hybrid 联机闭环、Tiny 双进程与 Unity 无头验收、判断 Tiny 当前完成度与证据等级。
---

# tiny-demo skill

## Protocol changes

Tiny 的战斗输入与状态载荷是**不透明字节**，经通用 Room 协议承载；`TinyBattle.InputOpCode=1`、`TinyBattleStateCodec.PayloadOpCode=31001`、`TinyTurnBattle.InputOpCode=2`、`TinyTurnBattle.SnapshotOpCode=31002` 都定义在共享 Logic 源码里，**不在** `Protocols/` YAML 目录中。改动 Tiny 之外任何 opcode / MemoryPack 字段 / 生成 DTO 时，走 [`protocol-wire`](../protocol-wire/SKILL.md)。Tiny 自身要改线上格式时，直接读本文 [change_checklist.md](change_checklist.md) 的协议演进一节——Tiny 的"协议演进"练习用的是独立教学封套，不是正式 wire。

基于源码核校（2026-09-28）。**当前 tiny 的 .NET 与 TCP 验收已实测通过；Unity 侧本轮未复跑**（见下"证据等级"）。

## 定位（必读）

Tiny 是**新玩法接入 AbilityKit 的最小可运行参考**：两名玩家、整数格移动、一次带冷却的攻击。它**不新增框架能力**，只演示"项目该自己决定什么、框架已经提供什么"。

与另两个示例的分工：

| | Tiny | MOBA | Shooter |
|---|---|---|---|
| 定位 | **最小接入闭环**（Room + 三模式同步 + 恢复） | 完整玩法生产线（技能/Buff/触发器） | 服务端权威 StateSync + 性能 |
| 同步 | State / Frame / Hybrid 三选一 | 默认 FrameSync + 预测回滚 | 默认 StateSync + 插值 |
| 战斗内核 | 自写 142 行整数规则 | Entitas + 技能栈 | Svelto.ECS |
| 代码量 | 规则 323 行，联机包 4452 行 | 数十万行 | 数万行 |
| 该复用谁 | 想**照抄接入路径**时读 Tiny | 想要**完整玩法**时读 MOBA | 想读**同步性能**时读 Shooter |

**"最少代码"要说清楚是哪种最少**：规则层确实极少（`tiny.logic` 323 行、零依赖、服务端与客户端共编译同一份源码）；章节样例也少（`Samples~/` 6 个片段共 261 行）。会话自 2026-09-28 组合层下沉后也不大——`TinyBattleSession.cs` 344 行、`TinyTurnSession.cs` 290 行，房间生命周期/恢复/守卫都在框架基类 `RoomGatewayBattleSessionBase`（见 [framework_boundaries.md](framework_boundaries.md)）。**改造前**会话是 627 行的手写状态机；那部分复杂度（Room 生命周期 / 同步协商 / 基线等待 / 重连 / 命令去重）没有消失，是**进了框架**——接入方现在继承基类填钩子即可，但理解这些语义仍是接入 Tiny 的必修课。`TinyGameplayRoot.cs` 306 行的场景编排仍是项目自己的。

## 包与骨架

三个 Unity 包 + 一个服务端插件工程，规则**只有一份源码**：

| 位置 | 量级 | 职责 |
|---|---|---|
| `com.abilitykit.demo.tiny.logic` | 7 .cs / 323 行 | 确定性规则 + 线载荷。**零依赖**，`noEngineReferences: true` |
| `com.abilitykit.demo.tiny` | 29 .cs / 4452 行 | 联机实现：`Runtime/View`（会话/表现）、`Runtime/FrameSync`（回滚适配）、`Composition/`、`Scenes/`、`Samples~/02-07`、`Tests/` |
| `com.abilitykit.demo.tiny.turn` | 6 .cs / 1229 行 | 可选回合制第二玩法；单独安装 |
| `Server/Orleans/src/AbilityKit.Demo.Tiny.Server` | 6 .cs / 551 行 | 玩法模块 + 三个 adapter；**不是新 Grain** |
| `src/AbilityKit.Demo.Tiny.*` | 19 个 .NET 工程 | 3 个投影库 + 1 个验收驱动库 + 10 个章节/流程 exe + 1 个 xunit |
| `tools/verify-tiny-starter.ps1` | 1042 行 / 17 阶段 | 统一验收入口（**不在任何 gate 里**） |

**规则共源机制**（改动前必读）：`src/AbilityKit.Demo.Tiny.Core` 与 `Turn.Core` 是**只有 csproj 的投影工程**，用 `<Compile Include="../../Unity/Packages/com.abilitykit.demo.tiny.logic/Runtime/Logic/*.cs" />` 直接编译 Unity 包源码。所以 ① 服务端构建**依赖 Unity 目录树存在**；② 改 `Unity/Packages/**` 下的规则会**静默改变服务端行为**；③ 永远不要另建第二份规则。

## Sections

- [packages_overview.md](packages_overview.md) — 三个 Unity 包 + 服务端 + 19 个 .NET 工程的依赖图与装配引用
- [rules_and_determinism.md](rules_and_determinism.md) — `TinyBattle`/`TinyInput`/Codec/哈希的完整 API 与不变式；确定性口径
- [sync_modes.md](sync_modes.md) — State/Frame/Hybrid 语义、服务端模板映射表、能力协商与 schema v1
- [session_and_view.md](session_and_view.md) — `TinyBattleSession`/`TinyGameplayRoot`/View 模块/`TinyFrameReplication`；测试靠反射的陷阱
- [server_and_host.md](server_and_host.md) — 模块注册、三个 adapter、Tiny 明确不支持什么、网关目录与 sln 缺口
- [acceptance.md](acceptance.md) — `verify-tiny-starter.ps1` 的 17 阶段、`-FocusChapter`、证据目录、**无 gate/无 CI 的现状**
- [change_checklist.md](change_checklist.md) — 跨层改规则的逐处清单（含 `RulesKey`、启动清单哈希、在途改动）
- [framework_boundaries.md](framework_boundaries.md) — **框架边界**：单连接 vs 双连接拓扑、用了/没用哪些包（含"该用却手写"的清单）、声明 vs 实现的漂移、`IWorld` 契约税、复杂项目走的是另一种接入形态

## 证据等级（当前）

- **E3 已实测（本轮复跑）**：`dotnet run --project src/AbilityKit.Demo.Tiny.Logic.Sample` → `hash=3287469023`；`dotnet test src/AbilityKit.Demo.Tiny.Replication.Tests` → 6/6 通过。
- **E3 有源码有测试**：Unity Editor 6 文件 / **37 个测试方法**（36 `[Test]` + 1 `[UnityTest]`，另有 5 行 `[TestCase]` 参数化；`TinyBattleSessionTests.cs` 独占 26 个方法）、PlayMode 6、NetworkPlayMode 1（需真实 Gateway）；服务端 `Grains.Tests` **12 个 `[Fact]` + 1 个 `[Theory]`×3**（已逐个核对）。
- **E4 有历史产物**：`local/Logs/tiny-acceptance-*/`（`process-{Mode}/owner.json`、`guest.json`、`unity-cross-{Mode}/`、`phases.json`）。
- **E5 = 无**：`tools/test-gates.json` 30 个 gate **零处提及 Tiny**；`.github/workflows/` 已整体删除（末次提交"暂时删除ci检测"）。刻意区分：**有验收脚本 ≠ 有 CI 阻断**。
- **未验证**：本轮没跑 Unity 批处理，也没有任何"两台可视化 Unity 客户端的画面/交互"证据——这是 Tiny 文档自己反复声明的非目标，不要拿无头结果顶替。

## 高置信陷阱（先看这五条）

1. **改规则常量必须同步 bump `RulesKey`**：`TinyBattle.RulesKey`（`"tiny:rules.v1"`）被客户端 `TinyAssetPreparation.Validate` 与服务端 `TinyRoomGameplayAdapter.BuildLaunchManifest` 同时消费，**靠人工约定**、无自动强制。只改 `AttackDamage` 不 bump，测试仍绿但线上启动清单校验会拒。
2. **改规则会牵动多处写死期望**：`AttackDamage 10→12` 要求 HP 期望 `90→88`、BattleStyles 的 `80→76`——分布在样例、服务端测试、双进程断言、脚本里。逐处按语义判断，别批量替换数字（见 [change_checklist.md](change_checklist.md)）。
3. **`Tests/Editor/TinyBattleSessionTests.cs` 用私有反射**（`SetPrivate`/`SetRootPrivate`/`TickRoot`）写会话与 Root 的私有字段并调私有 `Update`。2026-09-28 起 `SetPrivate` 沿继承链查找且找不到时 `Assert.Fail`——字段名改错会**响亮失败**而非静默 NRE，但改 `TinyBattleSession`/`TinyGameplayRoot` 的字段名前仍要先 grep 测试文件。
4. **Tiny 固定两名玩家**：`"Tiny requires exactly two players."`；Turn 还要求槽位有序 1、2。`TinyServerGameplayModule` 默认模板是 **State**；`ResolveSyncCapabilities` 是**按字符串 switch**，与 `SyncProfile` 是两份独立声明，加模板必须同时改两处。
5. **`-Tests/PlayMode` 与 `Samples~` 没有 asmdef**：`Samples~` 不被包编译，"可独立运行"由外部 `src/AbilityKit.Demo.Tiny.*Sample` 工程证明；`07-ProjectEntry` 在包内**没有**测试覆盖其 `Enter`（只测了 `TinyProjectLaunch`）。

## 环境与运行

- Unity **2022.3**；三个包 `0.0.1`。`demo.*` 在 `release-manifest.json` 的 `neverReleased.prefixes` 内——**永不发布**，不要给 Tiny 套 cohort `0.1.0`。
- 多客户端必须用**两个独立工程目录**（共用 `Library` 会锁）。`tools/create-tiny-validation-project.ps1 -OutputPath <A>` 生成两次。
- `verify-tiny-starter.ps1` 的 `-UnityExe` **默认写死** `C:\Software\Unity 2022.3.62f3\Editor\Unity.exe`，换机器要显式传参。
- 服务端发布包：`tools/publish-tiny-server.ps1 -OutputPath <目录>` 产出 `host/`+`gateway/`+可编译 `composition/`；`AbilityKit__Tiny__EnableTurn=false` 关掉回合制。

## 相关 skill

- Room / Gateway / 恢复 / 同步协商的框架侧细节 → [coordinator](../coordinator/SKILL.md)
- 帧同步预测与回滚基础设施（Tiny 只做适配，机器在 `com.abilitykit.world.framesync`）→ [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 会话状态句柄/控制器准则（`TinyBattleSession` 的单飞与 generation 校验遵循它）→ [state-handles-controllers](../state-handles-controllers/SKILL.md)
- 验收产物落盘规范 → [test-artifacts](../test-artifacts/SKILL.md)
- 完整玩法生产线对照 → [moba-demo](../moba-demo/SKILL.md)；服务端权威同步对照 → [shooter-demo](../shooter-demo/SKILL.md)
- 新玩法装进 Orleans 玩法目录 → [host-extension](../host-extension/SKILL.md)

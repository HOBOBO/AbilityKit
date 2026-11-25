# MOBA 服务端权威状态同步战斗流程

## 目标

MOBA 的服务端状态同步复用现有 `BattleLogicHostGrain` 与 `IBattleRuntimeSession` 生命周期。同步模式决定时钟和状态分发方式；战斗规则仍运行在服务端 MOBA battle world 中。状态同步客户端只提交玩家命令，并从服务端接收权威快照与可靠事件。

## 服务端战斗流程

```text
Room StartBattle(syncTemplateId)
  -> 解析 moba-state-sync-authority
  -> BattleLogicHostGrain 初始化 BattleRuntimeSession
  -> MobaBattleRuntimeAdapter 从正式 launch spec 创建 BattleWorld
  -> 校验 gameplay 已启动、runtime port 和 snapshot provider 可用
  -> 启动 BattleLogicHostGrain 定时器

Gateway SubmitBattleInput
  -> BattleLogicHostGrain 校验 world/player/opcode/payload 与 runtime 规则
  -> BattleInputFrameScheduler 分配可接受的服务端帧
  -> MobaBattleRuntimeAdapter 映射为 PlayerInputCommand 并提交 World
  -> 定时 tick 推进 world
  -> 捕获可靠事件、按策略生成 actor 投影快照并投递观察者

断线恢复
  -> StateSync observer 订阅 / 请求 full snapshot
  -> 当前权威 actor roster 与状态作为 baseline 推送
  -> 可靠事件 epoch/watermark 一并恢复
  -> 客户端 ACK watermark，之后继续增量快照和事件流
```

`BattleLogicHostGrain` 负责通用 battle 生命周期、输入安全检查、定时驱动、快照节奏和事件恢复；`MobaBattleRuntimeAdapter` 负责 MOBA world 的建立、命令映射和状态投影。没有另设一套只服务于 StateSync 的战斗模拟器，因此 frame-sync 与 state-sync 使用同一组 MOBA gameplay、属性、技能和触发器逻辑。

## 当前正式配置

| 项目 | 值 |
|---|---|
| 模板 | `moba-state-sync-authority` |
| 服务端运行时 | `BattleWorld`，由 BattleLogicHost 定时推进 |
| 权威 | 服务端逻辑 World |
| 状态流 | Full baseline + delta snapshot，固定快照节奏 |
| 输入 | 立即提交，经安全检查与服务端帧调度 |
| 恢复 | 请求 full snapshot，恢复 reliable event epoch/watermark 并 ACK |
| 默认模板 | 仍为 `frame-sync-authority`，不影响现有默认入口 |

本配置属于可用的权威状态同步基线，覆盖当前 MOBA runtime 已导出的 actor 投影字段和可靠事件；完整技能状态（例如 buff、召唤物、冷却与 HFSM）是否可恢复，仍以各自 projection/recovery schema 和英雄验收为准，不能仅由移动 smoke 推断。

## 可重复服务端验收

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Server/Orleans/tools/run_moba_multiprocess_smoke.ps1 `
  -SyncTemplateId moba-state-sync-authority -TcpPort 44421
```

脚本启动独立 Orleans host 和场景客户端进程；场景客户端建立 owner/member 两条独立 TCP 会话，自动创建房间、进入战斗、订阅状态流、提交移动输入、验证两端观察到相同权威 Actor/位置，随后请求 full baseline 并 ACK 可靠事件 watermark。帧同步仍使用其自身的 frame/input 广播断言；状态同步不会误用帧确认或 catch-up 作为通过条件。

2026-11-10 验收记录：

- `moba-state-sync-authority` 双连接场景通过；输入接受帧 2，两观察者看到 Actor 1 从 `(-12.000, 0.000)` 移动到 `(-11.827, 0.000)`。
- full recovery 返回 frame 4、2 个 actors、schema 1；可靠事件 ACK 到 watermark 0。
- 本次为 server/gateway protocol smoke，不代表 Unity 双实例表现层验收，也不覆盖所有技能组件恢复。

## 正式客户端：移动预测和解（D1–D3 已确认）

实现分批推进。第一批按 `BattleStartPlan.Sync.SyncMode` 分开正式客户端世界宿主：`Session/Features/FrameSync/` 保留帧输入、检查点和预测时钟；`Session/Features/StateSync/` 独立导入权威快照，并在导入及基线提交成功后采用其 Tick，不安装帧预测驱动；`Session/Features/Shared/` 复用宿主和世界创建，不保存协议游标。帧同步附带的状态流不改变主同步模式。

第二批接到正式 MOBA 服务端：状态同步定时宿主在派发前对齐自己的逻辑 Tick，完成世界步进才可采样主控实体代际、姿态、持续移动意图、有效移速和移动资格。连续终结记录读取正常派发结果；未覆盖的结果、代际变化或序号缺口不提供证明，不能使用入队回执或最大消费序号替代。该类型化读取尚未随客户端快照交付：仍需绑定接收会话、输入代际、状态流和修订，再接入 D3 隔离重演。客户端仍默认权威驱动，不裁剪历史，不启用战斗预测。

权威投影共享 `BattleWorldProjectionViewSource` 和表现组件，不共享权威数据或时钟。状态同步的 `ShowConfirmedView` 只读取已提交快照重建的逻辑世界，恢复等待期间不提供源；帧同步仍读取按权威输入推进的 Confirmed 世界。状态同步主视图仍走远端快照插值，两者不会用诊断投影反向改写逻辑。

**第三批：终结记录绑定网关连接来源。** 网关完成鉴权后为输入附加服务端连接代际；状态同步宿主在已完成的 Tick 中记录它，读取时要求预期来源与实体代际同时匹配。换连接的输入到达后，不能填补旧连接的序号缺口；混入不同或未知来源时，本次世界运行对该玩家不再提供终结前缀，但继续正常处理输入并发布权威。连接代际不是客户端输入代际。共享固定基线快照仍不带逐观察者证明；后续需绑定接收会话、客户端输入代际和已提交快照修订，再启用和解。本批不改变客户端快照格式、帧同步输入或表现投影。

**第四批：已发布修订绑定到单次接收订阅。** 权威状态同步的服务端投递信封在预算校验成功后记录世界、流代际、修订、基线修订和 Tick。各观察者复用同一份不可变快照载荷，只附加自己的绑定、连接及订阅代际；观察者在入队和送出前检查接收身份，重连或重新订阅不接收旧信封。该元数据仅在服务端内部流转，不能当作客户端已提交或输入已终结的证明。帧同步的附属状态推送仍走原结构化路径。客户端输入生产者代际尚未跨正式输入和订阅入口传递，因此仍不下发终结证明、不裁剪、不重演。

**第五批：正式输入生产者代际。** 配对部署可在 `BattleStartPlanGatewayOptions` 显式设置 `UseScopedStateSyncInput`，仅固定基线 MOBA 状态同步选择独立的订阅与输入入口；默认关闭，旧请求格式不变。同一传输的编号分配、重试和重订阅共用生产者代际；新传输使用新代际。网关先校验账号、战斗、世界与本地主控，待订阅成功才接受相同代际的输入，不改变限速、去重或调度规则。终结账本同时核对输入代际、连接来源及实体版本；无匹配或发生换代混用时不提供终结前缀。接收订阅携带已鉴权的玩家与输入代际，但还未将移动锚点和终结证明随快照交给客户端，正式客户端仍只采用权威状态。

**第六批：移动锚点和连续终结证明随快照提交。** 显式启用 `UseScopedStateSyncInput` 的 MOBA 固定基线客户端使用新的证明订阅入口；旧订阅与输入请求格式不变，默认关闭。服务端从同一已完成 Tick 读取该接收者的移动截面，绑定主控生命周期、输入代际和固定基线修订。玩家专属证明不进入共享投影；它与该接收者的权威快照使用同一次投递。缺证明或附加后超出预算时只发原权威快照。客户端先检查订阅的世界、玩家和输入代际，只有固定基线准入、物化、世界导入或应用、基线及 Tick 提交都成功后，才在状态同步恢复会话中发布 `CommittedMovementProof`。换代、恢复和已提交快照缺证明时清除该值；不裁剪输入、不重演。

**第七批：正式客户端隔离移动预测（2026-11-24，E3 本地契约）**。网关状态同步启动在连接前绑定快照和恢复回调。`WithStateSyncMovementPrediction()` 与 `UseScopedStateSyncInput` 均启用且传输具备证明输入代际时才创建预测会话，默认关闭。移动输入从输入层携带类型化方向，保留传输首次分配的序号、输入代际和计划 Tick；回执和传输重试不作为终结证明。输入丢失、非移动输入或错过本地采集截止点时停止预测，直到权威证明覆盖相关输入；无法确认编号归属时本次输入代际不再预测。

`StateSyncMovementPrediction` 用独立固定步时钟封存输入 Tick（包括无输入 Tick）；收到已提交证明后，对照同 Tick 的姿态、持续移动意图、有效移速与移动资格。匹配且终结前缀不影响后续计划时复用预测，否则用单 Actor 隔离逻辑世界和正式普通移动系统重演，追到当前才发布。预算为 32 Tick、64 条待终结记录。权威领先直接采用其 Tick；缺证明、有碰撞求解依赖、受控、历史不足或换代时退回权威，不把已导入的逻辑世界继续步进。`ShowPredictionView` 可单独展示主控预览，`ShowConfirmedView` 和主视图仍只消费权威；不预测技能、投射物、命中和伤害。该批不表示地图碰撞已支持预测、线上已联调或 Unity 画面已验收。

`MobaLogicWorldStateImporter` 的常规导入仍不重建持续 `MoveInput`；预测使用移动证明中的值，而非继续运行原导入世界。`MobaLocalDualModeContractTests` 验证了直接步进原导入世界会使用旧输入。正式接线与逐 Tick 断言由 `AuthoritativeStateRecoveryRuntimeTests.ScopedMovementProof` 用本地类型化快照与传输替身验收。

**Battle Scenario 本地复现。** 打开 `Window → AbilityKit → 战斗测试用例编辑器`，在「状态同步复现样例 → 选样例」选择「MOBA · 现场运行正式客户端 → 移动纠偏与追平（离线）」。每次选中时重新运行客户端，使用类型化快照替代服务端下发，不连接或发送数据，不改当前草稿。逐步查看 Tick 14 收到 Tick 11 权威快照后的同 Tick 比较、权威锚点、Tick 12/13/14 重演和 Tick 15 继续预测。逐 Tick 姿态取自成功提交的重演记录；比较与锚点是诊断步骤，不表示候选世界的中间姿态会发布到正式视图。双投影只展示普通移动，不验证线上交付、碰撞、技能或帧同步。

服务端的 `AcceptedFrame` 在输入入队后返回，不证明命令结果已进入快照。宿主维护的最大消费序号同样不能证明缺口内的输入都已终结；移动证明使用截面对应的连续输入终结账本，不复用最大消费序号、可靠事件水位、快照序号或帧同步确认帧。

**建议首批范围：仅本地主控普通移动，默认保留权威驱动。**

| 已确认项 | 实施契约 |
| --- | --- |
| D1：移动锚点 | 用已提交的权威截面绑定主控身份及生命周期。包含 pose、持续移动意图、有效移速与该截面的移动资格；Motion 派生值按同一世界规则重建，不从显示速度反推输入。碰撞、动态控制或其他未覆盖依赖影响步进时，停止预测，只发布权威。 |
| D2：终结证明 | 服务端在该截面提交后生成连续终结前缀：纳入已体现在锚点中的输入，以及确定不会再执行的拒绝输入；未终结缺口不得跨过。证明绑定会话、流代际、主控生命周期、快照修订与 Tick；旧版或缺证明时继续权威导入，不裁剪、不重演。 |
| D3：会话编排 | StateSync 会话在快照成功导入并提交后才处理锚点，拥有本地固定步时钟和有界输入历史。对照同 Tick 快照与历史预测；有差异时在隔离候选中按原顺序重演未终结输入（包括空 Tick），追到当前才替换本地主控预测；权威领先时先采用其 Tick，不向过去重演。失败留在最近有效权威状态，换代清理旧历史。保持帧同步的输入帧时钟及恢复链不变。 |

**本地验收顺序**：在正式会话入口投递类型化快照，先证明全量导入及增量物化成功后才允许和解；再覆盖同 Tick 匹配、迟到错位、序号缺口、无证明、空 Tick、恢复后继续输入、权威领先、重复与换代。至少逐 Tick 对照独立权威世界，断言重演不二次发送输入或重复派发战斗副作用。编辑器样例只覆盖其中的迟到纠偏、空 Tick、逐 Tick 重演和继续预测，不能取代服务器与客户端联合验收；本提案不启用技能、投射物、命中或伤害预测，也不适用于帧同步。

## 后续边界

1. 保持该 server flow 为唯一 StateSync 战斗入口；有新同步语义时扩展通用模板、snapshot projection 或 recovery contract。
2. 扩展 server smoke 输入录制，使其可覆盖一次技能命令及权威生命值/事件变化；断言使用权威快照字段，不以客户端本地预测为 oracle。
3. 再运行 Unity 双实例 StateSync acceptance，单独校验插值、技能表现、断线 baseline 导入和继续输入；与当前 server smoke 分开记录证据。

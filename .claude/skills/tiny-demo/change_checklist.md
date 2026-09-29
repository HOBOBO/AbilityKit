# Tiny 跨层改动清单

Tiny 的价值之一是把"改一条规则要动几层"显式化（教程 08/10 就是这两道练习题）。改之前先按本文过一遍。

## 铁律

1. **规则只有一份源码**：`Unity/Packages/com.abilitykit.demo.tiny.logic/Runtime/Logic/`（回合制是 `com.abilitykit.demo.tiny.turn/Runtime/Logic/`）。**永远不要**在 `src/` 下另建副本——`src/AbilityKit.Demo.Tiny.Core` 等只是 `<Compile Include>` 投影。
2. **改 `Unity/Packages/**` 下的规则 = 同时改服务端**（服务端 csproj 依赖该目录树）。这是特性，不是巧合。
3. **不破坏 `noEngineReferences`**：`Logic` 与 `FrameSync` 装配必须保持纯净（无 `UnityEngine`、无 .NET 5+ API、无 `record`/file-scoped namespace）。见 `CLAUDE.md`「已知工程坑」。
4. 改完跑 `./tools/verify-tiny-starter.ps1 -SkipUnity`，Unity 侧改动再跑完整门禁。

## 例：把普通攻击伤害 10 → 12（教程 08 的练习）

规则改动本身只有一行（`TinyBattle.AttackDamage`），但**期望值散落在多处**：

| 位置 | 要改什么 |
|---|---|
| `tiny.logic/.../TinyBattle.cs` | `AttackDamage`；**同时 bump `RulesKey`**（见下） |
| Logic 样例与 `TinyLogicTests` | 相关哈希/状态期望 |
| 服务端 `TinyBattleIntegrationTests` | 目标 HP 断言（90 → 88） |
| 客户端 State/Frame/Hybrid 验收 | HP 期望 |
| `src/AbilityKit.Demo.Tiny.Client/TinyProcessSmoke.cs` | 双进程证据断言 |
| `tools/verify-tiny-starter.ps1` | 双进程证据断言里的 HP |
| `Tiny.Record.Sample` / `LiveRecord.Sample` | 最终状态 / 权威帧哈希 |
| `Tiny.BattleStyles.Sample` | **两次**命中结果（80 → 76，不是 90 → 88） |

**逐处按语义判断，不要批量替换数字**——同一个文件里可能有无关的测试数据。保持三字节 `TinyInput` 编码、`InputOpCode`、`TinyBattleStateCodec` 布局不变（本练习只改常量）。

预期：两客户端与独立进程都观察到 88 HP，恢复后的完整快照仍为 88 HP，Frame/Hybrid 哈希与重放测试通过。

**失败路径是有诊断价值的**：只改规则不改验收 → State TCP 断言失败；规则与客户端/服务端不共源 → Frame/Hybrid 的哈希或快照校验失败。记录这两种失败各出现在哪个阶段，才算完成练习。

回合制的对应练习：`TinyTurnBattle.MaxHp` 2 → 3，房主获胜回合从第 3 推迟到第 5；24 字节快照格式**无需**改变。

## 改规则时**必须**同步的三件事

1. **`RulesKey`**（`tiny:rules.v1`，回合制 `tiny:turn.rules.v1`）——客户端 `TinyAssetPreparation.Validate` 与服务端 `TinyRoomGameplayAdapter.BuildLaunchManifest` 同时消费。**纯人工约定，无自动检查**：不改会本地全绿、线上启动清单校验拒绝。
2. **确定性**：新逻辑保持纯整数；若引入浮点或不可稳定排序的集合，Frame/Hybrid 的哈希会漂移。溢出用 `long` 兜。
3. **共源**：确认改动落在 Unity 包内，且服务端构建仍能解析（服务端构建**要求 Unity 目录存在**）。

## 加/改同步模式

三处必须同时改，没有共享枚举强制一致（详见 [sync_modes.md](sync_modes.md)）：

1. `tiny.logic/.../TinySyncTemplates.cs` —— 模板 id 字符串（**唯一一份**，服务端与客户端共编译）。
2. 服务端 `TinyServerGameplayModule.Create()` —— 在 `ServerSyncCapabilityDeclaration.FromTemplates(...)` 里加一个 `ServerSyncTemplateDeclaration`（模板形状 + 档案 + schema 区间一次声明；`ServerGameplayModule` 的 `SyncProfile` 由此派生，不会再出现"两份声明打架"）。
3. 客户端 `tiny/Runtime/View/Sync/TinySyncMode.cs` —— 映射到自己的 `TinySyncMode` 枚举（服务端没有的镜像类型）。

两侧各有独立守卫：服务端 `TinySyncModeProfileTests`（`[Theory]`×3），客户端 `TinyBattleSessionTests.CreateRoomSelectsServerSyncTemplate`。改完两边都要跑。

若真的要改**线上** schema 版本：先改 `Protocols/` 目录并重新生成 + 兼容检查（见 [`protocol-wire`](../protocol-wire/SKILL.md)），再同步服务端与客户端能力协商。当前 Tiny 本地 schema 钉死 **1..1**，不兼容版本会在订阅前被拒。

## 加字段到战斗状态

- `TinyActorState` / `TinyBattleState` 加字段 → 同步改 `TinyBattleStateCodec`（布局与 `Decode` 的**精确长度校验** `Length == 5 + count*20`）、`ComputeHash`（顺序！）、`RestoreState` 范围校验、`TinyBattleRollbackProvider`（若属回滚字段）。
- 快照/回滚字段进 `FrameSync` 时要考虑**回滚快照环**的容量语义，并更新 `FullStateCodecPreservesRollbackFields` 类测试。
- 跨端一致性：State 客户端只读服务端快照、不自行推导伤害；Frame/Hybrid 必须与服务端共源。

## 在途改动（未提交，2026-09-28 观察）

工作区有与本 skill 相关的**未提交改动**，动手前先 `git status` / `git diff` 确认（仓库存在并行编辑会话，见 `CLAUDE.md`）。

**Tiny 相关未提交改动共 18 个文件、+522 / −234**（`git diff --stat` 实测，2026-09-28）。**主干是"Tiny Turn 端到端"**，服务端那几个文件只是支撑：

| 改动 | Δ | 内容 | 行为影响 |
|---|---:|---|---|
| `tiny.turn/Runtime/View/TinyTurnSession.cs` | **243** | 回合制会话重写（主干） | 进行中 |
| `tools/verify-tiny-starter.ps1` | **271**（改写） | 加 `-ServerBundlePath` / `-UseCompositionHost` / `-FocusChapter` / `-IncludeTurn` / 跨进程阶段 | 验收面扩大 |
| `tiny.turn/Runtime/View/TinyTurnGameplayRoot.cs` | 54 | 配合会话重写 | 进行中 |
| `tools/tiny-consumer-template/Turn~/.../TinyConsumerCrossProcessPlayModeTests.cs` | 49 | Turn 跨进程 PlayMode 断言 | 测试 |
| `tiny.turn/Tests/NetworkPlayMode/TinyTurnGatewayPlayModeTests.cs` | 47 | Turn 网络 PlayMode 断言 | 测试 |
| `RoomLaunchManifestBuilder.cs`（服务端） | 25 | 内联 SHA-256 改为委派新共享助手 `RoomLaunchManifestHash` | **无**（逐字节等价：同样的 `ref:`/`meta:` 行框、`StringComparer.Ordinal` 排序、UTF-8 → SHA-256 → 小写 hex） |
| `tiny/Runtime/View/Loading/TinyAssetPreparation.cs` + `TinyBattleSession.cs` + `TinyBattleSessionTests.cs` | 10/1/4 | 客户端侧改用共享清单兼容助手 | 进行中 |
| `AbilityKit.Orleans.Host/Program.cs` | 10 | Tiny Turn 变为可开关：`AbilityKit:Tiny:EnableTurn`，**默认 true** | 默认行为不变，多一个关闭开关 |
| `TinyRoomGameplayAdapter.cs`、`TinyTurnGameplayModule.cs` | 5/2 | `"tiny:arena"` / `"tiny:rules.v1"` 字面量换成 `TinyBattle.AssetKey` / `RulesKey` | **无**（值相同，纯去重） |
| `TinyBattle.cs` / `TinyTurnBattle.cs`（共享规则） | +2 each | 新增 `AssetKey` / `RulesKey` 常量 | **无** |
| `Docs/tutorials/tiny/{00,07}`、`IntegrationGuide.md`、`tiny-consumer-template/README.md` | 2/8/10/11 | 同步文档 | 仅文档 |

**新增未跟踪文件**（清单哈希客户端/服务端统一的最小集）：`protocol.room/Runtime/Room/RoomLaunchManifestHash.cs`、`network.room/Runtime/RoomGatewayLaunchManifestCompatibility.cs`、`src/AbilityKit.Network.Room.Tests/RoomGatewayLaunchManifestCompatibilityTests.cs`、`src/AbilityKit.Network.Room.Tests/TinyTurnSessionTests.cs`。`-UseCompositionHost` 就是为了验**发布包**（或编译出的 `composition/TinyCustomHost`）而不是源码树；`EnableTurn` 开关让同一份发布包能跑 Turn / State / Frame 的跨进程套件而不用重建。

> **顺带发现的仓库卫生问题**：`Server/Orleans/logs/dev/gateway.log` **是被跟踪的**（HEAD 里 4,371,832 字节），工作区已涨到 **7,885,942 字节**，且 `git check-ignore` 判定**未被忽略**。它与 Tiny 无关，但会被一并提交——建议移出跟踪或加 `.gitignore`（`CLAUDE.md` 的约定是测试产物落 `local/Logs/`，而 `local/` 已被忽略）。

## 排查症状速查

| 现象 | 先查 |
|---|---|
| 登录/建房失败 | Gateway 地址端口、账号会话令牌、服务端是否注册 `TinyServerGameplayModule` |
| 长时间 `Waiting for baseline` | Room 是否进入战斗、World 是否匹配、全量快照请求与推送是否成功 |
| `Resynchronizing` | 帧历史、哈希、收件箱溢出、快照是否覆盖缺口 |
| 返回大厅失败 | 大厅与 Tiny 场景是否都在 Build Settings、返回场景名 |
| 只有 Tiny 房间起不来、但没有任何报错 | `RoomFrameSyncRoute` / `RoomNetworkSyncCapabilityResolver` 是否**漏传**了 `ServerGameplayModuleCatalog`（会静默回退 Default，Tiny 消失） |
| 客户端启动清单校验拒绝（本地测试却全绿） | `RulesKey` 是否随规则改动一起 bump |
| 一批 Editor 用例运行时失败、编译却通过 | 是否重命名了 `TinyBattleSession` / `TinyGameplayRoot` 的**私有字段**（`TinyBattleSessionTests` 靠反射） |
| 编辑器列出玩法时看不到 tiny | `GatewayGameplayCatalog` 是硬编码 2 项的框架缺口（见 [server_and_host.md](server_and_host.md)） |

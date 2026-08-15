# AbilityKit ServerRuntime Starter

ServerRuntime 组合的最小可运行入口：用 `host`（权威服运行时）+ `host.extension`（帧同步驱动 /
服务器时间模块）把一个权威战斗世界放到服务器上跑，本地回环客户端演示完整闭环。

## 运行

```bash
dotnet run --project src/AbilityKit.Samples.ServerRuntime
```

## 它证明了什么

> 客户端只发输入、只收帧包；权威逻辑全部在服务器侧闭环。

```
客户端                 服务器（HostRuntime）
  │ SubmitInput ──────▶ FrameSyncDriverModule（IFrameSyncInputHub）
  │                      │ PreTick：输入 flush → world.Services.Resolve<IWorldInputSink>().Submit
  │                      │ Tick：权威世界确定性推进
  │                      │ PostTick：IWorldStateSnapshotProvider 取快照 → Broadcast(FrameMessage)
  │ ◀──── FrameMessage(帧号 + 当帧输入 + 权威快照) × 30 帧
  │ ◀──── WorldCreated / WorldDestroyed 生命周期消息
```

运行输出末尾：
```
[Server] 最终权威状态 —— x=2.00, hp=50
[Client] 共收到 30 个帧包（含每帧广播的权威快照）
[结论] 客户端只发输入、只收帧包 —— 权威逻辑全部在服务器侧闭环。
```

（与 [SyncRuntime Starter](../AbilityKit.Samples.SyncRuntime) 用同一套输入语义（每帧右移、每 5 帧受击），
30 帧后同为 x=2.00 / hp=50 —— 同一逻辑在本地帧驱动和服务器权威驱动下结果一致。）

## 它演示了什么

| 能力 | 包 | 在示例中的位置 |
|---|---|---|
| 权威服运行时（世界生命周期 + Tick + 广播） | host | `HostRuntime.CreateWorld / Tick / Broadcast` |
| 运行时扩展模块装配 | host | `HostRuntimeModuleHost.Add + InstallAll`（hook 挂接 + feature 注册） |
| 帧同步驱动模块（输入聚合 → flush → 帧包广播） | host.extension | `FrameSyncDriverModule`：PreTick/PostTick 两个钩子构成权威帧循环 |
| 服务器帧时间 | host.extension | `ServerFrameTimeModule(fixedDelta)` |
| 世界侧输入契约 | host | `ServerBattleWorld : IWorldInputSink`（模块通过 `world.Services` 解析） |
| 世界侧快照契约 | host | `ServerBattleWorld : IWorldStateSnapshotProvider`（模块 PostTick 取快照广播） |
| 模拟客户端连接 | host | `LoopbackClient : IServerConnection`（只实现 `Send`） |
| 帧包结构 | world.networkfragments | `FramePacket(worldId, frame, inputs, snapshot)` / `FrameMessage` |

## 关键设计点

- **模块通过契约解耦**：`FrameSyncDriverModule` 不认识具体世界，它在 PreTick 从
  `world.Services` 解析 `IWorldInputSink` 投递输入，在 PostTick 解析 `IWorldStateSnapshotProvider`
  取快照广播。接入方的世界实现这两个契约即可被帧同步驱动，无需改模块。
- **hook + feature 双扩展面**：模块 Install 时把回调挂进 `HostRuntimeOptions` 的
  WorldCreated/PreTick/PostTick hook 容器，并把自身注册为 `IFrameSyncInputHub` /
  `IFrameSyncDriverEvents` feature——外部经 `runtime.Features` 拿 hub 提交输入。
- **传输层可替换**：本演示用本地回环（`IServerConnection` 只有一个 `Send` 方法）；
  生产接入 `network.transport.*`（WebSocket/LiteNet）或 Orleans Gateway 即可，世界与模块代码不变。

## 组合全景

至此五个 Starter 覆盖 README 的全部推荐组合：

| Starter | 组合 |
|---|---|
| Foundation | core + world.di |
| SkillCore | + triggering + pipeline + modifiers |
| BattleRuntime | + targeting + projectile + damage |
| SyncRuntime | framesync + statesync + record |
| ServerRuntime | host + host.extension（权威服闭环） |

真实项目 = 从任一层切入 + 按需组合（例：BattleRuntime 的战斗内容 + ServerRuntime 的权威驱动
+ SyncRuntime 的录像回放 = 一个完整的帧同步服务端）。再往上接 `protocol` + `network.transport.*`
+ `Server/Orleans` 即为生产形态。

组合分级的完整定义见 `Unity/Packages/README.md`。

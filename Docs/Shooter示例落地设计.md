# Shooter 示例落地设计

## 目标

Shooter 示例用于展示 AbilityKit 多玩法聚合能力。它应该比 MOBA 小很多，但链路要完整：公共房间协议、玩法协议、逻辑世界、输入提交、帧驱动、快照输出、视图消费、Orleans 多玩法 adapter。

## 从 MOBA 保留的关键链路

MOBA 中值得保留的是结构，不是复杂玩法内容：

- 世界蓝图：`IWorldBlueprint` + `WorldCreateOptions` + `IWorldModule`，用于声明玩法世界类型和安装运行时服务。
- 运行端口：类似 `IMobaBattleRuntimePort`，作为外部进入逻辑层的单一入口，承载 StartGame、SubmitInput、GetSnapshot。
- 会话驱动：类似 `MobaBattleDriverHost`，只驱动世界 Tick 和端口调用，不直接操作实体。
- 启动适配：类似 `MobaSessionCoordinatorHost`，负责 world type、world id、初始玩家数据和配置注入。
- 网关房间入口：统一使用 `com.abilitykit.protocol.room`，玩法只提供 roomType/gameplayId/worldType 和玩法启动参数。
- 快照分发：类似 `FrameSnapshotDispatcher` 的 opCode -> decoder -> handler 路由模型，可作为视图侧通用范式。

## 不作为 Shooter 模板复制的 MOBA 内容

以下内容是 MOBA 历史复杂度或特定玩法能力，Shooter 第一版不复制：

- Entitas 生成上下文和大量 ECS 系统安装链。
- 技能流水线、Buff、Trigger、Effect、DamagePipeline 的完整组合。
- 投射物、范围、召唤物、碰撞服务的复杂配置表驱动形态。
- 复杂预测/确认双世界表现；Shooter 第一版落地单客户端本地预测、权威快照校正、未确认输入重放，并接入框架快照型 rollback provider。
- MOBA 专属 hero/loadout/team/spawn/skill 配置结构。
- 大量 Unity 表现绑定、VFX、浮字、区域表现和触发器表现事件。

## Shooter 最小完整闭环

Shooter 采用更直接的玩法模型：

- 玩家：id、位置、朝向、hp、score、alive。
- 输入：moveX、moveY、aimX、aimY、fire。
- 子弹：id、owner、position、velocity、ttl。
- 逻辑：固定帧推进，玩家移动，开火生成子弹，子弹命中扣血和加分。
- 快照：每帧输出玩家数组、子弹数组、事件数组。
- 视图：按快照创建/更新玩家和子弹表现，事件可先只打印或触发简单效果。

## 包边界

- `com.abilitykit.protocol.shooter`：Shooter wire 协议、opCode、MemoryPack codec。
- `com.abilitykit.demo.shooter.share`：玩法常量、通用描述、输入/快照领域模型。
- `com.abilitykit.demo.shooter.runtime`：逻辑世界蓝图、运行端口、最小战斗状态和 Tick。
- `com.abilitykit.demo.shooter.view.runtime`：Unity 表现入口、网关房间接入、快照应用。
- Orleans：新增 `ShooterRoomGameplayAdapter` 和 `ShooterBattleRuntimeAdapter`，注册 roomType=`shooter`。

## 第一版验收线

- Unity 包能被 asmdef 识别。
- .NET 能编译 Shooter protocol 桥接项目。
- Orleans 能创建 `roomType=shooter` 的房间并启动 battle。
- Shooter runtime 可接受输入并推进帧。
- Shooter snapshot 能被服务端推送、客户端解码。

## 网络协议与战斗时间锚点分析

Shooter 的同步方案不是单纯状态同步，也不是单纯帧同步，而是两条协议链路同时存在：

- 帧同步输入链路：客户端按目标帧提交输入并本地预测推进，服务端按固定 Tick 消费输入并推进权威世界。
- 状态同步链路：开始、晚进、重连、校正时，服务端下发权威快照，客户端覆盖本地世界后裁剪已确认输入并重放未确认输入追帧。

当前协议已经有基础雏形，但还不能完整支撑严格的双端同帧追赶。

### 当前已有能力

- Room Gateway 生命周期协议已经覆盖创建、加入、准备、启动、订阅、输入提交：`WireCreateRoomReq`、`WireJoinRoomReq`、`WireStartRoomBattleReq`、`WireSubscribeStateSyncReq`、`WireSubmitBattleInputReq`。
- 输入提交已经带 `BattleId`、`WorldId`、`Frame`、`PlayerId`、`InputOpCode`、玩法 payload，可承载 Shooter 的 `ShooterPlayerCommand`。
- 状态同步推送已经有 `WireStateSyncSnapshotPush`，包含 `WorldId`、`Frame`、兼容字段 `Timestamp`、明确服务端时间域的 `ServerTicks`、`IsFullSnapshot`、`PayloadOpCode`、`Payload`。
- Shooter 协议已经区分输入、启动、普通快照、packed 快照、delta 快照和 state hash。
- packed 快照已支持 `Full`、`Delta`、`KeyFrame`、`AuthorityOverride` 标记，可用于开始/重连覆盖和普通增量同步。
- 时间同步协议已有 `WireTimeSyncReq` / `WireTimeSyncRes`，并统一返回与 `WorldStartAnchor` 一致的 server ticks 时间域，可让客户端估算服务端时间。
- Room 协议已经定义 `WireWorldStartAnchor`，包含 `StartServerTicks`、`ServerTickFrequency`、`StartFrame`、`FixedDeltaSeconds`。
- 客户端追帧计算已抽到 host extension client：玩法只需要把自己的 anchor wire model 映射为通用 `WorldStartFrameAnchor`，再通过 `WorldStartFrameCatchUpCalculator` 计算目标帧和追赶帧数。
- Shooter `ShooterStartGamePayload` 已追加 `WorldId`、`StartServerTicks`、`ServerTickFrequency`、`StartFrame`、`FixedDeltaSeconds`；Gateway launcher 会把 Room Gateway flow 的 anchor 合并到玩法启动 payload 后再启动 runtime，服务端 Shooter adapter 也会使用 `BattleLogicHostGrain` 生成的同一份 anchor。
- Shooter Gateway launcher 已在 `StartGame` 后调用客户端帧同步控制器追到 `flow.TargetFrame`，启动、晚进、重连路径拿到 anchor 后会先把本地逻辑世界推进到目标帧，再进入后续输入提交。

### 关键缺口

- `WireWorldStartAnchor` 已从 Orleans 战斗生命周期传回 `WireStartRoomBattleRes` 与运行中 `WireJoinRoomRes`，首次进入、重连、晚进都能拿到同一局的时间锚点。
- `BattleLogicHostGrain` 已记录战斗开始 server ticks、tick frequency、start frame 和 fixed delta；Room Gateway 返回 `ServerNowTicks` 作为客户端首轮追帧计算基准。
- `TimeSyncHandler` 使用与 Room Gateway 相同的 `DateTime.UtcNow.Ticks` / `TimeSpan.TicksPerSecond` 时间域，避免 `Stopwatch` ticks 和 anchor ticks 混算。
- `WireStateSyncSnapshotPush.ServerTicks` 已作为明确字段追加，`Timestamp` 保留为兼容字段；后续新逻辑应优先读取 `ServerTicks`，逐步避免测试或业务继续写入小数秒语义。
- `ShooterStartGamePayload` 已与 Room Gateway 的 start anchor 对齐，客户端启动 spec 和服务端 Shooter runtime 启动 spec 都能携带同一局的 `WorldId` / `StartServerTicks` / `ServerTickFrequency` / `StartFrame` / `FixedDeltaSeconds`。
- 输入响应 `WireSubmitBattleInputRes` 只有 `AcceptedFrame`，缺少服务器当前帧/拒绝原因枚举/建议重同步标记。第一版可以先保留，但后续纠偏会需要更明确的反馈。

### 推荐协议形态

开始战斗时，服务端应该返回一个明确的世界时间锚点：

- `WorldId`：本局逻辑世界标识。
- `BattleId`：输入和订阅绑定的战斗标识。
- `StartServerTicks`：服务端时间域中的战斗起始 ticks。
- `ServerTickFrequency`：服务端 ticks 频率。
- `StartFrame`：通常为 0，也允许恢复/迁移场景从非 0 开始。
- `FixedDeltaSeconds`：固定帧间隔，例如 `1.0 / 30.0`。
- `ServerNowTicks`：响应发出时服务端 ticks，便于客户端立即估算应该追到哪一帧。

重连/晚进时，客户端应先通过 TimeSync 估算服务端时间偏移，再 Join/Subscribe，然后接收带 `AuthorityOverride` 的 full/keyframe packed snapshot。客户端导入快照后，根据：

```text
elapsedSeconds = (estimatedServerNowTicks - StartServerTicks) / ServerTickFrequency
targetFrame = StartFrame + floor(elapsedSeconds / FixedDeltaSeconds)
catchUpFrames = targetFrame - snapshot.Frame
```

将本地世界追到目标帧。追赶期间可以只消费本地已知输入/空输入，直到下一次权威快照校正。客户端目标帧与追赶帧数由 host extension client 的 `WorldStartFrameCatchUpCalculator` 统一计算；预测校正由 host extension 的输入历史与 reconciliation coordinator 承担通用编排；本地回滚状态由 `com.abilitykit.world.framesync` 的 `IRollbackStateProvider`、`RollbackCoordinator` 和 `RollbackSnapshotRingBuffer` 管理，Shooter 通过 packed snapshot provider 提供序列化/反序列化边界。

### 预测回滚类型边界

- 快照型回滚：用于玩家、子弹、血量、分数、当前帧等纯逻辑状态。Shooter 使用 `ShooterPackedSnapshotRollbackProvider` 包装 `ExportPackedSnapshotBytes` / `ImportPackedSnapshotBytes`，预测 tick 后写入本地 rollback ring buffer，收到权威快照后导入权威状态并重放未确认输入。
- 命令补偿型回滚：用于不适合进入纯状态快照的可逆副作用，例如预测创建表现对象、注册外部资源或维护结构关系。框架通过 `CommandRollbackLog` 记录 `CommandType + PayloadVersion + byte[] Payload`，由密封的 `RollbackCommandHandlerRegistry` 分派稳定 Handler；`CommandRollbackStateProvider` 在快照中保存精确 Journal Checkpoint，恢复时预检完整命令后按 Order 逆序执行。命令不得捕获运行时对象闭包，一次性表现事件还应配合 PredictionKey 去重/取消。
- 两类机制可以同时注册到 `RollbackRegistry`。纯逻辑状态优先走快照型 provider，副作用和命令生命周期走命令补偿日志，避免把表现副作用混入战斗状态快照。

### 推荐落地顺序

1. 在 Room/Orleans contract 中新增统一的 `WorldStartAnchor` 模型，并让 `StartRoomBattleResponse` 携带它。
2. 在 `BattleLogicHostGrain.InitializeBattleAsync` 记录战斗开始 server ticks、tick frequency、start frame、fixed delta。
3. 扩展 `WireStartRoomBattleRes`，追加 `WireWorldStartAnchor WorldStartAnchor` 和 `long ServerNowTicks`，保持 MemoryPack 字段追加以降低破坏性。
4. 让 `WireJoinRoomRes` 在战斗已存在时返回真实 `WorldStartAnchor`，用于重连/晚进。
5. 明确 `WireStateSyncSnapshotPush.Timestamp` 语义，已追加 `ServerTicks` 作为优先使用的服务端时间域字段，`Timestamp` 保留兼容。
6. Shooter 客户端在 start/join/subscribe 后保存 anchor，并通过 host extension client 的通用追帧计算器计算目标追赶帧；Gateway launcher 已在启动 session 前把 flow anchor 合并到 `ShooterStartGamePayload`，启动后把本地 runtime 追到 `flow.TargetFrame`。
7. `ShooterStartGamePayload` 已补齐 `WorldId` / start anchor 字段，并保持旧 4 参数构造兼容；Orleans `BattleInitParams` 会把 `BattleLogicHostGrain` 生成的 `WorldStartAnchor` 传给 Shooter 服务端 runtime。
8. 为开始、晚进、重连三类路径分别加协议测试，确保 wire 层字段完整且客户端能算出并执行正确 catch-up frame；通用计算器另有边界测试覆盖。
9. 输入提交响应已追加 `CurrentFrame`、`Status`、`ShouldResync`、`ServerTicks`，客户端可据此区分正常接收、重映射、拒绝和需要快照重同步的情况。
10. Shooter view 层推荐通过 GameFramework `INetworkManager` / `INetworkChannel` 创建 Gateway 连接，`ShooterClientConnectionFactory.FromGameFrameworkNetwork` 和 `FromGameFrameworkChannel` 只把 GameFramework 通道包装成 AbilityKit `IConnection`；Shooter 同步、房间和预测代码继续只依赖 `IConnection`，`TcpTransport` 路径保留为测试和非 Unity fallback。

### 同步模块当前验收线

- 网络入口：Unity 组合层优先使用 GameFramework network channel，经 `com.abilitykit.gameframework.network` 适配为 AbilityKit `IConnection`；Shooter 示例自身不实现 socket/channel 生命周期，只展示 Gateway 协议和同步能力。
- 启动链路：Room Gateway 返回 `WorldId`、`WorldStartAnchor`、`ServerNowTicks`；Shooter launcher 将 anchor 合并进 `ShooterStartGamePayload`，并在启动后追到 `flow.TargetFrame`。
- 输入链路：客户端提交当前本地帧输入；服务端通过 frame scheduler 计算 `AcceptedFrame`，并返回 `CurrentFrame`、`Status`、`ShouldResync`、`ServerTicks` 供客户端诊断和后续重同步策略使用。
- 推送链路：服务端状态同步推送优先携带 `ServerTicks` 和 packed snapshot payload；客户端 gateway connection 收到 `SnapshotPushed` 后分发到 session。
- 校正链路：客户端导入权威 packed snapshot 后，裁剪已确认输入，重放未确认输入，并发布 reconciliation 结果和最新表现快照。
- 防乱序链路：客户端忽略晚到的旧权威快照，避免已经预测到更高帧后被旧包回退。

### 冒烟测试验证流程

冒烟测试目标不是覆盖所有玩法细节，而是验证 Shooter 作为正式同步示例的端到端协议闭环：

1. 启动 Orleans host 与 Gateway，确认 TCP Gateway 端口可连接。
2. 使用 guest login 获取 `SessionToken`。
3. 创建 `roomType=shooter` 的房间，加入房主并设置 ready。
4. 调用 start battle，断言响应包含非零 `WorldId`、非空 `BattleId`、有效 `WorldStartAnchor` 和 `ServerNowTicks`。
5. 调用 subscribe state sync，断言订阅成功，并等待至少一帧 `WireStateSyncSnapshotPush`。
6. 客户端根据 start anchor 计算 `TargetFrame`，启动 Shooter runtime，并追到目标帧。
7. 提交一帧 Shooter 输入，断言 `WireSubmitBattleInputRes.Success=true`，`AcceptedFrame>=RequestedFrame`，`CurrentFrame>=0`，`Status` 非空，`ServerTicks>0`。
8. 如果输入被重映射，记录 `Status` 和 `AcceptedFrame`；如果 `ShouldResync=true`，冒烟流程必须等待下一帧 authority snapshot 并确认客户端能导入校正。
9. 等待 packed snapshot 推送，断言 `ServerTicks>0`、`PayloadOpCode` 为 Shooter packed snapshot 类型、payload 可被 `ShooterPackedSnapshotCodec` 解码。
10. 将推送交给 `ShooterClientSession.ApplyGatewayPush`，断言返回 `AppliedPackedSnapshot` 或对旧帧返回 `IgnoredStaleSnapshot`；成功应用时客户端 runtime frame、state hash、presentation frame 与权威 snapshot 对齐。
11. 连续提交多帧输入并模拟一次权威 snapshot 晚到，断言旧快照不会回退客户端已预测状态。
12. 冒烟输出应记录 `BattleId`、`WorldId`、start anchor、target frame、每次输入的 requested/accepted/current/status/server ticks，以及最后一次 snapshot frame/hash，方便定位同步问题。

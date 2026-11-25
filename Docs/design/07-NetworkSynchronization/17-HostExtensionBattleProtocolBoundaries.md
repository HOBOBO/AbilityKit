# Host Extension 战斗流程与协议边界

本文固定首发的职责边界，描述现有接入，不改变运行时、线上消息或兼容策略。**提交被接受、命令实际执行、权威状态已发布，是三个不同的事实。**

## 1. 首发职责

| 所有者 | 负责 | 不由该层推断 |
| --- | --- | --- |
| `host.extension` | 宿主步进、世界驱动/启动钩子、输入队列与帧包适配、预测和回滚装配原语 | 房间就绪、玩家归属、命令执行结果、权威恢复是否完成 |
| 网关与项目会话 | 认证与玩家绑定、房间/开局、订阅范围、战局与运行实例切换、重连协商 | 传输成功或提交成功不等于玩法成功 |
| 战斗权威与项目协议 | 排帧和玩法执行；按模式发布可验证的结算记录或状态；定义消息格式、顺序、恢复与兼容规则 | 不从通用帧包或通用缺帧补值反推出领域结果 |

`WorldAutoStartModule` 只是调用世界提供的启动处理器，不代替网关或房间批准开局。`FramePacketNetAdapter` 是 MOBA 客户端的双世界装配，不是框架默认入口。

## 2. 三条权威链

**通用帧包：输送宿主帧输入与可选快照。**

```text
可信会话适配 → FrameSyncDriverModule 入队 → 帧前提交世界输入 → 帧尾包装 FramePacket
→ 已绑定的帧包流（可选分片聚合）→ 项目会话适配（例如 MOBA FramePacketNetAdapter）→ 输入缓冲 / 快照路由
```

[`FrameSyncDriverModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs#L142) 的 `SubmitInput` 返回真仅表示当前会话接受入队，方法内不校验 `clientId` 与玩家的归属。帧尾包携带输入批次，不过滤为已执行命令。多个快照拆分到同帧多个包时，首包带输入，其余只带快照。接收侧的 [`RemoteFrameAggregator`](../../../Unity/Packages/com.abilitykit.world.networkfragments/Runtime/Frames/RemoteFrameAggregator.cs) 只按整数帧号聚合，非空输入按到达顺序追加；[`FramePacketNetAdapter`](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/FramePacketNetAdapter.cs#L67) 在 MOBA 会话层仍不做世界/代际校验，可初始化 `FillDefault` 双缓冲，输入路由异常被记录后仍可能路由快照。调用者必须按已验证的会话和世界隔离、在切换实例时更换或清理缓冲；不能把这些工具当作身份校验、分片闭合、去重或原子提交的证据。

**MOBA Orleans：消费已提交的结算记录，而不是再解释原始输入批次。**

```text
SubmitFrameInputHandler → 按绑定代际提交给 BattleFrameSyncGrain → BattleLogicHostGrain 执行
→ SettledFrameHistory 连续提交 → 实时推送 / 按游标补帧 → MobaSettledFrameReceiver
→ ResolveSettledFrames → 客户端远端驱动
```

[`SubmitFrameInputHandler`](../../../Server/Orleans/src/AbilityKit.Orleans.Gateway/Gateway/Handlers/SubmitFrameInputHandler.cs#L58) 检查账号、房间、世界、玩家与结算订阅绑定，提交应答仍不是玩法回执。[`BattleLogicHostGrain`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L654) 才生成 `Executed`、`Rejected`、`Duplicate` 及领域决策，[`BattleFrameSyncGrain`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs#L598) 先提交历史再推送。客户端 [`MobaSettledFrameSession`](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Net/MobaSettledFrameSession.cs#L103) 绑定订阅，[`MobaSettledFrameReceiver`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Input/MobaSettledFrameReceiver.cs) 对实时与补帧做同一身份校验。帧零是初始化证明，不是可替代世界快照的存档；客户端在该证明之后只连续消费，不合成结算帧。同身份同帧的缓冲记录或仍保留摘要的已消费记录可去重，内容冲突会使该接收器故障。

**Shooter：消费服务器输出的权威状态，具体路径按 profile 装配。**

```text
Shooter 输入提交策略 → 战斗输入网关 → 服务端战斗运行时
→ ShooterBattleRuntimeAdapter 导出状态 → 项目同步控制器应用快照 → 表现 / 对账
```

[`ShooterRemoteInputSubmitStrategy`](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Unity/PlayMode/ShooterRemoteInputSubmitStrategy.cs#L35) 使用有界在途、只保留最新待发项的队列；这不是可靠逐命令通道。只有允许待发项被替换的输入才可复用该策略。一次性语义若要求逐条处理，不能从这个队列得到交付保证。战斗输入应答映射 `Accepted`、帧、`Status`、`ShouldResync`，不是快照也不是命中或伤害结算。[`ShooterBattleRuntimeAdapter`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplays/Shooter/Battle/ShooterBattleRuntimeAdapter.cs#L594) 区分 Packed 与 PureState 输出；客户端按 [profile 装配](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientSyncControllerFactory.cs)，不要求接入 MOBA 的帧输入双写或结算源。

## 3. 消息语义与恢复

| 消息 | 键与信任来源 | 能证明什么 / 何时可消费或恢复 |
| --- | --- | --- |
| 通用 `FramePacket` | 包带 `WorldId`、`Frame`，不带运行代际；聚合器实例内部的键仅是 `Frame` | 表示发送者包装的输入批次和可选快照，不表示执行回执。单包空输入也可能只是同帧快照片。分片何时齐备、是否等待或补值、恢复用哪个可信基线，由会话装配定义；`FillDefault` 的空数组不是服务端证据。 |
| MOBA 提交应答 / 结算帧 | 上行由绑定订阅提供代际；下行身份为协议版本、`RoomId`、`WorldId`、`BattleId`、`RuntimeGeneration`，再以 `Frame` 定位记录 | 应答只报告是否接受排帧。含零条命令的正常结算帧仍是权威记录。新世界从初始化证明开始；缺口从同代际历史请求连续后缀，冷补帧依赖已有记录，历史不足或代际冲突不假报成功。对应依据见 [`SettledFrameHistory`](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/SettledFrameHistory.cs)。 |
| Shooter Packed 状态 | 项目绑定的世界，负载带 `WorldId`、`Frame`、版本和快照标志；[`ShooterFrameworkSnapshotPipeline`](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterFrameworkSnapshotPipeline.cs#L297) 在应用路径检查版本及帧顺序 | Full 与 Delta 分别由导入器重建和更新状态；不能把帧序检查当成 PureState 的基线校验，也不能用补一条空输入恢复状态。 |
| Shooter PureState 状态 | 项目绑定的流与帧；增量描述还带 `BaselineFrame`、`BaselineHash` | [`ShooterPureStateSnapshotSyncController`](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterPureStateSnapshotSyncController.cs) 区分基线缺失与不匹配；需要全量时由项目会话请求权威基线。可靠事件水位与输入提交应答、状态基线各有语义，不互相替代。 |

## 4. 诊断与不承诺项

- [通用帧包表征测试](../../../src/AbilityKit.Host.Extension.Tests/FramePacketProtocolCharacterizationTests.cs) 覆盖同帧快照片先到、重复与跨世界包混入同一聚合器、显式空与缺帧的聚合结果，以及缓冲器 `FilledDefaultCount` 的区别。测试记录现实现象，不承诺允许重复、串场或缺帧即空输入。
- MOBA 代际、连续提交、帧零证明和冷补帧沿用 [现有结算历史测试](../../../Server/Orleans/src/AbilityKit.Orleans.Grains.Tests/Battle/SettledFrameHistoryTests.cs)；不在通用宿主测试中重新造一套结算历史。
- 旧 `FrameSyncCatchUpClientModule` 保留但不进入首发重连承诺，不用它替代活跃的 MOBA 结算补帧或 Shooter 全量恢复。逐世界帧提交/回滚失败策略见 [Host Extension 稳定化审计](../10-EngineeringQuality/18-HostExtensionContractAudit.md)，是独立决定。改变身份键、分片闭合、去重、补帧、回滚或玩法结算语义，需单独确认后再实现。

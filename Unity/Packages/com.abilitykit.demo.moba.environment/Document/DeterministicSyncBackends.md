# BattleFlow 确定性同步后端使用说明

使用 `MobaBattleFlowDslParser.Parse` 解析 MOBA 网络场景，再通过
`BattleFlowCompiler.Compile` 编译。BattleFlow 编辑器通过
`BattleFlowRunnerRegistry.DslParser` 注册项目解析器，解析过程不再临时修改全局断言工厂。

## 后端选择

```text
sync-backend headless
```

`headless` 是默认后端，在 .NET 中运行。它使用正式的状态哈希编解码器，
将权威状态哈希直接交给真实的 `ClientPredictionDriverModule`。

```text
sync-backend unity-route
```

`unity-route` 在 Unity 进程内运行，使用以下正式同步路径：

```text
BattleLogicSession.InjectRemoteFrame
-> FrameSnapshotDispatcher
-> BattleSyncFeature
-> ClientPredictionDriverModule
```

两个后端共用 `MobaBattleFlowPredictionScenarioRunner`，使用相同的虚拟网络场景播放器、
整数毫秒时钟和固定步长累加器。它们都不创建真实套接字，也不按真实时间推进。
如果将请求 Unity 后端的场景交给 .NET 执行宿主，会在战斗世界启动前明确报错，
不会自动降级到其他后端。

后端选择保存在 `MobaBattleFlowAssertions.PredictionBackend` 场景元数据中，
不是网络时间线命令。经过 `.battleflow` 文档及场景 JSON 的序列化与反序列化后，
后端选择仍会保留。

同一场景在同一后端重复运行，必须得到一致的状态轨迹和确定性指纹。
不同后端即使状态轨迹一致，指纹也不同，因为指纹包含后端标识，
用于区分两种执行路径的测试覆盖范围。

## 断线、补帧与回滚

```text
seed 83
sync-backend unity-route
network packet inbound opcode=5202 seq=1 frame=1 at=0
network disconnect at=34
network packet inbound opcode=5202 seq=2 frame=2 at=68
network packet inbound opcode=5202 seq=3 frame=3 at=102
network reconnect at=136
network packet inbound opcode=5202 seq=102 frame=2 hash=999 at=136
network packet inbound opcode=5202 seq=103 frame=3 hash=6 at=136
network packet inbound opcode=5202 seq=104 frame=4 at=238
assert-sync predictedHashes gte 3
assert-sync mismatch eq 1
assert-sync rollback eq 1
assert-sync mismatchFrame eq 2
assert-sync rollbackFrame eq 1
assert-sync confirmedFrame eq 3
assert-sync predictedFrame eq 3
assert-sync wasReplaying eq true
assert-sync replaying eq false
assert-sync finalHash eq 6
```

只需把后端声明改为 `sync-backend headless`，即可在 .NET 中执行相同场景和断言。
在每秒 30 帧的配置下，断线期间第 2、3 帧被阻断，驱动仍对这两帧进行预测。
重连补帧交付不匹配的第 2 帧权威哈希，触发恢复第 1 帧状态，再按权威输入历史重演。
最后一次帧交付时，重演已经结束，状态值为 6。

回滚次数、失配帧和重演状态等观测数据均读取自真实驱动，DSL 不会伪造这些统计。

## 覆盖边界

共享预测执行器目前使用整数累加测试世界，并配有真实的回滚状态提供器。
它验证网络调度、预测、对账、回滚和 Unity 快照路由，
尚未覆盖完整的 MOBA 技能世界或表现层渲染。

Unity 编辑器入口会明确拒绝当前尚不支持的角色、初始化操作、玩法时间线、
障碍物、环境和玩法断言，不会忽略这些内容后仍然判定场景通过。

编辑器批量执行保留原有的 JSON 格式 `.battleflow` 文档。
当批次包含 Unity 路由场景时，每个场景会按其指定后端分派到对应执行宿主；
格式错误的文档和不支持的场景会分别报告为失败，不影响其他有效场景的执行。
全部使用 `headless` 后端的批次仍由原有 .NET 批量执行器处理。

下一层是在现有执行契约下接入正式 MOBA 战斗世界与回滚适配器。
必须先实现真实战斗状态的捕获与恢复，才能将确定性复现能力扩展到技能级断言，
或用于 GDC 战斗演示的完整流程复现。

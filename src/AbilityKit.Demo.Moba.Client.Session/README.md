# MOBA 客户端会话

`BattleClientSessionDriver` 负责客户端启动、停止和宿主步进。
其 `IBattleClientSessionHost` 实现负责具体的网络及战斗资源。
`BattleClientSnapshotStream` 负责快照准入、角色状态、有界接收队列和可选的表现投影。

网络回调可从任意线程调用 `QueueSnapshot` 和 `NotifyConnectionLost`。
`Tick`、直接表现方法和投影挂载与卸载在宿主的表现线程执行。
会话可不挂载投影，也可挂载多个独立投影；挂载时会从当前完整角色状态重建该投影。

根据预期接收速率设置 `BattleClientSessionOptions.MaxPendingSnapshots`。
队列溢出时丢弃待处理快照，并要求新的完整基线。
订阅 `FullSnapshotRequired`，通过宿主网络 SDK 请求该基线；事件在表现线程触发。
若完整快照已在下一次步进前入队，可直接满足恢复要求，无需额外请求。
`DiscardedPendingSnapshotCount` 统计队列压力和连接丢失导致的丢弃，
不统计因帧或基线检查而被拒绝的快照。

此包面向 .NET 8 和 .NET 10 构建，不依赖 Unity。
Godot 示例目前验证的是测试夹具投影；实际 Godot 网关接入仍需兼容 .NET 8 的网关 SDK 和端到端测试。

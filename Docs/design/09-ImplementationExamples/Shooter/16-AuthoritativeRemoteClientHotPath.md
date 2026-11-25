# Shooter 权威远端客户端热路径

## 问题

多人客户端此前创建与服务器相同的玩法场景。在 512、2048 或 8192 个活动敌人时，
客户端会与服务器同时执行敌人波次、移动、战斗及 RVO。
生成的本地姿态随后可能被权威 PureState 更新覆盖，表现为延迟和可见的拉扯。

## 当前设计

对于 `AuthoritativeInterpolation`、`BatchStateSync` 和 `MassBattleLodSync`，
`ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld` 注入
`ShooterEnemySimulationOverride(false)`。本地世界保留玩家输入、预测、场地边界约束和快照表现支持，
敌人模拟仅在服务器执行。AOI PureState 增量仍创建并更新可见的远端实体。

上述配置默认使用 `ShooterClientPredictionBufferOptions.Disabled`。
显式提供时仍可使用输入历史、回滚快照和状态哈希历史，但常规权威客户端不再分配 240 帧环形缓冲区。
此模式禁用逐步结果哈希；快照导入和重新同步路径在对账需要时仍计算哈希。

PureState 生命周期批次在渲染前排序。后续生成会移除同一键已排队的销毁；
后续销毁会移除已排队实体及其变换。因此，同一 Unity 帧内收到多个生成或销毁增量时，最终投影仍确定。

## 验证

- Shooter 运行时：`543/543` 通过。
- 权威插值、装配选项和轻量客户端世界定向测试：`40/40` 通过。
- 既有快照分配诊断继续通过；预热后，临时 PureState 序列化仍无分配。

## 后续测量

使用 Unity 多人性能采集器，在 512、2048 和 8192 实体下比较改动前后的
`LauncherTick`、`SessionTick`、`ViewRender` 和每帧 GC。
预期客户端 `SessionTick` 与 `LauncherTick` 不再包含第二套敌人及 RVO 模拟，
同时远端视图继续接收相同的 AOI 载荷和生命周期事件。

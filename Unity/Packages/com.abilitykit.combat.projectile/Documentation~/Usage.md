# Projectile 使用契约与接入顺序

安装条件见[包入口](../README.md)。

1. **装配。** 在组合入口提供 `ICollisionService`，由 `ProjectileService` 使用项目的碰撞世界。准备 `ProjectileSpawnParams`，由调用方提供模板、所有者、运动和命中参数。
2. **发射。** 立即创建用 `Spawn`；它记录的 spawn 通知帧为 `0`。要按模拟帧安排发射、保留发射帧信息时用 `ScheduleEmit`。
3. **推进及消费。** 由世界 tick 的唯一驱动者按模拟帧调用 `Tick(frame, fixedDeltaSeconds)`。安装了已有驱动时，不再手工调用。消费方用 `DrainSpawnEvents`、`DrainHitEvents`、`DrainExitEvents` 等入口读取对应队列；这些方法会移出队列数据，不是可重读的历史日志。

区域能力由 `SpawnArea` 等入口接入。是否结算伤害、向谁发送表现通知由上层决定；不能把通知队列当作持久状态，也不能凭单个包内快照推定宿主、调度、区域或业务绑定已完整恢复。

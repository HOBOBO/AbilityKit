# 寻路跟随系统（PathFollowing）

基于源码核校（2026-07-29）。`MobaPathFollowingSystem` 直接读 BT 脑决策的移动目标，经 `INavigationService` 规划路径后驱动 `PathFollowerMotionSource` 接管移动。

## 系统定位

`MobaPathFollowingSystem`（`[WorldSystem(order: MobaSystemOrder.PathFollowing, Phase=Execute)]`）——位于 BrainOutputApply 之后、MotionTick 之前。

**执行链**：`BrainTick(决策) → BrainOutputApply(写 MoveInput 作兜底) → MotionLocomotionInput → PathFollowing(读脑目标,查路径,驱动 Path 源) → MotionTick`

与 `MobaBrainOutputApplySystem` 的关系：
- PathFollowing 与 BrainOutputApply 都通过 `MobaBrainIntentReader.Read(behavior)` 读取当前合法意图，包含目标位置与 `MoveStopRange`；不从旧 Movement 输出绕过仲裁结果。
- **导航可用时**：PathFollowing 创建 Path 源→policy 抑制 Locomotion（`MotionPipelinePolicy: Path→[Locomotion]`）→寻路接管移动
- **导航不可用或寻路失败时**：BrainOutputApply 的直线 MoveInput 兜底→Locomotion 驱动

## Per-actor 状态管理

镜像 `MobaMotionLocomotionInputSystem` 的内联源管理模式：
- `Dictionary<int, PathFollowingState> _stateByActorId` + stamp 失活清扫
- `PathFollowingState` 存：`Source`(PathFollowerMotionSource)、`LastTarget`、`Waypoints`、`FramesSinceRepath`
- Entity group: `ActorMatcher.AllOf(ActorId, ActorBrain, Transform, Motion)`

## 重算逻辑

- **目标移动超过阈值** `RepathTargetThreshold=1.0f`（平方比较）→ 重算
- **强制重算周期** `RepathIntervalFrames=20` 帧
- **CanMove 门控**：`!combatRules.CanMove(actorId)` → CancelPath（眩晕/死亡停止导航）
- **意图/资源撤销**：Brain 缺失或不运行、Hold/Cast（不带移动）/方向移动、意图非法、导航不可用、速度为零时取消旧位置路径。
- **停止距离**：进入当前意图的平面 `MoveStopRange` 时取消路径；跟随与追击不继续走到主人/敌人的中心。
- **到达判断**：`DistanceXZSquared(owner,target) <= ArriveEpsilon²`（0.25m）
- **精确起点**：可行网格内的重算路径从实际起点开始，避免先折返到当前格中心。目标端保留既有精确目标/投影规则。

## Path 源策略

`PathFollowerMotionSource.Rent(waypoints, speed, stacking: MotionStacking.OverrideLowerPriority, groupId: MotionGroups.Path)`

- group=Path(4)，stacking=**OverrideLowerPriority**（必须，否则 policy 不触发抑制）
- `MotionPipelinePolicy.CreateDefault()` 已设 `Path→[Locomotion]` 抑制
- 到达或无目标时 `CancelPath`（RemoveSource+Release），自动停止导航

## Debug 写入

每帧 `WriteDebugState()`：收集活跃 state 的 waypoints → `NavigationDebugState.SetPaths()`——供 Editor Gizmo 绘制。见 [navigation.md](navigation.md)。

## 相关
- 导航基础设施 → [navigation.md](navigation.md)
- 墙体系统 → [collision_and_walls.md](collision_and_walls.md)
- 系统顺序 → [MobaSystemOrder.cs](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/MobaSystemOrder.cs)

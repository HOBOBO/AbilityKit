# 移动系统模块文档索引

> `com.abilitykit.combat.motion` 官方文档入口。

## 文档列表

- [设计文档](./Design.md)：模块目标、核心抽象、tick 数据流、碰撞求解、快照恢复和对象池设计。
- [使用文档](./Usage.md)：实体接入、输入移动、技能位移、路径跟随、solver 配置、快照和对象池使用方式。

## 核心类型速查

| 类型 | 职责 |
| --- | --- |
| `MotionPipeline` | 移动协调器，组合所有移动来源并写回状态。 |
| `MotionPipelinePolicy` | 配置不同移动来源组之间的抑制规则；提供独立完整值捕获、只读验证与静默替换。 |
| `MotionPipelinePolicyState` | Policy 的完整纯值规则映射；不包含对象身份或管线依赖。 |
| `MotionPipelinePool` | `MotionPipeline` 与内部临时集合的对象池入口。 |
| `IMotionSource` | 移动来源接口。 |
| `IMotionSnapshotSource` | 移动来源快照导出/导入接口。 |
| `IMotionSolver` | 位移约束与碰撞求解接口。 |
| `ConfigurableMotionSolver` | 可配置 solver，对接项目碰撞世界。 |
| `MotionState` | 位置、速度、朝向和时间等移动状态。 |
| `MotionOutput` | 本帧期望位移、实际位移和输出速度。 |

## 移动来源

| 类型 | 用途 |
| --- | --- |
| `LocomotionMotionSource` | 玩家或 AI 输入移动。 |
| `TrajectoryMotionSource` | 冲刺、突进、跳跃等轨迹移动。 |
| `FixedDeltaMotionSource` | 击退、拉拽、持续推力。 |
| `PathFollowerMotionSource` | 多点路径跟随；提供独立完整领域状态捕获、验证与静默恢复。 |
| `PathFollowerState` | 路径点、冻结参数、仲裁参数、游标与活动标志；数组须按完整状态 API 复制。 |
| `ScaledMotionSource` | 包装并缩放另一个 source。 |

## 推荐接入方式

1. 为每个可移动实体租借一个 `MotionPipeline`。
2. 将输入、技能、击退、路径等移动分别建模为 `IMotionSource`。
3. 通过 `MotionGroups`、`MotionStacking` 和 `MotionPipelinePolicy` 控制叠加与抑制。
4. 使用 `ConfigurableMotionSolver` 对接项目碰撞世界。
5. 使用 `IMotionSnapshotSource` 保存/恢复移动源局部进度；PathFollower 使用[完整领域状态 API](./Usage.md#71-pathfollower-完整领域状态)，Policy 纯值状态见[跨组抑制策略](./Usage.md#5-跨组抑制策略)。对象身份、成员次序和运行依赖由接入方管理。
6. 高频创建销毁路径优先使用 `Rent()` / `Release()` 池化 API。

## 源码路径

```text
com.abilitykit.combat.motion/Runtime/MotionSystem/
├── Core/
├── Collision/
├── Constraints/
├── Trajectory/
└── Generic/
```

## 适用场景

- MOBA 英雄移动、技能位移和控制效果。
- ARPG 冲刺、击退、拉拽和路径跟随。
- RTS 单位寻路与阵型移动。
- 帧同步/状态同步项目中的预测、回滚和重放。

最后更新：2026-11-19（补充 PathFollower 与 MotionPipelinePolicy 完整状态入口）。

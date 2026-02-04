# MotionSystem（框架层：纯逻辑运动系统）

本目录用于提供**通用、可扩展、纯计算**的运动系统（Motion），用于承载：

- 角色移动（输入/技能位移/控制）
- 子弹运动（直线、曲线、抛物线等）
- 可采样轨迹（按时间采样位置）
- 回放/快速步进（fixed-step）

约束：
- **不依赖 Entitas/Transform**：外部负责读写位置/朝向，本模块只做计算。
- **不做 Unity Physics**：碰撞/阻挡通过接口 `IMotionSolver` 由外部实现。

## 文档

- 设计文档：`Document/Design.md`
- 使用文档：`Document/Usage.md`

## 目录结构

- `Core/`
  - `MotionState`：运动状态（Position/Velocity/Forward/Time）
  - `MotionOutput`：输出（DesiredDelta/AppliedDelta/…）
  - `IMotionSource`：运动源（输入、技能位移、轨迹等都可实现）
  - `LocomotionMotionSource`：通用“角色移动”source（支持 world/local 输入空间）
  - `MotionPipeline`：组合多个 source，计算 desired delta，并交给 solver
  - `FixedStepRunner`：用于回放/快进的固定步长推进

- `Trajectory/`
  - `ITrajectory3D`：轨迹采样接口（time -> position）
  - `LinearTrajectory3D`：线性轨迹示例
  - `TrajectoryMotionSource`：把轨迹包装成 motion source

- `Generic/`
  - `WaypointTrajectory3D`：航点路径（waypoints）按速度推进的可采样轨迹，用于寻路路径执行
  - `PathFollowerMotionSource`：逐航点推进的 source（更适合动态改路/停走）
  - `ScaledMotionSource`：对任意 source 的输出做缩放，便于做权重混合（例如 Path + 输入微调）

- `Collision/`
  - `IMotionSolver`：纯逻辑求解器（可做碰撞、反弹、穿透、停止）
  - `MotionSolveResult/MotionHit`：求解结果（AppliedDelta + 命中信息）

- `Events/`
  - `IMotionEventSink`：可选事件输出（命中、到达、过期等）

- `Examples/`
  - `MotionPipelineExample`：最小示例（线性轨迹 + fixed-step）
  - `WaypointTrajectoryExample`：航点路径作为轨迹执行的示例
  - `BlendPathLocomotionExample`：Path 与输入移动按权重混合的示例

## v1 vs v2

当前目录以 v2 设计为主：
- `Core/MotionPipeline + IMotionSource + Trajectory`

建议：
- 角色移动使用 `LocomotionMotionSource`
- 子弹/曲线移动使用 `TrajectoryMotionSource(ITrajectory3D)`

## 多输入合并 / 分组 / 优先级

`MotionPipeline` 支持多个 `IMotionSource` 同时存在，并通过以下维度控制合并方式：

- `GroupId`：分组 id（int）。框架提供默认组 `MotionGroups`：
  - `Locomotion`、`Ability`、`Control`、`Path`
  - 实现层可自行扩展新的 groupId（例如 Projectile=100）。

- `Stacking`：同组叠加策略（`MotionStacking`）
  - `Additive`：同组内全部 source tick 并叠加
  - `ExclusiveHighestPriority`：同组内只生效 priority 最高的 source（其它不会 tick）
  - `OverrideLowerPriority`：语义上用于“覆盖/抑制”，同组行为等同于 exclusive；可结合 policy 做跨组抑制

### 跨组抑制（例如 Control 压制输入移动）

`MotionPipelinePolicy` 用于配置“某组在 OverrideLowerPriority 时抑制其它组”。

- 默认策略：`Control` 抑制 `Locomotion/Ability/Path`
- 使用方式：`pipeline.Policy = MotionPipelinePolicy.CreateDefault()` 或自行 `SetSuppressedGroups(...)`

## 事件：命中与“移动完成”回调

外部可通过给 `MotionPipeline.Events` 赋值来接收回调：

- `OnHit`：solver 返回命中信息时触发
- `OnArrive`：某个可结束的 motion source 在本次 tick 内完成时触发（例如路径/轨迹走完）
- `OnExpired`：某个可结束的 motion source 因“时间耗尽/效果结束”完成时触发

Source 可通过实现 `IMotionFinishEventSource` 来声明完成时应触发 `Arrive` 还是 `Expired`。

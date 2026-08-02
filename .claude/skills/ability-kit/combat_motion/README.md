# combat.motion — 运动组合与约束求解内核
> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests, zero hard errors.

包 `com.abilitykit.combat.motion`。运动管线 + 碰撞约束求解 + 轨迹/路径跟随源。**不依赖 Entitas、不依赖 Unity Physics**——纯逻辑 math 包。

## 核心架构

```
IMotionSource (desired delta per tick)
    → MotionPipeline.Tick (按 group/优先级/叠加策略求和)
    → IMotionSolver.Solve (碰撞约束 + 终点处理)
    → MotionOutput (AppliedDelta / NewVelocity / NewForward)
```

## Core/

| 类型 | 职责 |
|------|------|
| `MotionPipeline` | 管理 `List<IMotionSource>`，按 group 选最优、加和、调 solver |
| `IMotionSource` | `{GroupId, Stacking, Priority, IsActive, Tick(...), Cancel()}` |
| `MotionPipelinePolicy` | 跨 group 抑制：`CreateDefault()` 设 `Ability→[Locomotion]`、`Path→[Locomotion]`、`Control→[Locomotion,Ability,Path]` |
| `MotionGroups` | 常量：`Locomotion=1, Ability=2, Control=3, Path=4, PassiveDisplacement=5` |
| `MotionStacking` | 枚举：`Additive`, `ExclusiveHighestPriority`, `OverrideLowerPriority`（仅此触发 policy 抑制） |
| `MotionState` | `Position, Velocity, Forward, Time` |
| `MotionOutput` | `DesiredDelta, AppliedDelta, NewVelocity, NewForward, DominantCollisionPolicy, HasDominantCollisionPolicy` |
| `IMotionCollisionPolicySource` | 可选接口：`{HasCollisionPolicy, CollisionPolicy}` — source 可声明自身墙体策略覆盖 actor 默认 |

## Collision/

| 类型 | 职责 |
|------|------|
| `IMotionSolver` | `Solve(id, state, input, dt) → MotionSolveResult` |
| `ConfigurableMotionSolver` | 生产求解器：`AllowPassThrough` 跳 sweep、`SlideAlongWalls` 切向迭代、终点 `EndOverlapPolicy` |
| `IMotionCollisionWorld` | `Sweep`, `Overlap`, `TryProjectToFree`, `TryProjectToFreeDirectional` — adapter 接口 |
| `IMotionSolverDiagnostics` | solver 事件钩子（OnHit/OnEndOverlapResolved/OnConstraintsProviderException） |

## Constraints/

| 类型 | 职责 |
|------|------|
| `MotionConstraints` | 组合 `MotionCollisionConstraints` + `MotionLeashConstraints` |
| `MotionCollisionConstraints` | `Enable, AllowPassThrough, EndOverlapPolicy, Radius, Skin, ObstacleMask, IgnoreMask, SlideAlongWalls, MaxSlideIterations` — 可选 ctor 参数向后兼容 |
| `MotionEndOverlapPolicy` | `Reject/ClampToLastValid/ProjectToNearestFree/AllowInside/ProjectAlongDirection(4)` |
| `MotionLeashConstraints` | `Enable, Center, Radius, Policy` — 约束到半径内 |

## Generic/ — 运动源实现

| 类型 | 职责 |
|------|------|
| `FixedDeltaMotionSource` | 固定速度/时长位移（dash/blink），池化，实现 `IMotionSnapshotSource` + `IMotionCollisionPolicySource` |
| `PathFollowerMotionSource` | 沿路径点前进，池化，`Arrive` 完成事件 |
| `LocomotionMotionSource` | 2 轴输入→世界方向×速度，group=Locomotion |
| `ScaledMotionSource` | 包装另一个源做速度缩放 |
| `WaypointTrajectory3D` | 路径点轨迹（时间采样） |

## 关键设计要点

- **OverrideLowerPriority 才触发 policy 抑制**：`PathFollowerMotionSource` 默认 `ExclusiveHighestPriority`（不触发），Rent 时需显式传 `OverrideLowerPriority` 才能让 Path→Locomotion 生效
- **Constraints provider 是 per-actor 的默认**：`ConfigurableMotionSolver` 的 `ConstraintsProvider` 对整条管线返回一个 `MotionConstraints`；per-skill 策略通过 `IMotionCollisionPolicySource` + `MotionOutput.DominantCollisionPolicy` 透传覆盖
- **Solver 抽出 `Resolve(moverId, start, delta, constraints)`** — 供 blink 等技能直接调用而无需走管线

## 关键文件

- `Runtime/MotionSystem/Core/MotionPipeline.cs` — 管线主逻辑（AddSource/RemoveSource/Tick）
- `Runtime/MotionSystem/Core/MotionOutput.cs` — 输出 struct（含 DominantCollisionPolicy）
- `Runtime/MotionSystem/Core/IMotionCollisionPolicySource.cs` — per-skill 策略可选接口（新）
- `Runtime/MotionSystem/Collision/ConfigurableMotionSolver.cs` — 生产求解器（含 ResolveMovementWithSlide + Resolve 公开方法）
- `Runtime/MotionSystem/Constraints/MotionConstraints.cs` — 约束定义
- `Runtime/MotionSystem/Generic/FixedDeltaMotionSource.cs` — dash/blink 源
- `Runtime/MotionSystem/Generic/PathFollowerMotionSource.cs` — 路径跟随源

## 相关
- 碰撞世界 → [combat_collision](../combat_collision/README.md)
- moba demo 墙体系统 → [collision_and_walls](../../moba-demo/collision_and_walls.md)
- moba demo 寻路跟随 → [path_following](../../moba-demo/path_following.md)

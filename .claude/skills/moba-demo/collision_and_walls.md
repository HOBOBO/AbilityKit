# 碰撞世界 / 移动碰撞 / 墙体系统

基于源码核校（2026-07-29）。覆盖碰撞世界同步、移动求解器装配、adapter、墙滑、per-skill 墙体策略。

## 碰撞世界同步

`Application/Systems/Collision/CollisionWorldSyncSystem.cs`（PreExecute, `MobaSystemOrder.Base + Early`）：
- 每帧同步 Entitas Entity（Transform + Collider + CollisionLayer）→ `ICollisionWorld` Add/Update/Remove
- 仅管理 actor 碰撞体；静态地图碰撞体由 `MobaMapRuntimeService` 独立管理
- 回收"己有"碰撞 Id（非地图碰撞体）

## 移动初始化

`Application/Systems/Motion/MobaMotionInitSystem.cs`（PreExecute, `MobaSystemOrder.MotionInit`）：
- 懒惰初始化 actor 的 `MotionPipeline` + `ConfigurableMotionSolver` + `MotionPipelinePolicy`
- **Default Solver**：`ConfigurableMotionSolver(MobaMotionCollisionWorldAdapter(collisionWorld), constraintsProvider)`
- **Default Constraints**：`MotionCollisionConstraints(enable:true, allowPassThrough:false, endOverlapPolicy:AllowInside, radius:0.5f, obstacleMask:WorldMask, slideAlongWalls:true, maxSlideIterations:2)`
- **Policy**：`MobaMotionGroupConfigResolver.CreatePolicy(services)`——从 `motion_groups.json` 加载 `SuppressedGroupIds`；无配置时回退 `MotionPipelinePolicy.CreateDefault()`（Ability/Path→[Locomotion], Control→[Locomotion,Ability,Path]）

## MobaMotionCollisionWorldAdapter

`Application/Services/Motion/MobaMotionHitTriggerRuntime.cs`（内部类，line ~134），实现 `IMotionCollisionWorld`：
- `Sweep(start, delta, radius, mask, ...)` — 优先 OBB sweep（`IOrientedBoxSweepCollisionWorld`），回退 10 采样 OverlapSphere+Raycast；自动忽略 mover 自身碰撞体
- `Overlap(position, radius, mask)` — 球体重叠查询
- `TryProjectToFree(position, radius, mask)` — 恒等桩（只检查空闲，不搜索）
- `TryProjectToFreeDirectional(from, to, radius, mask)` — **方向投影**：若 `to` 在墙内，沿 `to→from` 二分 16 步找边界出墙点。用于穿墙技能终点落墙内修正

## 墙滑（B1）

`ConfigurableMotionSolver`（`combat.motion` 包）在 `SlideAlongWalls=true` 时：
1. Sweep(start, delta) → hit → applied（钳制到墙前）
2. remaining = delta - applied
3. 沿 `hit.Normal`(XZ) 去除法向分量得 tangent
4. 二次 Sweep(start+applied, tangent) → 累加
5. 迭代（≤ `MaxSlideIterations=2`）

默认开启（`MobaMotionInitSystem` 约束中 `slideAlongWalls:true`）。

## per-skill 墙体策略（F1–F6）

### 可选项接口
`com.abilitykit.combat.motion` 的 `IMotionCollisionPolicySource { HasCollisionPolicy; CollisionPolicy }`——不改 `IMotionSource`（6+ 实现者），可选实现。

### 透传路径
1. `FixedDeltaMotionSource` 实现 `IMotionCollisionPolicySource`（可选 ctor/Rent 参数，默认无策略）
2. `MotionPipeline.Tick` 按固定 group 优先级 `[Control,Ability,Path,Locomotion]` 选主导源，`as` 命中后写 `MotionOutput.DominantCollisionPolicy` + `HasDominant=true`
3. `ConfigurableMotionSolver.Solve` 用主导策略覆盖 per-actor 默认 → `Resolve(start, delta, collision)`
4. `Resolve`：
   - `AllowPassThrough=true`→跳过 sweep（穿墙），applied=delta
   - `AllowPassThrough=false`→sweep+墙滑
   - 终点 `ResolveEndOverlap`：`ProjectAlongDirection`→调 `TryProjectToFreeDirectional`

### Dash（持续移动）
`DashPlanActionModule`：`PassThroughWalls=true`→`FixedDeltaMotionSource` 带 `AllowPassThrough+ProjectAlongDirection`；`false`→默认 sweep+滑（actor 默认约束）

### Blink（瞬时）
`BlinkPlanActionModule`：借 actor 的 `ConfigurableMotionSolver` 调 `Resolve(actorId, start, delta, blinkPolicy)` 落安全终点
- `PassThroughWalls=false`（block）：全距 sweep 钳到墙前
- `PassThroughWalls=true`（pass）：穿墙，终点落墙内→`ProjectAlongDirection` 到边界

### 新增 EndOverlap 策略
`MotionEndOverlapPolicy.ProjectAlongDirection=4`（`combat.motion` 包枚举）

### 配置
`DashArgs`/`BlinkArgs` 增 `PassThroughWalls(bool)` 字段，Schema 读 `"pass_through_walls"` / `"passwalls"` 等 key。

## 相关
- `com.abilitykit.combat.motion` 包 → [ability-kit combat_motion](../ability-kit/combat_motion/README.md)
- `com.abilitykit.combat.collision.abstractions` 包 → [ability-kit combat_collision](../ability-kit/combat_collision/README.md)
- 寻路系统 → [path_following.md](path_following.md)

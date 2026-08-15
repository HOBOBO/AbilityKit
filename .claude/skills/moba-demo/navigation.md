# 导航运行时（寻路系统）

基于源码核校（2026-07-29）。覆盖 `com.abilitykit.combat.navigation` 纯包 + demo 侧的烘焙与消费。

## 架构分层

| 层 | 位置 | 职责 |
|---|---|---|
| 纯包 | `Unity/Packages/com.abilitykit.combat.navigation/Runtime/` | `NavigationGrid`、`INavigationWorld`、`INavigationService`、`GridPathfinder`（确定性 A\*） |
| 烘焙 | `Runtime/Application/Services/Navigation/MobaNavigationBake.cs` | 从 `BattleMapMO` + `ICollisionWorld` 采样生成 `NavigationGrid` |
| 服务 | `Runtime/Application/Services/Navigation/MobaNavigationService.cs` | `[WorldService] Scoped`，实现 `INavigationService`，持有 `NavigationWorld` |
| 调试 | `Runtime/Application/Services/Navigation/NavigationDebugState.cs` | `[WorldService]`，存 `Grid` + `ActivePaths`，供 Editor Gizmo 读取 |

## 纯包 `com.abilitykit.combat.navigation`

镜像 `combat.collision.abstractions` 的 package.json/asmdef/命名空间拆分。依赖仅 `core` + `world.di`。

### Math/ (命名空间 `AbilityKit.Core.Mathematics`)
- `NavigationGrid.cs` — 均匀方格导航网格：`Origin`(世界最小角)、`CellSize`、`Width/Height`、`bool[] Blocked`（行主序）。`WorldToCell`/`CellCenter`/`IsBlocked`/`IsInBounds`。
- `NavigationPath.cs` — `PathStatus {Found,Partial,Failed}` + `readonly struct NavigationPath{Vec3[] Waypoints, Status}`。
- `INavigationWorld.cs` — `FindPath(start,target,agentRadius,out path)` / `IsWalkable` / `TryProjectToWalkable`。
- `INavigationService.cs` — `INavigationService : IService { INavigationWorld World; }`。

### Navigation/ (命名空间 `AbilityKit.Combat.Navigation`)
- `NavigationWorldOptions.cs` — `CellSize`(默认0.5)、`AgentRadius`(0.5)、`AllowDiagonal`(true)、`SimplifyPath`(true)、`MaxIterations`。
- `NavigationWorld.cs` — `INavigationWorld` 实现，持有 `NavigationGrid` + `GridPathfinder`；`Grid` 属性公开。
- `NavigationWorldFactory.cs` — `Create(grid, options)` 返回 `INavigationWorld`。
- `NavigationService.cs` — 默认 `INavigationService` 实现，`Rebuild(grid)` 替换 world。
- `GridPathfinder.cs` — **确定性整数格 A\***：cell 整数坐标、步进代价正交=10/对角=14、固定邻居展开序、`searchId` 计数器代替清零数组、二叉堆 tie-break(f→插入序)、LOS 超覆盖化简输出 `Vec3[]`。决策路径零 Sqrt——确定性由整数运算保证，无需定点数学栈。

### 关键设计决策
- **本包自身无定点类型**：A\* 全程在整数 cell 空间运算（决策路径零 Sqrt），只在与世界坐标互转时除以常数格距（IEEE-754 基本运算确定）。（仓库其余模拟栈已于 2026-08 定点化，见 `com.abilitykit.world.framesync/Document/定点帧同步接入指南.md`；导航包因纯整数实现天然确定，未引入 Fixed64。）
- **烘焙复用碰撞世界**：`MobaNavigationBake` 在 `BattleMapMO.Bounds` 内按格距采样，每 cell 用 `collisionWorld.OverlapSphere(center, agentRadius, WorldMask)` 测阻塞 + "不在 WalkableArea 内"→阻塞。一次性、复用精确窄相、确定性。
- **包不依赖 map/碰撞类型**：纯包只有 grid+planner+接口；烘焙逻辑全部在 demo runtime 侧。

## demo 烘焙

`MobaNavigationBake.Build(map, collisionWorld, options)`：
```
for cx, cz in grid:
   center = CellCenter(cx,cz)
   blocked = !InWalkableArea(center) || OverlapSphere(center, agentRadius, WorldMask) > 0
```
`InWalkableArea` 镜像 `MobaMapRuntimeService.ContainsXZ` 的半径收缩逻辑。

`MobaNavigationService.Build()` 在 `MapRuntimeStage` 的 `maps.Load(mapId)` 后调用。烘焙数据源：
- `maps.CurrentMap.CollisionObjects`（World 层障碍，已注册进 `ICollisionWorld`）
- `maps.CurrentMap.WalkableAreas`（可走区域边界）
- `maps.CurrentMap.Bounds`（烘焙范围）

## 烘焙时机（Bootstrap）

`MapRuntimeStage.Install()`：
```
maps.Load(mapId);                        // ← 障碍物注册进碰撞世界
navigation.Build();                      // ← 烤 nav grid，Set World
```
阶段依赖链：`WorldInit → MapRuntime → StartGame`。

## Debug 状态

`NavigationDebugState`（`[WorldService] Scoped`）：
- `Grid` / `Options` — 由 `MobaNavigationService.Build()` 写入
- `ActivePaths` (`List<ActivePathEntry>`) — 由 `MobaPathFollowingSystem` 每帧更新

此服务供 Editor `NavigationGizmoDrawer`（`com.abilitykit.demo.moba.editor`）读取，绘制 Scene View 网格与路径线。

## 相关类型
- `MobaPathFollowingSystem` — 寻路跟随系统，见 [path_following.md](path_following.md)
- `MapRuntimeStage` — 见 [bootstrap_flow.md](bootstrap_flow.md)
- `NavigationGizmoDrawer` — 见 [editor_toolchain.md](editor_toolchain.md)

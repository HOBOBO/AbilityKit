# combat.navigation — 导航网格与确定性寻路
> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests, zero hard errors.

包 `com.abilitykit.combat.navigation`。均匀方格导航网格 + 确定性整数 A\* + `INavigationWorld`/`INavigationService`。镜像 `collision.abstractions` 的 package.json/asmdef/命名空间拆分。依赖仅 `core` + `world.di`。

## 关键类型

### Math/ (`AbilityKit.Core.Mathematics`)
- `NavigationGrid` — `Origin`(世界最小角)、`CellSize`、`Width/Height`、`bool[] Blocked`(行主序)。`WorldToCell`/`CellCenter`/`IsBlocked`/`IsInBounds`/`Bounds`
- `NavigationPath` — `readonly struct {Vec3[] Waypoints, PathStatus}`。`PathStatus {Found,Partial,Failed}`
- `INavigationWorld` — `FindPath/IsWalkable/TryProjectToWalkable`

### Navigation/ (`AbilityKit.Combat.Navigation`)
- `NavigationWorld` — `INavigationWorld` 实现，持 `NavigationGrid` + `GridPathfinder`；`Grid` 属性公开
- `NavigationWorldOptions` — `CellSize`(0.5)、`AgentRadius`(0.5)、`AllowDiagonal`(true)、`SimplifyPath`(true)、`MaxIterations`
- `NavigationWorldFactory` — `Create(grid,options)→INavigationWorld`
- `INavigationService : IService { INavigationWorld World; }`
- `NavigationService` — 默认实现，`Rebuild(grid,options)` 替换 world

## GridPathfinder — 确定性整数 A\*

**确定性保证（无需定点数学）**：
- 全程 cell 整数坐标，步进代价正交=10/对角=14，启发式为整数 octile 距离
- 决策路径不含 Sqrt / sin 等 IEEE 跨平台不确定超越函数
- 固定邻居展开序（E/NE/N/NW/W/SW/S/SE），`searchId` 计数器代替清空节点数组
- 二叉堆 open list（键 f，tie-break f→插入序 counter），closed 用 searchId 判定
- 无 `System.Random`，无基于哈希迭代的定序
- LOS 超覆盖 Bresenham 化简输出 `Vec3[]`

**方向投影**：目标 cell blocked 时，螺旋搜索（BFS bounded）找最近空闲 cell（`TryNearestFree`），状态 `Partial`。

## demo 侧集成

- `MobaNavigationBake.Build(map, collisionWorld, options)` — 从 `BattleMapMO` + `ICollisionWorld.OverlapSphere` 采样生成 grid
- `MobaNavigationService` (`[WorldService] Scoped`) — 依赖 `ICollisionService`+`IMobaMapRuntimeService`
- `MapRuntimeStage` → `maps.Load→nav.Build`
- `NavigationDebugState` — 供 Editor Gizmo 读取

详见 [moba-demo navigation](../../moba-demo/navigation.md)。

## 相关
- collision → [combat_collision](../combat_collision/README.md)
- motion PathFollower → [combat_motion](../combat_motion/README.md)

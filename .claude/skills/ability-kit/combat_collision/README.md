# combat.collision.abstractions — 碰撞世界接口与数学实现
> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests, zero hard errors.

包 `com.abilitykit.combat.collision.abstractions`。纯 math 碰撞：形状、查询、OBB sweep、网格广相（GridBroadphase）、工厂。

## Math/ — 形状 + 查询 + 世界接口（命名空间 `AbilityKit.Core.Mathematics`）

| 类型 | 职责 |
|------|------|
| `CollisionPrimitives` | `Sphere`, `Aabb`, `Capsule`, `Obb` (readonly structs) + `ColliderShape`(并集) + `CollisionResponse` |
| `CollisionQueries` | 静态 Raycast/Overlap/Distance（Sphere/Aabb/Capsule/OBB 间测试） |
| `ICollisionWorld` | `Add/Remove/Update/Raycast/OverlapSphere/ShouldCollide/GetLayer` |
| `ICollisionService` | `: IService { ICollisionWorld World; }` |
| `CollisionService` | 默认实现，ctor 可选 `CollisionWorldOptions`，委派 `CollisionWorldFactory.Create` |
| `OrientedBoxSweep` | OBB sweep 输入结构 |
| `IOrientedBoxSweepCollisionWorld` | `SweepOrientedBox(box, dir, maxDist, filter, out hit)` — 由 `NaiveCollisionWorld` 和 `GridCollisionWorld` 实现 |
| `OrientedBoxSweepQueries` | 共享 OBB sweep 窄相静态类：`ToBoxLocal/FromBoxLocal/ToBoxLocalBounds/SweepVsShape`（Box-local raycast 技巧，从 `NaiveCollisionWorld.SweepOrientedBox` 抽出供两世界共用） |
| `ColliderId` | `readonly struct {int Value}` — 碰撞体标识 |
| `RaycastHit` | `{ColliderId, Distance, Point, Normal}` |
| `NaiveCollisionWorld` | O(n) 线性扫，参考实现，实现 `IOrientedBoxSweepCollisionWorld` + `ICollisionLayerRelation` |

## Collision/ — 广相 + 层过滤 + Debug（命名空间 `AbilityKit.Combat.Collision`）

| 类型 | 职责 |
|------|------|
| `CollisionWorldFactory` | `Create(options)`, `CreateNaive()`, `CreateWithGrid(cellSize,cap)`, `CreateWithDynamicTree` |
| `CollisionWorldOptions` | `BroadphaseType {Naive,Grid,DynamicAabbTree}`, `GridCellSize`(4f), `InitialCapacity`(64) |
| `GridBroadphase` | 均匀空间哈希网格（`IBroadphase`），`Query(Aabb)` → 候选 ID，`Update/Remove` |
| `GridCollisionWorld` | 基于 `GridBroadphase` 的实现。**现已实现 `IOrientedBoxSweepCollisionWorld`**——broadphase 取候选→共享 `OrientedBoxSweepQueries` 窄相。`Raycast/OverlapSphere/SweepOrientedBox` 均补 `filter.ShouldIgnore`（修 mover 自撞缺陷）。`_queryResults` scratch 消除每调用分配 |
| `DynamicAabbTree` | Tree `IBroadphase`——已实现但无对应 world 类（工厂 `DynamicAabbTree` 分支返回 Grid 别名） |
| `LayerFilter` | `IncludeMask/ExcludeMask/IgnoredColliders` — `IsLayerIncluded/ShouldIgnore` |
| `CollisionLayerMatrix` | 64×64 层关系矩阵 |
| `ICollisionWorldDebugView` | Debug 视图接口（暂未实现——见 Gizmo 死代码） |
| `BroadphaseType` | 枚举 `Naive=0, Grid=1, DynamicAabbTree=2` |

## 关键设计要点

- **Demo 生产翻转至 Grid**（2026-07-29）：7 个注册点 `new CollisionService(options{BroadphaseType.Grid,GridCellSize=4})`。Grid 现正确实现 `IOrientedBoxSweepCollisionWorld` → motion/projectile 扫掠保持解析精度
- **`ToBoxLocalBounds` 现已支持 OBB**（G1 修复）：不处理 OBB 形状时 SweepOrientedBox 对 OBB 障碍（地图 Box 以 OBB 注册）不准确；已补 OBB case（中心转盒本地系 + 三轴投影求 extent）
- **`NaiveCollisionWorld.ToWorldShape` 已补 OBB case**（G2 修复，镜像 GridCollisionWorld）
- **`GridBroadphase.CellEntry` 存完整 min..max 范围**（G3 修复）：`RemoveFromRange` 清所有占用 cell，修多 cell 对象移动后非 min-cell 残留 ID 泄漏
- **`OrientedBoxSweepQueries`** 从 `NaiveCollisionWorld.SweepOrientedBox` 抽取的共享静态，供两世界共用 Box-local raycast 技巧

## 既有缺陷记录

- `ICollisionWorldDebugView` 未实现（`NaiveCollisionWorld`/`GridCollisionWorld` 均未实现）→ `CollisionWorldGizmoDrawer` 为 no-op
- `DynamicAabbTree` 无对应 world 类（工厂返回 Grid 别名，已注释标记）

## 关键文件

- `Runtime/Math/CollisionWorld.cs` — ICollisionWorld + NaiveCollisionWorld + OrientedBoxSweep
- `Runtime/Math/OrientedBoxSweepQueries.cs` — 共享 OBB sweep 窄相（G1 新）
- `Runtime/Collision/GridCollisionWorld.cs` — Grid 实现（含 SweepOrientedBox + ShouldIgnore + scratch）
- `Runtime/Collision/GridBroadphase.cs` — 空间哈希（已修多 cell 残留）
- `Runtime/Collision/CollisionWorldFactory.cs` — 工厂

## 相关
- motion → [combat_motion](../combat_motion/README.md)
- moba demo 墙体系统 → [collision_and_walls](../../moba-demo/collision_and_walls.md)

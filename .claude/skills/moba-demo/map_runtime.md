# 地图运行时服务

基于源码核校（2026-07-29）。覆盖 `MobaMapRuntimeService`、地图配置 `battle_maps.json`、碰撞对象注册。

## 服务

`Application/Services/Map/MobaMapRuntimeService.cs`：
- `[WorldService(typeof(IMobaMapRuntimeService), WorldLifetime.Scoped)]` + `[WorldService(typeof(MobaMapRuntimeService), WorldLifetime.Scoped)]`
- 构造器注入 `MobaConfigDatabase` + `ICollisionService`
- 实现 `IWorldDeinitializable`（OnDeinit 清卸载）

### 接口 `IMobaMapRuntimeService : IService`
- `CurrentMap` / `IsLoaded`
- `Load(mapId)` / `Unload()`
- `IsPositionWalkable(position, radius)` / `TryProjectToWalkable(position, radius, out projected)`
- `TryGetMapObject(colliderId, out MapCollisionObjectMO)` / `TryGetColliderId(mapObjectId, out colliderId)`
- `TryGetSpawnPointById` / `TryGetTeamSpawnPoint`

### Load 流程
1. `MobaConfigDatabase.TryGetBattleMap(mapId)` → `BattleMapMO`
2. `ValidateMap`（边界 XZ 正、可走区在边界内、碰撞形状合法、仅 Yaw 旋转）
3. `Unload`（先清旧地图碰撞体）
4. `RegisterSpawnPoints` + `RegisterCollisionObjects`
5. `CurrentMap = map`

### 碰撞对象注册
对 `map.CollisionObjects` 逐一：
- `Transform3(position, CreateYawRotation(RotationEuler.Y), Vec3.One)`
- `ColliderShape`：Box→`CreateObb`(OBB)，Sphere→`CreateSphere`，Capsule→`CreateCapsule`
- `_collisionWorld.Add(transform, shape, mapObject.CollisionLayer)`
- 双向 map `mapObjectId ↔ ColliderId`

## 地图配置 `battle_maps.json`

位置：`src/AbilityKit.Demo.Moba.Console/Configs/moba/battle_maps.json`（权威）+ Unity 镜像 `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/battle_maps.json`

单地图 "Prototype Arena"（Id=1）：
- **Bounds**：36×4×24，中心原点
- **WalkableArea**：35×23（Main Arena）
- **SpawnPoints**：Team1 (-12,0,0) Yaw90°，Team2 (12,0,0) Yaw-90°
- **CollisionObjects**（6 个，CollisionLayer=2=WorldId，BlocksMovement=true）：
  - North Wall：Box 36×2×1 at (0,1,12)
  - South Wall：Box 36×2×1 at (0,1,-12)
  - West Wall：Box 1×2×24 at (-18,1,0)
  - East Wall：Box 1×2×24 at (18,1,0)
  - **Center Blocker**：Box 3×2×6 at **(5,1,2)** rotated 30°Y（原在 (0,1,0)，已移开）
  - **Pillar**：Capsule r=1 h=3 at **(-5,1.5,-2)**（原在 (0,1.5,7)，已移）

### 修改（2026-07-29）
中央阻挡物从 (0,0) 移到 (5,2)，柱子从 (0,7) 移到 (-5,-2)——原因：出生点（测试 `BattleStartConfig` 放 (0,0)）与障碍物重叠导致卡死。

## 场景可视化

Editor 的 `MobaMapSceneGenerator` 读 `battle_maps.json` → 生成 `Assets/Generated/Moba/BattleMap_1.prefab`（含 Ground / WalkableAreas / SpawnPoints / CollisionObjects 的视觉网格 + Unity Collider）→ 实例化进 `MobaDemoScene`。运行 `Tools/AbilityKit/MOBA Demo/Create Or Refresh Demo Scene` 刷新。

## 相关
- 烘焙 → [navigation.md](navigation.md)
- 碰撞世界 → [collision_and_walls.md](collision_and_walls.md)
- Editor Gizmo → [editor_toolchain.md](editor_toolchain.md)

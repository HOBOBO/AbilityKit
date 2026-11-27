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

地图工具统一从 `Tools/AbilityKit/Demos/Moba/Map` 进入，不与 Demo 场景装配或战斗调试混用：

- 「校验地图与兵线（Map 1）」只读已发布的地图和兵线，不校验建筑、不刷新资源。
- 「预检并刷新地图 Prefab（Map 1）」读取已发布地图、兵线和建筑，确认后写 `Assets/Generated/Moba/BattleMap_1.prefab`，替换活动场景的 `GeneratedBattleMap` 根对象，不自动保存场景。
- 「编辑兵线与波次」和「编辑地图建筑」继续用独立窗口维护各自的权威源，确认后才写 Unity、Console 副本。路径见 [MOBA 玩法资源](../../../LubanConfig/Moba/Production/Gameplay/README.md)。

自动化保留 `MobaMapSceneGenerator.ValidateMapAndMinionRoutesBatch`、`RegenerateMapPrefabBatch`；不要新增平行菜单或从交互入口绕过确认。

## 相关
- 烘焙 → [navigation.md](navigation.md)
- 碰撞世界 → [collision_and_walls.md](collision_and_walls.md)
- Editor Gizmo → [editor_toolchain.md](editor_toolchain.md)

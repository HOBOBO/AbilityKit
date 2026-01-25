---
trigger: manual
---

# 表现层（Game/Battle）实体管理与 View 流程

## 1) Battle 表现层世界（EC.EntityWorld）结构

- `BattleContext` 持有表现层 ECS：
  - `EntityWorld` / `EntityNode` / `EntityLookup` / `EntityFactory` / `EntityQuery`
- `BattleEntityFeature` 在 `OnAttach` 时创建：
  - `EntityNode = CreateChild("BattleEntity")`
  - `Factory`：创建 Character/Projectile 的表现层 `EC.Entity`（不是 Unity GameObject）
  - `Lookup`：netId -> EntityId 映射
  - `Query`：查询封装（TryResolve/TryGetTransform 等）

## 2) Snapshot 驱动实体生成/更新

- 数据入口：`FrameSnapshots + BattleSnapshotPipeline`
- 常用快照：
  - ActorSpawnSnapshot：创建/刷新表现层实体（BattleActorSpawnApplier）
  - ActorTransformSnapshot：更新 BattleTransformComponent，并把 entityId 写入 DirtyEntities
  - ProjectileEventSnapshot(4006)：Spawn/Hit/Exit 事件流（用于表现）

## 3) BattleViewFeature：ViewBinder 绑定/同步 GameObject

- `BattleViewFeature` 负责把 `EC.Entity` 的表现数据同步到 Unity GameObject
- 核心：
  - `RefreshDirtyViews()`：遍历 DirtyEntities -> binder.Sync(entity)
  - binder 维护 `EntityId -> Handle`（包含 GameObject、VFX 绑定等）
  - `OnEntityDestroyed`：清理对应 Handle、Destroy GameObject、清理其绑定的 VFX entity

## 4) VFX 统一管理（VFX 也是 EC.Entity）

- `VfxDatabase`：Resources/vfx/vfx.json（VfxDTO：Id/Resource/DurationMs）
- `BattleVfxManager`：
  - 创建 VFX entity（挂 BattleVfxComponent + Follow + Lifetime）
  - `Tick(world)`：follow 同步 + lifetime 到期自动销毁

约定：VFX 配置为纯表现配置，不进入 MobaConfigDatabase 的 runtime 表注册（避免逻辑侧依赖 Unity 资源）。

---
trigger: manual
---

# 飞行物（Projectile）规则

## 1) 核心模块

- **逻辑层**：`IProjectileService` / `ProjectileWorld` / `ProjectileService`
- **Moba 适配**：`MobaProjectileService`（launcher + schedule emit）
- **同步**：`MobaProjectileSyncSystem`（创建 bullet ActorEntity + link）

## 2) 配置表约定

- `ProjectileLauncherDTO`
  - `DurationMs` / `IntervalMs` / `CountPerShot` / `FanAngleDeg` / `EmitterType`
- `ProjectileDTO`
  - `Speed` / `LifetimeMs` / `MaxDistance` / `HitPolicyKind` / `HitsRemaining` / ...
  - `VfxId` / `OnSpawnVfxId` / `OnHitVfxId` / `OnExpireVfxId`
  - `OnHitEffectId`
- `VfxDTO`
  - `Resource` / `DurationMs`
  - 约束：表现层独立加载，不进 runtime config registry

## 3) 溯源链路

- `ProjectileSpawnParams/events` 必须携带：
  - `TemplateId`
  - `LauncherActorId`
  - `RootActorId`

## 4) 网络事件

- `ProjectileEventSnapshot (4006)`：Spawn/Hit/Exit 的事件流（字段保持兼容）

## 5) 性能/池化注意

- schedule spawn 使用 list pool
- 禁止高频 `new List` / LINQ 等（参考 `performance.md`）

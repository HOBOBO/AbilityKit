# combat.projectile — 弹丸系统

包 `com.abilitykit.combat.projectile`。33 个 .cs 文件——最大的战斗基础设施包之一。

## 核心架构

```
IProjectileService → ProjectileWorld (碰撞查询 + 生命周期)
    ← ProjectileEmitter (BasicEmitter / 自定义)
    ← ProjectileSpawnPattern (SingleShot / Fan / Scatter / Burst)
    → IProjectileHitPolicy (ExitOnHit / Pierce)
    → IProjectileHitFilter (DefaultFilter)
    → AreaWorld (区域效果)
```

## 关键类型

### 运行时核心
- `Projectile` — 弹丸实例（位置/方向/速度/时间/碰撞半范围）
- `ProjectileWorld` — 管理弹丸集合，`Tick(dt)` + `TrySweep` + 碰撞响应
- `ProjectileRuntimeState` — 快照用运行时状态
- `ProjectileId` / `ProjectileSpawnParams` / `ProjectileExitReason` / `ProjectileEvents`

### 服务
- `IProjectileService` — 生成/取消弹丸的公共接口
- `ProjectileService` — 默认实现

### 发射器 / 模式 / 策略 / 过滤器
- `IProjectileEmitter` / `BasicProjectileEmitter` — 控制每 tick 发射行为
- `IProjectileSpawnPattern` — `SingleShotPattern` / `FanPattern` / `ScatterPattern` / `BurstPattern`
- `IProjectileHitPolicy` — `ExitOnHitPolicy` / `PierceHitPolicy`
- `IProjectileHitFilter` / `DefaultProjectileHitFilter`

### 区域效果
- `AreaWorld` — 管理区域效果（进入/离开/驻留）
- `AreaId` / `AreaSpawnParams` / `AreaEvents`

### 系统
- `ProjectileTickSystem` — per-tick 弹丸推进系统
- `ProjectileWorldModule` — DI 模块
- `ProjectileRollbackProvider` — 回滚支持
- `ProjectileScheduleId` / `ProjectileScheduleParams`

## 与 moba demo 集成
- `MobaProjectileService` — 对 `ProjectileWorld` 的 demo 端封装
- PlanAction `ShootProjectile` — 技能触发器调用弹丸服务

## 关键文件
- `Runtime/Projectile/ProjectileWorld.cs` — 核心 World（含 `TrySweep` OBB sweep 路径）
- `Runtime/Projectile/Services/ProjectileService.cs`
- `Runtime/Projectile/Emitters/BasicProjectileEmitter.cs`
- `Runtime/Projectile/Patterns/` — 4 种生成模式
- `Runtime/Projectile/Policies/` — 命中策略

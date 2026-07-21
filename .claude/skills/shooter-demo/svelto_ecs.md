# Svelto.ECS 适配

位置：`runtime/Runtime/Infrastructure/Ecs/Svelto/`

## ShooterSveltoWorld（核心）

`ShooterSveltoWorld.cs` / `IShooterSveltoWorld`：包 `ISveltoWorldContext`，提供 Svelto `EnginesRoot` 的初始化、Tick、销毁。

## 5 个 ExclusiveGroup（实体分类）

`ShooterSveltoEntities.cs`（`ShooterSveltoGroups`）：

```csharp
public static class ShooterSveltoGroups {
    public static readonly ExclusiveGroup Players;
    public static readonly ExclusiveGroup Projectiles;
    public static readonly ExclusiveGroup GameplayShooters;
    public static readonly ExclusiveGroup GameplayTargets;
    public static readonly ExclusiveGroup GameplayProjectiles;
}
```

- `Players`：玩家实体（战斗中真实玩家）
- `Projectiles`：子弹实体
- `GameplayShooters` / `GameplayTargets` / `GameplayProjectiles`：场景跑分/验收用（ScenarioRunner 创建）

## struct 组件清单（IEntityComponent）

按用途分组：

### 玩家
- `ShooterSveltoPlayerComponent`：PlayerId / X / Y / AimX / AimY / Hp / Score / Alive

### 投射物
- `ShooterSveltoProjectileComponent`：BulletId / Owner / Velocity / RemainingFrames / PenetrationRemaining / ExplosionRadius / ExplosionDamage
- `ShooterSveltoProjectileDamageComponent`

### 通用
- `ShooterSveltoTransformComponent`：位置 + 旋转
- `ShooterSveltoHealthComponent`：HP + MaxHp
- `ShooterSveltoWeaponComponent`：武器
- `ShooterSveltoCooldownComponent`：冷却
- `ShooterSveltoTargetComponent`：目标

### 场景跑分专用
- `ShooterSveltoGameplayComponents`
- `ShooterSveltoGameplayDescriptors`
- `ShooterSveltoEntityLayout`

## Svelto Task 工具

- `IShooterEcsEntityStoreSynchronization` — 实体存储同步接口
- `ShooterSveltoGameplayScenarioEcsUtility` — 场景跑分 ECS 工具

## 与 moba Entitas 的关键差异

| 维度 | Svelto（shooter） | Entitas（moba） |
|------|------------------|----------------|
| 组件类型 | `struct IEntityComponent`（值类型，零 GC） | class（生成代码） |
| 实体访问 | `EntitiesDB.QueryEntities<T>(group)` | `ActorContext` / `ActorEntity` |
| 组件匹配 | `ExclusiveGroup`（编译期分组） | `Matcher`（运行期匹配） |
| 系统循环 | `IEngine` + `EnginesRoot` | `ISystem` + `Systems` |
| 代码生成 | 无（struct + group） | 大量生成（`Actor*Component` / Contexts） |

## 命名空间与组件位置

namespace `AbilityKit.Demo.Shooter.Infrastructure.Ecs.Svelto`

- `ShooterSveltoWorld.cs` / `IShooterSveltoWorld.cs`
- `ShooterSveltoEntities.cs`（含 5 ExclusiveGroup + 所有 struct 组件）

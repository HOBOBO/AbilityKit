# AbilityKit BattleRuntime Starter

BattleRuntime 组合的最小可运行入口：在 [SkillCore Starter](../AbilityKit.Samples.SkillCore)
之上加入 `combat.targeting` + `combat.projectile` + `combat.damage`，演示一条完整的
「目标选择 → 投射物飞行命中 → 伤害结算 → 事件/触发规则」链路。

## 运行

```bash
dotnet run --project src/AbilityKit.Samples.BattleRuntime
```

## 链路一览

```
CastFireballVolley
  └─ targeting：CircleShapeRule(半径8) 过滤 → DistanceToEntityScorer 排序 → Top-K 取最近 2 个
       └─ Goblin(2001)、Orc(2002) 入选；Shaman(2003) 距离 9.0 > 8 被裁掉
  └─ projectile：向每个目标 Spawn 一枚火球（speed 15，ExitOnHit）
Tick 循环（90 帧 @ 30fps）
  └─ ProjectileService.Tick → DrainHitEvents
       └─ damage：DamageCalculationPipeline 结算（暴击 / 加成 / 魔抗减免 / 护盾）
            └─ EventBus.Publish(DamageEvent) → 触发规则「重伤告警 ≥ 35」
```

## 它演示了什么

| 能力 | 包 | 在示例中的位置 |
|---|---|---|
| 目标选择（候选来源 / 形状过滤 / 评分排序 / Top-K） | combat.targeting | `CastFireballVolley`：`SearchPipelineBuilder.From/Filter/ScoreBy/Select/Take` |
| 候选来源实现 | combat.targeting | `MonsterCandidateProvider : ICandidateProvider`（接入方自实现） |
| 位置提供者（过滤与评分共用同一份位置） | combat.targeting | `BattlePositionProvider : IPositionProvider, IEntityKeyProvider` |
| 投射物（Spawn / Tick / 命中事件 / 退出原因） | combat.projectile | `BattleService`：`ProjectileService.Spawn/Tick/DrainHitEvents` |
| 碰撞世界注册 | combat.collision.abstractions | `AddMonster`：`CollisionService.World.Add` 注册球形碰撞体 |
| 伤害结算（暴击/加成/魔抗/护盾管线） | combat.damage | `ApplyDamage`：`DamageCalculationPipeline.CreateDefault().Execute` |
| 与 SkillCore 的事件契约复用 | triggering | 伤害事件经同一 `EventBus` + `DamageEventPayloadAccessor` 喂给触发规则 |
| 组合递进 | world.di | 复用 `FoundationWorld`，`BattleRuntimeModule` 注册 5 个服务 |

## 关键设计点

- **collider ↔ 实体的映射由业务层负责**：框架只产出 `ProjectileHitEvent.HitCollider`，
  命中后回到哪个怪物、该扣谁的血，由接入方用 `Dictionary<ColliderId, int>` 自己维护（见 `_colliderToMonster`）。
- **projectile 的 Entitas 是可选传递依赖**：核心 `ProjectileService` 不依赖 Entitas，
  仅 `ProjectileWorldModule`（ECS 安装适配器）用 Entitas。Starter 直接 `new ProjectileService(new CollisionService())`，
  不引 Entitas 运行时。构建期 NuGet 会有 NU1701 警告（Entitas 是 .NETFramework 包），属仓库统一特征，不影响纯逻辑链路。
- **damage 管线是纯函数式结算**：`DamageRequest` + `DamageCalculationContext`（含暴击率/抗性/护盾等 dataflow slots）
  → `Execute` 返回 raw / resistReduction / final / shield / actual 等全套拆解值，业务层只负责把 `ActualDamage` 扣进实体。

## 下一步

- **SyncRuntime**：加入 `world.framesync` + `world.snapshot` + `record` + `protocol`，
  把这条战斗链路挂到帧同步 + 快照 + 回放上，面向多人 / 重连 / 状态恢复。
- **ServerRuntime**：加 `protocol` + `host` + `host.extension`，把战斗放到权威服上跑。

组合分级的完整定义见 `Unity/Packages/README.md`。

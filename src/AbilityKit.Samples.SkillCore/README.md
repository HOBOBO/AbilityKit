# AbilityKit SkillCore Starter

SkillCore 组合的最小可运行入口：在 [Foundation Starter](../AbilityKit.Samples.Foundation)（core + world.di）
之上加入 `triggering` + `pipeline` + `modifiers`，演示一次完整的技能核心链路。

## 运行

```bash
dotnet run --project src/AbilityKit.Samples.SkillCore
```

## 它演示了什么

| 能力 | 包 | 在示例中的位置 |
|---|---|---|
| 技能阶段编排（前摇/施法/后摇/重复） | pipeline | `CastFireball`：Delay → Repeat(3) → Delay；阶段间用 `SkillContext` 传递数据 |
| Buff 持续效果管线 | pipeline | `CastWeaken`：立即挂 Buff → 每 0.5s DOT × 5 → 结束移除 |
| 修饰器计算（Buff 数值） | modifiers | `ModifierData.Mul(MoveSpeed, 0.5)` 挂到业务自管的列表，`ModifierCalculator` 算出减速后移速 |
| 事件驱动触发规则（RPN 条件） | triggering | `SetupTriggerRule`：`payload:amount bb:combat:atk +` ≥ 阈值触发反击 |
| 黑板（条件数据源） | triggering | `bb:combat:atk = 7`，`DictionaryBlackboardResolver` 注册 |
| Payload 访问器 | triggering | `DamageEventPayloadAccessor` 把事件字段映射进 RPN 表达式 |
| 世界装配递进 | world.di | 复用 Foundation 的 `FoundationWorld`，`SkillCoreModule` 注册技能服务 |

## 关键设计点

- **事件是技能与触发规则的公共出口**：技能伤害走 `EventBus.Publish(DamageEvent)`，
  触发规则订阅同一事件——"技能产生效果、规则响应效果"在同一总线上闭环。
- **modifiers 不预设存储**：`CombatTarget.Modifiers` 是业务层自己持有的列表，
  框架只负责把 `ModifierData[]` 计算成最终值（Override > Mul > Add 的聚合顺序由框架保证）。
- **服务双接口暴露**：`SkillCastService` 同时以 `ISkillCastService`（供入口调用）
  和 `IFoundationTickLoop`（供 FoundationWorld.Tick 驱动）注册，两个注册解析到同一单例。
- **Samples 目录不随包分发**：pipeline 的 `DefaultAbilityPipelineConfig` / `ExampleAbilityPipelineContext`
  在 Samples 里，接入方需要自带 `SkillPipelineConfig` / `SkillContext`（本示例即参考实现）。

## 下一步

- **BattleRuntime**：加入 `combat.targeting` / `combat.projectile` / `combat.damage`，
  把 `DamageEvent` 替换成真实的目标选择 → 命中 → 伤害链路。

组合分级的完整定义见 `Unity/Packages/README.md`。

# combat.damage — 伤害类型与处理器

包 `com.abilitykit.combat.damage`。轻量包（4 个 .cs 文件），定义伤害类型枚举、数据结构、计算上下文和处理器链。

## 关键类型

- `DamageEnums` — 伤害类型/来源枚举
- `DamageData` — 核心伤害数据结构（伤害量、类型、来源、目标）
- `DamageCalculationContext` — 计算上下文（属性、Buff、修正因子）
- `DamageProcessors` — 处理器链（`IDamageProcessor` / `DamageProcessor`(抽象) / 可注册的处理器）

## moba demo 集成

moba demo 的 `DamagePipelineService`（在 `moba-demo` skill 的 `combat_continuous` 中）基于此包构建 9 阶段伤害事件管道：

```
PreDamage → ArmorCalc → ResistanceCalc → DamageModify → ShieldAbsorb
    → HealthDeduct → PostDamage → KillCheck → DamageReport
```

通过 `com.abilitykit.triggering` 的 Plan 事件总线发布每个阶段。

## 关键文件
- `Runtime/Damage/DamageEnums.cs`
- `Runtime/Damage/DamageData.cs`
- `Runtime/Damage/DamageCalculationContext.cs`
- `Runtime/Damage/DamageProcessors.cs`

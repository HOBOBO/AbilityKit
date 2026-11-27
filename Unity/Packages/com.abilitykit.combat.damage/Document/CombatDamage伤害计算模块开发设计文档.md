# Ability-Kit Combat Damage 伤害计算模块开发设计文档

> 文档类型：历史开发说明
> 核校状态：历史主体基线为 2026-06-05；2026-11-25 保留原路径，核正类型名称与异形传递描述。现行接入、八阶段行为和已知限制由[使用指南](../Documentation~/Usage.md)维护。
>
> **阅读对象**：需要维护伤害数据、伤害上下文和伤害处理器链的战斗系统开发者。
>
> **文档目标**：说明 Combat Damage 包如何表达伤害类型、计算上下文和处理阶段。

---

## 一、设计理念

Combat Damage 包把伤害计算拆成数据、枚举和处理器。它可以与 Dataflow 或战斗系统组合，用于表达基础伤害、类型、来源、目标、暴击、减免等计算过程。

---

## 二、模块边界

负责：

- 在 DamageData.cs 定义 `DamageRequest` 和 `DamageResult`。
- 定义 `DamageCalculationContext`。
- 在 DamageEnums.cs 定义 `DamageType`、`DamageFlags` 和 `DamageSourceType`。
- 提供 `DamageProcessors` 处理器集合。

不负责：

- 不直接扣血或修改实体属性。
- 不负责命中检测。
- 不负责表现层事件。
- 不决定完整技能流程。

---

## 三、目录结构

| 路径 | 职责 |
|------|------|
| `Runtime/Damage/Data` | 伤害数据和计算上下文 |
| `Runtime/Damage/Enum` | 伤害枚举 |
| `Runtime/Damage/Processor` | 伤害处理器 |

---

## 四、典型流程

```mermaid
sequenceDiagram
    participant Skill as 技能
    participant Damage as DamageContext
    participant Processor as DamageProcessors
    participant World as 战斗世界

    Skill->>Damage: 创建 DamageRequest
    Damage->>Processor: 计算修正
    Processor-->>Damage: 输出最终伤害
    Damage->>World: 由上层应用到属性
```

---

## 五、注意事项

- 伤害处理器应保持纯逻辑，避免直接访问 Unity 对象。
- 上层应决定伤害结果如何落到属性系统或事件系统。
- 当前默认链已继承 `DataflowPipeline<DamageRequest, DamageResult>`，通过 `DamageCalculationContext.Result` 显式累计异形结果，不要求输入输出同形。

---

*文档版本：1.0*  
*最后更新：2026-06-05*

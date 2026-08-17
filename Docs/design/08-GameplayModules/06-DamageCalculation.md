# 8.6 伤害计算

> 文档类型：Canonical 设计（含 MOBA 结算示例）
> 事实基线：2026-08-16
> 文档版本：v3.0
>
> 基于真实源码说明 AbilityKit 的伤害计算模型：既包括通用 `AbilityKit.Combat` 伤害数据流管线，也包括 `com.abilitykit.demo.moba.runtime` 中的 MOBA 伤害服务、减伤、护盾、触发事件与快照输出。

---

## 目录

- [8.6 伤害计算](#86-伤害计算)
  - [目录](#目录)
  - [1. 系统定位](#1-系统定位)
    - [1.1 计算内核与结算应用的所有权](#11-计算内核与结算应用的所有权)
  - [2. 源码入口](#2-源码入口)
    - [2.1 通用伤害包](#21-通用伤害包)
    - [2.2 MOBA 示例实现](#22-moba-示例实现)
  - [3. 领域模型](#3-领域模型)
    - [3.1 通用伤害模型](#31-通用伤害模型)
    - [3.2 MOBA 伤害模型](#32-moba-伤害模型)
  - [4. 通用伤害管线 AbilityKit.Combat](#4-通用伤害管线-abilitykitcombat)
    - [4.1 默认管线顺序](#41-默认管线顺序)
    - [4.2 上下文数据槽](#42-上下文数据槽)
    - [4.3 处理器职责](#43-处理器职责)
      - [验证阶段](#验证阶段)
      - [暴击阶段](#暴击阶段)
      - [基础伤害阶段](#基础伤害阶段)
      - [加成阶段](#加成阶段)
      - [减伤阶段](#减伤阶段)
      - [最终阶段](#最终阶段)
      - [溢出阶段](#溢出阶段)
  - [5. MOBA 伤害管线 AbilityKit.Demo.Moba](#5-moba-伤害管线-abilitykitdemomoba)
    - [5.1 战斗入口](#51-战斗入口)
    - [5.2 伤害构造](#52-伤害构造)
    - [5.3 计算阶段服务](#53-计算阶段服务)
    - [5.4 标准公式阶段](#54-标准公式阶段)
      - [Base 阶段](#base-阶段)
      - [Mitigation 阶段](#mitigation-阶段)
      - [Shield 阶段](#shield-阶段)
      - [Final 阶段](#final-阶段)
    - [5.5 实际扣血与回血](#55-实际扣血与回血)
  - [6. 触发、快照与表现联动](#6-触发快照与表现联动)
    - [6.1 伤害事件总线](#61-伤害事件总线)
    - [6.2 负载访问](#62-负载访问)
    - [6.3 快照输出](#63-快照输出)
    - [6.4 死亡判定](#64-死亡判定)
  - [7. 典型执行流程](#7-典型执行流程)
    - [7.1 给伤害的时序](#71-给伤害的时序)
    - [7.2 通用管线时序](#72-通用管线时序)
  - [8. 扩展边界](#8-扩展边界)
    - [8.1 适合扩展的点](#81-适合扩展的点)
    - [8.2 不建议耦合的点](#82-不建议耦合的点)
    - [8.3 当前实现的约束](#83-当前实现的约束)
    - [8.4 证据状态与已知限制](#84-证据状态与已知限制)

---

## 1. 系统定位

AbilityKit 里的“伤害计算”不是单一公式，而是分层职责：

1. **通用伤害计算层**：`AbilityKit.Combat` 提供一个可插拔的 `DataflowPipeline`，用于验证输入、计算暴击、攻击力加成、伤害加成、护甲/魔抗减免、最终值和溢出值。
2. **游戏业务编排层**：MOBA 示例在 `AbilityKit.Demo.Moba` 中把伤害拆成“构造攻击信息 → 运行计算阶段 → 应用到目标血量 → 产出快照与触发事件”。
3. **表现与回放层**：同一笔伤害会同步进入触发事件总线、快照 emitter、日志和死亡判定订阅者。

这意味着该模块的重点不是“某个固定公式”，而是：

- 统一伤害输入和结果结构；
- 把减伤、护盾、暴击、穿透等能力拆成独立阶段；
- 允许不同业务层只复用其中一部分；
- 支持触发器、快照、诊断和回放接入。

### 1.1 计算内核与结算应用的所有权

| 层级 | 稳定职责 | 项目必须定义的策略 |
|------|----------|--------------------|
| Combat Damage 框架 | Request/Result、数据槽、处理阶段和可插拔计算顺序 | 不修改角色 HP，不决定护盾、死亡、吸血、事件顺序和表现 |
| 项目应用层 | 公式目录、属性读取、护盾/免疫、生命修改、死亡判定、事件事务、快照与诊断 | 必须规定计算值与实际应用值的关系，以及失败后的副作用边界 |
| MOBA 示例 | `DamagePipelineService`、`MobaDamageService`、Shield、事件总线和 Snapshot 的一套编排 | 不是公共 Damage Pipeline 的固定公式或默认结算协议 |

“计算伤害”和“把结果结算到世界”必须保持两个边界。前者适合通用化，后者高度依赖角色状态、事件规则和同步拓扑，应保留在项目应用层。

还需要明确：MOBA 当前没有把通用 `DamageCalculationPipeline` 作为生产结算内核。通用包使用 float `DamageRequest/Result + DataflowContext`；MOBA 使用独立的 `AttackCalcInfo`、Fixed64 阶段、Shield preview/commit 和 `MobaDamageService`。两者证明“阶段化计算”这一组织方式，但没有共享同一个公式目录、结果 DTO 或确定性承诺。

---

## 2. 源码入口

### 2.1 通用伤害包

- [`DamageData.cs`](../../../Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Data/DamageData.cs)
- [`DamageCalculationContext.cs`](../../../Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Data/DamageCalculationContext.cs)
- [`DamageProcessors.cs`](../../../Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Processor/DamageProcessors.cs)

### 2.2 MOBA 示例实现

- [`DamageEnums.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Common/Shared/Enum/DamageEnums.cs)
- [`DamagePipelineModels.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineModels.cs)
- [`DamagePipelineStages.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineStages.cs)
- [`DamagePipelineService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs)
- [`MobaDamageMitigationService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/MobaDamageMitigationService.cs)
- [`MobaShieldService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/MobaShieldService.cs)
- [`MobaDamageService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/MobaDamageService.cs)
- [`MobaCombatEffectService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/MobaCombatEffectService.cs)
- [`DamagePipelineEvents.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineEvents.cs)
- [`GiveDamagePlanActionModule.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Skill/GiveDamagePlanActionModule.cs)
- [`TakeDamagePlanActionModule.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Skill/TakeDamagePlanActionModule.cs)
- [`GiveDamageArgs.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Skill/GiveDamageArgs.cs)
- [`MobaDamageEventSnapshotService.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Snapshot/MobaDamageEventSnapshotService.cs)
- [`MobaBattlePayloadAccessor.cs`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Gameplay/Triggering/MobaBattlePayloadAccessor.cs)

---

## 3. 领域模型

### 3.1 通用伤害模型

通用包里的核心对象是：

- `DamageRequest`：伤害输入。
- `DamageResult`：伤害计算输出。
- `DamageCalculationContext`：Dataflow 上下文，承载目标护甲、魔抗、生命值与攻击者攻击力等计算数据。
- `DamageCalculationPipeline`：默认管线。
- `IDamageProcessor` / `DamageProcessor`：伤害处理器抽象。

`DamageRequest` 的字段很少，但足够表达一次伤害请求：

- `Source`：来源对象，可能是技能、Buff、物件或任意业务对象；
- `Attacker`：攻击者；
- `Target`：目标；
- `BaseValue`：基础伤害；
- `DamageType`：物理 / 魔法 / 真实；
- `Flags`：是否暴击、持续伤害等；
- `SourceType`：来源类别。

`DamageResult` 则把一次计算拆成多个可观测阶段：

- `RawDamage`
- `PreArmorDamage`
- `ArmorReduction`
- `ResistReduction`
- `BonusDamage`
- `FinalDamage`
- `CriticalMultiplier`
- `Overkill`
- `ActualDamage`
- `ShieldDamage`

### 3.2 MOBA 伤害模型

MOBA 示例没有直接把通用 `DamageRequest` 作为对外接口，而是使用更完整的战斗上下文：

- `AttackInfo`：攻击创建阶段的输入对象；
- `AttackCalcInfo`：中间计算阶段上下文；
- `DamageResult`：最终应用结果；
- `DamageType` / `CritType` / `DamageReasonKind` / `DamageFormulaKind`：战斗语义枚举。

MOBA 侧还保留了一个“可逐阶段覆盖”的数值系统：

- `BaseDamage`
- `DamageRate`
- `FlatBonus`
- `FinalDamage`
- `RawDamage`
- `MitigatedDamage`
- `ShieldAbsorb`
- `HpDamage`

这些值都由 `NumberValue` 承载，便于在触发或阶段执行时动态叠加或覆盖。

---

## 4. 通用伤害管线 AbilityKit.Combat

### 4.1 默认管线顺序

`DamageCalculationPipeline.CreateDefault()` 的默认阶段顺序是：

1. `ValidateDamageProcessor`
2. `CalculateCriticalProcessor`
3. `CalculateBaseDamageProcessor`
4. `ApplyDamageBonusProcessor`
5. `ApplyArmorReductionProcessor`
6. `ApplyMagicResistReductionProcessor`
7. `CalculateFinalDamageProcessor`
8. `CalculateOverkillProcessor`

这条管线的设计特征是：

- 处理器之间通过 `DataflowContext` 共享状态；
- 计算过程可中断；
- 每一步都可独立替换；
- 结果对象在处理链中逐步累积。

### 4.2 上下文数据槽

`DamageSlots` 通过强类型 `DataflowSlot<T>` 统一存取辅助数据：

- `CritChance`
- `CritMultiplier`
- `CritRoll`
- `DamageBonusPercent`
- `DamageBonusFlat`
- `ArmorPenetration`
- `ArmorPenetrationPercent`
- `MagicResistPenetration`
- `MagicResistPenetrationPercent`
- `TargetShield`

这说明通用管线并不依赖固定实体模型，而是通过上下文槽注入外部战斗状态。

### 4.3 处理器职责

#### 验证阶段

`ValidateDamageProcessor` 只负责输入合法性：

- 攻击者不能为空；
- 目标不能为空；
- 伤害值必须大于 0，或满足持续伤害条件。

不合法时直接 `Abort()`。

#### 暴击阶段

`CalculateCriticalProcessor` 从上下文读取：

- 暴击率
- 暴击倍数
- 暴击随机值

如果 `critRoll < critChance`，则设置 `DamageFlags.Critical` 并记录暴击倍数。

#### 基础伤害阶段

`CalculateBaseDamageProcessor` 会：

- 先写入 `RawDamage` / `PreArmorDamage`；
- 按伤害类型叠加攻击力；
- 若为暴击则乘以暴击倍数。

#### 加成阶段

`ApplyDamageBonusProcessor` 处理：

- 百分比加成；
- 固定值加成。

#### 减伤阶段

`ApplyArmorReductionProcessor` 与 `ApplyMagicResistReductionProcessor` 使用同一类公式：

```text
reduction = defense / (100 + defense)
final = damage * (1 - reduction)
```

其中护甲和魔抗各自独立；真实伤害不参与减免。

#### 最终阶段

`CalculateFinalDamageProcessor` 把最终值向下取整，避免浮点误差扩散。

#### 溢出阶段

`CalculateOverkillProcessor` 负责：

- 计算是否超过目标当前生命值；
- 区分 `Overkill` 与 `ActualDamage`；
- 若存在护盾，优先计算护盾吸收。

---

## 5. MOBA 伤害管线 AbilityKit.Demo.Moba

### 5.1 战斗入口

MOBA 侧对外入口是 [`MobaCombatEffectService`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/MobaCombatEffectService.cs)。

它只做两件事：

- `DealDamage(AttackInfo)`：交给 `DamagePipelineService`；
- `Heal(...)`：交给 `MobaDamageService`。

也就是说，**计算** 和 **实际扣血/加血** 是两个服务。

### 5.2 伤害构造

`AttackInfo` 负责承载一次攻击上下文：

- 攻击方/目标方 actor id；
- 伤害类型；
- 暴击类型；
- 原因类型与原因参数；
- 公式类型 / 公式 id；
- 原始来源与目标对象；
- 玩法起源 `Origin`。

`GiveDamagePlanActionModule` 会创建 `AttackInfo`，并把：

- `DamageType`
- `ReasonKind = Skill`
- `ReasonParam`
- `FormulaKind = Standard`
- `BaseDamage.BaseValue`

填入后提交给 `combat.DealDamage(attack)`。

`TakeDamagePlanActionModule` 则相反：

- 取一个已有伤害结果、计算上下文或攻击上下文；
- 构造反向伤害；
- 通常用于 Buff 反伤、受击联动等场景。

### 5.3 计算阶段服务

`DamagePipelineService` 是 MOBA 伤害计算主编排器。

它的执行过程是：

1. 校验攻击和目标；
2. 发布 `AttackCreated` / `BeforeCalc`；
3. 创建 `AttackCalcInfo`；
4. 发布 `CalcBegin`；
5. 运行公式阶段；
6. 发布 `BeforeApply`；
7. 调用 `MobaDamageService.ApplyDamage()`；
8. 构造最终 `DamageResult`；
9. 发布 `AfterApply`；
10. 记录诊断指标。

### 5.4 标准公式阶段

当前实现只注册了一个标准公式：`DamageFormulaKind.Standard`。

标准公式由 4 个阶段组成：

1. `MobaBaseDamagePipelineStage`
2. `MobaDamageMitigationPipelineStage`
3. `MobaShieldAbsorbPipelineStage`
4. `MobaFinalDamagePipelineStage`

#### Base 阶段

`baseDamage * damageRate + flatBonus`

#### Mitigation 阶段

由 `MobaDamageMitigationService` 完成：

- 若伤害类型是 `True`，直接透传；
- 读取目标防御或魔防；
- 读取攻击者穿透率；
- 计算有效防御：

```text
effectiveDefense = max(0, defense * (1 - penetrationRatio))
mitigated = rawDamage * 100 / (100 + effectiveDefense)
```

#### Shield 阶段

由 `MobaShieldService.Absorb()` 完成：

- 先尝试从目标护盾容器中吸收伤害；
- 按护盾层级、类型掩码和优先级消耗；
- 计算剩余 `HpDamage`。

#### Final 阶段

若 `FinalDamage` 被显式覆盖，则直接写入 `HpDamage`。

### 5.5 实际扣血与回血

`MobaDamageService` 只做状态修改：

- `ApplyDamage(...)`：减少目标 `Hp`；
- `ApplyHeal(...)`：增加目标 `Hp`；
- 同时通过 `MobaDamageEventSnapshotService` 报告快照事件。

因此 `DamagePipelineService` 只负责“算出多少”，`MobaDamageService` 负责“真正改血量”。

---

## 6. 触发、快照与表现联动

### 6.1 伤害事件总线

`DamagePipelineService` 在关键节点发布事件：

- `damage.attack.created`
- `damage.attack.before_calc`
- `damage.calc.begin`
- `damage.calc.after_base`
- `damage.calc.after_mitigate`
- `damage.calc.after_shield`
- `damage.calc.final`
- `damage.apply.before`
- `damage.apply.after`

这些事件被 `MobaTriggerEventAttribute` 注册到触发系统，因此脚本和计划动作可以在不同阶段插入逻辑。

### 6.2 负载访问

[`MobaBattlePayloadAccessor`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Gameplay/Triggering/MobaBattlePayloadAccessor.cs) 把以下对象暴露给触发器/条件系统：

- `AttackInfo`
- `AttackCalcInfo`
- `DamageResult`
- `UnitDieEventPayload`

它支持读取：

- attacker/target actor id；
- damage value；
- target hp / max hp；
- damage type；
- crit type；
- reason kind / reason param。

### 6.3 快照输出

[`MobaDamageEventSnapshotService`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Snapshot/MobaDamageEventSnapshotService.cs) 会把每一帧的伤害/治疗事件批量打包成快照：

- `ReportDamage(...)`
- `ReportHeal(...)`
- `TryGetSnapshot(...)`

这使得伤害结果可以进入回放、观战或状态同步链路。

### 6.4 死亡判定

虽然本节不展开死亡系统，但伤害结果已直接供以下逻辑使用：

- 单位死亡订阅者；
- 召唤物死亡订阅者；
- 生命值条件判断；
- 受击/击杀触发器。

这说明伤害并不是终点，而是后续战斗逻辑的起点。

---

## 7. 典型执行流程

```mermaid
flowchart TB
    A["技能/BUFF/反伤/普通攻击"] --> B["构造 AttackInfo"]
    B --> C["DamagePipelineService.Execute"]
    C --> D["发布 AttackCreated / BeforeCalc"]
    D --> E["创建 AttackCalcInfo"]
    E --> F["BaseDamage 阶段"]
    F --> G["Mitigation 阶段"]
    G --> H["ShieldAbsorb 阶段"]
    H --> I["FinalDamage 阶段"]
    I --> J["调用 MobaDamageService.ApplyDamage"]
    J --> K["写入目标 Hp"]
    K --> L["ReportDamage / Snapshot"]
    L --> M["发布 AfterApply / 触发死亡与后续反应"]
```

### 7.1 给伤害的时序

```mermaid
sequenceDiagram
    participant Plan as PlanAction
    participant Combat as MobaCombatEffectService
    participant Pipe as DamagePipelineService
    participant Calc as AttackCalcInfo
    participant Mit as MobaDamageMitigationService
    participant Shield as MobaShieldService
    participant Damage as MobaDamageService
    participant Snap as MobaDamageEventSnapshotService

    Plan->>Combat: DealDamage(AttackInfo)
    Combat->>Pipe: Execute(AttackInfo)
    Pipe->>Pipe: Publish AttackCreated / BeforeCalc
    Pipe->>Calc: new AttackCalcInfo(attack)
    Pipe->>Calc: Run BaseDamage
    Pipe->>Mit: Mitigate(attack, rawDamage)
    Pipe->>Shield: Absorb(attack, mitigatedDamage)
    Pipe->>Damage: ApplyDamage(..., hpDamage)
    Damage->>Snap: ReportDamage(...)
    Pipe-->>Plan: DamageResult
```

### 7.2 通用管线时序

```mermaid
sequenceDiagram
    participant Input as DamageRequest
    participant Pipe as DamageCalculationPipeline
    participant V as ValidateDamageProcessor
    participant C as CalculateCriticalProcessor
    participant B as CalculateBaseDamageProcessor
    participant R as Reduction/Bonus Processors
    participant O as Overkill Processor

    Input->>Pipe: Execute(request, context)
    Pipe->>V: Validate
    Pipe->>C: CalculateCritical
    Pipe->>B: CalculateBaseDamage
    Pipe->>R: ApplyDamageBonus / Armor / MagicResist
    Pipe->>O: CalculateFinalDamage / Overkill
    Pipe-->>Input: DamageResult
```

---

## 8. 扩展边界

### 8.1 适合扩展的点

- 新增 `DamageFormulaKind`，为不同技能体系提供不同公式；
- 在 `DamagePipelineService` 中追加阶段；
- 在 `MobaDamageMitigationService` 中引入更多防御维度；
- 在 `MobaShieldService` 中扩展护盾规则；
- 通过触发事件插入受击、吸血、反弹、免疫、转化等效果；
- 通过 `DamageSlots` 或 `AttackCalcInfo` 增加更多计算参数。

### 8.2 不建议耦合的点

- 不要把“扣血”直接塞进公式阶段；
- 不要让触发器直接修改低层通用伤害结果结构；
- 不要在 `MobaDamageService` 里混入复杂公式；
- 不要让 snapshot emitter 参与战斗裁决。

### 8.3 当前实现的约束

- 通用包偏向通用管线，MOBA 包偏向业务编排；
- 通用 Damage 默认管线使用 float，MOBA 结算主链使用 Fixed64；不能把 MOBA 的确定性证据外推给通用 Dataflow；
- 真实伤害绕过防御，但仍可能经过护盾和应用阶段；
- `DamagePipelineService` 当前默认只使用 `Standard` 公式；
- 伤害结果的“计算值”和“应用值”是不同概念。

### 8.4 证据状态与已知限制

- **E0 实现**：通用 Damage Request/Result、计算管线和 MOBA 结算服务均有源码入口。
- **E1 示例**：MOBA PlanAction、属性读取、护盾、事件和快照展示完整接入方式。
- **E2 集成**：技能、投射物、Buff/Trigger、角色血量和死亡链真实消费伤害编排。
- **E3 契约**：2026-08-16 当次 `AbilityKit.Combat.Damage.Tests` 仅 `1/1`，只固定默认 `DamageRequest` 结构；没有执行默认八阶段公式、Abort、CritRoll、护盾、负值或 Dataflow 异常矩阵。MOBA 的伤害、生命值和护盾测试属于另一个项目级结算实现。
- **E4 场景**：P0 Smoke/Unity artifact 只能作为其日期化覆盖战斗路径的场景证据；当前 MOBA 主工程 `279/305` 在 World 创建前阻断，不能证明当次伤害结算已重新运行。
- **E5 门禁**：尚无统一公式兼容、数值预算、跨端重放和完整结算事务的发布门禁。

当前管线阶段和业务事件都可能调用外部代码；采用方必须明确异常时是否 fail-fast、保留部分结果或执行补偿。Snapshot emitter 只能记录裁决结果，不能反向成为伤害权威源。

---

*文档类型：Canonical 设计（含 MOBA 结算示例） | 事实基线：2026-08-16 | 证据等级：通用包最小 E3 + MOBA 项目证据分层 | 文档版本：v3.0*

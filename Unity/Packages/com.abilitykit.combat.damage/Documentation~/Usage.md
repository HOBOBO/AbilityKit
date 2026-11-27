# Combat Damage 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.combat.damage` 0.1.0，2026-11-25 Runtime 与 .NET 10 镜像
> 核校状态：部分核校；默认八阶段、Context、结果与限制已对照源码，局部 E3 见第 6 节
> 事实基线：2026-11-25；不包含项目实际扣血或完整战斗验收

## 1. 目标与前置条件

计算输入为 `DamageRequest`，执行上下文必须显式使用 `DamageCalculationContext`，输出是 `DataflowResult<DamageResult>`。项目负责攻击者/目标的身份、有效性、属性读取与最终应用；本包只检查引用非空和基础值，不访问实体系统、不改生命。

安装与 Unity 程序集见[包 README](../README.md)，版本和依赖见 [package.json](../package.json)。使用 `AbilityKit.Combat` 命名空间；不能把文件名 DamageData 当作类型名。

## 2. 完整示例：物理减伤计算

本例使用占位 object 身份，测试纯计算，不代表真实目标校验。复用 Context 前 Reset 并重新填数据。预期输出 `damage calculation contracts passed`。

```csharp
using System;
using AbilityKit.Combat;

internal static class Program
{
    private static void Main()
    {
        var pipeline = DamageCalculationPipeline.CreateDefault();
        var request = DamageRequest.Create(new object(), new object(), new object(),
            baseValue: 100, damageType: DamageType.Physical);
        var context = new DamageCalculationContext
        {
            AttackerPhysicalDamage = 20,
            TargetArmor = 100,
            TargetCurrentHealth = 80
        };
        var result = pipeline.Execute(request, context);
        Require(result.IsSuccess && result.ProcessedCount == 8);
        Require(result.Output.FinalDamage == 60 && result.Output.ActualDamage == 60);
        Require(result.Output.Overkill == 0 && context.Result.FinalDamage == 60);

        context.Reset();
        var invalid = request;
        invalid.Target = null;
        var stopped = pipeline.Execute(invalid, context);
        Require(stopped.IsAborted && !stopped.HasError && stopped.ProcessedCount == 1);
        context.Reset();
        Console.WriteLine("damage calculation contracts passed");
    }
    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("Damage example failed.");
    }
}
```

## 3. 默认阶段与输入输出

默认顺序由 [DamageProcessors.cs](../Runtime/Damage/Processor/DamageProcessors.cs) 的 CreateDefault 定义：

| 顺序 | 阶段 | 当前行为 |
| --- | --- | --- |
| 1 | ValidateDamage | Attacker/Target 为空或 BaseValue 不为正则 Abort；DOT 标志不会豁免非正基础值 |
| 2 | CalculateCritical | 读取 CritChance、CritRoll、CritMultiplier；命中条件为 chance > 0 且 roll < chance，随机值由宿主注入 |
| 3 | CalculateBaseDamage | Physical 加 AttackerPhysicalDamage，Magic 加 AttackerMagicDamage；按当前请求暴击标记乘倍数 |
| 4 | ApplyDamageBonus | RawDamage 乘百分比加成，再加固定值；BonusDamage 记录累计加成 |
| 5–6 | Armor / MagicResistReduction | 仅精确等于 Physical / Magic 时减伤；有效抗性先按正百分比穿透缩放，再减固定穿透，钳到 0 |
| 7 | CalculateFinalDamage | FinalDamage = Floor(RawDamage)；没有统一非负/有限值钳制 |
| 8 | CalculateOverkill | 按 TargetCurrentHealth 计算溢出和实际值，再处理 TargetShield；边界见第 5 节 |

减伤系数为 `100 / (100 + effectiveResistance)`。PreArmorDamage 在基础伤害阶段更新，此后加成不会同步刷新它；RawDamage 会持续改写到减伤后的值，不始终代表原始伤害。FinalDamage、ActualDamage、ShieldDamage、Overkill 是计算字段，不是已提交事件或实际生命变更。

这是 `DamageRequest -> DamageResult` 异形 Dataflow：每阶段仍收到原 Request，`DamageProcessor` 从 Context.Result 取累积结果，在 OnAfterProcess 写回。当前 Execute 签名要求 context 参数；传 null 会创建普通 DataflowContext，不能代替领域 Context，否则阶段间结果不累计。Context.Request 是外部可填字段，默认链不会自动将它设置为本次 request。

## 4. 生命周期、失败与扩展

- 每笔执行独占领域 Context。Reset / Clear 同时清除基础槽位、Source、Abort、Request/Result 和攻击/目标数值；再填本笔属性。若直接复用不清理，减免、加成或结果字段可能残留。
- Request / Result 是 struct，但 Source/Attacker/Target 是引用，复制不隔离其对象。不要跨延迟边界持有已失效的业务对象；本包不管理或释放这些引用。
- 先检查 IsSuccess、IsAborted、HasError。验证失败计为完成 1 阶段，部分结果中仍带请求；异常由 Dataflow 记录失败阶段和上个完整输出，不回滚 Context 或业务副作用。
- 可构建自定义 DamageCalculationPipeline 并 AddProcessor；继承 DamageProcessor 时遵守 Context.Result 的读写协议。内置管线可在配置固定、每笔独立 Context 的条件下共享；自定义 Processor 自行保证可重入，不在运行时并发修改列表。
- CritRoll 默认 1，CritChance 默认 0，CritMultiplier 默认 1.5；TargetShield、加成与穿透默认 0。槽位是名称加类型标识，数值不由本包做完整值域验证。

## 5. 当前限制与迁移注意

以下是当前实现事实，不是推荐的项目结算规则；本轮只修文档，不调整机制：

1. **暴击标记不会可靠跨阶段保留。** CalculateCritical 会在结果 Request 中加 Critical，但每个 DamageProcessor.GetCurrentResult 都重新用原 input 覆盖 result.Request，下一阶段会丢掉新加标记。因此只设置 CritChance/CritRoll 不足以让默认链的最终伤害暴击。若输入已带 Critical，后续标记会保留，但仍受该阶段计算倍数影响；调用者不能据此推断完整暴击机制。
2. **Mixed 不会拆成两段。** 虽然 DamageType 带 Flags，攻击力、护甲和魔抗阶段使用精确枚举相等判断；Mixed 不吃物理/魔法攻击力加成，也不走对应抗性减免。True 跳过抗性，但仍经过其余默认阶段。
3. **Flags 多数只携带元数据。** 默认链未消费 PenetrateArmor、PenetrateMagicResist、IgnoreDamageReduction、IgnoreShield、Lifesteal、SpellVamp、AreaOfEffect、Reflected、Unavoidable 来执行对应行为；穿透由槽位提供。不要把枚举存在当机制已实现。
4. **护盾与生命上限并非完整提交语义。** TargetCurrentHealth > 0 时先算 Overkill / 生命钳制，再按 FinalDamage 减 TargetShield，可能覆盖先前钳制。例如 Final=100、HP=50、Shield=10，结果 ActualDamage=90、Overkill=50；生命 <= 0 时跳过护盾分支，ActualDamage=FinalDamage。不要把 ActualDamage 无条件当作真实扣血或死亡依据。
5. **输入与确定性限制。** 未统一拒绝 NaN/Infinity，也未限制穿透、加成、暴击概率的全部值域；最终可出现负值。float 与 Floor 不构成跨端帧同步确定性承诺。本包不提供真实命中、免疫、护盾消耗、吸血、死亡或补偿事务。

若项目需要这些规则，应在其结算契约和机制审批范围内定义；不要在文档核校中默默改变默认行为。旧开发说明中的类型概称和“同形约束”不作为接入依据。

## 6. 验证范围与关联文档

2026-11-25 核对 [Request/Result](../Runtime/Damage/Data/DamageData.cs)、[Context](../Runtime/Damage/Data/DamageCalculationContext.cs)、[枚举](../Runtime/Damage/Enum/DamageEnums.cs)和[全部默认阶段](../Runtime/Damage/Processor/DamageProcessors.cs)。同源 .NET suite、本页完整示例和限制探针结果由仓库 D4 记录维护，证据限 E0 与局部 E3。

没有运行 Unity、独立安装、完整结算、跨平台确定性或性能验收；局部测试通过不代表第 5 节限制已修复。返回[包内导航](README.md)。

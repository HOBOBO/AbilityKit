#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// ModifierBasics - 修改器基础概念
    /// 展示修改器框架的核心概念和使用方式
    /// </summary>
    [Sample]
    public sealed class ModifierBasics : SampleBase
    {
        public override string Title => "修改器基础 (Modifier Basics)";
        public override string Description => "理解 AbilityKit.Modifiers 框架的核心概念";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("=== 修改器系统 (Modifier System) ===");
            Output.Divider();

            Log("【1】核心类型:");
            Output.Bullet("ModifierKey: 修改目标的唯一标识");
            Output.Bullet("ModifierData: 单个修改器的数据");
            Output.Bullet("ModifierOp: 修改操作类型");
            Output.Bullet("MagnitudeSource: 数值来源策略");
            Output.Bullet("ModifierCalculator: 计算最终值");

            Output.Divider();

            Log("【2】修改操作 (ModifierOp):");
            Output.Bullet("Add (+): 基础值 + 修改值");
            Output.Bullet("Mul (×): 基础值 × 修改值");
            Output.Bullet("PercentAdd (%+): 基础值 × (1 + 修改值)");
            Output.Bullet("Override (=): 直接覆盖为基础值");
            Output.Bullet("Custom: 自定义操作");

            Output.Divider();

            Log("【3】计算优先级 (数字越小越先执行):");
            Output.Bullet("Override (优先级 0): 终止所有其他修改");
            Output.Bullet("Add (优先级 10): 加法加成");
            Output.Bullet("PercentAdd (优先级 15): 百分比加成");
            Output.Bullet("Mul (优先级 20): 乘法加成");

            Output.Divider();

            Log("【4】最终公式 (无 Override 时):");
            Log("  Final = (Base + Add Sum) × PercentAdd Product × Mul Product");

            Output.Divider();

            Log("【5】示例计算:");
            float baseValue = 100f;
            Log($"  基础值: {baseValue}");

            var modifiers = new List<ModifierData>
            {
                ModifierData.Add(ModifierKey.AttackPower, 50f),
                ModifierData.Add(ModifierKey.AttackPower, 20f),
                ModifierData.PercentAdd(ModifierKey.AttackPower, 0.2f),
                ModifierData.Mul(ModifierKey.AttackPower, 1.5f)
            };

            var calculator = new ModifierCalculator();
            var result = calculator.Calculate(modifiers.ToArray(), baseValue);

            Log($"  应用修改器:");
            Log($"    - Add: +50");
            Log($"    - Add: +20");
            Log($"    - PercentAdd: +20%");
            Log($"    - Mul: ×1.5");
            Log($"  计算过程: (100 + 50 + 20) × 1.2 × 1.5 = {result.FinalValue}");

            Output.Divider();

            Log("【6】MagnitudeSource 数值来源:");
            Output.Bullet("Fixed(value): 固定值");
            Output.Bullet("LevelCurve: 随等级变化的曲线");
            Output.Bullet("Attribute: 引用其他属性值");
            Output.Bullet("TimeDecay: 随时间衰减");
            Output.Bullet("Pipeline: 多个变换组合");

            Output.Divider();

            Log("【7】时间衰减示例:");
            var timeDecayMod = ModifierData.AddWithTimeDecay(
                ModifierKey.MoveSpeed,
                initialValue: 50f,
                duration: 5f,
                DecayType.Linear
            );

            Log("  初始速度加成: +50, 持续 5 秒");
            Log("  时间进程:");

            var context = new SampleModifierContext();
            for (int i = 0; i <= 5; i++)
            {
                context.ElapsedTime = i;
                float bonus = timeDecayMod.GetMagnitude(1f, context);
                Log($"    t={i}s: +{bonus:F1} 速度 (剩余 {Math.Max(0, 5 - i):F1}s)");
            }
        }
    }
}

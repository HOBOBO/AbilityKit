#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// ModifierPipelineExample - 修改器管道示例
    /// 演示 ModifierPipeline、OperatorComposer、ModifierCalculator 等高级特性
    /// </summary>
    [Sample]
    public sealed class ModifierPipelineExample : SampleBase
    {
        public override string Title => "Modifier Pipeline";
        public override string Description => "演示修改器管道、数值计算流水线、复杂 MagnitudeSource 组合";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("=== ModifierPipeline 修改器管道示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("ModifierPipeline - 修改器计算流水线");
            Output.Bullet("OperatorComposer - 操作符组合器");
            Output.Bullet("ModifierCalculator - 修改器计算器");
            Output.Bullet("MagnitudeSource - 数值来源策略");
            Output.Bullet("IModifierContext - 修改器上下文");
            Log("");

            // 2. 管道阶段
            Log("【2】管道阶段");
            Log("");
            Log("  ModifierPipeline 执行流程:");
            Log("    1. 收集所有相关修改器");
            Log("    2. 按优先级排序");
            Log("    3. 按操作类型分组 (Add/Mul/PercentAdd/Override)");
            Log("    4. 依次应用各组修改器");
            Log("    5. 返回最终计算结果");
            Log("");

            // 3. 操作符组合顺序
            Log("【3】操作符组合顺序");
            Output.Bullet("Override - 最先应用，作为基础值");
            Output.Bullet("Add - 加法修改");
            Output.Bullet("Mul - 乘法修改");
            Output.Bullet("PercentAdd - 百分比加成");
            Log("");
            Log("  计算公式: Result = Override + Add + BaseValue * (1 + Mul) * (1 + PercentAdd)");
            Log("");

            // 4. MagnitudeSource 类型
            Log("【4】MagnitudeSource 类型");
            Log("");
            Log("  // 固定值");
            Log("  MagnitudeSource.Fixed(100f)");
            Log("");
            Log("  // 等级曲线");
            Log("  MagnitudeSource.LevelCurve(curveData)");
            Log("");
            Log("  // 属性引用");
            Log("  MagnitudeSource.AttributeRef(ModifierKey.Strength, 0.5f)");
            Log("");
            Log("  // 时间衰减");
            Log("  MagnitudeSource.TimeDecay(50f, 5f, DecayType.Exponential)");
            Log("");
            Log("  // 管道组合");
            Log("  MagnitudeSource.Pipeline(customPipeline)");
            Log("");

            // 5. 时间衰减
            Log("【5】时间衰减 (TimeDecay)");
            Output.Bullet("指数衰减 (Exponential) - 快速衰减");
            Output.Bullet("线性衰减 (Linear) - 匀速衰减");
            Output.Bullet("阶梯衰减 (Step) - 分段衰减");
            Output.Bullet("自定义衰减 (Custom) - 自定义曲线");
            Log("");
            Log("  // 指数衰减示例");
            Log("  MagnitudeSource.TimeDecay(");
            Log("      initialValue: 50f,");
            Log("      duration: 5f,");
            Log("      decayType: DecayType.Exponential,");
            Log("      decayCoefficient: 2f");
            Log("  );");
            Log("  // 初始 50，每秒按 e^(-2t) 衰减");
            Log("");

            // 6. 管道组合
            Log("【6】管道组合 (Pipeline)");
            Log("");
            Log("  // 创建复杂管道");
            Log("  var pipeline = ModifierPipeline.Create()");
            Log("      .ThenTimeDecay(50f, 5f, DecayType.Linear)");
            Log("      .ThenLevelCurve(10f, levelCurve)");
            Log("      .ThenAttributeRef(ModifierKey.Strength, 0.5f);");
            Log("");
            Log("  var mod = ModifierData.AddWithPipeline(");
            Log("      ModifierKey.AttackPower,");
            Log("      pipeline,");
            Log("      sourceId: 1");
            Log("  );");
            Log("");

            // 7. ModifierCalculator 使用
            Log("【7】ModifierCalculator 使用");
            Log("");
            Log("  // 创建计算器");
            Log("  var calculator = new ModifierCalculator(cache);");
            Log("");
            Log("  // 计算最终值");
            Log("  var result = calculator.Calculate(");
            Log("      ModifierKey.AttackPower,");
            Log("      baseValue: 100f,");
            Log("      level: 5,");
            Log("      context");
            Log("  );");
            Log("");
            Log("  // Result 包含:");
            Log("  // - FinalValue: 最终计算结果");
            Log("  // - Modifiers: 应用的修改器列表");
            Log("  // - Breakdown: 计算明细");
            Log("");

            // 8. 代码示例 - 应用修改器
            Log("【8】代码示例 - 应用修改器");
            Log("");
            Log("  // 创建修改器");
            Log("  var mod1 = ModifierData.Add(ModifierKey.AttackPower, 50f, sourceId: 1);");
            Log("  var mod2 = ModifierData.Mul(ModifierKey.AttackPower, 0.2f, sourceId: 2);");
            Log("  var mod3 = ModifierData.PercentAdd(ModifierKey.AttackPower, 0.1f, sourceId: 3);");
            Log("");
            Log("  // 添加到缓存");
            Log("  cache.AddModifier(characterId, mod1);");
            Log("  cache.AddModifier(characterId, mod2);");
            Log("  cache.AddModifier(characterId, mod3);");
            Log("");
            Log("  // 批量移除（通过 SourceId）");
            Log("  cache.RemoveBySourceId(characterId, 2);");
            Log("");

            // 9. ModifierKey
            Log("【9】ModifierKey");
            Output.Bullet("定义修改的目标属性");
            Output.Bullet("支持自定义键");
            Output.Bullet("提供命名空间隔离");
            Log("");
            Log("  // 预定义键");
            Log("  ModifierKey.AttackPower");
            Log("  ModifierKey.Defense");
            Log("  ModifierKey.MoveSpeed");
            Log("  ModifierKey.MaxHealth");
            Log("  ModifierKey.CriticalChance");
            Log("  ModifierKey.CriticalDamage");
            Log("");
            Log("  // 自定义键");
            Log("  var customKey = new ModifierKey(\"Custom.Stat\");");
            Log("");

            // 10. ModifierContext
            Log("【10】ModifierContext");
            Output.Bullet("提供修改器计算时的上下文信息");
            Output.Bullet("包含等级、源属性、目标实体等");
            Output.Bullet("支持自定义数据注入");
            Log("");
            Log("  var context = new ModifierContext");
            Log("  {");
            Log("      Level = character.Level,");
            Log("      SourceEntity = caster,");
            Log("      TargetEntity = target,");
            Log("      SourceAttribute = strength,");
            Log("  };");
            Log("");

            // 11. 叠加策略
            Log("【11】叠加策略");
            Output.Bullet("Independent - 独立叠加，每个修改器单独计算");
            Output.Bullet("Stacked - 叠加后计算，效果增强");
            Output.Bullet("Refresh - 刷新持续时间");
            Output.Bullet("HighestOnly - 只保留最高值");
            Log("");

            // 12. API 参考
            Log("【12】关键 API 参考");
            Output.Bullet("AbilityKit.Modifiers.Core.ModifierPipeline");
            Output.Bullet("AbilityKit.Modifiers.Core.Engine.ModifierCalculator");
            Output.Bullet("AbilityKit.Modifiers.Core.Engine.OperatorComposer");
            Output.Bullet("AbilityKit.Modifiers.Core.Source.MagnitudeSource");
            Output.Bullet("AbilityKit.Modifiers.Core.Data.ModifierData");
            Output.Bullet("AbilityKit.Modifiers.Core.Data.ModifierContext");
            Log("");

            Output.Divider();
            Log("【总结】ModifierPipeline 提供强大的数值计算流水线，支持复杂的效果组合和计算");
        }
    }
}

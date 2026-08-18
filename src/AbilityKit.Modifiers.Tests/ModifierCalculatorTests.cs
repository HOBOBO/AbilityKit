using System;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// ModifierCalculator 计算语义测试。
    /// 公式（无 Override）：FinalValue = (BaseValue + AddSum) × PercentProduct × MulProduct。
    /// 注意：ModifierCalculator 实例内建结果缓存（按 修改器数量+哈希+baseValue 识别），
    /// 涉及不同 level 的用例一律使用新实例，避免缓存串扰。
    /// </summary>
    public sealed class ModifierCalculatorTests
    {
        [Fact]
        public void Calculate_empty_modifiers_returns_base_unchanged()
        {
            var calc = new ModifierCalculator();
            var result = calc.Calculate(Array.Empty<ModifierData>(), 123.5f);
            Assert.Equal(123.5f, result.BaseValue);
            Assert.Equal(123.5f, result.FinalValue);
            Assert.Equal(0, result.Count);
            Assert.False(result.HasModifiers);
            Assert.False(result.HasOverride);
        }

        [Fact]
        public void Calculate_single_add_applies_base_plus_value()
        {
            var calc = new ModifierCalculator();
            var result = calc.Calculate(new[] { ModifierData.Add(TestKeys.Attack, 50f) }, 100f);
            Assert.Equal(100f, result.BaseValue);
            Assert.Equal(50f, result.AddSum);
            Assert.Equal(1f, result.PercentProduct);
            Assert.Equal(1f, result.MulProduct);
            Assert.Equal(1, result.Count);
            FloatAssert.Near(150f, result.FinalValue);
        }

        [Fact]
        public void Calculate_single_mul_applies_base_times_value()
        {
            var calc = new ModifierCalculator();
            var result = calc.Calculate(new[] { ModifierData.Mul(TestKeys.Attack, 1.5f) }, 100f);
            FloatAssert.Near(150f, result.FinalValue);
            Assert.Equal(1.5f, result.MulProduct);
        }

        [Fact]
        public void Calculate_single_percent_add_applies_base_times_one_plus_value()
        {
            var calc = new ModifierCalculator();
            var result = calc.Calculate(new[] { ModifierData.PercentAdd(TestKeys.Attack, 0.2f) }, 100f);
            FloatAssert.Near(120f, result.FinalValue);
            FloatAssert.Near(1.2f, result.PercentProduct);
        }

        [Fact]
        public void Calculate_single_override_replaces_value_and_counts_one()
        {
            var calc = new ModifierCalculator();
            var result = calc.Calculate(new[] { ModifierData.Override(TestKeys.Attack, 77f) }, 100f);
            Assert.True(result.HasOverride);
            Assert.Equal(77f, result.FinalValue);
            Assert.Equal(1, result.Count);
            Assert.Equal(77f, result.OverrideValue);
        }

        [Fact]
        public void Calculate_combined_groups_follow_formula_base_plus_add_times_percent_times_mul()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Add(TestKeys.Attack, 10f),
                ModifierData.PercentAdd(TestKeys.Attack, 0.2f), // ×1.2
                ModifierData.Mul(TestKeys.Attack, 1.5f),
            };
            var result = calc.Calculate(mods, 100f);
            // (100 + 10) × 1.2 × 1.5 = 198
            FloatAssert.Near(198f, result.FinalValue);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Calculate_input_order_does_not_change_result()
        {
            var add = ModifierData.Add(TestKeys.Attack, 10f);
            var percent = ModifierData.PercentAdd(TestKeys.Attack, 0.2f);
            var mul = ModifierData.Mul(TestKeys.Attack, 1.5f);

            // 组合器会按操作优先级稳定排序，任意输入顺序都应得到同一结果。
            var orders = new[]
            {
                new[] { add, percent, mul },
                new[] { mul, add, percent },
                new[] { percent, mul, add },
                new[] { mul, percent, add },
            };

            var expected = new ModifierCalculator().Calculate(orders[0], 100f).FinalValue;
            foreach (var order in orders)
            {
                // 每个顺序用独立计算器，排除缓存影响。
                FloatAssert.Near(expected, new ModifierCalculator().Calculate(order, 100f).FinalValue);
            }
            FloatAssert.Near(198f, expected);
        }

        [Fact]
        public void Calculate_multiple_adds_sum()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Add(TestKeys.Attack, 10f, sourceId: 1),
                ModifierData.Add(TestKeys.Attack, 20f, sourceId: 2),
                ModifierData.Add(TestKeys.Attack, -5f, sourceId: 3),
            };
            var result = calc.Calculate(mods, 100f);
            FloatAssert.Near(25f, result.AddSum);
            FloatAssert.Near(125f, result.FinalValue);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Calculate_multiple_muls_product()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Mul(TestKeys.Attack, 2f),
                ModifierData.Mul(TestKeys.Attack, 0.5f),
                ModifierData.Mul(TestKeys.Attack, 3f),
            };
            var result = calc.Calculate(mods, 100f);
            FloatAssert.Near(3f, result.MulProduct);
            FloatAssert.Near(300f, result.FinalValue);
        }

        [Fact]
        public void Calculate_percent_adds_compound_multiplicatively_not_summed()
        {
            // 两个 PercentAdd(0.2 / 0.1) → ×(1.2 × 1.1) = ×1.32，而不是 ×1.3（钉复合语义）。
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.PercentAdd(TestKeys.Attack, 0.2f),
                ModifierData.PercentAdd(TestKeys.Attack, 0.1f),
            };
            var result = calc.Calculate(mods, 100f);
            FloatAssert.Near(1.32f, result.PercentProduct);
            FloatAssert.Near(132f, result.FinalValue);
        }

        [Fact]
        public void Calculate_override_discards_all_other_modifiers()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Add(TestKeys.Attack, 1000f),
                ModifierData.Override(TestKeys.Attack, 50f),
                ModifierData.Mul(TestKeys.Attack, 10f),
            };
            var result = calc.Calculate(mods, 100f);
            Assert.True(result.HasOverride);
            Assert.Equal(50f, result.FinalValue);
            // Override 是终止操作：生效计数被重置为 1。
            Assert.Equal(1, result.Count);
        }

        [Fact]
        public void Calculate_level_feeds_level_curve_magnitude()
        {
            var curve = new float[] { 1f, 1f, 5f, 5f }; // [等级, 值] 对
            var mods = new[] { ModifierData.Add(TestKeys.Attack, MagnitudeSource.LevelCurve(10f, curve, 1f)) };

            // 等级 1 → 插值 1 → 幅度 10；等级 3 → 插值 3 → 幅度 30。分别用独立计算器避免缓存。
            FloatAssert.Near(110f, new ModifierCalculator().Calculate(mods, 100f, level: 1f).FinalValue);
            FloatAssert.Near(130f, new ModifierCalculator().Calculate(mods, 100f, level: 3f).FinalValue);
        }

        [Fact]
        public void Calculate_context_attribute_reference_resolves_from_context()
        {
            var ctx = new TestModifierContext { Level = 1f };
            ctx.Attributes[TestKeys.Strength] = 80f;
            var mods = new[] { ModifierData.Add(TestKeys.Attack, MagnitudeSource.Attribute(TestKeys.Strength, 0.5f)) };

            var result = new ModifierCalculator().Calculate(mods, 100f, ctx);
            // 属性 80 × 系数 0.5 = 40 → 100 + 40
            FloatAssert.Near(140f, result.FinalValue);
        }

        [Fact]
        public void Calculate_recorder_records_entries_in_input_order_with_contributions()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Mul(TestKeys.Attack, 2f, sourceId: 7),
                ModifierData.Add(TestKeys.Attack, 10f, sourceId: 3),
            };
            var recorder = new DefaultRecorder(4);

            var result = calc.Calculate(mods, 100f, recorder);
            FloatAssert.Near(220f, result.FinalValue); // (100+10) × 2

            Assert.Equal(2, recorder.Count);
            // 记录顺序 = 输入顺序（而非排序后的执行顺序）：Mul 在前。
            ref readonly var mulEntry = ref recorder.GetEntry(0);
            Assert.Equal(ModifierOp.Mul, mulEntry.Op);
            Assert.Equal(2f, mulEntry.Value);
            // Mul 贡献基准是 baseValue（不含 AddSum）：100 × (2-1) = 100。
            FloatAssert.Near(100f, mulEntry.Contribution);
            Assert.Equal(7, mulEntry.SourceId);

            ref readonly var addEntry = ref recorder.GetEntry(1);
            Assert.Equal(ModifierOp.Add, addEntry.Op);
            FloatAssert.Near(10f, addEntry.Contribution);
            Assert.Equal(3, addEntry.SourceId);
        }

        [Fact]
        public void Calculate_percent_add_contribution_base_includes_add_sum()
        {
            var calc = new ModifierCalculator();
            var mods = new[]
            {
                ModifierData.Add(TestKeys.Attack, 10f),
                ModifierData.PercentAdd(TestKeys.Attack, 0.2f),
            };
            var recorder = new DefaultRecorder(4);

            calc.Calculate(mods, 100f, recorder);

            // PercentAdd 的贡献基准 = baseValue + AddSum = 110 → 贡献 110 × 0.2 = 22。
            FloatAssert.Near(22f, recorder.GetEntry(1).Contribution);
        }

        [Fact]
        public void Calculate_with_recorder_bypasses_cache_so_level_change_recomputes()
        {
            var curve = new float[] { 1f, 1f, 5f, 5f };
            var mods = new[] { ModifierData.Add(TestKeys.Attack, MagnitudeSource.LevelCurve(10f, curve, 1f)) };
            var calc = new ModifierCalculator();

            // 传 recorder 时不写/读缓存。
            var r1 = new DefaultRecorder(2);
            var r2 = new DefaultRecorder(2);
            FloatAssert.Near(110f, calc.Calculate(mods, 100f, r1, level: 1f, null).FinalValue);
            FloatAssert.Near(130f, calc.Calculate(mods, 100f, r2, level: 3f, null).FinalValue);
        }

        [Fact]
        public void Calculate_cache_returns_first_level_result_when_level_changes_documents_current_behavior()
        {
            // 钉当前行为（可疑）：缓存键不包含 level/context，
            // 同一组修改器 + 同一 baseValue 换等级会命中旧缓存，返回等级 1 的结果。
            // 若未来修复此问题（缓存键纳入 level），本测试应删除或反转。
            var curve = new float[] { 1f, 1f, 5f, 5f };
            var mods = new[] { ModifierData.Add(TestKeys.Attack, MagnitudeSource.LevelCurve(10f, curve, 1f)) };
            var calc = new ModifierCalculator();

            FloatAssert.Near(110f, calc.Calculate(mods, 100f, level: 1f).FinalValue);
            FloatAssert.Near(110f, calc.Calculate(mods, 100f, level: 3f).FinalValue); // 期望 130，实际命中缓存 110
        }

        [Fact]
        public void Invalidate_forces_recompute_after_level_change()
        {
            var curve = new float[] { 1f, 1f, 5f, 5f };
            var mods = new[] { ModifierData.Add(TestKeys.Attack, MagnitudeSource.LevelCurve(10f, curve, 1f)) };
            var calc = new ModifierCalculator();

            FloatAssert.Near(110f, calc.Calculate(mods, 100f, level: 1f).FinalValue);
            calc.Invalidate();
            FloatAssert.Near(130f, calc.Calculate(mods, 100f, level: 3f).FinalValue);
        }

        [Fact]
        public void Calculate_base_value_change_misses_cache_and_recomputes()
        {
            var mods = new[] { ModifierData.Add(TestKeys.Attack, 10f) };
            var calc = new ModifierCalculator();

            FloatAssert.Near(110f, calc.Calculate(mods, 100f).FinalValue);
            FloatAssert.Near(210f, calc.Calculate(mods, 200f).FinalValue);
        }

        [Fact]
        public void Calculate_null_recorder_behaves_like_no_recorder_and_allows_cache()
        {
            var mods = new[] { ModifierData.Add(TestKeys.Attack, 10f) };
            var calc = new ModifierCalculator();
            NullRecorder recorder = NullRecorder.Default;

            var result = calc.Calculate(mods, 100f, recorder);
            FloatAssert.Near(110f, result.FinalValue);
            Assert.Equal(0, recorder.Count);
        }

        [Fact]
        public void Calculate_repeated_call_returns_cached_same_result()
        {
            var mods = new[] { ModifierData.Add(TestKeys.Attack, 10f), ModifierData.Mul(TestKeys.Attack, 2f) };
            var calc = new ModifierCalculator();

            var first = calc.Calculate(mods, 100f);
            var second = calc.Calculate(mods, 100f);
            Assert.Equal(first.FinalValue, second.FinalValue);
            Assert.Equal(first.AddSum, second.AddSum);
            Assert.Equal(first.Count, second.Count);
        }

        [Fact]
        public void CalculateFinal_returns_final_value_shortcut()
        {
            var mods = new[] { ModifierData.Add(TestKeys.Attack, 7f) };
            FloatAssert.Near(107f, new ModifierCalculator().CalculateFinal(mods, 100f));
        }

        [Fact]
        public void CalculateBatch_computes_each_base_value()
        {
            var mods = new[] { ModifierData.Add(TestKeys.Attack, 5f) };
            var bases = new float[] { 10f, 20f, 30f };
            var results = new ModifierResult[3];

            new ModifierCalculator().CalculateBatch(mods, bases, level: 1f, context: null, results);

            FloatAssert.Near(15f, results[0].FinalValue);
            FloatAssert.Near(25f, results[1].FinalValue);
            FloatAssert.Near(35f, results[2].FinalValue);
        }

        [Fact]
        public void Calculate_custom_registered_operator_joins_composition()
        {
            // 注册 Priority=12 / IsAdditive=true 的自定义操作符 → 落入 AddSum 分支。
            ModifierOperatorRegistry.Register(new CustomAdditiveOperator());
            var mods = new[]
            {
                ModifierData.Add(TestKeys.Attack, 10f),
                new ModifierData { Key = TestKeys.Attack, Op = OpCodes.CustomAdd, Magnitude = MagnitudeSource.Fixed(5f) },
            };
            var result = new ModifierCalculator().Calculate(mods, 100f);
            FloatAssert.Near(15f, result.AddSum);
            FloatAssert.Near(115f, result.FinalValue);
        }
    }
}

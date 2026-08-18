using System;
using System.Linq;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// OperatorComposer 组合语义测试：
    /// 默认策略按操作优先级分组累积（Override 0 / Add 10 / PercentAdd 15 / Mul 20），
    /// FinalValue = Override ? OverrideValue : (Base + AddSum) × PercentProduct × MulProduct。
    /// </summary>
    public sealed class OperatorComposerTests
    {
        private static ModifierData Mod(ModifierOp op, float value, int priority, int sourceId = 0) => new()
        {
            Key = TestKeys.Attack,
            Op = op,
            Priority = priority,
            SourceId = sourceId,
            Magnitude = MagnitudeSource.Fixed(value),
        };

        [Fact]
        public void Compose_empty_returns_empty_result()
        {
            var result = OperatorComposer.Compose(Array.Empty<ModifierData>(), 42f, 1f, null);
            Assert.Equal(42f, result.BaseValue);
            Assert.Equal(42f, result.FinalValue);
            Assert.Equal(0, result.Count);
            Assert.Equal(1f, result.PercentProduct);
            Assert.Equal(1f, result.MulProduct);
        }

        [Fact]
        public void Compose_single_add_modifier_applies_directly()
        {
            var result = OperatorComposer.Compose(new[] { Mod(ModifierOp.Add, 8f, 10) }, 100f, 1f, null);
            FloatAssert.Near(108f, result.FinalValue);
            Assert.Equal(1, result.Count);
        }

        [Fact]
        public void Compose_groups_by_operator_priority_regardless_of_input_order()
        {
            // Add 先于 PercentAdd / Mul 生效（加在基础值上，再整体乘）。
            var add = Mod(ModifierOp.Add, 10f, 10);
            var percent = Mod(ModifierOp.PercentAdd, 0.5f, 10);
            var mul = Mod(ModifierOp.Mul, 2f, 10);

            var a = OperatorComposer.Compose(new[] { add, percent, mul }, 100f, 1f, null);
            var b = OperatorComposer.Compose(new[] { mul, percent, add }, 100f, 1f, null);

            // (100 + 10) × 1.5 × 2 = 330
            FloatAssert.Near(330f, a.FinalValue);
            FloatAssert.Near(a.FinalValue, b.FinalValue);
            FloatAssert.Near(a.AddSum, b.AddSum);
            FloatAssert.Near(a.PercentProduct, b.PercentProduct);
            FloatAssert.Near(a.MulProduct, b.MulProduct);
        }

        [Fact]
        public void Compose_add_group_applies_before_mul_group_semantics()
        {
            // (100 + 10) × 2 = 220，而非 100 × 2 + 10 = 210 —— 钉"先加后乘"分组语义。
            var mods = new[] { Mod(ModifierOp.Add, 10f, 10), Mod(ModifierOp.Mul, 2f, 10) };
            FloatAssert.Near(220f, OperatorComposer.Compose(mods, 100f, 1f, null).FinalValue);
        }

        [Fact]
        public void Compose_override_terminates_and_reports_count_one()
        {
            var mods = new[]
            {
                Mod(ModifierOp.Add, 10f, 10),
                Mod(ModifierOp.Mul, 3f, 10),
                Mod(ModifierOp.Override, 7f, 0),
                Mod(ModifierOp.PercentAdd, 0.5f, 10),
            };
            var result = OperatorComposer.Compose(mods, 100f, 1f, null);
            Assert.True(result.HasOverride);
            Assert.Equal(7f, result.FinalValue);
            Assert.Equal(1, result.Count);
        }

        [Fact]
        public void Compose_first_override_in_sorted_order_wins_by_modifier_priority()
        {
            // 同为 Override：排序键为（操作优先级 0, 修改器 Priority）→ Priority 小者在前、先触发、生效。
            var highPrioValue = Mod(ModifierOp.Override, 20f, priority: 5);
            var lowPrioValue = Mod(ModifierOp.Override, 10f, priority: 1);

            var result = OperatorComposer.Compose(new[] { highPrioValue, lowPrioValue }, 100f, 1f, null);
            Assert.Equal(10f, result.FinalValue);
            Assert.Equal(10f, result.OverrideValue);
        }

        [Fact]
        public void Compose_equal_priority_overrides_first_input_wins()
        {
            // 稳定排序下同键保持输入顺序 → 先输入的 Override 生效。
            var first = Mod(ModifierOp.Override, 11f, priority: 0);
            var second = Mod(ModifierOp.Override, 22f, priority: 0);
            var result = OperatorComposer.Compose(new[] { first, second }, 100f, 1f, null);
            Assert.Equal(11f, result.FinalValue);
        }

        [Fact]
        public void Compose_unregistered_operator_is_skipped()
        {
            // (ModifierOp)200 从未注册 → Get 返回 null → 修改器被跳过。
            var mods = new[]
            {
                Mod(ModifierOp.Add, 10f, 10),
                Mod(OpCodes.NeverRegistered, 999f, 10, sourceId: 9),
            };
            var result = OperatorComposer.Compose(mods, 100f, 1f, null);
            FloatAssert.Near(110f, result.FinalValue);
            Assert.Equal(1, result.Count);
        }

        [Fact]
        public void Compose_custom_multiplicative_operator_falls_into_mul_group()
        {
            // Priority=25（未定义分组）且 IsAdditive=false → 走 default 分支并入 MulProduct。
            ModifierOperatorRegistry.Register(new CustomMultiplicativeOperator());
            var mods = new[]
            {
                Mod(ModifierOp.Mul, 2f, 10),
                Mod(OpCodes.CustomMul, 3f, 10),
            };
            var result = OperatorComposer.Compose(mods, 100f, 1f, null);
            FloatAssert.Near(6f, result.MulProduct);
            FloatAssert.Near(600f, result.FinalValue);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void SortByPriority_orders_by_operator_priority_then_modifier_priority()
        {
            var mods = new[]
            {
                Mod(ModifierOp.Mul, 2f, priority: 10),
                Mod(ModifierOp.Add, 1f, priority: 10),
                Mod(ModifierOp.Override, 3f, priority: 0),
                Mod(ModifierOp.Add, 4f, priority: 5),
                Mod(ModifierOp.PercentAdd, 5f, priority: 10),
            };
            OperatorComposer.SortByPriority(mods);

            Assert.Equal(new[]
            {
                ModifierOp.Override,
                ModifierOp.Add,      // priority 5
                ModifierOp.Add,      // priority 10
                ModifierOp.PercentAdd,
                ModifierOp.Mul,
            }, mods.Select(m => m.Op).ToArray());
            Assert.Equal(new[] { 5, 10 }, new[] { mods[1].Priority, mods[2].Priority });
        }

        [Fact]
        public void SortByPriority_is_stable_for_equal_keys()
        {
            var first = Mod(ModifierOp.Add, 1f, priority: 10, sourceId: 1);
            var second = Mod(ModifierOp.Add, 2f, priority: 10, sourceId: 2);
            var third = Mod(ModifierOp.Add, 3f, priority: 0, sourceId: 3);
            var mods = new[] { first, second, third };

            OperatorComposer.SortByPriority(mods);

            // third (priority 0) 提前；first/second 同键保持输入相对顺序。
            Assert.Equal(3, mods[0].SourceId);
            Assert.Equal(1, mods[1].SourceId);
            Assert.Equal(2, mods[2].SourceId);
        }

        [Fact]
        public void SortByPriority_single_or_empty_is_noop()
        {
            var single = new[] { Mod(ModifierOp.Mul, 2f, 10) };
            OperatorComposer.SortByPriority(single);
            Assert.Equal(ModifierOp.Mul, single[0].Op);

            var empty = Array.Empty<ModifierData>();
            OperatorComposer.SortByPriority(empty);
            Assert.Empty(empty);
        }

        [Fact]
        public void SortByPriority_unregistered_op_sorts_last()
        {
            // 未注册操作优先级视为 int.MaxValue → 排到最后。
            var mods = new[]
            {
                Mod(OpCodes.NeverRegistered, 1f, priority: 0),
                Mod(ModifierOp.Mul, 2f, priority: 100),
            };
            OperatorComposer.SortByPriority(mods);
            Assert.Equal(ModifierOp.Mul, mods[0].Op);
            Assert.Equal(OpCodes.NeverRegistered, mods[1].Op);
        }

        [Fact]
        public void ComposeSorted_on_presorted_input_matches_compose()
        {
            // 逆序输入走 Compose（内部排序）与手工排好序走 ComposeSorted，结果一致。
            var add = Mod(ModifierOp.Add, 10f, 10);
            var mul = Mod(ModifierOp.Mul, 2f, 10);
            var unsorted = new[] { mul, add };
            var sorted = new[] { add, mul };

            var viaCompose = OperatorComposer.Compose(unsorted, 100f, 1f, null);
            var viaSorted = OperatorComposer.ComposeSorted(sorted, 100f, 1f, null);

            FloatAssert.Near(viaCompose.FinalValue, viaSorted.FinalValue);
            FloatAssert.Near(220f, viaSorted.FinalValue);
        }

        [Fact]
        public void Compose_custom_strategy_is_used()
        {
            var strategy = new AddOnlyStrategy();
            var mods = new[] { Mod(ModifierOp.Mul, 100f, 10), Mod(ModifierOp.Add, 1f, 10) };

            var result = OperatorComposer.Compose(mods, 50f, 1f, null, strategy);

            Assert.Equal("AddOnly", strategy.Name);
            // 自定义策略无视操作类型，把所有幅度加进 AddSum：50 + 100 + 1 = 151。
            FloatAssert.Near(151f, result.FinalValue);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Compose_null_strategy_falls_back_to_default()
        {
            var mods = new[] { Mod(ModifierOp.Add, 10f, 10), Mod(ModifierOp.Mul, 2f, 10) };
            var withDefault = OperatorComposer.Compose(mods, 100f, 1f, null);
            var withNull = OperatorComposer.Compose(mods, 100f, 1f, null, null);

            // 注意：strategy==null 分支不排序直接交给 DefaultStrategy（分组累积下结果与排序路径一致）。
            FloatAssert.Near(withDefault.FinalValue, withNull.FinalValue);
            FloatAssert.Near(220f, withNull.FinalValue);
        }

        [Fact]
        public void DefaultStrategy_name_is_Default()
        {
            Assert.Equal("Default", OperatorComposer.DefaultStrategy.Name);
        }

        [Fact]
        public void ComposeBatch_fills_all_results()
        {
            var mods = new[] { Mod(ModifierOp.Add, 1f, 10) };
            var bases = new float[] { 10f, 20f };
            var results = new ModifierResult[2];

            OperatorComposer.ComposeBatch(mods, bases, 1f, null, results);

            FloatAssert.Near(11f, results[0].FinalValue);
            FloatAssert.Near(21f, results[1].FinalValue);
        }

        [Fact]
        public void ApplyModifiers_extension_returns_final_value()
        {
            ReadOnlySpan<ModifierData> mods = new[] { Mod(ModifierOp.Add, 5f, 10) };
            FloatAssert.Near(105f, mods.ApplyModifiers(100f));
        }

        [Fact]
        public void CalculateWithDetails_extension_returns_full_result()
        {
            ReadOnlySpan<ModifierData> mods = new[] { Mod(ModifierOp.Add, 5f, 10) };
            var result = mods.CalculateWithDetails(100f);
            FloatAssert.Near(105f, result.FinalValue);
            Assert.Equal(1, result.Count);
        }

        /// <summary>测试用自定义策略：所有修改器幅度一律相加，忽略操作类型。</summary>
        private sealed class AddOnlyStrategy : IComposerStrategy
        {
            public string Name => "AddOnly";

            public ModifierResult Compose(ReadOnlySpan<ModifierData> modifiers, float baseValue, float level, IModifierContext context)
            {
                float sum = 0f;
                for (int i = 0; i < modifiers.Length; i++)
                    sum += modifiers[i].GetMagnitude(level, context);

                return new ModifierResult { BaseValue = baseValue, AddSum = sum, PercentProduct = 1f, MulProduct = 1f, Count = modifiers.Length };
            }
        }
    }
}

using System;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// MagnitudeSource 来源解析测试：Fixed / Scalable(等级曲线) / Attribute / TimeDecay / ContextFloat / Pipeline。
    /// </summary>
    public sealed class MagnitudeSourceTests
    {
        [Fact]
        public void Fixed_returns_data0_and_is_not_time_varying()
        {
            var source = MagnitudeSource.Fixed(42.5f);
            Assert.Equal(MagnitudeSourceType.Fixed, source.Type);
            Assert.Equal(42.5f, source.Calculate());
            Assert.Equal(42.5f, source.BaseValue);
            Assert.False(source.IsTimeVarying);
        }

        [Fact]
        public void Fixed_zero_is_reported_as_empty()
        {
            Assert.True(MagnitudeSource.Fixed(0f).IsEmpty);
            Assert.False(MagnitudeSource.Fixed(1f).IsEmpty);
            Assert.True(default(MagnitudeSource).IsEmpty); // default = Fixed(0)
        }

        [Fact]
        public void WithBaseValue_and_WithCoefficient_copy_other_fields()
        {
            var source = MagnitudeSource.TimeDecay(10f, 5f, DecayType.Linear);
            var withBase = source.WithBaseValue(99f);
            var withCoef = source.WithCoefficient(7f);

            Assert.Equal(99f, withBase.Data0);
            Assert.Equal(5f, withBase.Data1);
            Assert.Equal(MagnitudeSourceType.TimeDecay, withBase.Type);

            Assert.Equal(10f, withCoef.Data0);
            Assert.Equal(7f, withCoef.Data1);
            Assert.Equal(MagnitudeSourceType.TimeDecay, withCoef.Type);
        }

        [Fact]
        public void LevelCurve_without_curve_returns_base_times_coefficient()
        {
            var source = MagnitudeSource.LevelCurve(10f, curve: null, coefficient: 2f);
            FloatAssert.Near(20f, source.Calculate(level: 3f));
        }

        [Fact]
        public void LevelCurve_interpolates_between_pairs()
        {
            // 曲线 [1,1, 5,5]：等级 3 → 插值 (3-1)/(5-1)=0.5 → 3；10×1×3 = 30。
            var source = MagnitudeSource.LevelCurve(10f, new float[] { 1f, 1f, 5f, 5f }, 1f);
            FloatAssert.Near(30f, source.Calculate(level: 3f));
            FloatAssert.Near(10f, source.Calculate(level: 1f));
            FloatAssert.Near(50f, source.Calculate(level: 5f));
        }

        [Fact]
        public void LevelCurve_above_last_level_clamps_to_last_value()
        {
            var source = MagnitudeSource.LevelCurve(10f, new float[] { 1f, 1f, 5f, 5f }, 1f);
            FloatAssert.Near(50f, source.Calculate(level: 100f));
        }

        [Fact]
        public void LevelCurve_below_first_level_extrapolates_documents_current_behavior()
        {
            // 钉当前行为：低于首段等级会线性外推（t 为负），可得到低于首值的数值，
            // 这里 [1,1, 5,5] 在等级 0 外推出 0，而不是夹取到首值 1。
            var source = MagnitudeSource.LevelCurve(10f, new float[] { 1f, 1f, 5f, 5f }, 1f);
            FloatAssert.Near(0f, source.Calculate(level: 0f));
        }

        [Fact]
        public void LevelCurve_single_pair_returns_pair_value()
        {
            var source = MagnitudeSource.LevelCurve(10f, new float[] { 10f, 5f }, 1f);
            FloatAssert.Near(50f, source.Calculate(level: 2f));
        }

        [Fact]
        public void Attribute_without_context_returns_zero()
        {
            var source = MagnitudeSource.Attribute(TestKeys.Strength, 0.5f);
            Assert.Equal(0f, source.Calculate());
        }

        [Fact]
        public void Attribute_multiplies_context_value_by_coefficient()
        {
            var ctx = new TestModifierContext();
            ctx.Attributes[TestKeys.Strength] = 80f;

            var source = MagnitudeSource.Attribute(TestKeys.Strength, 0.5f);
            FloatAssert.Near(40f, source.Calculate(1f, ctx));
        }

        [Fact]
        public void AttributeKey_roundtrips_through_float_storage_for_common_keys()
        {
            // Data1 是 float，Packed 值需 < 2^24 才能无损往返；常规键（高字节为保留位 0）满足。
            var key = ModifierKey.Create(200, 255, 255); // Packed = 0x00C8FFFF
            var source = MagnitudeSource.Attribute(key, 1f);
            Assert.Equal(key, source.AttributeKey);
        }

        [Fact]
        public void TimeDecay_without_context_returns_initial_value()
        {
            var source = MagnitudeSource.TimeDecay(50f, 5f, DecayType.Linear);
            Assert.Equal(50f, source.Calculate());
        }

        [Fact]
        public void TimeDecay_zero_elapsed_returns_initial_value()
        {
            var source = MagnitudeSource.TimeDecay(50f, 5f, DecayType.Linear);
            FloatAssert.Near(50f, source.Calculate(1f, new TestModifierContext { ElapsedTime = 0f }));
        }

        [Fact]
        public void TimeDecay_linear_midpoint_halves_value()
        {
            var source = MagnitudeSource.TimeDecay(50f, 4f, DecayType.Linear);
            var ctx = new TestModifierContext { ElapsedTime = 1f }; // t = 0.25 → 系数 0.75
            FloatAssert.Near(37.5f, source.Calculate(1f, ctx));
        }

        [Fact]
        public void TimeDecay_at_or_after_duration_returns_zero()
        {
            var source = MagnitudeSource.TimeDecay(50f, 4f, DecayType.Linear);

            FloatAssert.Near(0f, source.Calculate(1f, new TestModifierContext { ElapsedTime = 4f }));
            FloatAssert.Near(0f, source.Calculate(1f, new TestModifierContext { ElapsedTime = 100f }));
        }

        [Fact]
        public void TimeDecay_exponential_uses_e_minus_2t()
        {
            var source = MagnitudeSource.TimeDecay(100f, 4f, DecayType.Exponential);
            var ctx = new TestModifierContext { ElapsedTime = 1f }; // t = 0.25
            FloatAssert.Near(100f * (float)Math.Exp(-0.5f), source.Calculate(1f, ctx), 1e-3f);
        }

        [Fact]
        public void TimeDecay_nonpositive_duration_returns_initial_value()
        {
            var source = MagnitudeSource.TimeDecay(50f, 0f, DecayType.Linear);
            FloatAssert.Near(50f, source.Calculate(1f, new TestModifierContext { ElapsedTime = 10f }));
        }

        [Fact]
        public void TimeDecay_custom_curve_is_ignored_falls_back_to_linear_documents_current_behavior()
        {
            // 钉当前行为（可疑）：带自定义曲线的工厂把 ArrayData 存进来源，
            // 但 CalculateTimeDecay 走 MagnitudeModifierUtils.CalculateDecay，
            // 其 CustomCurve 分支是固定 1-t（曲线数据从未参与计算）。
            // 同场景下 TimeDecaySource（IValueSource 版本）会按曲线插值 —— 两处实现不一致。
            var curve = new float[] { 1f, 0.5f };
            var source = MagnitudeSource.TimeDecay(100f, 4f, curve);
            Assert.Equal(DecayType.CustomCurve, source.DecayType);

            var ctx = new TestModifierContext { ElapsedTime = 2f }; // t = 0.5
            FloatAssert.Near(50f, source.Calculate(1f, ctx)); // 线性 1-t = 0.5；若按曲线应为 0.75
        }

        [Fact]
        public void ContextFloat_resolves_from_context_slot()
        {
            var source = MagnitudeSource.ContextFloat("mst_ctx_float_a", coefficient: 2f, baseValue: 10f);
            var ctx = new TestModifierContext();
            ctx.Floats["mst_ctx_float_a"] = 5f;

            // (Data0 + ctx 值) × 系数 = (10 + 5) × 2
            FloatAssert.Near(30f, source.Calculate(1f, ctx));
        }

        [Fact]
        public void ContextFloat_without_context_returns_zero()
        {
            var source = MagnitudeSource.ContextFloat("mst_ctx_float_b", 2f, 10f);
            Assert.Equal(0f, source.Calculate());
        }

        [Fact]
        public void ContextFloat_unregistered_key_returns_base_value()
        {
            // 索引 0 查不到 key（空串）→ 直接返回 Data0。
            var source = new MagnitudeSource { Type = MagnitudeSourceType.ContextFloat, Data0 = 10f, Data1 = 2f, Data2 = 0f };
            var ctx = new TestModifierContext();
            ctx.Floats["anything"] = 999f;
            FloatAssert.Near(10f, source.Calculate(1f, ctx));
        }

        [Fact]
        public void Pipeline_calculates_through_all_stages()
        {
            var pipeline = ModifierPipeline.Scale(2f).ThenScale(3f);
            var source = MagnitudeSource.Pipeline(pipeline);

            // Data0 = 管道各阶段 GetBaseValue 之和 = 2 + 3 = 5，再依次 ×2 ×3。
            FloatAssert.Near(30f, source.Calculate(1f, null));
        }

        [Fact]
        public void Pipeline_serialization_caps_at_four_stages_documents_current_behavior()
        {
            // 钉当前行为：MagnitudePipelineData 只存 4 个修饰器，第 5 级被静默丢弃。
            var pipeline = ModifierPipeline.Scale(2f).ThenScale(2f).ThenScale(2f).ThenScale(2f).ThenScale(2f);
            Assert.Equal(5, pipeline.Count);

            var source = MagnitudeSource.Pipeline(pipeline);
            // Data0 = 5×2 = 10；实际只乘 4 个 ×2 → 160（若 5 级全效应为 320）。
            FloatAssert.Near(160f, source.Calculate(1f, null));
        }

        [Fact]
        public void IsTimeVarying_only_fixed_is_stable()
        {
            Assert.False(MagnitudeSource.Fixed(1f).IsTimeVarying);
            Assert.True(MagnitudeSource.LevelCurve(1f).IsTimeVarying);
            Assert.True(MagnitudeSource.Attribute(TestKeys.Strength).IsTimeVarying);
            Assert.True(MagnitudeSource.TimeDecay(1f, 1f).IsTimeVarying);
            Assert.True(MagnitudeSource.Pipeline(ModifierPipeline.Scale(2f)).IsTimeVarying);
            Assert.True(MagnitudeSource.ContextFloat("mst_ctx_float_c").IsTimeVarying);
        }

        [Fact]
        public void Calculate_unknown_type_falls_back_to_data0()
        {
            var source = new MagnitudeSource { Type = (MagnitudeSourceType)99, Data0 = 7f };
            FloatAssert.Near(7f, source.Calculate());
        }
    }
}

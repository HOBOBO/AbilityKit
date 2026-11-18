using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.Core.Tests;

/*
 * 以固定的原始计数、精确可表示的输入和明确异常验证浮点边界契约。
 * 不把桥接委托给定点内核这一实现细节本身作为算法正确性的证明。
 */
public sealed class DeterministicMathBridgeContractTests
{
    private const float Quantum = 1f / 4294967296f;

    [Theory]
    [InlineData(0f, 0L)]
    [InlineData(1f, 4294967296L)]
    [InlineData(-1f, -4294967296L)]
    [InlineData(Quantum / 2f, 0L)]
    [InlineData(-Quantum / 2f, 0L)]
    [InlineData(Quantum * 1.5f, 1L)]
    [InlineData(-Quantum * 1.5f, -1L)]
    [InlineData(-2147483648f, long.MinValue)]
    [InlineData(2147483520f, 9223371487098961920L)]
    public void Scalar_conversion_truncates_toward_zero_at_fixed_resolution(float value, long raw)
    {
        Assert.Equal(raw, DeterministicMathBridge.ToFixed(value).RawValue);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Scalar_guard_and_strict_geometry_entries_have_different_nonfinite_contracts(float value)
    {
        Assert.Equal(0L, DeterministicMathBridge.ToFixed(value).RawValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Sqrt(value));

        foreach (var vector in new[]
        {
            new Vec3(value, 0f, 0f),
            new Vec3(0f, value, 0f),
            new Vec3(0f, 0f, value)
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.ToFixed(in vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Normalize(in vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Magnitude(in vector));
        }

        foreach (var rotation in new[]
        {
            new Quat(value, 0f, 0f, 1f),
            new Quat(0f, value, 0f, 1f),
            new Quat(0f, 0f, value, 1f),
            new Quat(0f, 0f, 0f, value)
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Normalize(in rotation));
        }
    }

    [Theory]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(2147483648f)]
    public void Finite_conversion_overflow_is_not_clamped_or_replaced_with_zero(float value)
    {
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.ToFixed(value));
        var vector = new Vec3(value, 0f, 0f);
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.ToFixed(in vector));
    }

    [Fact]
    public void Epsilon_is_quantized_but_is_not_the_geometry_zero_length_threshold()
    {
        Assert.Equal(4294L, DeterministicMathBridge.Epsilon.RawValue);
        var vector = new Vec3(1f / 32768f, 0f, 0f);
        var rotation = new Quat(1f / 32768f, 0f, 0f, 0f);

        Assert.Equal(Vec3.Right, DeterministicMathBridge.Normalize(in vector));
        Assert.Equal(new Quat(1f, 0f, 0f, 0f), DeterministicMathBridge.Normalize(in rotation));
    }

    [Fact]
    public void Returning_to_float_can_lose_raw_bits_and_round_outside_the_reconversion_range()
    {
        var original = Fixed64.FromRaw(Fixed64.OneRaw + 1);
        Assert.Equal(1f, DeterministicMathBridge.ToSingle(original));
        Assert.Equal(Fixed64.OneRaw,
            DeterministicMathBridge.ToFixed(DeterministicMathBridge.ToSingle(original)).RawValue);

        Assert.Equal(2147483648f, DeterministicMathBridge.ToSingle(Fixed64.MaxValue));
        Assert.Throws<OverflowException>(() =>
            DeterministicMathBridge.ToFixed(DeterministicMathBridge.ToSingle(Fixed64.MaxValue)));
    }

    [Fact]
    public void Vector_conversion_preserves_axis_order_sign_and_fixed_quantization()
    {
        var vector = new Vec3(Quantum * 1.5f, -2f, 0.5f);
        var converted = DeterministicMathBridge.ToFixed(in vector);

        Assert.Equal(1L, converted.X.RawValue);
        Assert.Equal(-8589934592L, converted.Y.RawValue);
        Assert.Equal(2147483648L, converted.Z.RawValue);
        Assert.Equal(new Vec3(Quantum, -2f, 0.5f), DeterministicMathBridge.ToVec3(in converted));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(Quantum)]
    public void Zero_length_including_quantized_square_underflow_uses_distinct_fallbacks(float component)
    {
        var vector = new Vec3(component, 0f, 0f);
        var rotation = new Quat(component, 0f, 0f, 0f);

        Assert.Equal(0f, DeterministicMathBridge.Magnitude(in vector));
        Assert.Equal(Vec3.Zero, DeterministicMathBridge.Normalize(in vector));
        Assert.Equal(Quat.Identity, DeterministicMathBridge.Normalize(in rotation));
    }

    [Fact]
    public void Ordinary_geometry_retains_fixed_length_and_normalization_results()
    {
        var vector = new Vec3(3f, 4f, 0f);
        var rotation = new Quat(0f, 0f, 0f, 2f);

        Assert.Equal(5f, DeterministicMathBridge.Magnitude(in vector));
        Assert.Equal(new Vec3(0.6f, 0.8f, 0f), DeterministicMathBridge.Normalize(in vector));
        Assert.Equal(Quat.Identity, DeterministicMathBridge.Normalize(in rotation));
    }

    [Theory]
    [InlineData(50000f, 0f)]
    [InlineData(40000f, 40000f)]
    public void Representable_components_can_overflow_in_products_or_the_sum(float x, float y)
    {
        var vector = new Vec3(x, y, 0f);
        var rotation = new Quat(x, y, 0f, 0f);

        Assert.Equal(x, DeterministicMathBridge.ToSingle(DeterministicMathBridge.ToFixed(x)));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Magnitude(in vector));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Normalize(in vector));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Normalize(in rotation));
    }

    [Fact]
    public void Square_root_checks_the_quantized_value_instead_of_the_original_sign()
    {
        Assert.Equal(0f, DeterministicMathBridge.Sqrt(-Quantum / 2f));
        Assert.Equal(0f, DeterministicMathBridge.Sqrt(Quantum / 2f));
        Assert.Equal(1f / 65536f, DeterministicMathBridge.Sqrt(Quantum));
        Assert.Equal(3f, DeterministicMathBridge.Sqrt(9f));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Sqrt(-Quantum));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Guarded_functions_receive_zero_but_still_apply_their_own_domains(float value)
    {
        Assert.Equal(DeterministicMathBridge.Sin(0f), DeterministicMathBridge.Sin(value));
        Assert.Equal(DeterministicMathBridge.Cos(0f), DeterministicMathBridge.Cos(value));
        Assert.Equal(DeterministicMathBridge.Tan(0f), DeterministicMathBridge.Tan(value));
        Assert.Equal(1f, DeterministicMathBridge.Exp(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Ln(value));
        Assert.Equal(0f, DeterministicMathBridge.Pow(value, 2f));
        Assert.Equal(1f, DeterministicMathBridge.Pow(2f, value));
        Assert.Equal(1f, DeterministicMathBridge.Pow(value, value));
        Assert.Equal(MathF.PI, DeterministicMathBridge.Atan2(value, -1f));
        Assert.Equal(MathF.PI / 2f, DeterministicMathBridge.Atan2(1f, value));
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(1f, 0f, MathF.PI / 2f)]
    [InlineData(-1f, 0f, -MathF.PI / 2f)]
    [InlineData(-0f, -1f, MathF.PI)]
    public void Atan2_uses_y_then_x_and_does_not_preserve_negative_zero(float y, float x, float expected)
    {
        Assert.Equal(expected, DeterministicMathBridge.Atan2(y, x));
    }

    [Fact]
    public void Trigonometric_inputs_are_radians_and_poles_are_not_promised_to_saturate()
    {
        Assert.InRange(DeterministicMathBridge.Sin(MathF.PI / 2f), 0.999999f, 1.000001f);
        Assert.InRange(DeterministicMathBridge.Cos(MathF.PI), -1.000001f, -0.999999f);
        Assert.InRange(DeterministicMathBridge.Tan(MathF.PI / 4f), 0.999999f, 1.000001f);

        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Cos(float.MaxValue));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Tan(float.MaxValue));
    }

    [Fact]
    public void Natural_exponential_underflow_and_overflow_do_not_follow_float_math_range()
    {
        Assert.Equal(1f, DeterministicMathBridge.Exp(0f));
        Assert.Equal(0f, DeterministicMathBridge.Exp(-24f));
        Assert.InRange(DeterministicMathBridge.Exp(1f), 2.71828f, 2.71829f);
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Exp(22f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(Quantum / 2f)]
    public void Natural_logarithm_requires_a_positive_quantized_value(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Ln(value));
    }

    [Fact]
    public void Natural_logarithm_preserves_unit_and_quantum_boundaries()
    {
        Assert.Equal(0f, DeterministicMathBridge.Ln(1f));
        Assert.InRange(DeterministicMathBridge.Ln(Quantum), -22.18072f, -22.18070f);
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Ln(float.MaxValue));
    }

    [Theory]
    [InlineData(2f, 3f, 8f)]
    [InlineData(-2f, 3f, -8f)]
    [InlineData(-2f, -3f, -0.125f)]
    [InlineData(0f, 0f, 1f)]
    [InlineData(0f, 2f, 0f)]
    [InlineData(2f, -1f, 0.5f)]
    public void Integer_power_and_zero_base_keep_their_published_rules(float value, float exponent, float expected)
    {
        Assert.Equal(expected, DeterministicMathBridge.Pow(value, exponent));
    }

    [Fact]
    public void Power_domain_and_intermediate_overflow_are_checked_after_conversion()
    {
        Assert.InRange(DeterministicMathBridge.Pow(4f, 0.5f), 1.999999f, 2.000001f);
        Assert.Equal(1f, DeterministicMathBridge.Pow(-2f, Quantum / 2f));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMathBridge.Pow(-2f, 0.5f));
        Assert.Throws<DivideByZeroException>(() => DeterministicMathBridge.Pow(0f, -1f));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Pow(2f, 63f));
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Pow(2f, -63f));

        /*
         * 最小负定点值不能取正值；当前内核在处理该底数的零指数前也会触发溢出。
         * 固定这一兼容边界，避免把数学恒等式误写为所有可转换输入都能成功的承诺。
         */
        Assert.Throws<OverflowException>(() => DeterministicMathBridge.Pow(-2147483648f, 0f));
    }
}

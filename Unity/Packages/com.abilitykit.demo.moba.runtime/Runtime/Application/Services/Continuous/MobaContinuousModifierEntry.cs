using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaContinuousModifierEntry
    {
        public MobaContinuousModifierEntry(IContinuous continuous, IMobaContinuousProjectionConfig projection, IMobaContinuousModifierSpec spec, int stack)
        {
            Continuous = continuous;
            Projection = projection;
            Spec = spec;
            Stack = stack < 1 ? 1 : stack;
        }

        public IContinuous Continuous { get; }
        public IMobaContinuousProjectionConfig Projection { get; }
        public IMobaContinuousModifierSpec Spec { get; }
        public int Stack { get; }
    }

    public static class MobaContinuousModifierMath
    {
        public static float EvaluateNumeric(float baseValue, IReadOnlyList<MobaContinuousModifierEntry> entries, IModifierContext context = null)
        {
            if (entries == null || entries.Count == 0) return baseValue;

            var modifiers = new List<ModifierData>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var spec = entry.Spec;
                if (spec == null) continue;

                var key = ModifierKey.Create((byte)spec.TargetKind, ToByte(spec.TargetId));
                modifiers.Add(new ModifierData
                {
                    Key = key,
                    Op = ToModifierOp(spec.Op),
                    Magnitude = ApplyStack(spec.Magnitude, entry.Stack, spec.Op),
                    Priority = spec.Priority,
                    SourceId = entry.Projection?.ModifierSourceId ?? 0,
                    SourceNameIndex = -1,
                    Metadata = ModifierMetadata.CreateByIndex(-1, 0, entry.Projection?.ModifierSourceId ?? 0)
                });
            }

            if (modifiers.Count == 0) return baseValue;

            var calculator = new ModifierCalculator { EnableCache = false };
            return calculator.Calculate(modifiers.ToArray(), baseValue, context).FinalValue;
        }

        public static ModifierOp ToModifierOp(int op)
        {
            if (op == (int)ModifierOp.Mul) return ModifierOp.Mul;
            if (op == (int)ModifierOp.Override) return ModifierOp.Override;
            if (op == (int)ModifierOp.PercentAdd) return ModifierOp.PercentAdd;
            return ModifierOp.Add;
        }

        public static float ApplyStackValue(float value, int stack, int op)
        {
            if (stack <= 1 || op == (int)ModifierOp.Override) return value;
            var result = op == (int)ModifierOp.Mul
                ? IntegerPow(value, stack)
                : (double)value * stack;
            if (double.IsNaN(result) || double.IsInfinity(result) ||
                result < float.MinValue || result > float.MaxValue)
                throw new InvalidOperationException("Continuous modifier stack magnitude is not finite.");
            return (float)result;
        }

        /// <summary>
        /// 整数幂用连乘实现：IEEE 位一致、跨运行时确定。Math.Pow 的实现依运行时而异，
        /// 整数指数也可能在末位漂移，帧同步下禁止用于叠层（见 MobaModifierFormalDesign §4）。
        /// </summary>
        internal static double IntegerPow(double value, int exponent)
        {
            var result = 1.0;
            for (var i = 0; i < exponent; i++) result *= value;
            return result;
        }

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack, int op)
        {
            if (stack <= 1 || op == (int)ModifierOp.Override) return magnitude;
            return magnitude.WithBaseValue(ApplyStackValue(magnitude.BaseValue, stack, op));
        }

        private static byte ToByte(int value)
        {
            if (value <= 0) return 0;
            if (value >= byte.MaxValue) return byte.MaxValue;
            return (byte)value;
        }
    }
}

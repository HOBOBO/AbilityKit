using System;
using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaContinuousModifierEntry
    {
        public MobaContinuousModifierEntry(IContinuous continuous, IMobaContinuousProjectionConfig projection,
            IMobaContinuousModifierSpec spec, int stack, int ordinal = 0)
        {
            Continuous = continuous;
            Projection = projection;
            Spec = spec;
            Stack = stack < 1 ? 1 : stack;
            Ordinal = ordinal < 0 ? 0 : ordinal;
        }

        public IContinuous Continuous { get; }
        public IMobaContinuousProjectionConfig Projection { get; }
        public IMobaContinuousModifierSpec Spec { get; }
        public int Stack { get; }
        public int Ordinal { get; }
    }

    public static class MobaContinuousModifierMath
    {
        /// <summary>
        /// 无来源追踪求值的复用计算器。
        /// 缓存默认开启：该路径不传 recorder，命中缓存是安全的；词条集合未变时可直接复用结果。
        /// </summary>
        [ThreadStatic]
        private static ModifierCalculator _threadCalculator;

        private static ModifierCalculator GetCalculator()
        {
            return _threadCalculator ??= new ModifierCalculator { EnableCache = true };
        }

        [Obsolete("Diagnostic-only compatibility helper. Formal combat consumers must use AttributeContext or a domain ParameterResolver.")]
        public static float EvaluateNumeric(float baseValue, IReadOnlyList<MobaContinuousModifierEntry> entries, IModifierContext context = null)
        {
            if (entries == null || entries.Count == 0) return baseValue;

            // 先按 TargetKind/TargetId 过滤，避免为不相关词条分配或计算。
            var matchCount = 0;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].Spec != null) matchCount++;
            if (matchCount == 0) return baseValue;

            var rented = ArrayPool<ModifierData>.Shared.Rent(matchCount);
            try
            {
                var written = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var spec = entry.Spec;
                    if (spec == null) continue;

                    var key = ModifierKey.Create((byte)spec.TargetKind, ToByte(spec.TargetId));
                    rented[written++] = new ModifierData
                    {
                        Key = key,
                        Op = ToModifierOp(spec.Op),
                        Magnitude = ApplyStack(spec.Magnitude, entry.Stack, spec.Op),
                        Priority = spec.Priority,
                        SourceId = entry.Projection?.ModifierSourceId ?? 0,
                        SourceNameIndex = -1,
                        Metadata = ModifierMetadata.CreateByIndex(-1, 0, entry.Projection?.ModifierSourceId ?? 0)
                    };
                }

                if (written == 0) return baseValue;

                return GetCalculator().Calculate(rented.AsSpan(0, written), baseValue, context).FinalValue;
            }
            finally
            {
                ArrayPool<ModifierData>.Shared.Return(rented, clearArray: true);
            }
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

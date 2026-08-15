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
                    Magnitude = ApplyStack(spec.Magnitude, entry.Stack),
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

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack)
        {
            if (stack <= 1) return magnitude;
            return magnitude.WithBaseValue(magnitude.BaseValue * stack);
        }

        private static byte ToByte(int value)
        {
            if (value <= 0) return 0;
            if (value >= byte.MaxValue) return byte.MaxValue;
            return (byte)value;
        }
    }
}

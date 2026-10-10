using System;
using System.Runtime.InteropServices;
using System.Text;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 修改器数值解析的跨运行时 golden 位锁用例。.NET 与 Unity(Mono/IL2CPP) 测试共用
    /// 同一份用例与同一份期望值执行：float 用例锁 float 位（十六进制），int 用例锁整数结果。
    /// 任何一侧的组合算术、舍入或 float/fixed 换算语义漂移都会被位级检出。
    /// 期望值来自 .NET 当前实现的单次锁定（见 Expected 数组）；修改用例或内核语义时
    /// 必须在 .NET 与 Unity 两侧同步重锁并确认差异来源，不得只改一侧期望值。
    /// </summary>
    public static class MobaModifierNumericGoldenCases
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public int Bits;
        }

        public static int BitsOf(float value)
        {
            var union = default(FloatBits);
            union.Value = value;
            return union.Bits;
        }

        public enum GoldenMode
        {
            Float,
            Int,
            SimFloat,
        }

        public readonly struct GoldenCase
        {
            public GoldenCase(string name, GoldenMode mode, ModifierKey key, float baseValue,
                ModifierData[] modifiers, MobaParameterSelector[] selectors,
                MobaParameterRecord record, bool fixedPolicy, IModifierContext context)
            {
                Name = name;
                Mode = mode;
                Key = key;
                BaseValue = baseValue;
                Modifiers = modifiers ?? Array.Empty<ModifierData>();
                Selectors = selectors ?? Array.Empty<MobaParameterSelector>();
                Record = record;
                FixedPolicy = fixedPolicy;
                Context = context;
            }

            public string Name { get; }
            public GoldenMode Mode { get; }
            public ModifierKey Key { get; }
            public float BaseValue { get; }
            public ModifierData[] Modifiers { get; }
            public MobaParameterSelector[] Selectors { get; }
            public MobaParameterRecord Record { get; }
            public bool FixedPolicy { get; }
            public IModifierContext Context { get; }
        }

        private sealed class GoldenContext : IModifierContext
        {
            private readonly float _elapsedSeconds;

            public GoldenContext(float elapsedSeconds)
            {
                _elapsedSeconds = elapsedSeconds;
            }

            public float Level => 1f;
            public float CurrentTime => 0f;
            public float DeltaTime => 0f;
            public float ElapsedTime => _elapsedSeconds;
            public int CurrentFrame => 0;
            public int ElapsedFrames => 0;
            public int CurrentTimeMs => 0;
            public int DeltaTimeMs => 0;
            public int ElapsedTimeMs => 0;
            public ModifierMetadata Metadata => default;

            public float GetAttribute(ModifierKey key) => 0f;
            public T GetData<T>(string key) where T : class => null;
            public bool TryGetData<T>(string key, out T value) where T : class
            {
                value = null;
                return false;
            }

            public float GetFloat(string key) => 0f;
            public bool TryGetFloat(string key, out float value)
            {
                value = 0f;
                return false;
            }

            public int GetInt(string key) => 0;
            public bool TryGetInt(string key, out int value)
            {
                value = 0;
                return false;
            }

            public float[] GetCurveData(int index) => null;
        }

        private static ModifierData Fixed(ModifierKey key, ModifierOp op, float magnitude,
            int priority = 0, int sourceId = 1)
        {
            return new ModifierData
            {
                Key = key,
                Op = op,
                Magnitude = MagnitudeSource.Fixed(magnitude),
                Priority = priority,
                SourceId = sourceId,
            };
        }

        public static GoldenCase[] BuildCases()
        {
            var castRange = MobaSkillParamModifierKeys.Skill.CastRange;
            var cooldownMs = MobaSkillParamModifierKeys.Skill.CooldownMs;
            var speed = MobaSkillParamModifierKeys.Projectile.Speed;
            var castRecord = new MobaParameterRecord(MobaParameterStage.CastPrepare);
            var spawnRecord = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
            var launcherSpawnRecord = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
                launcherTemplateId: 20);
            var launcherMissRecord = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
                launcherTemplateId: 30);

            var timeDecay = default(MagnitudeSource);
            timeDecay.Type = MagnitudeSourceType.TimeDecay;
            timeDecay.Data0 = 8f;
            timeDecay.Data1 = 2f;
            timeDecay.Data2 = (int)DecayType.CustomCurve;
            timeDecay.ArrayData = new[] { 1f, 0.25f, 0f };

            var exponentialDecay = default(MagnitudeSource);
            exponentialDecay.Type = MagnitudeSourceType.TimeDecay;
            exponentialDecay.Data0 = 8f;
            exponentialDecay.Data1 = 2f;
            exponentialDecay.Data2 = (int)DecayType.Exponential;

            return new[]
            {
                new GoldenCase("empty.castrange.float", GoldenMode.Float, castRange, 4.5f,
                    new ModifierData[0], new MobaParameterSelector[0], castRecord, false, null),
                new GoldenCase("add.pair.int", GoldenMode.Int, cooldownMs, 1000f,
                    new[] { Fixed(cooldownMs, ModifierOp.Add, 150f, sourceId: 102), Fixed(cooldownMs, ModifierOp.Add, 50f, sourceId: 101) },
                    new MobaParameterSelector[2], castRecord, false, null),
                new GoldenCase("percent.compound.float", GoldenMode.Float, castRange, 2f,
                    new[]
                    {
                        Fixed(castRange, ModifierOp.PercentAdd, 0.1f, sourceId: 3),
                        Fixed(castRange, ModifierOp.PercentAdd, 0.15f, sourceId: 2),
                        Fixed(castRange, ModifierOp.PercentAdd, 0.2f, sourceId: 1),
                    },
                    new MobaParameterSelector[3], castRecord, false, null),
                new GoldenCase("mixed.layers.float", GoldenMode.Float, castRange, 10f,
                    new[]
                    {
                        Fixed(castRange, ModifierOp.Add, 2f, sourceId: 1),
                        Fixed(castRange, ModifierOp.PercentAdd, 0.5f, sourceId: 2),
                        Fixed(castRange, ModifierOp.Mul, 2f, sourceId: 3),
                    },
                    new MobaParameterSelector[3], castRecord, false, null),
                new GoldenCase("override.priority.float", GoldenMode.Float, speed, 5f,
                    new[]
                    {
                        Fixed(speed, ModifierOp.Override, 7f, priority: 5, sourceId: 1),
                        Fixed(speed, ModifierOp.Override, 9f, priority: 10, sourceId: 2),
                    },
                    new MobaParameterSelector[2], spawnRecord, false, null),
                new GoldenCase("override.tie.float", GoldenMode.Float, speed, 5f,
                    new[]
                    {
                        Fixed(speed, ModifierOp.Override, 8f, priority: 10, sourceId: 30),
                        Fixed(speed, ModifierOp.Override, 6f, priority: 10, sourceId: 20),
                    },
                    new MobaParameterSelector[2], spawnRecord, false, null),
                new GoldenCase("override.terminal.float", GoldenMode.Float, castRange, 10f,
                    new[]
                    {
                        Fixed(castRange, ModifierOp.Add, 5f, priority: 100, sourceId: 1),
                        Fixed(castRange, ModifierOp.Override, 3f, priority: 1, sourceId: 2),
                    },
                    new MobaParameterSelector[2], castRecord, false, null),
                new GoldenCase("selector.hit.float", GoldenMode.Float, speed, 5f,
                    new[] { Fixed(speed, ModifierOp.Override, 8f, priority: 10, sourceId: 5) },
                    new[] { new MobaParameterSelector(launcherTemplateId: 20) },
                    launcherSpawnRecord, false, null),
                new GoldenCase("selector.miss.float", GoldenMode.Float, speed, 5f,
                    new[] { Fixed(speed, ModifierOp.Override, 8f, priority: 10, sourceId: 5) },
                    new[] { new MobaParameterSelector(launcherTemplateId: 20) },
                    launcherMissRecord, false, null),
                new GoldenCase("round.banker.down.int", GoldenMode.Int, cooldownMs, 2f,
                    new[] { Fixed(cooldownMs, ModifierOp.Add, 0.5f, sourceId: 1) },
                    new MobaParameterSelector[1], castRecord, false, null),
                new GoldenCase("round.banker.up.int", GoldenMode.Int, cooldownMs, 3f,
                    new[] { Fixed(cooldownMs, ModifierOp.Add, 0.5f, sourceId: 1) },
                    new MobaParameterSelector[1], castRecord, false, null),
                new GoldenCase("timedecay.curve.float", GoldenMode.Float, castRange, 1.5f,
                    new[]
                    {
                        new ModifierData
                        {
                            Key = castRange, Op = ModifierOp.Add, Magnitude = timeDecay,
                            Priority = 0, SourceId = 1,
                        },
                    },
                    new MobaParameterSelector[1], castRecord, false, new GoldenContext(0.75f)),
                new GoldenCase("sim.passthrough.float", GoldenMode.SimFloat, speed, 6f,
                    new[] { Fixed(speed, ModifierOp.Add, 1.25f, sourceId: 1) },
                    new MobaParameterSelector[1], spawnRecord, false, null),
                new GoldenCase("sim.fixed.roundtrip.float", GoldenMode.SimFloat, speed, 6f,
                    new[] { Fixed(speed, ModifierOp.Add, 1.25f, sourceId: 1) },
                    new MobaParameterSelector[1], spawnRecord, true, null),
                new GoldenCase("timedecay.exponential.float", GoldenMode.Float, castRange, 1.5f,
                    new[]
                    {
                        new ModifierData
                        {
                            Key = castRange, Op = ModifierOp.Add, Magnitude = exponentialDecay,
                            Priority = 0, SourceId = 1,
                        },
                    },
                    new MobaParameterSelector[1], castRecord, false, new GoldenContext(0.75f)),
                new GoldenCase("sim.fixed.tiny.float", GoldenMode.SimFloat, speed, 1.5e-10f,
                    new ModifierData[0], new MobaParameterSelector[0], spawnRecord, true, null),
            };
        }

        /// <summary>期望值与 <see cref="BuildCases"/> 同序：Float/SimFloat 用例为 float 位（十六进制转储），Int 用例为整数结果。锁定自 .NET 10 当前实现（2026-10-08）。</summary>
        public static readonly int[] Expected =
        {
            // empty.castrange.float = 4.5f
            0x40900000,
            // add.pair.int = 1000 + 150 + 50
            1200,
            // percent.compound.float = 2f * 1.1 * 1.15 * 1.2（PercentAdd 现行连乘语义）
            0x40424DD3,
            // mixed.layers.float = (10 + 2) * (1 + 0.5) * 2 = 36f
            0x42100000,
            // override.priority.float = 9f（Priority 10 胜出）
            0x41100000,
            // override.tie.float = 6f（同优先级 SourceId 较小者胜出）
            0x40C00000,
            // override.terminal.float = 3f（Override 终止，Add 不生效）
            0x40400000,
            // selector.hit.float = 8f
            0x41000000,
            // selector.miss.float = 5f（未命中回落基础值）
            0x40A00000,
            // round.banker.down.int = 2（Math.Round 银行家舍入 2.5 -> 2）
            2,
            // round.banker.up.int = 4（3.5 -> 4）
            4,
            // timedecay.curve.float = 1.5f + 8f * lerp([1,0.25,0], 0.375) = 5f
            0x40A00000,
            // sim.passthrough.float = 7.25f
            0x40E80000,
            // sim.fixed.roundtrip.float = 7.25f（Q32.32 往返位等价）
            0x40E80000,
            // timedecay.exponential.float = 1.5 + 8 * DeterministicMathBridge.Exp(-0.75) ~ 5.279
            0x40A8ED04,
            // sim.fixed.tiny.float = 0f（小于 2^-32 截断为零）
            0,
        };

        public static int Execute(in GoldenCase testCase)
        {
            using var service = new MobaSkillParamModifierService();
            service.UsePrecisionPolicyForTesting(testCase.FixedPolicy
                ? new Fixed64ModifierPrecisionPolicy()
                : (IMobaModifierPrecisionPolicy)PassThroughModifierPrecisionPolicy.Instance);
            var owner = MobaModifierOwnerRef.Actor(1);
            for (var i = 0; i < testCase.Modifiers.Length; i++)
                service.AddModifier(owner, in testCase.Modifiers[i], in testCase.Selectors[i]);
            var key = testCase.Key;
            var record = testCase.Record;
            var context = testCase.Context;
            switch (testCase.Mode)
            {
                case GoldenMode.Int:
                    return service.ResolveInt(owner, key, (int)testCase.BaseValue, in record, context);
                case GoldenMode.SimFloat:
                    return BitsOf(service.ResolveSimFloat(owner, key, testCase.BaseValue, in record, context));
                default:
                    return BitsOf(service.ResolveFloat(owner, key, testCase.BaseValue, in record, context));
            }
        }

        public static string Describe(int value, GoldenMode mode)
        {
            return mode == GoldenMode.Int
                ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "0x" + value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string RenderActuals()
        {
            var cases = BuildCases();
            var text = new StringBuilder();
            for (var i = 0; i < cases.Length; i++)
            {
                if (i > 0) text.AppendLine(",");
                text.Append("            ").Append(Describe(Execute(in cases[i]), cases[i].Mode));
            }
            text.AppendLine();
            return text.ToString();
        }
    }
}

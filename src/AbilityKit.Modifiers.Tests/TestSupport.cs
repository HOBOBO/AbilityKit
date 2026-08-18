using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using Xunit;

// 禁用本测试程序集内的用例级并行。
// 包内 ModifierOperatorRegistry / ModifierMetadataRegistry / ModifierContextKeyRegistry
// 是进程级静态注册表（非线程安全），测试需要注册自定义操作符时必须串行执行，避免并发写坏字典。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// 测试用 IModifierContext 实现。
    /// 提供可控的等级、时间与属性/浮点数据槽。
    /// </summary>
    public sealed class TestModifierContext : IModifierContext
    {
        public float Level { get; set; } = 1f;
        public float CurrentTime { get; set; }
        public float DeltaTime { get; set; }
        public float ElapsedTime { get; set; }
        public int CurrentFrame { get; set; }
        public int ElapsedFrames { get; set; }
        public int CurrentTimeMs { get; set; }
        public int DeltaTimeMs { get; set; }
        public int ElapsedTimeMs { get; set; }
        public ModifierMetadata Metadata { get; set; } = ModifierMetadata.Empty;

        public Dictionary<ModifierKey, float> Attributes { get; } = new();
        public Dictionary<string, float> Floats { get; } = new();
        public Dictionary<string, int> Ints { get; } = new();
        public Dictionary<string, object> Data { get; } = new();

        public float GetAttribute(ModifierKey key) => Attributes.TryGetValue(key, out var v) ? v : 0f;

        public T GetData<T>(string key) where T : class => Data.TryGetValue(key, out var v) ? v as T : null;

        public bool TryGetData<T>(string key, out T value) where T : class
        {
            value = GetData<T>(key);
            return value != null;
        }

        public float GetFloat(string key) => Floats.TryGetValue(key, out var v) ? v : 0f;

        public bool TryGetFloat(string key, out float value) => Floats.TryGetValue(key, out value);

        public int GetInt(string key) => Ints.TryGetValue(key, out var v) ? v : 0;

        public bool TryGetInt(string key, out int value) => Ints.TryGetValue(key, out value);
    }

    /// <summary>自定义操作符：Priority=12（未定义分组），IsAdditive=true → 走 AddSum 分支。</summary>
    public sealed class CustomAdditiveOperator : IModifierOperator
    {
        public ModifierOp OpCode => OpCodes.CustomAdd;
        public string Name => "CustomAdd";
        public int Priority => 12;
        public bool IsTerminal => false;
        public bool IsAdditive => true;
        public float Apply(float baseValue, float modifierValue) => baseValue + modifierValue;
        public float CalculateContribution(float baseValue, float modifierValue) => modifierValue;
    }

    /// <summary>自定义操作符：Priority=25（未定义分组），IsAdditive=false → 走 MulProduct 分支。</summary>
    public sealed class CustomMultiplicativeOperator : IModifierOperator
    {
        public ModifierOp OpCode => OpCodes.CustomMul;
        public string Name => "CustomMul";
        public int Priority => 25;
        public bool IsTerminal => false;
        public bool IsAdditive => false;
        public float Apply(float baseValue, float modifierValue) => baseValue * modifierValue;
        public float CalculateContribution(float baseValue, float modifierValue) => baseValue * (modifierValue - 1f);
    }

    /// <summary>本测试程序集专用操作码。值只在此处定义，避免与其他测试类使用的值冲突。</summary>
    public static class OpCodes
    {
        // 101/102：由测试注册的自定义操作符使用（见 CustomAdditiveOperator / CustomMultiplicativeOperator）。
        public const ModifierOp CustomAdd = (ModifierOp)101;
        public const ModifierOp CustomMul = (ModifierOp)102;

        // 200：从不注册，专用于"未注册操作"路径。
        public const ModifierOp NeverRegistered = (ModifierOp)200;
    }

    public static class TestKeys
    {
        public static readonly ModifierKey Attack = ModifierKey.Create(1, 0, 0);
        public static readonly ModifierKey Health = ModifierKey.Create(1, 1, 0);
        public static readonly ModifierKey Speed = ModifierKey.Create(1, 2, 0);
        public static readonly ModifierKey Strength = ModifierKey.Create(1, 9, 0);
    }

    /// <summary>浮点比较辅助：以绝对误差判定（包内全部为 float 计算）。</summary>
    public static class FloatAssert
    {
        public static void Near(float expected, float actual, float tolerance = 1e-4f)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                $"expected {expected} (±{tolerance}) but was {actual}");
        }
    }
}

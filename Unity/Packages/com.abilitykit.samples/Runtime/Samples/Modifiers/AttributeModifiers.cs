#nullable enable

using System.Collections.Generic;
using System.Linq;
using AbilityKit.Modifiers;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// 演示 ModifierData 与 ModifierCalculator 如何把基础属性、装备、Buff 和覆盖效果合成为最终属性。
    /// </summary>
    [Sample(501, "modifiers", "attribute", "calculator", "package-api", "web", "deterministic")]
    public sealed class AttributeModifiers : SampleBase
    {
        public override string Title => "Attribute Modifiers";
        public override string Description => "使用 ModifierData 和 ModifierCalculator 计算角色属性";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            var attributes = new CharacterAttributes();

            Section("基础属性");
            KeyValue("Modifier.BaseKeys", "MaxHealth,AttackPower,Defense");
            PrintAttributes(attributes, "Base");

            Divider();
            Section("装备与 Buff 修改器");
            attributes.Add("weapon", ModifierData.Add(ModifierKey.AttackPower, 35f, sourceId: 1001));
            attributes.Add("ring", ModifierData.PercentAdd(ModifierKey.AttackPower, 0.20f, sourceId: 1002));
            attributes.Add("battle-shout", ModifierData.Mul(ModifierKey.AttackPower, 1.25f, sourceId: 1003));
            attributes.Add("armor", ModifierData.Add(ModifierKey.Defense, 18f, sourceId: 1004));
            KeyValue("Modifier.SourceCount", attributes.ModifierCount.ToString());
            KeyValue("Modifier.AttackSources", attributes.GetModifierCount(ModifierKey.AttackPower).ToString());
            PrintAttributes(attributes, "Equipped");

            Divider();
            Section("Override 的优先级");
            attributes.Add("training-mode", ModifierData.Override(ModifierKey.AttackPower, 80f, sourceId: 2001));
            KeyValue("Modifier.OverrideSource", "training-mode");
            PrintAttributes(attributes, "Override");
            Bullet("Override 使用更高优先级，适合训练场、变身、剧情状态这类强制属性。");

            Divider();
            Section("时间衰减修改器");
            attributes.Remove("training-mode");
            attributes.Add("rage-decay", ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 40f,
                duration: 4f,
                decayType: DecayType.Linear,
                sourceId: 3001));

            for (var second = 0; second <= 4; second++)
            {
                var context = new SampleModifierContext { ElapsedTime = second, CurrentTime = second, DeltaTime = 1f };
                var attack = attributes.GetFinalValue(ModifierKey.AttackPower, context);
                KeyValue($"t={second}s Attack", attack.ToString("F1"));
                KeyValue("Modifier.Decay.Attack", $"t={second}s,value={attack:F1}");
            }

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("ModifierData：描述一次 Add、PercentAdd、Mul、Override 或 TimeDecay 修改。");
            Bullet("ModifierCalculator：按 ModifierOp 和 Priority 把同一属性的修改器合成为最终值。");
            Bullet("ModifierKey：让业务用稳定键区分 MaxHealth、AttackPower、Defense 等属性。");
            Bullet("IModifierContext：为时间衰减、等级曲线和属性捕获提供运行时上下文。");
        }

        private void PrintAttributes(CharacterAttributes attributes, string label)
        {
            var context = new SampleModifierContext();
            KeyValue($"{label}.MaxHealth", attributes.GetFinalValue(ModifierKey.MaxHealth, context).ToString("F1"));
            KeyValue($"{label}.AttackPower", attributes.GetFinalValue(ModifierKey.AttackPower, context).ToString("F1"));
            KeyValue($"{label}.Defense", attributes.GetFinalValue(ModifierKey.Defense, context).ToString("F1"));
        }

        private sealed class CharacterAttributes
        {
            private readonly Dictionary<ModifierKey, float> _baseValues = new Dictionary<ModifierKey, float>
            {
                [ModifierKey.MaxHealth] = 1000f,
                [ModifierKey.AttackPower] = 100f,
                [ModifierKey.Defense] = 50f
            };

            private readonly Dictionary<string, ModifierData> _modifiers = new Dictionary<string, ModifierData>();
            private readonly ModifierCalculator _calculator = new ModifierCalculator();

            public int ModifierCount => _modifiers.Count;

            public int GetModifierCount(ModifierKey key)
            {
                return _modifiers.Values.Count(modifier => modifier.Key == key);
            }

            public void Add(string source, ModifierData modifier)
            {
                _modifiers[source] = modifier;
                _calculator.Invalidate();
            }

            public void Remove(string source)
            {
                if (_modifiers.Remove(source))
                {
                    _calculator.Invalidate();
                }
            }

            public float GetFinalValue(ModifierKey key, IModifierContext context)
            {
                if (!_baseValues.TryGetValue(key, out var baseValue))
                {
                    return 0f;
                }

                var modifiers = _modifiers.Values.Where(modifier => modifier.Key == key).ToArray();
                return _calculator.Calculate(modifiers, baseValue, context).FinalValue;
            }
        }
    }
}

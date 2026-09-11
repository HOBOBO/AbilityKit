#nullable enable

using System;
using AbilityKit.Modifiers;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// TimeDecayModifiers - 时间衰减修改器
    /// 展示如何使用时间衰减修改器实现持续效果
    /// </summary>
    [Sample]
    public sealed class TimeDecayModifiers : SampleBase
    {
        public override string Title => "时间衰减修改器 (Time Decay)";
        public override string Description => "展示带持续时间的修改器效果";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("=== 时间衰减修改器示例 ===");
            Output.Divider();

            Log("【场景】战士获得增益 BUFF: +100 攻击力");
            Log("       持续 5 秒，然后逐渐衰减");
            Output.Divider();

            float baseAttack = 100f;

            Log("【1】线性衰减 (Linear):");
            Log("  每秒减少固定值");

            var linearMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.Linear
            );

            SimulateDecay(linearMod, baseAttack, "Linear");

            Output.Divider();

            Log("【2】指数衰减 (Exponential):");
            Log("  初始快速衰减，然后变慢");

            var exponentialMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.Exponential
            );

            SimulateDecay(exponentialMod, baseAttack, "Exponential");

            Output.Divider();

            Log("【3】对数衰减 (Logarithmic):");
            Log("  初始缓慢衰减，然后加速");

            var logMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.Logarithmic
            );

            SimulateDecay(logMod, baseAttack, "Logarithmic");

            Output.Divider();

            Log("【4】缓出衰减 (EaseOut):");
            Log("  初始快，接近结束时慢");

            var easeOutMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.EaseOut
            );

            SimulateDecay(easeOutMod, baseAttack, "EaseOut");

            Output.Divider();

            Log("【5】缓入衰减 (EaseIn):");
            Log("  初始慢，然后加速");

            var easeInMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.EaseIn
            );

            SimulateDecay(easeInMod, baseAttack, "EaseIn");

            Output.Divider();

            Log("【6】组合衰减 (百分比加成 + 时间衰减):");
            Log("  +20% 攻击 (线性衰减) + +100 攻击 (指数衰减)");

            var percentDecayMod = ModifierData.PercentAddWithTimeDecay(
                ModifierKey.AttackPower,
                initialPercent: 0.2f,
                duration: 5f,
                decayType: DecayType.Exponential
            );

            var addDecayMod = ModifierData.AddWithTimeDecay(
                ModifierKey.AttackPower,
                initialValue: 100f,
                duration: 5f,
                DecayType.Exponential
            );

            Log("  时间进程:");

            var context = new SampleModifierContext();
            for (int i = 0; i <= 5; i++)
            {
                context.ElapsedTime = i;
                float percentBonus = percentDecayMod.GetMagnitude(1f, context);
                float addBonus = addDecayMod.GetMagnitude(1f, context);

                float finalAttack = baseAttack * (1 + percentBonus) + addBonus;
                Log($"    t={i}s: 百分比加成 +{percentBonus * 100:F1}%, 加法加成 +{addBonus:F1}, 最终 ATK = {finalAttack:F1}");
            }

            Output.Divider();

            Log("【总结】时间衰减修改器适用于:");
            Output.Bullet("技能增益 BUFF (加速、攻击提升等)");
            Output.Bullet("DOT (持续伤害)");
            Output.Bullet("HOT (持续治疗)");
            Output.Bullet("临时状态效果");
        }

        private void SimulateDecay(ModifierData modifier, float baseAttack, string decayType)
        {
            Log($"  时间进程 ({decayType}):");

            var context = new SampleModifierContext();
            for (int i = 0; i <= 5; i++)
            {
                context.ElapsedTime = i;
                float bonus = modifier.GetMagnitude(1f, context);
                float finalAttack = baseAttack + bonus;

                int barLength = (int)(bonus / 10);
                string bar = new string('█', Math.Max(0, barLength));
                string empty = new string('░', Math.Max(0, 10 - barLength));
                Log($"    t={i}s: +{bonus:F1} ATK [{bar}{empty}] = {finalAttack:F1}");
            }
        }
    }
}

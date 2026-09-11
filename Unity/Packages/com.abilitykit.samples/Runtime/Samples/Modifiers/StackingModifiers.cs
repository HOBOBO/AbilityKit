#nullable enable

using System;
using AbilityKit.Modifiers;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// StackingModifiers - 堆叠修改器
    /// 展示如何实现可叠加的修改器效果
    /// </summary>
    [Sample]
    public sealed class StackingModifiers : SampleBase
    {
        public override string Title => "堆叠修改器 (Stacking)";
        public override string Description => "展示修改器的叠加机制";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("=== 堆叠修改器示例 ===");
            Output.Divider();

            Log("【场景】MOBA 游戏中的装备效果叠加");
            Log("       多个相同来源的修改器如何叠加");
            Output.Divider();

            Log("【1】独占模式 (Exclusive) vs 聚合模式 (Aggregate):");
            Output.Divider();

            Log("独占模式示例 - 多把无尽之刃:");
            Log("  效果: +100 攻击力");
            Log("  规则: 只能装备一把");
            Log("  结果: 只能获得一个修改器效果");

            float baseAttack = 100f;
            float[] exclusiveResults = SimulateExclusiveStacking(baseAttack, 3, 100f);
            Log($"  装备 1 把: ATK = {exclusiveResults[0]:F0}");
            Log($"  装备 2 把: ATK = {exclusiveResults[1]:F0} (不生效)");
            Log($"  装备 3 把: ATK = {exclusiveResults[2]:F0} (不生效)");

            Output.Divider();

            Log("聚合模式示例 - 多把多兰剑:");
            Log("  效果: +10 攻击力");
            Log("  规则: 可以叠加，最多 3 层");
            Log("  结果: 每层叠加效果");

            float[] aggregateResults = SimulateAggregateStacking(baseAttack, 5, 10f, maxStack: 3);
            Log($"  装备 1 把: ATK = {aggregateResults[0]:F0} (+10, 1/3 层)");
            Log($"  装备 2 把: ATK = {aggregateResults[1]:F0} (+20, 2/3 层)");
            Log($"  装备 3 把: ATK = {aggregateResults[2]:F0} (+30, 3/3 层)");
            Log($"  装备 4 把: ATK = {aggregateResults[3]:F0} (+30, 3/3 层上限)");
            Log($"  装备 5 把: ATK = {aggregateResults[4]:F0} (+30, 3/3 层上限)");

            Output.Divider();

            Log("【2】百分比叠加:");
            Log("  多件 +10% 攻击力的装备叠加");
            Log("  规则: 百分比加法叠加 (加法形式)");

            float[] percentResults = SimulatePercentStacking(baseAttack, 5, 0.1f, maxStack: 3);
            Log($"  装备 1 件: ATK = {percentResults[0]:F0} (+10%)");
            Log($"  装备 2 件: ATK = {percentResults[1]:F0} (+20%)");
            Log($"  装备 3 件: ATK = {percentResults[2]:F0} (+30%)");
            Log($"  装备 4 件: ATK = {percentResults[3]:F0} (+30% 上限)");
            Log($"  装备 5 件: ATK = {percentResults[4]:F0} (+30% 上限)");

            Output.Divider();

            Log("【3】乘法叠加:");
            Log("  多件 ×1.1 攻击力的装备");
            Log("  规则: 乘法叠加");

            float[] mulResults = SimulateMulStacking(baseAttack, 5, 1.1f);
            Log($"  装备 1 件: ATK = {mulResults[0]:F0} (×1.1)");
            Log($"  装备 2 件: ATK = {mulResults[1]:F0} (×1.21)");
            Log($"  装备 3 件: ATK = {mulResults[2]:F0} (×1.331)");
            Log($"  装备 4 件: ATK = {mulResults[3]:F0} (×1.4641)");
            Log($"  装备 5 件: ATK = {mulResults[4]:F0} (×1.61051)");

            Output.Divider();

            Log("【4】技能层数叠加 (DOT 伤害):");
            Log("  持续伤害技能可以叠加层数");
            Log("  每层 +50 伤害/秒, 最多 5 层");

            float baseDamage = 100f;
            int[] stackResults = SimulateDotStacking(baseDamage, 7, 50f, maxStack: 5);

            for (int i = 0; i < stackResults.Length; i++)
            {
                int stacks = Math.Min(i + 1, 5);
                float damagePerSec = stackResults[i];
                Log($"  {i + 1} 层燃烧: 伤害 = {damagePerSec:F0} DPS ({stacks} 层 × 50)");
            }

            Output.Divider();

            Log("【总结】堆叠类型选择指南:");
            Output.Bullet("Exclusive: 唯一效果 (如装备唯一被动)");
            Output.Bullet("Aggregate: 可叠加效果 (如攻击速度)");
            Output.Bullet("注意: 需要配合 MaxStackCount 防止无限叠加");
            Output.Bullet("百分比叠加: 使用 PercentAdd 而不是 Mul");
        }

        private float[] SimulateExclusiveStacking(float baseValue, int count, float bonus)
        {
            var results = new float[count];
            float currentValue = baseValue;

            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    currentValue += bonus;
                }
                results[i] = currentValue;
            }

            return results;
        }

        private float[] SimulateAggregateStacking(float baseValue, int count, float bonus, int maxStack)
        {
            var results = new float[count];
            int stacks = 0;

            for (int i = 0; i < count; i++)
            {
                if (stacks < maxStack)
                {
                    stacks++;
                }
                results[i] = baseValue + (bonus * stacks);
            }

            return results;
        }

        private float[] SimulatePercentStacking(float baseValue, int count, float percent, int maxStack)
        {
            var results = new float[count];
            int stacks = 0;

            for (int i = 0; i < count; i++)
            {
                if (stacks < maxStack)
                {
                    stacks++;
                }
                results[i] = baseValue * (1 + percent * stacks);
            }

            return results;
        }

        private float[] SimulateMulStacking(float baseValue, int count, float multiplier)
        {
            var results = new float[count];

            for (int i = 0; i < count; i++)
            {
                results[i] = baseValue * MathF.Pow(multiplier, i + 1);
            }

            return results;
        }

        private int[] SimulateDotStacking(float baseDamage, int maxStacks, float perStack, int maxStack)
        {
            var results = new int[maxStacks];

            for (int i = 0; i < maxStacks; i++)
            {
                int stacks = Math.Min(i + 1, maxStack);
                results[i] = (int)(baseDamage + perStack * stacks);
            }

            return results;
        }
    }
}

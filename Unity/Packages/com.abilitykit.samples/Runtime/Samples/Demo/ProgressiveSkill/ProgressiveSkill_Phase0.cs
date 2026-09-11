#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase0 - 纯 C# Tick 循环
    ///
    /// 需求: 每秒对目标造成 10 点火焰伤害，持续 5 秒。
    ///
    /// 零框架依赖，纯 C# 实现。展示最朴素的定时行为写法。
    /// 当需要管理多个定时行为时，这种方式的痛点就会暴露出来。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase0 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase0";
        public override string Description => "纯 C# Tick 循环 - 最朴素的定时行为";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase0: 纯 C# Tick 循环                 ===");
            Log("================================================================================");
            Output.Divider();

            // 需求：每秒造成 10 点火焰伤害，持续 5 秒
            Log("【需求】每秒对目标造成 10 点火焰伤害，持续 5 秒");
            Log("");

            // 创建目标
            var target = new Phase0Target(1, "哥布林", 200f);
            Log($"  目标: {target}");

            // 模拟 5 秒，每 0.5 秒Tick一次
            Log("");
            Log("【执行】每 0.5 秒 Tick 一次...");
            Output.Line();

            float elapsed = 0f;
            float interval = 1f;
            float lastTickTime = 0f;
            float totalDamage = 0f;

            while (elapsed < 6f)
            {
                // Tick: 累积时间
                elapsed += 0.5f;
                lastTickTime += 0.5f;

                // 每秒造成伤害
                if (lastTickTime >= interval)
                {
                    float damage = 10f;
                    target.Health -= damage;
                    totalDamage += damage;
                    lastTickTime = 0f;
                    Log($"  [Tick {elapsed:F1}s] 造成 {damage:F0} 点火焰伤害！ 目标剩余: {target.Health:F0} HP");
                }
                else
                {
                    Log($"  [Tick {elapsed:F1}s] ...");
                }
            }

            Log("");
            Log($"  总计造成 {totalDamage:F0} 点伤害，目标剩余 {target.Health:F0} HP");
            Log("");

            // 痛点暴露
            Log("【痛点】如果同时有 3 个定时行为（灼烧 + 减速 + 中毒）呢？");
            Output.Line();
            Log("  需要手动维护:");
            Output.Bullet("每个行为的 elapsed、interval、lastTickTime");
            Output.Bullet("每个行为的激活/暂停/恢复/中断状态");
            Output.Bullet("每个行为结束时的回调处理");
            Output.Bullet("Owner 死亡时清理所有定时行为");
            Log("");
            Log("  这些都是重复性工作，而且容易出错。");
            Log("");

            // 演示多定时行为
            Log("【演示】同时管理灼烧和减速");
            var enemy = new Phase0Target(2, "哥布林王", 500f);
            SimulateMultipleTimers(enemy, 5f, 0.5f);
            Log("");

            Output.Divider();
            Log("【总结】Phase0 展示了定时行为的基本写法");
            Log("       当定时行为数量增加时，需要统一的生命周期管理");
            Log("       -> Phase1: IContinuous 接口 + IContinuousManager");
            Output.Divider();
        }

        /// <summary>
        /// 模拟多定时行为管理
        /// </summary>
        private void SimulateMultipleTimers(Phase0Target target, float duration, float tickInterval)
        {
            // 灼烧定时器
            float burnElapsed = 0f;
            float burnLastTick = 0f;
            bool burnActive = true;

            // 减速定时器
            float slowElapsed = 0f;
            float slowLastTick = 0f;
            bool slowActive = true;

            float totalTime = 0f;
            while (totalTime < duration)
            {
                totalTime += tickInterval;
                burnElapsed += tickInterval;
                slowElapsed += tickInterval;

                // 灼烧Tick
                if (burnActive && burnLastTick >= 1f)
                {
                    target.Health -= 8f;
                    Log($"  [灼烧] 造成 8 点火焰伤害 (剩余: {target.Health:F0} HP)");
                    burnLastTick = 0f;
                }
                burnLastTick += tickInterval;

                // 减速Tick (每 2 秒检测一次)
                if (slowActive && slowLastTick >= 2f)
                {
                    Log($"  [减速] 检测: 目标速度降低 50%");
                    slowLastTick = 0f;
                }
                slowLastTick += tickInterval;

                // 检查结束
                if (burnElapsed >= duration) burnActive = false;
                if (slowElapsed >= duration) slowActive = false;

                if (!burnActive && !slowActive) break;
            }
            Log($"  所有定时行为结束，目标剩余: {target.Health:F0} HP");
        }
    }

    /// <summary>
    /// Phase0 演示用的目标实体
    /// </summary>
    public class Phase0Target
    {
        public long Id { get; }
        public string Name { get; }
        public float Health { get; set; }
        public float MaxHealth => 200f;
        public float Mana { get; set; }
        public float AttackPower { get; set; }
        public bool IsAlive => Health > 0;

        public Phase0Target(long id, string name, float health)
        {
            Id = id;
            Name = name;
            Health = health;
            Mana = 100f;
            AttackPower = 80f;
        }

        public void TakeDamage(float damage)
        {
            Health = Math.Max(0, Health - damage);
        }

        public bool HasMana(float cost) => Mana >= cost;
        public void ConsumeMana(float amount) => Mana = Math.Max(0, Mana - amount);

        public override string ToString() => $"{Name} (HP: {Health:F0}/{MaxHealth}, Mana: {Mana:F0})";
    }
}

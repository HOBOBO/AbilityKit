#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// ContinuousBehaviorModifiers - 持续行为修改器
    /// 展示如何用修改器统一管理所有持续影响数值的效果
    /// </summary>
    [Sample]
    public sealed class ContinuousBehaviorModifiers : SampleBase
    {
        public override string Title => "持续行为修改器 (Continuous Effects)";
        public override string Description => "用修改器统一管理所有持续效果";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("=== 持续行为修改器系统 ===");
            Output.Divider();

            Log("【核心概念】");
            Log("  修改器可用于任何需要:");
            Output.Bullet("持续影响数值");
            Output.Bullet("在移除后能还原原始值");
            Output.Bullet("统一管理和计算");
            Output.Bullet("持续影响 HFSM 状态参数");

            Output.Divider();

            Log("【1】Buff/Debuff 系统:");
            Log("  场景: 战士中了冰冻减速, 又获得加速 BUFF");
            Output.Bullet("冰冻 Debuff: 移动速度 -50% (3秒)");
            Output.Bullet("疾跑 BUFF: 移动速度 +30% (5秒)");
            Output.Bullet("计算: 基础速度 × (1-0.5) × (1+0.3) = 195");

            float baseSpeed = 300f;
            float speedWithDebuff = baseSpeed * (1 - 0.5f);
            float speedWithBoth = baseSpeed * (1 - 0.5f) * (1 + 0.3f);

            Log($"    基础速度: {baseSpeed}");
            Log($"    冰冻后: {speedWithDebuff} (-50%)");
            Log($"    同时疾跑: {speedWithBoth} (-50% × +30%)");

            Output.Divider();

            Log("【2】DOT (持续伤害) 系统:");
            Log("  场景: 目标同时受到燃烧和中毒效果");

            float burnDps = 50f;
            float poisonDps = 30f;
            float totalDps = burnDps + poisonDps;

            Log($"    燃烧 DOT: {burnDps} 伤害/秒");
            Log($"    中毒 DOT: {poisonDps} 伤害/秒");
            Log($"    总 DOT: {totalDps} 伤害/秒");

            Output.Divider();

            Log("【3】HOT (持续治疗) 系统:");
            Log("  场景: 目标同时受到治疗效果");

            float healDps = 20f;
            Log($"    持续治疗: {healDps} 生命/秒");

            Output.Divider();

            Log("【4】HFSM 状态参数修改:");
            Log("  场景: 根据状态调整 HFSM 参数");

            var hero = new HeroWithModifiers(Log);
            hero.PrintState();

            Output.Bullet("技能1: 狂暴 (AttackPower ×2, Defense -50%)");
            Output.Bullet("技能2: 防御姿态 (Defense ×1.5, MoveSpeed -30%)");
            Output.Bullet("技能3: 疾跑 (MoveSpeed ×1.5)");

            Log("");
            Log("  激活 狂暴:");
            hero.ApplyModifier("AttackPower", ModifierData.Mul(ModifierKey.AttackPower, 2f));
            hero.ApplyModifier("Defense", ModifierData.PercentAdd(ModifierKey.Defense, -0.5f));
            hero.PrintState();

            Log("");
            Log("  额外激活 疾跑:");
            hero.ApplyModifier("MoveSpeed", ModifierData.Mul(ModifierKey.MoveSpeed, 1.5f));
            hero.PrintState();

            Log("");
            Log("  移除 狂暴:");
            hero.RemoveModifier("AttackPower");
            hero.RemoveModifier("Defense");
            hero.PrintState();

            Output.Divider();

            Log("【5】护盾系统:");
            Log("  场景: 角色有护盾时抵挡伤害");

            float health = 1000f;
            float shield = 500f;
            float damage = 700f;

            Log($"    生命: {health}, 护盾: {shield}, 伤害: {damage}");

            float remainingDamage = System.Math.Max(0, damage - shield);
            float remainingShield = System.Math.Max(0, shield - damage);
            float remainingHealth = System.Math.Max(0, health - remainingDamage);

            Log($"    护盾吸收: {System.Math.Min(damage, shield)}");
            Log($"    剩余护盾: {remainingShield}");
            Log($"    穿透护盾伤害: {remainingDamage}");
            Log($"    最终生命: {remainingHealth}");

            Output.Divider();

            Log("【6】状态免疫系统:");
            Log("  场景: 某些效果可以免疫负面状态");

            Log("    激活: 免疫沉默 BUFF (持续 3 秒)");
            Log("    期间: 无法被沉默");
            Log("    移除: 免疫消失，可能被沉默");

            Output.Divider();

            Log("【7】分层衰减效果:");
            Log("  场景: 一个技能有多层效果, 每层衰减不同");

            Log("  技能: 雷霆之力");
            Log("    第1层: +100 攻击力 (立即)");
            Log("    第2层: +50 攻击力 (1秒后衰减)");
            Log("    第3层: +25 攻击力 (2秒后衰减)");
            Log("    第4层: +12.5 攻击力 (3秒后衰减)");
            Log("    第5层: +6.25 攻击力 (4秒后衰减)");

            float[] layers = { 100, 50, 25, 12.5f, 6.25f };
            float totalAttackBonus = 0;

            for (int i = 0; i < layers.Length; i++)
            {
                totalAttackBonus += layers[i];
                Log($"    层{i + 1}: +{layers[i]}, 累计: +{totalAttackBonus:F1}");
            }

            Output.Divider();

            Log("【总结】修改器统一管理的优势:");
            Output.Bullet("单一数据源: 所有属性修改都通过修改器计算");
            Output.Bullet("可追溯: 可以查看谁修改了什么");
            Output.Bullet("可还原: 移除修改器后自动还原");
            Output.Bullet("可叠加: 支持多种叠加策略");
            Output.Bullet("可视化: 便于调试和展示属性变化");
        }
    }

    /// <summary>
    /// 带有状态修改器的英雄示例
    /// </summary>
    internal sealed class HeroWithModifiers
    {
        private readonly Action<string> _log;
        private float _attackPower = 100f;
        private float _defense = 50f;
        private float _moveSpeed = 300f;
        private float _attackSpeed = 1.0f;

        private readonly Dictionary<string, ModifierData> _activeModifiers = new();

        public HeroWithModifiers(Action<string> log)
        {
            _log = log;
        }

        public void ApplyModifier(string category, ModifierData modifier)
        {
            _activeModifiers[category] = modifier;
            _log($"    [应用] {category}: {modifier.Op} {modifier.GetMagnitude(1f, null)}");
        }

        public void RemoveModifier(string category)
        {
            if (_activeModifiers.Remove(category))
            {
                _log($"    [移除] {category}");
            }
        }

        public void PrintState()
        {
            _log($"    ATK: {_attackPower}, DEF: {_defense}, SPD: {_moveSpeed}, ATK_SPD: {_attackSpeed}");
        }
    }

    /// <summary>
    /// 护盾系统示例
    /// </summary>
    internal sealed class ShieldSystem
    {
        private float _health;
        private float _maxHealth;
        private float _shield;
        private float _maxShield;

        public ShieldSystem(float maxHealth, float maxShield)
        {
            _maxHealth = maxHealth;
            _maxShield = maxShield;
            _health = maxHealth;
            _shield = 0;
        }

        public void AddShield(float amount)
        {
            _shield = System.Math.Min(_shield + amount, _maxShield);
        }

        public void RemoveShield(float amount)
        {
            _shield = System.Math.Max(_shield - amount, 0);
        }

        public void TakeDamage(float damage)
        {
            float remainingDamage = System.Math.Max(0, damage - _shield);
            _shield = System.Math.Max(0, _shield - damage);
            _health = System.Math.Max(0, _health - remainingDamage);
        }

        public float Health => _health;
        public float Shield => _shield;
    }
}

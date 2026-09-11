#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Action;

namespace AbilityKit.Samples.Logic.Ability.Samples.Action
{
    /// <summary>
    /// 伤害动作的实现示例。
    /// </summary>
    public sealed class DamageAction : IAction
    {
        public string ActionId => "damage";
        public string DisplayName => "造成伤害";

        private readonly int _baseDamage;
        private readonly string _damageType;
        private bool _cancelled;

        public DamageAction(int baseDamage, string damageType = "physical")
        {
            _baseDamage = baseDamage;
            _damageType = damageType;
            _cancelled = false;
        }

        public ActionResult Execute(IActionContext context)
        {
            if (_cancelled)
                return ActionResult.Failed("Action was cancelled");

            var target = context.Target as ITarget;
            if (target == null)
                return ActionResult.Failed("No valid target");

            var finalDamage = CalculateDamage(context);
            target.ReceiveDamage(finalDamage, _damageType);

            return ActionResult.Succeeded(new DamageResult(finalDamage, _damageType));
        }

        private int CalculateDamage(IActionContext context)
        {
            var source = context.Source as ITarget;
            var bonus = source?.GetStat("attack") ?? 0;
            return _baseDamage + bonus;
        }

        public bool TryCancel()
        {
            _cancelled = true;
            return true;
        }

        public static DamageAction FromArgs(IReadOnlyDictionary<string, object> args)
        {
            var damage = args.TryGetValue("damage", out var d) ? Convert.ToInt32(d) : 0;
            var type = args.TryGetValue("damage_type", out var t) ? t?.ToString() ?? "physical" : "physical";
            return new DamageAction(damage, type);
        }
    }

    /// <summary>
    /// 伤害结果。
    /// </summary>
    public readonly struct DamageResult
    {
        public int Damage { get; }
        public string DamageType { get; }

        public DamageResult(int damage, string damageType)
        {
            Damage = damage;
            DamageType = damageType;
        }
    }

    /// <summary>
    /// 可作为目标的接口。
    /// </summary>
    public interface ITarget
    {
        int GetStat(string statName);
        void ReceiveDamage(int damage, string damageType);
    }

    /// <summary>
    /// 伤害动作工厂。
    /// </summary>
    public sealed class DamageActionFactory : IActionFactory
    {
        public string FactoryId => "damage_factory";

        public bool CanCreate(string actionType) => actionType == "damage";

        public IAction Create(string actionType, IReadOnlyDictionary<string, object> args)
            => DamageAction.FromArgs(args);
    }
}

using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaDamageMitigationService))]
    public sealed class MobaDamageMitigationService : IService
    {
        private readonly MobaActorLookupService _actors;

        public MobaDamageMitigationService(MobaActorLookupService actors)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        /// <summary>
        /// 定点减伤：属性系统仍是 float 存储，在读取处经
        /// <see cref="MobaResourceFixedConvert"/> 单次换算进 Q32.32 后做确定性算术。
        /// </summary>
        public Fixed64 Mitigate(AttackInfo attack, Fixed64 rawDamage)
        {
            if (attack == null) return Fixed64.Zero;
            if (rawDamage <= Fixed64.Zero) return Fixed64.Zero;
            if (attack.DamageType == DamageType.True) return rawDamage;

            if (!_actors.TryGetActorEntity(attack.TargetActorId, out var target) || target == null) return rawDamage;
            if (!target.hasAttributeGroup) return rawDamage;

            var targetAttrs = target.GetMobaAttrs();
            var defense = MobaResourceFixedConvert.ToFixed(ResolveDefense(targetAttrs, attack.DamageType));
            var penetrationR = MobaResourceFixedConvert.ToFixed(ResolvePenetrationRatio(attack.AttackerActorId, attack.DamageType));
            var effectiveDefense = DeterministicMath.Max(Fixed64.Zero, defense * (Fixed64.One - DeterministicMath.Clamp(penetrationR, Fixed64.Zero, MobaResourceFixedConvert.ToFixed(0.95f))));

            return rawDamage * 100 / (100 + effectiveDefense);
        }

        private float ResolvePenetrationRatio(int attackerActorId, DamageType damageType)
        {
            if (attackerActorId <= 0) return 0f;
            if (!_actors.TryGetActorEntity(attackerActorId, out var attacker) || attacker == null) return 0f;
            if (!attacker.hasAttributeGroup) return 0f;

            var attrs = attacker.GetMobaAttrs();
            switch (damageType)
            {
                case DamageType.Physical:
                    return attrs.PhysicsPenetrationR;
                case DamageType.Magic:
                    return attrs.MagicPenetrationR;
                default:
                    return 0f;
            }
        }

        private static float ResolveDefense(MobaAttrs attrs, DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical:
                    return attrs.PhysicsDefense;
                case DamageType.Magic:
                    return attrs.MagicDefense;
                default:
                    return 0f;
            }
        }

        public void Dispose()
        {
        }
    }
}

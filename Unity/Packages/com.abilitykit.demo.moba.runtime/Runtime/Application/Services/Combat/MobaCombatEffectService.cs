using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaCombatEffectService))]
    public sealed class MobaCombatEffectService : IService
    {
        private readonly DamagePipelineService _damagePipeline;
        private readonly MobaDamageService _damageApplier;

        public MobaCombatEffectService(DamagePipelineService damagePipeline, MobaDamageService damageApplier)
        {
            _damagePipeline = damagePipeline ?? throw new ArgumentNullException(nameof(damagePipeline));
            _damageApplier = damageApplier ?? throw new ArgumentNullException(nameof(damageApplier));
        }

        public DamageResult DealDamage(AttackInfo attack)
        {
            if (attack == null) return null;
            return _damagePipeline.Execute(attack);
        }

        public float Heal(int healerActorId, int targetActorId, int healType, float value, int reasonKind = 0, int reasonParam = 0)
        {
            return _damageApplier.CommitHeal(
                healerActorId,
                targetActorId,
                healType,
                value,
                reasonKind,
                reasonParam).AppliedValue;
        }

        public void Dispose()
        {
        }
    }
}

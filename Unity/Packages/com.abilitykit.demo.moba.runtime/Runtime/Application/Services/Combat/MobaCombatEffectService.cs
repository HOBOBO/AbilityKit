using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaCombatEffectService))]
    public sealed class MobaCombatEffectService : IService
    {
        private readonly DamagePipelineService _damagePipeline;
        private readonly HealPipelineService _healPipeline;

        public MobaCombatEffectService(DamagePipelineService damagePipeline, HealPipelineService healPipeline)
        {
            _damagePipeline = damagePipeline ?? throw new ArgumentNullException(nameof(damagePipeline));
            _healPipeline = healPipeline ?? throw new ArgumentNullException(nameof(healPipeline));
        }

        public DamageResult DealDamage(AttackInfo attack)
        {
            if (attack == null) return null;
            return _damagePipeline.Execute(attack);
        }

        public float Heal(int healerActorId, int targetActorId, int healType, float value, int reasonKind = 0, int reasonParam = 0)
        {
            var request = new MobaHealRequest(
                healerActorId,
                targetActorId,
                healType,
                value,
                reasonKind,
                reasonParam);
            return _healPipeline.Execute(in request).AppliedValue;
        }

        public void Dispose()
        {
        }
    }
}

using System;
using System.Linq;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Systems;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Effect;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    using AbilityKit.Ability.Share.Effect;
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterTargetingAndSkillServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<SearchTargetService, SearchTargetService>();
            builder.RegisterService<MobaSkillLoadoutService, MobaSkillLoadoutService>();

            builder.TryRegister<SkillConditionRegistry>(WorldLifetime.Singleton, _ =>
            {
                var reg = new SkillConditionRegistry();
                reg.DiscoverAndRegister();
                Log.Info($"[MobaWorldBootstrapModule] SkillConditionRegistry initialized with {reg.GetAllConditionIds().Count()} conditions");
                return reg;
            });

            builder.TryRegister<MobaEventSubscriptionRegistry>(WorldLifetime.Singleton, _ =>
            {
                var reg = new MobaEventSubscriptionRegistry();
                reg.RegisterPrefix<SkillCastContext>("skill.");
                reg.RegisterPrefix<BuffEventArgs>("buff.");
                reg.RegisterPrefix<AbilityKit.Ability.Share.Impl.Moba.Services.Projectile.AreaEventArgs>("area.");
                reg.RegisterPrefix<AbilityKit.Ability.Impl.Triggering.PresentationEventArgs>("presentation.");

                // damage.* uses multiple payload types, so we register exact mappings.
                reg.RegisterExact<AttackInfo>(DamagePipelineEvents.AttackCreated);
                reg.RegisterExact<AttackInfo>(DamagePipelineEvents.BeforeCalc);

                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.CalcBegin);
                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.AfterBase);
                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.AfterMitigate);
                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.AfterShield);
                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.CalcFinal);
                reg.RegisterExact<AttackCalcInfo>(DamagePipelineEvents.BeforeApply);

                reg.RegisterExact<DamageResult>(DamagePipelineEvents.AfterApply);

                // projectile.*
                reg.RegisterExact<ProjectileHitEvent>(ProjectileTriggering.Events.Hit);
                reg.RegisterExact<ProjectileSpawnEvent>(ProjectileTriggering.Events.Spawn);
                reg.RegisterExact<ProjectileTickEvent>(ProjectileTriggering.Events.Tick);
                reg.RegisterExact<ProjectileExitEvent>(ProjectileTriggering.Events.Exit);

                // summon.*
                reg.RegisterPrefix<SummonEventPayload>("summon.");

                // unit.*
                reg.RegisterPrefix<UnitEventPayload>("unit.");
                reg.RegisterExact<UnitDieEventPayload>(MobaUnitTriggering.Events.Die);
                return reg;
            });

            builder.TryRegister<MobaTriggerIndexService>(WorldLifetime.Singleton, _ =>
            {
                var loader = _.Resolve<ITextLoader>();
                var s = new MobaTriggerIndexService(loader);
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources begin");
                s.LoadFromResources();
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources end");
                return s;
            });
            builder.RegisterService<MobaEffectExecutionService, MobaEffectExecutionService>();
            builder.RegisterService<MobaEffectInvokerService, MobaEffectInvokerService>();

            builder.RegisterService<MobaPeriodicEffectService, MobaPeriodicEffectService>();
        }
    }
}

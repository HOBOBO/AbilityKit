using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private readonly struct BootstrapStage
        {
            public readonly string Name;
            public readonly Action<WorldContainerBuilder> Configure;
            public readonly Action<global::Entitas.IContexts, global::Entitas.Systems, IWorldResolver> Install;

            public BootstrapStage(
                string name,
                Action<WorldContainerBuilder> configure,
                Action<global::Entitas.IContexts, global::Entitas.Systems, IWorldResolver> install)
            {
                Name = name;
                Configure = configure;
                Install = install;
            }
        }

        private static BootstrapStage[] CreatePipelineStages()
        {
            return new[]
            {
                new BootstrapStage("WorldModules", RegisterWorldModules, install: null),
                new BootstrapStage("TriggeringRuntime", RegisterTriggeringRuntime, install: null),
                new BootstrapStage("CoreState", RegisterCoreState, install: null),
                new BootstrapStage("Config", RegisterConfig, install: null),
                // [REMOVED] Effects 包已删除 new BootstrapStage("Effects", RegisterEffects, install: null),
                new BootstrapStage("Tags", RegisterTags, install: null),
                new BootstrapStage("TriggerPlans", RegisterTriggerPlans, install: null),
                new BootstrapStage("EffectSources", RegisterEffectSources, install: null),
                new BootstrapStage("ActorAndEntity", RegisterActorAndEntityServices, install: null),
                new BootstrapStage("Snapshots", RegisterSnapshotServices, install: null),
                new BootstrapStage("Combat", RegisterCombatServices, install: null),
                new BootstrapStage("Summon", RegisterSummonServices, install: null),
                new BootstrapStage("MovementAndCollision", RegisterMovementAndCollision, install: null),
                new BootstrapStage("Projectile", RegisterProjectileServices, install: null),
                new BootstrapStage("TargetingAndSkills", RegisterTargetingAndSkillServices, install: null),
                new BootstrapStage("BuffAndSkillPipelines", RegisterBuffAndSkillPipelines, install: null),

                new BootstrapStage("Install.PlanTriggering", configure: null, install: static (c, s, r) => InstallPlanTriggering(r)),
                new BootstrapStage("Install.WorldInit", configure: null, install: static (c, s, r) => InstallWorldInitEnterGameReq(r)),
            };
        }

        private static void ExecuteConfigurePipeline(WorldContainerBuilder builder)
        {
            var stages = CreatePipelineStages();
            for (int i = 0; i < stages.Length; i++)
            {
                var s = stages[i];
                if (s.Configure == null) continue;

                try
                {
                    s.Configure(builder);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaWorldBootstrapModule] Configure stage failed. stage={s.Name}");
                    throw;
                }
            }
        }

        private static void ExecuteInstallPipeline(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            var stages = CreatePipelineStages();
            for (int i = 0; i < stages.Length; i++)
            {
                var s = stages[i];
                if (s.Install == null) continue;

                try
                {
                    s.Install(contexts, systems, services);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaWorldBootstrapModule] Install stage failed. stage={s.Name}");
                    throw;
                }
            }
        }
    }
}

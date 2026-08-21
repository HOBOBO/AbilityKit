using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaRuntimeDependencyValidationRules
    {
        public const string MissingDependencyCode = "moba.runtime.dependency.missing";
        public const string InvalidConfigurationCode = "moba.runtime.dependency.invalid_configuration";
    }

    public sealed class MobaRuntimeCoreDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.core";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<MobaConfigDatabase>(in context, report, "config.database");
            Require<MobaActorRegistry>(in context, report, "actor.registry");
            Require<MobaEntityManager>(in context, report, "entity.manager");
        }
    }

    public sealed class MobaRuntimeSkillDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.skill";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<SkillCastCoordinator>(in context, report, "cast.coordinator");
            Require<MobaSkillCastRuntimeService>(in context, report, "cast.runtime");
            Require<MobaEffectExecutionService>(in context, report, "effect.execution");
            Require<MobaTriggerExecutionGateway>(in context, report, "trigger.gateway");
        }
    }

    public sealed class MobaRuntimeContinuousDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.continuous";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<IContinuousManager>(in context, report, "manager");
            Require<IMobaContinuousRuntimeQueryService>(in context, report, "runtime.query");
            Require<IMobaContinuousTagRuleService>(in context, report, "tag.rules");
            Require<MobaBuffService>(in context, report, "buff.service");
        }
    }

    public sealed class MobaRuntimeCombatDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.combat";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            Require<DamagePipelineService>(in context, report, "damage.pipeline");
            Require<HealPipelineService>(in context, report, "heal.pipeline");
            if (!context.TryResolve<IMobaDamageStageProvider>(out var provider) || provider == null)
            {
                ReportMissing(report, "damage.stage_provider", typeof(IMobaDamageStageProvider));
                return;
            }

            var validation = provider.Validate();
            for (var i = 0; i < validation.Errors.Count; i++)
            {
                report.Error(
                    Name,
                    "damage.stage_configuration",
                    validation.Errors[i],
                    nameof(IMobaDamageStageProvider),
                    blocksStartup: true,
                    code: MobaRuntimeDependencyValidationRules.InvalidConfigurationCode,
                    category: MobaRuntimeValidationCategory.RuntimeContract);
            }
        }
    }

    public sealed class MobaRuntimeTemporaryEntityDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.temp_entity";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<MobaProjectileService>(in context, report, "projectile.runtime");
            Require<MobaAreaRuntimeService>(in context, report, "area.runtime");
            Require<MobaSummonService>(in context, report, "summon.runtime");
            Require<IMobaTemporaryEntityLifecycleService>(in context, report, "lifecycle");
        }
    }

    public sealed class MobaRuntimeOutputDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.output";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<MobaEnterGameSnapshotService>(in context, report, "snapshot.enter_game");
            Require<MobaActorSpawnSnapshotService>(in context, report, "snapshot.actor_spawn");
            Require<MobaSnapshotRouter>(in context, report, "snapshot.router");
        }
    }

    public sealed class MobaRuntimeDiagnosticsDependencyValidator : MobaRuntimeDependencyValidatorBase
    {
        public override string Name => "runtime.dependencies.diagnostics";

        public override void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            Require<IMobaBattleDiagnosticsService>(in context, report, "battle.diagnostics");
            Require<IMobaBattleExceptionPolicy>(in context, report, "exception.policy");
            Require<MobaTraceRegistry>(in context, report, "trace.registry");
        }
    }

    public abstract class MobaRuntimeDependencyValidatorBase : IMobaRuntimeValidator
    {
        public abstract string Name { get; }

        public abstract void Validate(
            in MobaRuntimeValidationContext context,
            MobaRuntimeValidationReport report);

        protected void Require<T>(
            in MobaRuntimeValidationContext context,
            MobaRuntimeValidationReport report,
            string path)
            where T : class
        {
            if (report == null) return;
            if (!context.TryResolve<T>(out var service) || service == null)
            {
                ReportMissing(report, path, typeof(T));
            }
        }

        protected void ReportMissing(MobaRuntimeValidationReport report, string path, System.Type serviceType)
        {
            report.Error(
                Name,
                path,
                serviceType.Name + " is required by the " + Name + " runtime capability.",
                serviceType.Name,
                blocksStartup: true,
                code: MobaRuntimeDependencyValidationRules.MissingDependencyCode,
                category: MobaRuntimeValidationCategory.RuntimeContract);
        }
    }
}

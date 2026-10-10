using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.GameplayTags;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal static class MobaMotionContinuousActionRuntime
    {
        public static bool TryActivate(
            ExecCtx<IWorldResolver> ctx,
            MobaMovementActionInput input,
            string kind,
            int sourceActorId,
            int targetActorId,
            int ownerActorId,
            float durationSeconds,
            IMotionSource motionSource,
            MobaMotionContinuousSettings settings,
            MobaMotionHitTriggerRuntime hitTriggerRuntime,
            out string rejectReason)
        {
            return TryActivate(
                ctx,
                input,
                kind,
                sourceActorId,
                targetActorId,
                ownerActorId,
                durationSeconds,
                motionSource,
                settings,
                hitTriggerRuntime,
                default,
                out rejectReason);
        }

        public static bool TryActivate(
            ExecCtx<IWorldResolver> ctx,
            MobaMovementActionInput input,
            string kind,
            int sourceActorId,
            int targetActorId,
            int ownerActorId,
            float durationSeconds,
            IMotionSource motionSource,
            MobaMotionContinuousSettings settings,
            MobaMotionHitTriggerRuntime hitTriggerRuntime,
            MobaMotionLandingTriggerRuntime landingTriggerRuntime,
            out string rejectReason)
        {
            rejectReason = null;
            if (ctx.Context == null)
            {
                rejectReason = "requires world resolver";
                return false;
            }

            if (!ctx.Context.TryResolve<IContinuousManager>(out var continuous) || continuous == null)
            {
                rejectReason = "requires IContinuousManager service";
                return false;
            }

            if (!ctx.Context.TryResolve<MobaActorRegistry>(out var actors) || actors == null)
            {
                rejectReason = "requires MobaActorRegistry service";
                return false;
            }

            MobaMotionContinuousResolvedConfig config;
            try
            {
                config = ResolveContinuousConfig(ctx.Context, settings, durationSeconds);
            }
            catch (InvalidOperationException error)
            {
                rejectReason = error.Message;
                return false;
            }

            var source = ResolveContextSource(input, sourceActorId, targetActorId, config.ConfigId);
            var sourceContextId = source.SourceContextId != 0
                ? source.SourceContextId
                : input.ActionInput.HasExecutionScope
                    ? input.ActionInput.ExecutionScope.EffectContextId
                    : 0L;

            var identity = new MobaMotionContinuousIdentity(
                kind, config.ConfigId, sourceActorId, targetActorId, ownerActorId, sourceContextId, source);
            var dependencies = new MobaMotionContinuousDependencies(
                actors, motionSource, hitTriggerRuntime, landingTriggerRuntime,
                ResolveLandingTriggerService(ctx.Context, landingTriggerRuntime));
            var runtime = new MobaMotionContinuousRuntime(in identity, in config, in dependencies);

            if (continuous.TryActivate(runtime))
            {
                return true;
            }

            var managerRejectReason = (continuous as DefaultContinuousManager)?.LastRejectReason;
            rejectReason = string.IsNullOrEmpty(managerRejectReason)
                ? "motion continuous activation rejected"
                : managerRejectReason;
            return false;
        }

        private static MobaMotionContinuousResolvedConfig ResolveContinuousConfig(
            IWorldResolver services,
            MobaMotionContinuousSettings settings,
            float fallbackDurationSeconds)
        {
            if (settings.ContinuousProcessId > 0 && settings.ContinuousTagTemplateId > 0)
                throw new InvalidOperationException("Motion cannot reference both a continuous process and a tag template.");

            var configId = settings.ContinuousProcessId > 0 ? settings.ContinuousProcessId : settings.ContinuousTagTemplateId;
            var durationSeconds = fallbackDurationSeconds;
            var requirements = new ContinuousTagRequirements();
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers = Array.Empty<IMobaContinuousModifierSpec>();
            IReadOnlyList<int> triggerIds = settings.TriggerIds ?? Array.Empty<int>();
            var intervalSeconds = settings.IntervalMs > 0 ? settings.IntervalMs / 1000f : 0f;
            IReadOnlyList<int> intervalTriggerIds = settings.IntervalTriggerIds ?? Array.Empty<int>();

            if (settings.ContinuousProcessId <= 0)
            {
                requirements = ResolveTemplateRequirements(services, settings.ContinuousTagTemplateId);
                return new MobaMotionContinuousResolvedConfig(configId, durationSeconds,
                    requirements, modifiers, triggerIds, intervalSeconds, intervalTriggerIds);
            }
            if (services == null || !services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null ||
                !configs.TryGetContinuousProcess(settings.ContinuousProcessId, out var process) || process == null)
                throw new InvalidOperationException($"Motion references missing continuous process {settings.ContinuousProcessId}.");

            configId = process.Id;
            if (process.DurationMs > 0)
            {
                durationSeconds = process.DurationMs / 1000f;
            }

            requirements = ResolveProcessRequirements(services, process);
            modifiers = process.Modifiers ?? (IReadOnlyList<IMobaContinuousModifierSpec>)Array.Empty<IMobaContinuousModifierSpec>();
            triggerIds = process.TriggerIds ?? Array.Empty<int>();
            intervalSeconds = process.IntervalMs > 0 ? process.IntervalMs / 1000f : 0f;
            intervalTriggerIds = process.IntervalTriggerIds ?? Array.Empty<int>();
            return new MobaMotionContinuousResolvedConfig(configId, durationSeconds,
                requirements, modifiers, triggerIds, intervalSeconds, intervalTriggerIds);
        }

        private static MobaMotionLandingTriggerService ResolveLandingTriggerService(IWorldResolver services, in MobaMotionLandingTriggerRuntime runtime)
        {
            if (!runtime.IsValid || services == null)
            {
                return null;
            }

            return services.TryResolve<MobaMotionLandingTriggerService>(out var service) ? service : null;
        }

        private static ContinuousTagRequirements ResolveProcessRequirements(IWorldResolver services, ContinuousProcessMO process)
        {
            var result = ResolveTemplateRequirements(services, process != null ? process.ContinuousTagTemplateId : 0);
            if (process?.Tags != null && process.Tags.Count > 0)
            {
                result.ApplicationTags.AppendTags(process.Tags);
            }

            return result;
        }

        private static ContinuousTagRequirements ResolveTemplateRequirements(IWorldResolver services, int templateId)
        {
            if (templateId <= 0)
            {
                return new ContinuousTagRequirements();
            }

            if (services == null || !services.TryResolve<IMobaContinuousTagTemplateRegistry>(out var templates)
                || templates == null
                || !templates.TryGet(templateId, out var template)
                || template == null)
            {
                throw new InvalidOperationException($"Motion references missing continuous tag template {templateId}.");
            }

            return CopyRequirements(template);
        }

        private static ContinuousTagRequirements CopyRequirements(ContinuousTagRequirements source)
        {
            if (source == null) return new ContinuousTagRequirements();
            return new ContinuousTagRequirements
            {
                ActivationRequired = source.ActivationRequired,
                ApplicationTags = CopyContainer(source.ApplicationTags),
                RemovalRequired = source.RemovalRequired,
                OngoingRequired = source.OngoingRequired,
                ExecutionBlockedResultMask = source.ExecutionBlockedResultMask
            };
        }

        private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
        {
            var copy = new GameplayTagContainer();
            copy.AppendTags(source);
            return copy;
        }

        private static MobaContextSourceView ResolveContextSource(MobaMovementActionInput input, int sourceActorId, int targetActorId, int configId)
        {
            var actionInput = input.ActionInput;
            if (actionInput.ExecutionContext.TryGetContextSource(out var source) && source.IsValid)
            {
                return new MobaContextSourceView(
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.LiveRuntime,
                    source.ContextKind != EffectContextKind.Unknown ? source.ContextKind : EffectContextKind.ContinuousPeriodic,
                    source.ExecutionKind != MobaExecutionKind.None ? source.ExecutionKind : MobaExecutionKind.EffectExecution,
                    source.SourceActorId != 0 ? source.SourceActorId : sourceActorId,
                    source.TargetActorId != 0 ? source.TargetActorId : targetActorId,
                    source.SourceContextId,
                    source.ParentContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    source.ConfigId != 0 ? source.ConfigId : configId,
                    source.TriggerId,
                    source.Frame,
                    "MotionContinuous",
                    configId,
                    true,
                    source.SkillRuntimeHandle);
            }

            if (actionInput.HasExecutionScope)
            {
                var executionScope = actionInput.ExecutionScope;
                var origin = new MobaGameplayOrigin(
                    sourceActorId,
                    targetActorId,
                    MobaExecutionKind.EffectExecution,
                    executionScope.EffectConfigId != 0 ? executionScope.EffectConfigId : configId,
                    executionScope.EffectContextId,
                    executionScope.EffectContextId,
                    executionScope.EffectContextId,
                    executionScope.EffectContextId);
                var lineage = origin.ToLineageContext(EffectContextKind.ContinuousPeriodic);
                return MobaContextSourceView.FromLineage(
                    in lineage,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.LiveRuntime,
                    default,
                    true,
                    "MotionContinuous",
                    configId);
            }

            return default;
        }
    }
}

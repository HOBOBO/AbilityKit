using System;
using System.Collections.Generic;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.ECS;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillCastPreparationService
    {
        private readonly IWorldResolver _services;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly IUnitResolver _units;
        private readonly MobaActorLookupService _actors;
        private readonly IMobaSkillPipelineLibrary _library;
        private readonly Dictionary<int, int> _castSequenceByActor = new Dictionary<int, int>();

        public SkillCastPreparationService(
            IWorldResolver services,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            IUnitResolver units,
            MobaActorLookupService actors,
            IMobaSkillPipelineLibrary library)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public SkillCastPreparationResult Prepare(in SkillCastPreparationInput input)
        {
            var actorId = input.ActorId;
            var skillId = input.SkillId;
            var slot = input.Slot;
            if (actorId <= 0) return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.InvalidCaster, $"Invalid caster actor id: {actorId}.");
            if (skillId <= 0) return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.InvalidSkill, $"Invalid skill id: {skillId}.");

            if (!_units.TryResolve(new EcsEntityId(actorId), out var caster) || caster == null)
            {
                return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.CasterMissing, "Caster not found.");
            }

            ResolveCasterTransform(actorId, out var casterPos, out var casterForward);
            var finalAimPos = input.HasAim ? input.AimPos : casterPos;
            var finalAimDir = input.HasAim ? input.AimDir : casterForward;
            if (finalAimDir.Equals(Vec3.Zero)) finalAimDir = casterForward;
            if (finalAimPos.Equals(Vec3.Zero)) finalAimPos = casterPos;

            var finalTargetActorId = input.TargetActorId > 0 ? input.TargetActorId : 0;
            IUnitFacade targetUnit = null;
            if (finalTargetActorId > 0)
            {
                if (!_units.TryResolve(new EcsEntityId(finalTargetActorId), out targetUnit) || targetUnit == null)
                {
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TargetMissing, $"Target not found. targetActorId={finalTargetActorId}.");
                }
            }

            if (!_services.TryResolve<MobaConfigDatabase>(out var configs) ||
                configs == null ||
                !configs.TryGetSkill(skillId, out var skill) ||
                skill == null)
            {
                return SkillCastPreparationResult.Failed(
                    SkillFailureCodes.Cast.ConfigurationInvalid,
                    $"Skill configuration is missing. skillId={skillId}.");
            }

            var castFlowId = skill.CastFlowId;
            if (RequiresTargetSearch(skill))
            {
                if (!_services.TryResolve<SearchTargetService>(out var search) || search == null)
                {
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TargetMissing, "Required target search service is unavailable.");
                }

                var targets = new List<int>(1);
                var found = skill.RequiredTargetQueryId > 0
                    ? search.TrySearchActorIds(
                        skill.RequiredTargetQueryId,
                        actorId,
                        in casterPos,
                        finalTargetActorId,
                        targets)
                    : search.TrySearchActorIds(
                        NormalAttackTargetQuery.Create(skill.Range),
                        actorId,
                        in casterPos,
                        finalTargetActorId,
                        targets);
                if (!found || targets.Count == 0)
                {
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TargetMissing, "No valid target is within cast range.");
                }

                finalTargetActorId = targets[0];
                if (!_units.TryResolve(new EcsEntityId(finalTargetActorId), out targetUnit) || targetUnit == null)
                {
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TargetMissing, $"Resolved target not found. targetActorId={finalTargetActorId}.");
                }
            }

            if (!_library.TryGet(skillId, out var preConfig, out var prePhases, out var castConfig, out var castPhases))
            {
                Log.Warning($"[SkillCastCoordinator] Cast failed: pipeline missing. actor={actorId}, skillId={skillId}, slot={slot}, target={finalTargetActorId}");
                return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.PipelineMissing, "Skill pipeline not found.");
            }

            var request = new SkillCastRequest(
                skillId: skillId,
                skillSlot: slot,
                casterActorId: actorId,
                targetActorId: finalTargetActorId,
                aimPos: in finalAimPos,
                aimDir: in finalAimDir,
                worldServices: _services,
                eventBus: _eventBus,
                casterUnit: caster,
                targetUnit: targetUnit);

            var skillLevel = ResolveSkillLevel(actorId, skillId, slot);
            if (!ResolvedSkillCastConfiguration.TryResolve(
                    configs,
                    skill,
                    skillLevel,
                    out var resolvedConfiguration,
                    out var configurationError))
            {
                return SkillCastPreparationResult.Failed(
                    SkillFailureCodes.Cast.ConfigurationInvalid,
                    configurationError);
            }

            var sequence = NextCastSequence(actorId);
            var context = SkillCastContextBuilder.Create()
                .FromRequest(in request)
                .WithSkillLevel(resolvedConfiguration.SkillLevel)
                .WithSequence(sequence)
                .Build();
            context.CastFlowId = castFlowId;
            context.ResolvedConfiguration = resolvedConfiguration;

            var trace = _services.Resolve<MobaTraceRegistry>();
            if (trace == null)
            {
                return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TraceRegistryMissing, "MobaTraceRegistry is required for formal skill cast tracing.");
            }

            context.SourceContextId = trace.CreateRootContext(
                MobaTraceKind.SkillCast,
                skillId,
                actorId,
                finalTargetActorId,
                TraceEndpoint.Actor(actorId),
                finalTargetActorId > 0 ? TraceEndpoint.Actor(finalTargetActorId) : default);
            if (context.SourceContextId == 0)
            {
                return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.TraceRootCreateFailed, "Skill cast trace root creation failed.");
            }

            MobaSkillCastRuntimeService runtimes = null;
            try
            {
                trace.TrySetSkillPhaseLocation(context.SourceContextId, skillId, castFlowId, string.Empty);

                runtimes = _services.Resolve<MobaSkillCastRuntimeService>();
                if (runtimes == null)
                {
                    trace.EndContext(context.SourceContextId, TraceLifecycleReason.Cancelled);
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.RuntimeServiceMissing, "MobaSkillCastRuntimeService is required for formal skill cast runtime tracking.");
                }

                var createRequest = MobaSkillCastRuntimeCreateRequestBuilder.Create()
                    .FromCastContext(context)
                    .Build();
                var runtime = runtimes.Create(in createRequest);
                context.RuntimeHandle = runtime.Handle;
                context.RuntimeId = runtime.RuntimeId;
                if (!context.RuntimeHandle.IsValid)
                {
                    trace.EndContext(context.SourceContextId, TraceLifecycleReason.Cancelled);
                    return SkillCastPreparationResult.Failed(SkillFailureCodes.Cast.RuntimeHandleInvalid, "Skill cast runtime creation returned an invalid handle.");
                }

                return SkillCastPreparationResult.Ready(in request, context, runtimes, preConfig, prePhases, castConfig, castPhases);
            }
            catch
            {
                if (context.RuntimeHandle.IsValid && runtimes != null)
                {
                    runtimes.ForceTerminate(in context.RuntimeHandle, MobaSkillRuntimeEndReason.RollbackCleanup);
                }
                else
                {
                    trace.EndContext(context.SourceContextId, TraceLifecycleReason.Cancelled);
                }

                throw;
            }
        }

        private static bool RequiresTargetSearch(SkillMO skill)
        {
            return skill.RequiredTargetQueryId > 0
                || skill.SkillType == SkillType.NormalAttack;
        }

        private void ResolveCasterTransform(int actorId, out Vec3 position, out Vec3 forward)
        {
            position = Vec3.Zero;
            forward = Vec3.Forward;
            if (_actors.TryGetActorEntity(actorId, out var actorEntity) && actorEntity != null && actorEntity.hasTransform)
            {
                var transform = actorEntity.transform.Value;
                position = transform.Position;
                forward = DeterministicMathBridge.Normalize(transform.Rotation.Rotate(Vec3.Forward));
            }
        }

        private int ResolveSkillLevel(int actorId, int skillId, int slot)
        {
            if (!_actors.TryGetActorEntity(actorId, out var actor) || actor == null || !actor.hasSkillLoadout) return 0;

            var skills = actor.skillLoadout.ActiveSkills;
            var index = slot - 1;
            if (skills == null || index < 0 || index >= skills.Length) return 0;

            var runtime = skills[index];
            return runtime != null && runtime.SkillId == skillId ? runtime.Level : 0;
        }

        public void RemoveActor(int actorId)
        {
            if (actorId > 0)
            {
                _castSequenceByActor.Remove(actorId);
            }
        }

        private int NextCastSequence(int actorId)
        {
            if (_castSequenceByActor.TryGetValue(actorId, out var sequence))
            {
                sequence++;
            }
            else
            {
                sequence = 1;
            }

            _castSequenceByActor[actorId] = sequence;
            return sequence;
        }
    }

    internal static class NormalAttackTargetQuery
    {
        public static SearchQueryTemplateMO Create(float range)
        {
            if (range <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "Normal attack range must be positive.");
            }

            return new SearchQueryTemplateMO(
                id: 0,
                name: "normal_attack_target_query",
                maxCount: 1,
                explicitTargetPolicy: (int)SearchQueryExplicitTargetPolicy.PreferExplicitTarget,
                provider: new SearchTargetProviderConfig(0, (int)SearchTargetProviderKind.EnemyTeam),
                rules: new[]
                {
                    new SearchTargetRuleConfig(
                        id: 0,
                        kind: (int)SearchTargetRuleKind.CircleShape,
                        center: (int)SearchTargetPointKind.Caster,
                        radius: range)
                },
                scorers: new[]
                {
                    new SearchTargetScorerConfig(
                        id: 0,
                        kind: (int)SearchTargetScorerKind.DistanceToCaster,
                        source: (int)SearchTargetPointKind.Caster)
                },
                selector: new SearchTargetSelectorConfig(
                    id: 0,
                    kind: (int)SearchTargetSelectorKind.TopKByScore));
        }
    }
}


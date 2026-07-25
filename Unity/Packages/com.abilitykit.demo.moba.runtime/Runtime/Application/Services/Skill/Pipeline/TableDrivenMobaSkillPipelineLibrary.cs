using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IMobaSkillPipelineLibrary), WorldLifetime.Scoped)]
    public sealed class TableDrivenMobaSkillPipelineLibrary : IMobaSkillPipelineLibrary
    {
        private static readonly AbilityPipelinePhaseId PreCastChecksPhaseId = new AbilityPipelinePhaseId("skill.checks.precast");
        private static readonly AbilityPipelinePhaseId PreCastTimelinePhaseId = new AbilityPipelinePhaseId("skill.timeline.precast");
        private static readonly AbilityPipelinePhaseId CastChecksPhaseId = new AbilityPipelinePhaseId("skill.checks.cast");
        private static readonly AbilityPipelinePhaseId CastTimelinePhaseId = new AbilityPipelinePhaseId("skill.timeline.cast");

        private readonly MobaConfigDatabase _configs;
        private readonly MobaEffectInvokerService _effects;
        private readonly IWorldResolver _services;
        private readonly Dictionary<int, SkillPipelineCacheEntry> _skillCache = new Dictionary<int, SkillPipelineCacheEntry>();
        private MobaTriggerPlanExecutor _rulePlanExecutor;

        public TableDrivenMobaSkillPipelineLibrary(
            MobaConfigDatabase configs,
            MobaEffectInvokerService effects,
            IWorldResolver services = null)
        {
            _configs = configs;
            _effects = effects;
            _services = services;
        }

        public int CachedSkillCount => _skillCache.Count;

        public bool IsCached(int skillId)
        {
            return skillId > 0 && _skillCache.ContainsKey(skillId);
        }

        public MobaSkillPipelinePrewarmResult PrewarmAll()
        {
            if (_configs == null) return new MobaSkillPipelinePrewarmResult(0, 0, 0);

            var skills = _configs.GetAllSkills();
            if (skills == null) return new MobaSkillPipelinePrewarmResult(0, 0, 0);

            var requested = 0;
            var warmed = 0;
            var failed = 0;
            foreach (var skill in skills)
            {
                if (skill == null || skill.Id <= 0) continue;

                requested++;
                if (TryGetOrCreateCacheEntry(skill.Id, out _)) warmed++;
                else failed++;
            }

            return new MobaSkillPipelinePrewarmResult(requested, warmed, failed);
        }

        public MobaSkillPipelinePrewarmResult Prewarm(IReadOnlyList<int> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return new MobaSkillPipelinePrewarmResult(0, 0, 0);

            var requested = 0;
            var warmed = 0;
            var failed = 0;
            for (int i = 0; i < skillIds.Count; i++)
            {
                var skillId = skillIds[i];
                if (skillId <= 0) continue;

                requested++;
                if (TryGetOrCreateCacheEntry(skillId, out _)) warmed++;
                else failed++;
            }

            return new MobaSkillPipelinePrewarmResult(requested, warmed, failed);
        }

        public bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases)
        {
            preCastConfig = null;
            preCastPhases = null;
            castConfig = null;
            castPhases = null;

            if (skillId <= 0) return false;
            if (_configs == null) return false;

            if (!TryGetOrCreateCacheEntry(skillId, out var cacheEntry)) return false;

            preCastConfig = cacheEntry.PreCastConfig;
            preCastPhases = BuildPhases(cacheEntry.PreCastPhases);
            castConfig = cacheEntry.CastConfig;
            castPhases = BuildPhases(cacheEntry.CastPhases);

            return true;
        }

        private bool TryGetOrCreateCacheEntry(int skillId, out SkillPipelineCacheEntry entry)
        {
            if (_skillCache.TryGetValue(skillId, out entry))
            {
                return entry != null;
            }

            if (!_configs.TryGetSkill(skillId, out var skill) || skill == null)
            {
                entry = null;
                return false;
            }

            if (skill.CastFlowId <= 0)
            {
                entry = null;
                return false;
            }

            if (!_configs.TryGetSkillFlow(skill.CastFlowId, out var castFlow) || castFlow == null)
            {
                entry = null;
                return false;
            }

            IReadOnlyList<PhaseDefinition> preCastDefinitions = null;
            IAbilityPipelineConfig preCastConfig = null;

            if (skill.PreCastFlowId > 0 && _configs.TryGetSkillFlow(skill.PreCastFlowId, out var preFlow) && preFlow != null)
            {
                preCastConfig = CreatePipelineConfig((skillId << 2) | 0, $"Skill_{skillId}_PreCast", preFlow);
                preCastDefinitions = BuildFlowDefinitions(preFlow, checksPhaseId: PreCastChecksPhaseId, timelinePhaseId: PreCastTimelinePhaseId);
            }

            var castConfig = CreatePipelineConfig((skillId << 2) | 1, $"Skill_{skillId}_Cast", castFlow);
            var castDefinitions = BuildFlowDefinitions(castFlow, checksPhaseId: CastChecksPhaseId, timelinePhaseId: CastTimelinePhaseId);

            entry = new SkillPipelineCacheEntry(preCastConfig, preCastDefinitions, castConfig, castDefinitions);
            _skillCache[skillId] = entry;
            return true;
        }

        private static IAbilityPipelineConfig CreatePipelineConfig(int configId, string configName, SkillFlowMO flow)
        {
            var inner = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig(configId, configName);
            return new MobaSkillPipelineConfig(inner, flow?.PipelineContinuousTagTemplateId ?? 0);
        }

        private static IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> BuildPhases(IReadOnlyList<PhaseDefinition> definitions)
        {
            if (definitions == null) return null;

            var phases = new List<IAbilityPipelinePhase<SkillPipelineContext>>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                phases.Add(definitions[i].CreatePhase());
            }

            return phases;
        }

        private IReadOnlyList<PhaseDefinition> BuildFlowDefinitions(
            SkillFlowMO flow,
            AbilityPipelinePhaseId checksPhaseId,
            AbilityPipelinePhaseId timelinePhaseId)
        {
            if (flow == null)
            {
                throw new InvalidOperationException("Skill flow is missing.");
            }

            if (flow.Phases == null || flow.Phases.Count == 0)
            {
                throw new InvalidOperationException($"Skill flow requires at least one phase. flowId={flow.Id}");
            }

            var list = new List<PhaseDefinition>(flow.Phases.Count);
            for (int i = 0; i < flow.Phases.Count; i++)
            {
                var phase = BuildPhaseDefinition(flow.Phases[i], checksPhaseId, timelinePhaseId, $"skill.flow.{flow.Id}.{i}");
                list.Add(phase);
            }

            return list;
        }

        private IReadOnlyList<PhaseDefinition> BuildChildDefinitions(
            IReadOnlyList<SkillPhaseDTO> children,
            AbilityPipelinePhaseId checksPhaseId,
            AbilityPipelinePhaseId timelinePhaseId,
            string fallbackPhaseId)
        {
            if (children == null || children.Count == 0)
            {
                throw new InvalidOperationException($"Composite skill phase requires at least one child phase. phaseId={fallbackPhaseId}");
            }

            var definitions = new List<PhaseDefinition>(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                definitions.Add(BuildPhaseDefinition(children[i], checksPhaseId, timelinePhaseId, fallbackPhaseId + "." + i));
            }

            return definitions;
        }

        private PhaseDefinition BuildPhaseDefinition(
            SkillPhaseDTO phase,
            AbilityPipelinePhaseId checksPhaseId,
            AbilityPipelinePhaseId timelinePhaseId,
            string fallbackPhaseId)
        {
            if (phase == null) throw new InvalidOperationException($"Skill flow phase is missing. phaseId={fallbackPhaseId}");

            var type = (SkillPhaseType)phase.Type;
            switch (type)
            {
                case SkillPhaseType.Checks:
                    throw new InvalidOperationException($"Skill Checks phase is deprecated. Use RulePlan trigger conditions instead. phaseId={MakePhaseId(phase, checksPhaseId.Value).Value}");
                case SkillPhaseType.Timeline:
                    if (phase.Timeline == null)
                    {
                        throw new InvalidOperationException($"Timeline skill phase requires timeline config. phaseId={MakePhaseId(phase, timelinePhaseId.Value).Value}");
                    }
                    var events = ToArray(phase.Timeline.Events);
                    return new TimelinePhaseDefinition(MakePhaseId(phase, timelinePhaseId.Value), phase.Timeline.DurationMs, events, _effects);
                case SkillPhaseType.Handlers:
                    throw new InvalidOperationException($"Skill Handlers phase is deprecated. Use RulePlan trigger actions instead. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
                case SkillPhaseType.RulePlan:
                    if (phase.RulePlan == null)
                    {
                        throw new InvalidOperationException($"RulePlan skill phase requires rule plan config. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
                    }
                    return new RulePlanPhaseDefinition(MakePhaseId(phase, fallbackPhaseId), phase.RulePlan, GetOrCreateRulePlanExecutor());
                case SkillPhaseType.Sequence:
                    return new SequencePhaseDefinition(
                        MakePhaseId(phase, fallbackPhaseId),
                        BuildChildDefinitions(phase.Children, checksPhaseId, timelinePhaseId, fallbackPhaseId));
                case SkillPhaseType.Parallel:
                    return new ParallelPhaseDefinition(
                        MakePhaseId(phase, fallbackPhaseId),
                        BuildChildDefinitions(phase.Children, checksPhaseId, timelinePhaseId, fallbackPhaseId));
                case SkillPhaseType.Repeat:
                    return BuildRepeatPhaseDefinition(phase, checksPhaseId, timelinePhaseId, fallbackPhaseId);
                case SkillPhaseType.Delay:
                    return BuildDelayPhaseDefinition(phase, fallbackPhaseId);
                case SkillPhaseType.WaitUntil:
                    return BuildWaitUntilPhaseDefinition(phase, fallbackPhaseId);
                default:
                    throw new InvalidOperationException($"Unsupported skill phase type. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, type={phase.Type}");
            }
        }

        private PhaseDefinition BuildRepeatPhaseDefinition(
            SkillPhaseDTO phase,
            AbilityPipelinePhaseId checksPhaseId,
            AbilityPipelinePhaseId timelinePhaseId,
            string fallbackPhaseId)
        {
            var repeatDto = phase.Repeat;
            if (repeatDto == null)
            {
                throw new InvalidOperationException($"Repeat skill phase requires repeat config. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
            }

            if (repeatDto.RepeatCount <= 0)
            {
                throw new InvalidOperationException($"Repeat skill phase repeat count must be positive. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, repeatCount={repeatDto.RepeatCount}");
            }

            if (repeatDto.IntervalMs < 0)
            {
                throw new InvalidOperationException($"Repeat skill phase interval cannot be negative. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, intervalMs={repeatDto.IntervalMs}");
            }

            if (repeatDto.Phase == null)
            {
                throw new InvalidOperationException($"Repeat skill phase requires an explicit child phase. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
            }

            return new RepeatPhaseDefinition(
                MakePhaseId(phase, fallbackPhaseId),
                repeatDto.RepeatCount,
                repeatDto.IntervalMs > 0 ? repeatDto.IntervalMs / 1000f : 0f,
                BuildPhaseDefinition(repeatDto.Phase, checksPhaseId, timelinePhaseId, fallbackPhaseId + ".repeat"));
        }

        private static PhaseDefinition BuildDelayPhaseDefinition(SkillPhaseDTO phase, string fallbackPhaseId)
        {
            if (phase.Delay == null)
            {
                throw new InvalidOperationException($"Delay skill phase requires delay config. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
            }

            var delayMs = phase.Delay.DelayMs;
            if (delayMs < 0)
            {
                throw new InvalidOperationException($"Delay skill phase delay cannot be negative. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, delayMs={delayMs}");
            }

            return new DelayPhaseDefinition(MakePhaseId(phase, fallbackPhaseId), delayMs / 1000f);
        }

        private PhaseDefinition BuildWaitUntilPhaseDefinition(SkillPhaseDTO phase, string fallbackPhaseId)
        {
            if (phase.WaitUntil == null)
            {
                throw new InvalidOperationException($"WaitUntil skill phase requires wait config. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}");
            }

            var wait = phase.WaitUntil;
            if (wait.TimeoutMs < 0)
            {
                throw new InvalidOperationException($"WaitUntil skill phase timeout cannot be negative. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, timeoutMs={wait.TimeoutMs}");
            }

            if (!SkillWaitConditionCatalog.TryValidate(wait, out var validationError))
            {
                throw new InvalidOperationException($"Invalid WaitUntil skill phase condition. phaseId={MakePhaseId(phase, fallbackPhaseId).Value}, {validationError}");
            }

            SkillWaitConditionCatalog.TryGet(wait.Condition, out var condition);
            return new WaitUntilPhaseDefinition(
                MakePhaseId(phase, fallbackPhaseId),
                condition,
                wait,
                wait.TimeoutMs > 0 ? wait.TimeoutMs / 1000f : -1f,
                wait.CompleteOnTimeout);
        }

        private MobaTriggerPlanExecutor GetOrCreateRulePlanExecutor()
        {
            if (_rulePlanExecutor != null) return _rulePlanExecutor;
            if (_services == null) return null;

            _services.TryResolve<AbilityKit.Triggering.Eventing.IEventBus>(out var eventBus);
            _services.TryResolve<AbilityKit.Triggering.Registry.FunctionRegistry>(out var functions);
            _services.TryResolve<AbilityKit.Triggering.Registry.ActionRegistry>(out var actions);
            _services.TryResolve<AbilityKit.Triggering.Payload.IPayloadAccessorRegistry>(out var payloads);
            _services.TryResolve<AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanJsonDatabase>(out var planDb);
            _services.TryResolve<MobaEffectExecutionService>(out var effects);

            _rulePlanExecutor = new MobaTriggerPlanExecutor(_services, planDb, eventBus, functions, actions, payloads, effects);
            return _rulePlanExecutor;
        }

        private static AbilityPipelinePhaseId MakePhaseId(SkillPhaseDTO phase, string fallback)
        {
            return new AbilityPipelinePhaseId(!string.IsNullOrEmpty(phase?.PhaseId) ? phase.PhaseId : fallback);
        }

        private static SkillTimelineEventDTO[] ToArray(IReadOnlyList<SkillTimelineEventDTO> list)
        {
            if (list == null || list.Count == 0) return System.Array.Empty<SkillTimelineEventDTO>();
            var arr = new SkillTimelineEventDTO[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i];
            return arr;
        }

        private static int[] CopySlots(IReadOnlyList<int> slots)
        {
            if (slots == null || slots.Count == 0) return System.Array.Empty<int>();
            var arr = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) arr[i] = slots[i];
            return arr;
        }

        public void Dispose()
        {
            _skillCache.Clear();
        }

        private sealed class SkillPipelineCacheEntry
        {
            public SkillPipelineCacheEntry(
                IAbilityPipelineConfig preCastConfig,
                IReadOnlyList<PhaseDefinition> preCastPhases,
                IAbilityPipelineConfig castConfig,
                IReadOnlyList<PhaseDefinition> castPhases)
            {
                PreCastConfig = preCastConfig;
                PreCastPhases = preCastPhases;
                CastConfig = castConfig;
                CastPhases = castPhases;
            }

            public IAbilityPipelineConfig PreCastConfig { get; }
            public IReadOnlyList<PhaseDefinition> PreCastPhases { get; }
            public IAbilityPipelineConfig CastConfig { get; }
            public IReadOnlyList<PhaseDefinition> CastPhases { get; }
        }

        private abstract class PhaseDefinition
        {
            protected PhaseDefinition(AbilityPipelinePhaseId phaseId)
            {
                PhaseId = phaseId;
            }

            protected AbilityPipelinePhaseId PhaseId { get; }

            public abstract IAbilityPipelinePhase<SkillPipelineContext> CreatePhase();
        }

        private sealed class TimelinePhaseDefinition : PhaseDefinition
        {
            private readonly int _durationMs;
            private readonly SkillTimelineEventDTO[] _events;
            private readonly MobaEffectInvokerService _effects;

            public TimelinePhaseDefinition(AbilityPipelinePhaseId phaseId, int durationMs, SkillTimelineEventDTO[] events, MobaEffectInvokerService effects)
                : base(phaseId)
            {
                _durationMs = durationMs;
                _events = events;
                _effects = effects;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                return new SkillTimelinePhase(PhaseId, _durationMs, _events, _effects);
            }
        }

        private sealed class RulePlanPhaseDefinition : PhaseDefinition
        {
            private readonly SkillRulePlanPhaseDTO _rulePlan;
            private readonly MobaTriggerPlanExecutor _executor;

            public RulePlanPhaseDefinition(AbilityPipelinePhaseId phaseId, SkillRulePlanPhaseDTO rulePlan, MobaTriggerPlanExecutor executor)
                : base(phaseId)
            {
                _rulePlan = rulePlan;
                _executor = executor;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                return new SkillRulePlanPhase(PhaseId, _rulePlan, _executor);
            }
        }

        private abstract class CompositePhaseDefinition : PhaseDefinition
        {
            private readonly IReadOnlyList<PhaseDefinition> _children;

            protected CompositePhaseDefinition(AbilityPipelinePhaseId phaseId, IReadOnlyList<PhaseDefinition> children)
                : base(phaseId)
            {
                _children = children;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                var composite = CreateCompositePhase();
                for (int i = 0; i < _children.Count; i++)
                {
                    composite.AddSubPhase(_children[i].CreatePhase());
                }

                return composite;
            }

            protected abstract AbilityCompositePhase<SkillPipelineContext> CreateCompositePhase();
        }

        private sealed class SequencePhaseDefinition : CompositePhaseDefinition
        {
            public SequencePhaseDefinition(AbilityPipelinePhaseId phaseId, IReadOnlyList<PhaseDefinition> children)
                : base(phaseId, children)
            {
            }

            protected override AbilityCompositePhase<SkillPipelineContext> CreateCompositePhase()
            {
                return new AbilitySequencePhase<SkillPipelineContext>(PhaseId);
            }
        }

        private sealed class ParallelPhaseDefinition : CompositePhaseDefinition
        {
            public ParallelPhaseDefinition(AbilityPipelinePhaseId phaseId, IReadOnlyList<PhaseDefinition> children)
                : base(phaseId, children)
            {
            }

            protected override AbilityCompositePhase<SkillPipelineContext> CreateCompositePhase()
            {
                return new AbilityParallelPhase<SkillPipelineContext>(PhaseId);
            }
        }

        private sealed class RepeatPhaseDefinition : PhaseDefinition
        {
            private readonly int _repeatCount;
            private readonly float _repeatInterval;
            private readonly PhaseDefinition _child;

            public RepeatPhaseDefinition(AbilityPipelinePhaseId phaseId, int repeatCount, float repeatInterval, PhaseDefinition child)
                : base(phaseId)
            {
                _repeatCount = repeatCount;
                _repeatInterval = repeatInterval;
                _child = child;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                var repeat = new AbilityRepeatPhase<SkillPipelineContext>(PhaseId, _repeatCount)
                {
                    RepeatInterval = _repeatInterval
                };

                repeat.SetRepeatPhase(_child.CreatePhase());
                return repeat;
            }
        }

        private sealed class DelayPhaseDefinition : PhaseDefinition
        {
            private readonly float _delaySeconds;

            public DelayPhaseDefinition(AbilityPipelinePhaseId phaseId, float delaySeconds)
                : base(phaseId)
            {
                _delaySeconds = delaySeconds;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                return new AbilityDelayPhase<SkillPipelineContext>(PhaseId, _delaySeconds);
            }
        }

        private sealed class WaitUntilPhaseDefinition : PhaseDefinition
        {
            private readonly ISkillWaitCondition _condition;
            private readonly SkillWaitUntilPhaseDTO _specification;
            private readonly float _timeoutSeconds;
            private readonly bool _completeOnTimeout;

            public WaitUntilPhaseDefinition(
                AbilityPipelinePhaseId phaseId,
                ISkillWaitCondition condition,
                SkillWaitUntilPhaseDTO specification,
                float timeoutSeconds,
                bool completeOnTimeout)
                : base(phaseId)
            {
                _condition = condition ?? throw new ArgumentNullException(nameof(condition));
                _specification = specification ?? throw new ArgumentNullException(nameof(specification));
                _timeoutSeconds = timeoutSeconds;
                _completeOnTimeout = completeOnTimeout;
            }

            public override IAbilityPipelinePhase<SkillPipelineContext> CreatePhase()
            {
                return new AbilityWaitUntilPhase<SkillPipelineContext>(PhaseId, IsConditionMet, _timeoutSeconds, _completeOnTimeout);
            }

            private bool IsConditionMet(SkillPipelineContext context)
            {
                return _condition.IsMet(context, _specification);
            }
        }
    }
}

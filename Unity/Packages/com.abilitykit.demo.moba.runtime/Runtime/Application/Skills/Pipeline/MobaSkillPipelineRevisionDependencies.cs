using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaSkillPipelineRevisionDependencies
    {
        private const string RuntimeContract = "moba-skill-pipeline:1";
        private readonly SortedSet<int> _tagTemplates = new SortedSet<int>();
        private readonly SortedSet<int> _planIds = new SortedSet<int>();
        private bool _unsupported;

        internal void Collect(SkillPhaseDTO[] phases, int pipelineTemplateId)
        {
            if (pipelineTemplateId > 0) _tagTemplates.Add(pipelineTemplateId);
            foreach (var phase in phases) CollectPhase(phase);
        }

        private void CollectPhase(SkillPhaseDTO phase)
        {
            if (phase == null) return;
            switch ((SkillPhaseType)phase.Type)
            {
                case SkillPhaseType.Timeline:
                    if (phase.Timeline?.CastGateTagTemplateId > 0) _tagTemplates.Add(phase.Timeline.CastGateTagTemplateId);
                    if (phase.Timeline?.Events != null)
                        foreach (var effect in phase.Timeline.Events)
                            if (effect?.EffectId > 0) _planIds.Add(effect.EffectId);
                    break;
                case SkillPhaseType.RulePlan:
                    CollectPlans(phase.RulePlan?.TriggerIds);
                    break;
                case SkillPhaseType.Window:
                    CollectPlans(phase.Window?.OpenTriggerIds);
                    CollectPlans(phase.Window?.TickTriggerIds);
                    CollectPlans(phase.Window?.CloseTriggerIds);
                    break;
                case SkillPhaseType.Sequence:
                case SkillPhaseType.Parallel:
                case SkillPhaseType.Race:
                    if (phase.Children != null)
                        foreach (var child in phase.Children) CollectPhase(child);
                    break;
                case SkillPhaseType.Repeat:
                    CollectPhase(phase.Repeat?.Phase);
                    break;
                case SkillPhaseType.WaitUntil:
                    _unsupported |= !SkillWaitConditionCatalog.TryGet(phase.WaitUntil?.Condition, out var condition) ||
                        !(condition is ObservedSlotsIdleSkillWaitCondition || condition is InputReleasedSkillWaitCondition);
                    break;
                case SkillPhaseType.DerivedSkill:
                    _unsupported = true;
                    break;
            }
        }

        private void CollectPlans(int[] plans)
        {
            if (plans == null) return;
            foreach (var id in plans) if (id > 0) _planIds.Add(id);
        }

        internal MobaSkillPipelineRevision Bind(IWorldResolver services, string content)
        {
            if (_unsupported) return null;
            var evidence = new JArray(RuntimeContract, content);
            MobaContinuousTagTemplateRegistry templates = null;
            var templateVersions = new Dictionary<int, string>();
            if (_tagTemplates.Count > 0)
            {
                if (services == null || !services.TryResolve<IMobaContinuousTagTemplateRegistry>(out var registry) ||
                    !(registry is MobaContinuousTagTemplateRegistry tableRegistry)) return null;
                templates = tableRegistry;
                foreach (var id in _tagTemplates)
                {
                    var revision = templates.GetDefinitionRevision(id);
                    if (revision == null) return null;
                    templateVersions.Add(id, revision);
                    evidence.Add(new JArray("tag", id, revision));
                }
            }

            TriggerPlanJsonDatabase plans = null;
            ActionRegistry actions = null;
            FunctionRegistry functions = null;
            if (_planIds.Count > 0)
            {
                if (services == null || !services.TryResolve(out plans) || plans?.ContentRevision == null) return null;
                foreach (var id in _planIds)
                    if (!plans.TryGetRecordByTriggerId(id, out _)) return null;
                services.TryResolve(out actions);
                services.TryResolve(out functions);
                evidence.Add(new JArray("plans", plans.ContentRevision,
                    actions?.RegistrationVersion, functions?.RegistrationVersion));
            }

            var templateSourceVersion = templates?.SourceVersion;
            var plansVersion = plans?.MutationVersion;
            var actionVersion = actions?.RegistrationVersion;
            var functionVersion = functions?.RegistrationVersion;
            return new MobaSkillPipelineRevision(MobaSkillPipelineRevision.Digest(evidence), () =>
            {
                if (templates?.SourceVersion != templateSourceVersion || plans?.MutationVersion != plansVersion ||
                    actions?.RegistrationVersion != actionVersion || functions?.RegistrationVersion != functionVersion) return false;
                foreach (var template in templateVersions)
                    if (templates.GetDefinitionRevision(template.Key) != template.Value) return false;
                return true;
            });
        }
    }
}

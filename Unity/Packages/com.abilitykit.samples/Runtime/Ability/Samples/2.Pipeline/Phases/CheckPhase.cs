#nullable enable

using System;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Samples.Pipeline.Phases
{
    /// <summary>
    /// 技能检查阶段，在执行前检查技能是否可用。
    /// </summary>
    public sealed class CheckPhase : IPipelinePhase
    {
        public string PhaseId => "skill_check";

        public int Priority => 0;

        public PhaseResult Execute(IPipelineContext context)
        {
            if (context is SkillContext skillContext)
            {
                Console.WriteLine($"[CheckPhase] Checking skill {skillContext.SkillId} for caster {skillContext.CasterId}");

                if (skillContext.TargetId.HasValue)
                {
                    Console.WriteLine($"[CheckPhase] Target: {skillContext.TargetId.Value}");
                }

                return PhaseResult.Success;
            }

            return PhaseResult.Failure;
        }

        public System.Threading.Tasks.Task<PhaseResult> ExecuteAsync(IPipelineContext context)
        {
            return System.Threading.Tasks.Task.FromResult(Execute(context));
        }
    }
}

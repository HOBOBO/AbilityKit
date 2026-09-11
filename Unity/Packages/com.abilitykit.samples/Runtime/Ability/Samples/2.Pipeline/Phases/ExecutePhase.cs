#nullable enable

using System;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Samples.Pipeline.Phases
{
    /// <summary>
    /// 技能执行阶段，执行技能的核心效果。
    /// </summary>
    public sealed class ExecutePhase : IPipelinePhase
{
        public string PhaseId => "skill_execute";

        public int Priority => 50;

        public PhaseResult Execute(IPipelineContext context)
        {
            if (context is SkillContext skillContext)
            {
                Console.WriteLine($"[ExecutePhase] Executing skill {skillContext.SkillId}");

                skillContext.SetData("execution_time", DateTime.Now);

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

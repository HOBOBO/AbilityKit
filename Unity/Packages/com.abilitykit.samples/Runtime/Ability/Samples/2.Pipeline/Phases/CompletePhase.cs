#nullable enable

using System;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Samples.Pipeline.Phases
{
    /// <summary>
    /// 技能完成阶段，清理资源并发送完成事件。
    /// </summary>
    public sealed class CompletePhase : IPipelinePhase
    {
        public string PhaseId => "skill_complete";

        public int Priority => 100;

        public PhaseResult Execute(IPipelineContext context)
        {
            if (context is SkillContext skillContext)
            {
                var executionTime = skillContext.GetData<DateTime>("execution_time");
                Console.WriteLine($"[CompletePhase] Skill {skillContext.SkillId} completed. Execution took {(DateTime.Now - executionTime).TotalMilliseconds}ms");

                skillContext.RemoveData("execution_time");

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

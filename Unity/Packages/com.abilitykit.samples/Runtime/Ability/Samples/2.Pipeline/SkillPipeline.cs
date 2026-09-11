#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Samples.Pipeline
{
    /// <summary>
    /// 技能管线示例，演示如何使用 Pipeline 框架实现技能释放流程。
    /// </summary>
    public sealed class SkillPipeline : IPipeline
    {
        private readonly List<IPipelinePhase> _phases;

        public SkillPipeline()
        {
            _phases = new List<IPipelinePhase>();
        }

        public string PipelineId => "skill_pipeline";
        public string DisplayName => "技能管线";

        public IReadOnlyList<IPipelinePhase> Phases => _phases.AsReadOnly();

        /// <summary>
        /// 添加阶段。
        /// </summary>
        public void AddPhase(IPipelinePhase phase)
        {
            if (phase == null)
            {
                throw new ArgumentNullException(nameof(phase));
            }

            _phases.Add(phase);
            _phases.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 执行技能管线。
        /// </summary>
        public PipelineResult Execute(IPipelineContext context)
        {
            foreach (var phase in _phases)
            {
                var result = phase.Execute(context);
                switch (result)
                {
                    case PhaseResult.Failure:
                        return PipelineResult.Failure;
                    case PhaseResult.Pending:
                        return PipelineResult.Cancelled;
                }
            }

            return PipelineResult.Success;
        }
    }
}

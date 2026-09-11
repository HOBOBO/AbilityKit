#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Samples.Logic.Ability.Core.Pipeline
{
    /// <summary>
    /// 管线执行器，负责运行和管理管线执行流程。
    /// </summary>
    public sealed class PipelineRunner
    {
        public PipelineRunner()
        {
        }

        /// <summary>
        /// 运行指定管线。
        /// </summary>
        public PipelineResult Run(IPipeline pipeline, IPipelineContext context)
        {
            if (pipeline == null)
                return PipelineResult.Failure;

            return pipeline.Execute(context);
        }

        /// <summary>
        /// 异步运行指定管线。
        /// </summary>
        public async Task<PipelineResult> RunAsync(
            IPipeline pipeline,
            IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                return PipelineResult.Failure;

            var phases = pipeline.Phases;
            foreach (var phase in phases)
            {
                if (cancellationToken.IsCancellationRequested)
                    return PipelineResult.Cancelled;

                var result = await phase.ExecuteAsync(context);
                switch (result)
                {
                    case PhaseResult.Failure:
                        return PipelineResult.Failure;
                    case PhaseResult.Pending:
                        return PipelineResult.Pending;
                    case PhaseResult.Skip:
                        continue;
                }
            }

            return PipelineResult.Success;
        }

        /// <summary>
        /// 运行一组管线。
        /// </summary>
        public PipelineResult RunAll(IEnumerable<IPipeline> pipelines, IPipelineContext context)
        {
            foreach (var pipeline in pipelines)
            {
                var result = Run(pipeline, context);
                if (result != PipelineResult.Success)
                    return result;
            }
            return PipelineResult.Success;
        }
    }
}

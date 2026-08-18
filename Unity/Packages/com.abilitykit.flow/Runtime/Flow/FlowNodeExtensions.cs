using System;
using AbilityKit.Ability.Flow.Pooling;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// IFlowNode 的正式执行扩展入口。适合一次性同步执行节点图，并自动管理 Runner 与 Context 生命周期。
    /// </summary>
    public static class FlowNodeExtensions
    {
        /// <summary>
        /// 同步执行节点直到终态或达到最大步数。执行期间会租借并释放池化 FlowRunner。
        /// </summary>
        public static FlowExecutionResult Execute(this IFlowNode root, FlowExecutionOptions options = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            options = options ?? FlowExecutionOptions.Default;
            Exception? capturedException = null;
            var runner = FlowPools.RentRunner();

            try
            {
                runner.ExceptionHandler = ex =>
                {
                    if (capturedException == null)
                    {
                        capturedException = ex;
                    }

                    options.ExceptionHandler?.Invoke(ex);
                };

                runner.Observer = options.Observer;
                runner.TraceRecorder = options.TraceRecorder;
                runner.Start(root);

                var steps = 0;
                while (runner.Status == FlowStatus.Running)
                {
                    if (options.MaxSteps > 0 && steps >= options.MaxSteps)
                    {
                        capturedException = new InvalidOperationException($"Flow execution step limit exceeded: limit={options.MaxSteps}");
                        runner.Stop();
                        break;
                    }

                    steps++;
                    runner.Step(options.DeltaTime);
                }

                if (capturedException != null && options.RethrowExceptions)
                {
                    throw capturedException;
                }

                return new FlowExecutionResult(runner.Status, steps, capturedException);
            }
            finally
            {
                FlowPools.ReleaseRunner(runner);
            }
        }
    }
}

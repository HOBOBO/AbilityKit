using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Context;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 行为执行器
    /// </summary>
    public static class ExecutableExecutor
    {
        public static ExecutionResult Execute(ISimpleExecutable executable, ActionContext ctx)
        {
            if (executable == null)
                return ExecutionResult.None;

            if (executable is IConditionalExecutable conditional)
            {
                return ExecuteConditional(conditional, ctx);
            }

            try
            {
                return executable.Execute(ctx);
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"Execute[{executable.Name}]: {ex.Message}");
            }
        }

        private static ExecutionResult ExecuteConditional(IConditionalExecutable executable, ActionContext ctx)
        {
            int matchedIndex = executable.EvaluateConditionIndex(ctx);

            if (matchedIndex >= 0 && matchedIndex < executable.ChildCount)
            {
                var child = executable.GetChild(matchedIndex);
                if (child != null)
                {
                    if (child is IConditionalExecutable childConditional)
                    {
                        return ExecuteConditional(childConditional, ctx);
                    }

                    try
                    {
                        return child.Execute(ctx);
                    }
                    catch (Exception ex)
                    {
                        return ExecutionResult.Failed($"Conditional[{matchedIndex}][{child.Name}]: {ex.Message}");
                    }
                }
            }

            return ExecutionResult.Skipped("No matching branch");
        }

        public static ExecutionResult ExecuteAll(
            IEnumerable<ISimpleExecutable> executables,
            ActionContext ctx)
        {
            var result = ExecutionResult.None;

            foreach (var executable in executables)
            {
                if (executable == null) continue;
                var execResult = Execute(executable, ctx);
                result = result.Merge(execResult);
            }

            return result;
        }
    }
}

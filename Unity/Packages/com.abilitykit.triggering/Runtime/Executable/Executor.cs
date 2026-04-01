using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 调度执行器
    /// </summary>
    public sealed class ScheduledExecutor
    {
        private readonly Dictionary<long, ControllerEntry> _controllers = new();
        private long _nextHandleId;

        public long Start(
            IScheduledExecutable executable,
            object ctx,
            Action<long> onCompleted = null,
            Action<long, string> onInterrupted = null)
        {
            var handleId = ++_nextHandleId;
            var controller = ScheduledExecutableFactory.CreateController(executable, ctx);

            _controllers[handleId] = new ControllerEntry
            {
                Executable = executable,
                Controller = controller,
                Context = ctx,
                OnCompleted = onCompleted,
                OnInterrupted = onInterrupted
            };

            return handleId;
        }

        public bool Interrupt(long handleId, string reason)
        {
            if (!_controllers.TryGetValue(handleId, out var entry))
                return false;

            if (entry.Controller.IsCompleted || entry.Controller.IsInterrupted)
                return false;

            if (entry.Executable is ExternalControlledExecutable ext && !ext.CanBeInterrupted)
                return false;

            entry.Controller.RequestInterrupt(reason);
            return true;
        }

        public bool TryGetHandle(long handleId, out ControllerEntry entry)
        {
            return _controllers.TryGetValue(handleId, out entry);
        }

        public void Update(float deltaTimeMs)
        {
            var completed = new List<long>();

            foreach (var kvp in _controllers)
            {
                var entry = kvp.Value;

                if (entry.Controller.IsCompleted)
                {
                    completed.Add(kvp.Key);
                    entry.OnCompleted?.Invoke(kvp.Key);
                }
                else if (entry.Controller.IsInterrupted)
                {
                    completed.Add(kvp.Key);
                    entry.OnInterrupted?.Invoke(kvp.Key, entry.Controller.InterruptionReason);
                }
                else
                {
                    entry.Controller.Update(deltaTimeMs);
                }
            }

            foreach (var handleId in completed)
            {
                _controllers.Remove(handleId);
            }
        }

        public void InterruptAll(string reason)
        {
            foreach (var kvp in _controllers)
            {
                var entry = kvp.Value;
                if (entry.Executable is ExternalControlledExecutable ext && !ext.CanBeInterrupted) continue;

                if (!entry.Controller.IsCompleted && !entry.Controller.IsInterrupted)
                {
                    entry.Controller.RequestInterrupt(reason);
                    entry.OnInterrupted?.Invoke(kvp.Key, reason);
                }
            }

            _controllers.Clear();
        }

        public int ActiveCount => _controllers.Count;
        public bool HasActive => _controllers.Count > 0;

        public struct ControllerEntry
        {
            public IScheduledExecutable Executable;
            public IScheduleController Controller;
            public object Context;
            public Action<long> OnCompleted;
            public Action<long, string> OnInterrupted;
        }
    }

    /// <summary>
    /// 行为执行器
    /// </summary>
    public static class ExecutableExecutor
    {
        public static ExecutionResult Execute(ISimpleExecutable executable, object ctx)
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

        private static ExecutionResult ExecuteConditional(IConditionalExecutable executable, object ctx)
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
            object ctx)
        {
            var result = ExecutionResult.None;

            foreach (var executable in executables)
            {
                if (executable == null) continue;
                var r = Execute(executable, ctx);
                result = result.Merge(r);
            }

            return result;
        }
    }
}

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 在一次收尾中尽力执行各项清理，并保留按执行顺序发生的异常。
    /// </summary>
    internal struct FlowCleanup
    {
        private Exception? _firstError;
        private List<Exception>? _errors;

        internal void CloseNode(FlowContext context, IFlowNode node, bool interrupt, FlowStatus status)
        {
            try
            {
                if (interrupt) FlowDiagnostics.Interrupt(context, node, status);
                else FlowDiagnostics.Exit(context, node, status);
            }
            catch (Exception exception)
            {
                AddError(exception);
            }
        }

        internal void CloseRunningChildren(FlowContext context, IFlowNode[] nodes, FlowStatus[] statuses, bool interrupt)
        {
            var terminal = interrupt ? FlowStatus.Canceled : FlowStatus.Succeeded;
            for (var index = 0; index < nodes.Length; index++)
            {
                if (statuses[index] != FlowStatus.Running) continue;
                statuses[index] = terminal;
                CloseNode(context, nodes[index], interrupt, terminal);
            }
        }

        internal void AddError(Exception exception)
        {
            if (_firstError == null)
            {
                _firstError = exception;
                return;
            }

            _errors ??= new List<Exception> { _firstError };
            _errors.Add(exception);
        }

        internal void ThrowIfFailed()
        {
            if (_errors != null) throw new AggregateException(_errors);
            if (_firstError != null) ExceptionDispatchInfo.Capture(_firstError).Throw();
        }
    }
}

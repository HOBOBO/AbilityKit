using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services.LogicWorld
{
    /// <summary>
    /// 逻辑世界输入协调器基类。
    /// 框架负责输入批次遍历、生命周期接入、上下文创建时机和命令路由顺序；具体逻辑层只实现上下文和命令处理。
    /// </summary>
    public abstract class LogicWorldInputCoordinatorBase<TContext> : IService, IWorldInitializable, ILogicWorldInputCoordinator where TContext : class
    {
        private IFrameTime _frameTime;
        private IMobaBattleDiagnosticsService _diagnostics;
        private bool _missingFrameTimeLogged;
        private bool _futureFrameLogged;

        protected IWorldResolver Services { get; private set; }

        public void OnInit(IWorldResolver services)
        {
            Services = services;
            services?.TryResolve(out _frameTime);
            services?.TryResolve(out _diagnostics);
            OnServicesReady(services);
        }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            TrySubmit(frame, inputs);
        }

        public LogicWorldInputSubmitResult TrySubmit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
            => TrySubmitWithContext(frame, inputs, null);

        /// <summary>
        /// 在保留批次校验和派发规则的前提下使用调用者指定的执行上下文。
        /// 未提供上下文时从当前世界创建默认上下文。
        /// </summary>
        protected LogicWorldInputSubmitResult TrySubmitWithContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, TContext context,
            Action<int> beforeCommand = null, Action<int, MobaInputCommandResult> afterCommand = null)
        {
            if (inputs == null || inputs.Count == 0)
            {
                const string message = "input command batch is null or empty";
                MobaInputDiagnostics.RecordBatchWarning(_diagnostics, "input.batch.empty", message, GetType().Name);
                return LogicWorldInputSubmitResult.Rejected(LogicWorldInputSubmitFailureCode.NullOrEmptyCommands, message);
            }

            if (!CanSubmit(frame, inputs))
            {
                var message = $"input batch rejected by frame validation: targetFrame={frame.Value}, count={inputs.Count}";
                MobaInputDiagnostics.RecordBatchWarning(_diagnostics, "input.batch.frameRejected", message, GetType().Name);
                return LogicWorldInputSubmitResult.Rejected(LogicWorldInputSubmitFailureCode.FrameRejected, message);
            }

            if (!ValidateCommandFrames(frame, inputs, out var commandFrameError))
            {
                MobaInputDiagnostics.RecordBatchWarning(_diagnostics, "input.batch.commandFrameMismatch", commandFrameError, GetType().Name);
                return LogicWorldInputSubmitResult.Rejected(LogicWorldInputSubmitFailureCode.CommandFrameMismatch, commandFrameError);
            }

            context ??= CreateContext(frame, inputs);
            if (context == null)
            {
                const string message = "input context creation returned null";
                MobaInputDiagnostics.RecordBatchWarning(_diagnostics, "input.batch.contextMissing", message, GetType().Name);
                return LogicWorldInputSubmitResult.Rejected(LogicWorldInputSubmitFailureCode.ContextMissing, message);
            }

            var handledCount = 0;
            MobaInputBatchDispatchTrace dispatchTrace = default;
            for (int i = 0; i < inputs.Count; i++)
            {
                PlayerInputCommand command = inputs[i];
                beforeCommand?.Invoke(i);
                bool handledBeforeDispatch = TryHandleBeforeDispatch(context, frame, command);
                bool handledByDispatch = false;
                if (handledBeforeDispatch)
                {
                    handledCount++;
                }
                else
                {
                    MobaInputCommandResult commandResult;
                    try
                    {
                        handledByDispatch = Dispatch(context, frame, command, out commandResult);
                    }
                    catch (Exception ex)
                    {
                        MobaInputDiagnostics.RecordCommandException(_diagnostics, frame, command, ex, GetType().Name);
                        return LogicWorldInputSubmitResult.Rejected(
                            LogicWorldInputSubmitFailureCode.HandlerException,
                            $"Input command handler threw. frame={frame.Value}, player={command.Player.Value}, op={command.OpCode}, exception={ex.GetType().Name}: {ex.Message}");
                    }

                    if (handledByDispatch)
                    {
                        handledCount++;
                    }
                    else
                    {
                        MobaInputDiagnostics.RecordCommandRejected(_diagnostics, frame, in commandResult, GetType().Name);
                    }

                    afterCommand?.Invoke(i, commandResult);
                    dispatchTrace.Record(i, handledBeforeDispatch, handledByDispatch, in commandResult);
                    continue;
                }

                afterCommand?.Invoke(i, default);
                dispatchTrace.Record(i, handledBeforeDispatch, handledByDispatch);
            }

            if (handledCount < inputs.Count)
            {
                var owner = GetType().Name;
                string message = FormatCompactBatchMessage(owner, frame, inputs.Count, handledCount);
                if (handledCount == 0)
                {
                    MobaInputDiagnostics.RecordBatchWarning(
                        _diagnostics,
                        "input.batch.noCommandHandled",
                        () => "Input batch accepted but no command was handled. " + FormatDetailedBatchMessage(owner, frame, inputs, handledCount, in dispatchTrace),
                        owner);
                }
                else
                {
                    MobaInputDiagnostics.RecordBatchWarning(
                        _diagnostics,
                        "input.batch.partialCommandHandled",
                        () => "Input batch partially handled. " + FormatDetailedBatchMessage(owner, frame, inputs, handledCount, in dispatchTrace),
                        owner);
                }

                return LogicWorldInputSubmitResult.Accepted(inputs.Count, handledCount, message);
            }

            MobaInputDiagnostics.RecordBatchAccepted(_diagnostics, inputs.Count, handledCount);
            return LogicWorldInputSubmitResult.Accepted(inputs.Count, handledCount);
        }

        protected virtual bool CanSubmit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (frame.Value < 0)
            {
                MobaInputDiagnostics.RecordBatchWarning(
                    _diagnostics,
                    "input.batch.negativeFrame",
                    () => $"Input batch rejected: targetFrame={frame.Value} is negative, count={inputs.Count}.",
                    GetType().Name);
                return false;
            }

            if (_frameTime == null)
            {
                if (!_missingFrameTimeLogged)
                {
                    _missingFrameTimeLogged = true;
                    MobaInputDiagnostics.RecordBatchWarning(_diagnostics, "input.batch.frameValidationMissing", "Input batch rejected: IFrameTime is required for deterministic frame validation.", GetType().Name);
                }

                return false;
            }

            int currentFrame = _frameTime.Frame.Value;
            if (frame.Value < currentFrame)
            {
                MobaInputDiagnostics.RecordBatchWarning(
                    _diagnostics,
                    "input.batch.pastFrame",
                    () => $"Input batch rejected: targetFrame={frame.Value}, currentFrame={currentFrame}, count={inputs.Count}.",
                    GetType().Name);
                return false;
            }

            if (frame.Value > currentFrame + 1 && !_futureFrameLogged)
            {
                _futureFrameLogged = true;
                MobaInputDiagnostics.RecordBatchWarning(
                    _diagnostics,
                    "input.batch.futureFrame",
                    () => $"Input batch accepted with future target frame: targetFrame={frame.Value}, currentFrame={currentFrame}, count={inputs.Count}.",
                    GetType().Name);
            }

            return true;
        }

        private static bool ValidateCommandFrames(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, out string error)
        {
            error = null;
            for (int i = 0; i < inputs.Count; i++)
            {
                PlayerInputCommand command = inputs[i];
                if (command.Frame.Value == frame.Value) continue;

                error = $"Input batch rejected: command frame mismatch. targetFrame={frame.Value}, commandIndex={i}, commandFrame={command.Frame.Value}, player={command.Player.Value}, op={command.OpCode}.";
                return false;
            }

            return true;
        }

        protected virtual void OnServicesReady(IWorldResolver services)
        {
        }

        protected abstract TContext CreateContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);

        protected virtual bool TryHandleBeforeDispatch(TContext context, FrameIndex frame, PlayerInputCommand command)
        {
            return false;
        }

        protected abstract bool Dispatch(TContext context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result);

        private static string FormatCompactBatchMessage(string owner, FrameIndex frame, int inputCount, int handledCount)
        {
            return $"Coordinator={owner}, Frame={frame.Value}, Count={inputCount}, Handled={handledCount}";
        }

        private static string FormatDetailedBatchMessage(string owner, FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, int handledCount, in MobaInputBatchDispatchTrace dispatchTrace)
        {
            return $"{FormatCompactBatchMessage(owner, frame, inputs.Count, handledCount)}, Commands={FormatCommands(inputs)}, DispatchTrace={dispatchTrace.Format()}";
        }

        private static string FormatCommands(IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (inputs == null || inputs.Count == 0) return "empty";

            var text = string.Empty;
            for (int i = 0; i < inputs.Count; i++)
            {
                PlayerInputCommand command = inputs[i];
                if (i > 0) text += ";";
                text += $"#{i}:Player={command.Player.Value},Op={command.OpCode},Payload={command.Payload?.Length ?? 0}";
            }

            return text;
        }

        private struct MobaInputBatchDispatchTrace
        {
            private int _count;
            private int _lastIndex;
            private bool _lastBeforeDispatch;
            private bool _lastDispatch;
            private MobaInputCommandResult _lastResult;
            private bool _hasLastResult;

            public void Record(int index, bool beforeDispatch, bool dispatch)
            {
                _count++;
                _lastIndex = index;
                _lastBeforeDispatch = beforeDispatch;
                _lastDispatch = dispatch;
                _lastResult = default;
                _hasLastResult = false;
            }

            public void Record(int index, bool beforeDispatch, bool dispatch, in MobaInputCommandResult result)
            {
                _count++;
                _lastIndex = index;
                _lastBeforeDispatch = beforeDispatch;
                _lastDispatch = dispatch;
                _lastResult = result;
                _hasLastResult = !string.IsNullOrEmpty(result.Message);
            }

            public string Format()
            {
                if (_count <= 0) return "empty";

                var text = $"count={_count},last=#{_lastIndex}:Before={_lastBeforeDispatch},Dispatch={_lastDispatch}";
                if (_hasLastResult)
                {
                    text += ",Result=" + _lastResult;
                }

                return text;
            }
        }

        public virtual void Dispose()
        {
        }
    }
}

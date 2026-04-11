using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 原子行为实现
    // ========================================================================

    /// <summary>
    /// Action 调用行为
    /// </summary>
    public sealed class ActionCallExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "ActionCall";
        public ExecutableMetadata Metadata => new(100, "ActionCall");

        public ActionId ActionId { get; set; }
        public NumericValueRef Arg0 { get; set; }
        public NumericValueRef Arg1 { get; set; }
        public int Arity { get; set; }
        public ActionRegistry Actions { get; set; }

        public ExecutionResult Execute(object ctx)
        {
            try
            {
                switch (Arity)
                {
                    case 0:
                        if (Actions.TryGet<Action<object>>(ActionId, out var action0, out _))
                            action0(ctx);
                        break;
                    case 1:
                        if (Actions.TryGet<Action<object, double>>(ActionId, out var action1, out _))
                            action1(ctx, Arg0.Resolve(ctx));
                        break;
                    case 2:
                        if (Actions.TryGet<Action<object, double, double>>(ActionId, out var action2, out _))
                            action2(ctx, Arg0.Resolve(ctx), Arg1.Resolve(ctx));
                        break;
                }
                return ExecutionResult.Success();
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"ActionCall[{ActionId}]: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 延迟行为 (瞬时返回，实际延迟由调度器控制)
    /// </summary>
    public sealed class DelayExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "Delay";
        public ExecutableMetadata Metadata => new(200, "Delay");

        public float DelayMs { get; set; }
        public float ActualDelayMs { get; private set; }

        public ExecutionResult Execute(object ctx)
        {
            ActualDelayMs = DelayMs;
            return ExecutionResult.Success();
        }
    }

    /// <summary>
    /// 空行为
    /// </summary>
    public sealed class NoOpExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public static readonly NoOpExecutable Instance = new();

        public string Name => "NoOp";
        public ExecutableMetadata Metadata => new(0, "NoOp");

        public ExecutionResult Execute(object ctx)
            => ExecutionResult.Success(0);
    }

    /// <summary>
    /// 失败行为
    /// </summary>
    public sealed class FailExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public static readonly FailExecutable Instance = new();

        public string Name => "Fail";
        public ExecutableMetadata Metadata => new(1, "Fail");

        public string Reason { get; set; }

        public ExecutionResult Execute(object ctx)
            => ExecutionResult.Failed(Reason ?? "Explicit failure");
    }

    /// <summary>
    /// 成功行为
    /// </summary>
    public sealed class SuccessExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public static readonly SuccessExecutable Instance = new();

        public string Name => "Success";
        public ExecutableMetadata Metadata => new(2, "Success");

        public ExecutionResult Execute(object ctx)
            => ExecutionResult.Success(0);
    }

    /// <summary>
    /// 等待行为 (用于并行调度)
    /// </summary>
    public sealed class WaitExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "Wait";
        public ExecutableMetadata Metadata => new(201, "Wait");

        public float DurationMs { get; set; }
        private float _elapsed;

        public ExecutionResult Execute(object ctx)
        {
            _elapsed = 0f;
            return ExecutionResult.Success();
        }

        public void Update(float deltaTimeMs)
        {
            _elapsed += deltaTimeMs;
        }

        public bool IsCompleted => _elapsed >= DurationMs;
    }

    /// <summary>
    /// 事件发送行为
    /// </summary>
    public sealed class EventSendExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "EventSend";
        public ExecutableMetadata Metadata => new(300, "EventSend");

        public string EventName { get; set; }
        public ActionRegistry Events { get; set; }

        public ExecutionResult Execute(object ctx)
        {
            try
            {
                // 将字符串事件名转换为 int 进行查找
                int eventId = EventName?.GetHashCode() ?? 0;
                if (Events.TryGet<Action<object>>(new ActionId(eventId), out var action, out _))
                {
                    action(ctx);
                }
                return ExecutionResult.Success();
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"EventSend[{EventName}]: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 打印调试行为
    /// </summary>
    public sealed class DebugLogExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "DebugLog";
        public ExecutableMetadata Metadata => new(999, "DebugLog");

        public string Message { get; set; }
        public bool LogToConsole { get; set; } = true;

        public ExecutionResult Execute(object ctx)
        {
            if (LogToConsole)
            {
                Log.Info($"[Triggering] {Message}");
            }
            return ExecutionResult.Success();
        }
    }
}

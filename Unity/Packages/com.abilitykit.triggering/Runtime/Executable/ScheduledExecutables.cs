using System;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 调度行为实现
    // ========================================================================

    /// <summary>
    /// 定时行为包装器
    /// </summary>
    public sealed class TimedExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public string Name => $"Timed({Inner?.Name ?? "null"})";
        public ExecutableMetadata Metadata => new(1000, "Timed", isScheduled: true);
        public EScheduleMode ScheduleMode => EScheduleMode.Timed;
        public bool IsPeriodic => false;
        public float PeriodMs => 0;
        public float DurationMs { get; set; }

        public ISimpleExecutable Inner { get; set; }
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(object ctx)
        {
            Inner?.Execute(ctx);
            return ExecutionResult.Success();
        }
    }

    /// <summary>
    /// 周期行为包装器
    /// </summary>
    public class PeriodicExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public string Name => $"Periodic({Inner?.Name ?? "null"})";
        public ExecutableMetadata Metadata => new(1001, "Periodic", isScheduled: true);
        public EScheduleMode ScheduleMode => EScheduleMode.Periodic;
        public bool IsPeriodic => true;
        public float PeriodMs { get; set; } = 1000f;
        public float DurationMs { get; set; } = -1;

        public ISimpleExecutable Inner { get; set; }
        public int MaxExecutions { get; set; } = -1;
        public bool CanBeInterrupted { get; set; } = true;

        public event Action<object> OnPeriodExecuted;

        public ExecutionResult Execute(object ctx)
        {
            Inner?.Execute(ctx);
            return ExecutionResult.Success();
        }

        internal void NotifyPeriodExecuted(object ctx)
        {
            OnPeriodExecuted?.Invoke(ctx);
        }
    }

    /// <summary>
    /// 外部控制行为包装器
    /// </summary>
    public sealed class ExternalControlledExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public string Name => $"External({Inner?.Name ?? "null"})";
        public ExecutableMetadata Metadata => new(1002, "ExternalControlled", isScheduled: true);
        public EScheduleMode ScheduleMode => EScheduleMode.External;
        public bool IsPeriodic => false;
        public float PeriodMs => 0;
        public float DurationMs => -1;

        public ISimpleExecutable Inner { get; set; }
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(object ctx)
        {
            Inner?.Execute(ctx);
            return ExecutionResult.Success();
        }
    }

    // ========================================================================
    // 调度控制器实现
    // ========================================================================

    /// <summary>
    /// 定时调度控制器
    /// </summary>
    public sealed class TimedScheduleController : IScheduleController
    {
        private readonly TimedExecutable _owner;
        private readonly object _ctx;
        private readonly float _durationMs;
        private float _elapsed;

        public bool IsCompleted => _elapsed >= _durationMs;
        public bool IsInterrupted { get; private set; }
        public string InterruptionReason { get; private set; }

        public TimedScheduleController(TimedExecutable owner, object ctx, float durationMs)
        {
            _owner = owner;
            _ctx = ctx;
            _durationMs = durationMs;
            _elapsed = 0f;
        }

        public void Update(float deltaTimeMs)
        {
            if (IsCompleted || IsInterrupted) return;
            _elapsed += deltaTimeMs;
        }

        public void RequestInterrupt(string reason)
        {
            if (!_owner.CanBeInterrupted) return;
            IsInterrupted = true;
            InterruptionReason = reason;
        }
    }

    /// <summary>
    /// 周期调度控制器
    /// </summary>
    public sealed class PeriodicScheduleController : IScheduleController
    {
        private readonly PeriodicExecutable _owner;
        private readonly object _ctx;
        private readonly float _periodMs;
        private readonly float _durationMs;
        private readonly int _maxExecutions;
        private float _elapsed;
        private int _executionCount;

        public bool IsCompleted
        {
            get
            {
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) return true;
                if (_durationMs > 0 && _elapsed >= _durationMs) return true;
                return false;
            }
        }

        public bool IsInterrupted { get; private set; }
        public string InterruptionReason { get; private set; }

        public PeriodicScheduleController(PeriodicExecutable owner, object ctx, float periodMs, float durationMs, int maxExecutions)
        {
            _owner = owner;
            _ctx = ctx;
            _periodMs = periodMs;
            _durationMs = durationMs;
            _maxExecutions = maxExecutions;
            _elapsed = 0f;
            _executionCount = 0;
        }

        public void Update(float deltaTimeMs)
        {
            if (IsCompleted || IsInterrupted) return;

            _elapsed += deltaTimeMs;

            while (_elapsed >= _periodMs)
            {
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) break;
                if (_durationMs > 0 && _elapsed > _durationMs) break;

                _elapsed -= _periodMs;
                _owner.Inner?.Execute(_ctx);
                _owner.NotifyPeriodExecuted(_ctx);
                _executionCount++;
            }
        }

        public void RequestInterrupt(string reason)
        {
            if (!_owner.CanBeInterrupted) return;
            IsInterrupted = true;
            InterruptionReason = reason;
        }
    }

    // ========================================================================
    // 调度行为工厂
    // ========================================================================

    /// <summary>
    /// 调度行为工厂
    /// </summary>
    public static class ScheduledExecutableFactory
    {
        public static IScheduleController CreateController(IScheduledExecutable scheduled, object ctx)
        {
            return scheduled switch
            {
                TimedExecutable timed => new TimedScheduleController(timed, ctx, timed.DurationMs),
                PeriodicExecutable periodic => new PeriodicScheduleController(
                    periodic, ctx,
                    periodic.PeriodMs,
                    periodic.DurationMs,
                    periodic.MaxExecutions),
                ExternalControlledExecutable external => new ExternalScheduleController(external, ctx),
                _ => NullScheduleController.Instance
            };
        }

        public static IScheduledExecutable WrapTimed(ISimpleExecutable inner, float durationMs)
        {
            return new TimedExecutable
            {
                Inner = inner,
                DurationMs = durationMs
            };
        }

        public static IScheduledExecutable WrapPeriodic(ISimpleExecutable inner, float periodMs, int maxExecutions = -1)
        {
            return new PeriodicExecutable
            {
                Inner = inner,
                PeriodMs = periodMs,
                MaxExecutions = maxExecutions
            };
        }

        public static IScheduledExecutable WrapExternal(ISimpleExecutable inner)
        {
            return new ExternalControlledExecutable
            {
                Inner = inner
            };
        }
    }

    /// <summary>
    /// 外部控制调度控制器
    /// </summary>
    public sealed class ExternalScheduleController : IScheduleController
    {
        private readonly ExternalControlledExecutable _owner;
        private readonly object _ctx;
        private bool _isCompleted;

        public bool IsCompleted => _isCompleted;
        public bool IsInterrupted { get; private set; }
        public string InterruptionReason { get; private set; }

        public ExternalScheduleController(ExternalControlledExecutable owner, object ctx)
        {
            _owner = owner;
            _ctx = ctx;
        }

        public void Update(float deltaTimeMs)
        {
        }

        public void RequestInterrupt(string reason)
        {
            if (!_owner.CanBeInterrupted) return;
            IsInterrupted = true;
            InterruptionReason = reason;
        }

        public void MarkCompleted()
        {
            _isCompleted = true;
        }
    }

    // ========================================================================
    // 延迟装饰器 (用于 Sequence 中表示延迟点)
    // ========================================================================

    /// <summary>
    /// 延迟点行为 (在 Sequence 中插入延迟)
    /// </summary>
    public sealed class DelayPointExecutable : IAtomicExecutable, ISimpleExecutable
    {
        public string Name => "DelayPoint";
        public ExecutableMetadata Metadata => new(2000, "DelayPoint");

        public float DurationMs { get; set; }

        public ExecutionResult Execute(object ctx)
        {
            return ExecutionResult.Success();
        }
    }
}

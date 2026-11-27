using System;
using AbilityKit.Triggering.Runtime.Context;

namespace AbilityKit.Triggering.Runtime.Executable
{
    [ExecutableTypeId(TypeIdRegistry.Executable.Timed, "Timed", isComposite: false, isScheduled: true, defaultDurationMs: 1000f)]
    public sealed class TimedExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public string Name => "Timed";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Timed, "Timed", isScheduled: true, defaultDurationMs: DurationMs);
        public ISimpleExecutable Inner { get; set; }
        public Config.EScheduleMode ScheduleMode => Config.EScheduleMode.Timed;
        public bool IsPeriodic => false;
        public float PeriodMs => 0f;
        public float DurationMs { get; set; } = 1000f;
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(ActionContext ctx)
        {
            return Inner?.Execute(ctx) ?? ExecutionResult.Skipped("No inner executable");
        }
    }

    [ExecutableTypeId(TypeIdRegistry.Executable.Periodic, "Periodic", isComposite: false, isScheduled: true, defaultPeriodMs: 100f)]
    public class PeriodicExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public event Action<ActionContext> OnPeriodExecuted;

        public string Name => "Periodic";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Periodic, "Periodic", isScheduled: true, defaultPeriodMs: PeriodMs);
        public ISimpleExecutable Inner { get; set; }
        public Config.EScheduleMode ScheduleMode => Config.EScheduleMode.Periodic;
        public bool IsPeriodic => true;
        public float PeriodMs { get; set; } = 100f;
        public float DurationMs { get; set; } = 0f;
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(ActionContext ctx)
        {
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Skipped("No inner executable");
            OnPeriodExecuted?.Invoke(ctx);
            return result;
        }
    }

    [ExecutableTypeId(TypeIdRegistry.Executable.External, "External", isComposite: false, isScheduled: true)]
    public sealed class ExternalControlledExecutable : ISimpleExecutable, IScheduledExecutable
    {
        public string Name => "External";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.External, "External", isScheduled: true, defaultDurationMs: DurationMs);
        public ISimpleExecutable Inner { get; set; }
        public Config.EScheduleMode ScheduleMode => Config.EScheduleMode.External;
        public bool IsPeriodic => false;
        public float PeriodMs => 0f;
        public float DurationMs { get; set; }
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(ActionContext ctx)
        {
            return Inner?.Execute(ctx) ?? ExecutionResult.Skipped("No inner executable");
        }
    }

    public static class ScheduledExecutableFactory
    {
        public static IScheduledExecutable WrapTimed(ISimpleExecutable inner, float durationMs)
        {
            return new TimedExecutable { Inner = inner, DurationMs = durationMs };
        }

        public static IScheduledExecutable WrapPeriodic(ISimpleExecutable inner, float periodMs, int maxExecutions = 0)
        {
            return new PeriodicExecutable { Inner = inner, PeriodMs = periodMs };
        }

        public static IScheduledExecutable WrapExternal(ISimpleExecutable inner)
        {
            return new ExternalControlledExecutable { Inner = inner };
        }
    }
}

using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class MobaTriggerSchedulerRollbackPreflightTests
{
    [Fact]
    public void Import_reactivates_saved_execution_and_discards_later_definitions()
    {
        var service = new MobaTriggerExecutionRuntimeService();
        var executable = new CountingExecutable();
        var context = default(ExecCtx<object>);
        var first = service.Schedule(executable, null, in context,
            EScheduleMode.Periodic, 100f, 2, true);
        service.Tick(100f);
        Assert.Equal(1, executable.Count);
        var frame = new FrameIndex(1);
        var payload = service.Export(frame);

        service.Tick(100f);
        Assert.Equal(0, service.ActiveCount);
        var later = service.Schedule(new CountingExecutable(), null, in context,
            EScheduleMode.Timed, 10f, 1, true);
        service.ValidateImport(frame, payload);
        service.Import(frame, payload);

        Assert.Equal(1, service.ActiveCount);
        Assert.Equal(payload, service.Export(frame));
        service.Tick(100f);
        Assert.Equal(3, executable.Count);
        Assert.Equal(0, service.ActiveCount);
        var reused = service.Schedule(new CountingExecutable(), null, in context,
            EScheduleMode.Timed, 10f, 1, true);
        Assert.Equal(later.Id, reused.Id);
        Assert.Equal(1L, first.Id);
    }

    [Fact]
    public void Missing_executable_definition_is_rejected_before_import()
    {
        var service = new MobaTriggerExecutionRuntimeService();
        var payload = MemoryPackSerializer.Serialize(new MobaTriggerExecutionRuntimePayload(
            1, 2, new[]
            {
                new MobaTriggerScheduledExecutionEntry(1,
                    (byte)TriggerScheduledExecutionState.Waiting, 10, 0, 0, string.Empty)
            }));

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.ValidateImport(new FrameIndex(1), payload));
        Assert.Contains("in-process definition", error.Message);
        Assert.Equal(0, service.ActiveCount);
    }

    [Fact]
    public void Confirmed_history_trim_releases_finished_definitions_but_keeps_waiting_definitions()
    {
        var service = new MobaTriggerExecutionRuntimeService();
        var context = default(ExecCtx<object>);
        var waiting = service.Schedule(new CountingExecutable(), null, in context,
            EScheduleMode.Periodic, 100f, -1, true);
        var finished = service.Schedule(new CountingExecutable(), null, in context,
            EScheduleMode.Timed, 1f, 1, true);

        service.Tick(1f);
        service.TrimConfirmedHistory(new FrameIndex(100), new FrameIndex(100), 0);

        Assert.True(service.ValidateDefinition(waiting));
        Assert.False(service.ValidateDefinition(finished));
    }

    private sealed class CountingExecutable : ITriggerPlanExecutable
    {
        public int Count { get; private set; }
        public string Name => "counting";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
        public float Weight => 1f;

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> context)
            where TCtx : class
        {
            Count++;
            return TriggerPlanExecutionResult.Success();
        }
    }
}

using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using System.IO;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class SkillAwaitEventRollbackTests
{
    [Fact]
    public void Nested_parallel_and_repeat_resume_without_replaying_completed_action()
    {
        var pipeline = new SkillCastPipeline();
        var actions = 0;
        pipeline.AddPhase(new AbilityActionPhase<SkillPipelineContext>(
            new AbilityPipelinePhaseId("before"), _ => actions++));
        var parallel = new AbilityParallelPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("parallel"));
        parallel.AddSubPhase(new AbilityDelayPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("delay"), 2f));
        var repeat = new AbilityRepeatPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("repeat"), 2);
        repeat.SetRepeatPhase(new AbilityDelayPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("child"), 1f));
        parallel.AddSubPhase(repeat);
        pipeline.AddPhase(parallel);
        pipeline.AddPhase(new AbilityActionPhase<SkillPipelineContext>(
            new AbilityPipelinePhaseId("after"), _ => actions++));
        var config = new TestConfig();
        var run = pipeline.Start(config, new SkillPipelineContext());
        run.Tick(0f);
        run.Tick(0.5f);
        var bytes = PipelineRollbackStateCodec.Serialize(SkillPipelineRollback.Capture(run, config));
        SkillPipelineRollback.Detach(run);

        var restored = SkillPipelineRollback.Restore(pipeline, config, new SkillPipelineContext(),
            PipelineRollbackStateCodec.Deserialize(bytes));
        restored.Tick(0.5f);
        Assert.Equal(EAbilityPipelineState.Executing, restored.State);
        restored.Tick(1f);
        Assert.Equal(EAbilityPipelineState.Completed, restored.State);
        Assert.Equal(2, actions);
    }

    [Fact]
    public void Invalid_phase_state_is_rejected_without_detaching_live_wait()
    {
        var events = new EventBus();
        var pipeline = new SkillCastPipeline();
        pipeline.AddPhase(new SkillAwaitEventPhase(new AbilityPipelinePhaseId("wait"),
            new SkillAwaitEventPhaseDTO { EventId = "rollback.invalid", TimeoutMs = 1000 }));
        var config = new TestConfig();
        var aim = Vec3.Zero;
        var direction = Vec3.Forward;
        var request = new SkillCastRequest(1, 1, 1, 0, in aim, in direction,
            null, events, null, null);
        var context = new SkillPipelineContext();
        context.Initialize(null, in request);
        var run = pipeline.Start(config, context);
        run.Tick(0f);
        var state = SkillPipelineRollback.Capture(run, config);
        state.Phases[0].Numbers[0] = -1;
        Assert.Throws<InvalidOperationException>(() =>
            SkillPipelineRollback.ValidateDefinition(config, run.Phases, state));

        object payload = new object();
        events.Publish(new EventKey<object>(TriggeringIdUtil.GetEventEid("rollback.invalid")), in payload);
        run.Tick(0f);
        Assert.Equal(EAbilityPipelineState.Completed, run.State);
    }

    [Fact]
    public void Run_codec_rejects_unknown_version_and_trailing_bytes()
    {
        var state = new PipelineRunRollbackState
        {
            ConfigId = 1, ConfigName = "await-rollback",
            Phases = new[] { new PipelinePhaseRollbackState { PhaseId = "wait", PhaseType = "test" } }
        };
        var bytes = PipelineRollbackStateCodec.Serialize(state);
        var trailing = new byte[bytes.Length + 1];
        bytes.CopyTo(trailing, 0);
        Assert.Throws<InvalidDataException>(() => PipelineRollbackStateCodec.Deserialize(trailing));
        bytes[0] = 99;
        Assert.Throws<InvalidDataException>(() => PipelineRollbackStateCodec.Deserialize(bytes));
    }

    [Fact]
    public void Restored_wait_receives_one_event_and_resumes_following_phase()
    {
        var events = new EventBus();
        var pipeline = new SkillCastPipeline();
        var completed = 0;
        pipeline.AddPhase(new SkillAwaitEventPhase(new AbilityPipelinePhaseId("wait"),
            new SkillAwaitEventPhaseDTO { EventId = "rollback.event", TimeoutMs = 1000 }));
        pipeline.AddPhase(new AbilityActionPhase<SkillPipelineContext>(
            new AbilityPipelinePhaseId("after"), _ => completed++));
        var config = new TestConfig();
        var aim = Vec3.Zero;
        var direction = Vec3.Forward;
        var request = new SkillCastRequest(1, 1, 1, 0, in aim, in direction,
            null, events, null, null);
        var firstContext = new SkillPipelineContext();
        firstContext.Initialize(null, in request);
        var run = pipeline.Start(config, firstContext);
        run.Tick(0f);
        run.Tick(0.25f);
        var savedContext = firstContext.CaptureRollbackState();
        var savedRun = PipelineRollbackStateCodec.Serialize(SkillPipelineRollback.Capture(run, config));

        SkillPipelineRollback.Detach(run);
        var restoredContext = new SkillPipelineContext();
        restoredContext.Initialize(null, in request);
        restoredContext.RestoreRollbackState(in savedContext);
        var restored = SkillPipelineRollback.Restore(pipeline, config, restoredContext,
            PipelineRollbackStateCodec.Deserialize(savedRun));
        object payload = new object();
        events.Publish(new EventKey<object>(TriggeringIdUtil.GetEventEid("rollback.event")), in payload);
        restored.Tick(0f);

        Assert.Equal(EAbilityPipelineState.Completed, restored.State);
        Assert.Equal(1, completed);
        Assert.Equal(EAbilityPipelineState.Failed, run.State);
    }

    [Fact]
    public void Event_received_before_checkpoint_resumes_without_republishing_history()
    {
        var events = new EventBus();
        var pipeline = new SkillCastPipeline();
        var completed = 0;
        pipeline.AddPhase(new SkillAwaitEventPhase(new AbilityPipelinePhaseId("wait"),
            new SkillAwaitEventPhaseDTO { EventId = "rollback.received", TimeoutMs = 1000 }));
        pipeline.AddPhase(new AbilityActionPhase<SkillPipelineContext>(
            new AbilityPipelinePhaseId("after"), _ => completed++));
        var config = new TestConfig();
        var aim = Vec3.Zero;
        var direction = Vec3.Forward;
        var request = new SkillCastRequest(1, 1, 1, 0, in aim, in direction,
            null, events, null, null);
        var context = new SkillPipelineContext();
        context.Initialize(null, in request);
        var run = pipeline.Start(config, context);
        run.Tick(0f);
        object payload = new object();
        events.Publish(new EventKey<object>(TriggeringIdUtil.GetEventEid("rollback.received")), in payload);
        var checkpoint = PipelineRollbackStateCodec.Serialize(SkillPipelineRollback.Capture(run, config));
        Assert.Equal(0, completed);
        SkillPipelineRollback.Detach(run);

        var restoredContext = new SkillPipelineContext();
        restoredContext.Initialize(null, in request);
        var restored = SkillPipelineRollback.Restore(pipeline, config, restoredContext,
            PipelineRollbackStateCodec.Deserialize(checkpoint));

        Assert.Equal(0, completed);
        restored.Tick(0f);
        Assert.Equal(EAbilityPipelineState.Completed, restored.State);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void Failed_rebind_detaches_the_new_run_and_releases_its_binding()
    {
        var registry = new PipelineRegistry();
        var runtime = new PipelineRuntime(registry);
        runtime.Initialize();
        var pipeline = new SkillCastPipeline { Runtime = runtime };
        var injected = new InvalidOperationException("rebind failed");
        var bindingChanges = new List<int>();
        pipeline.AddPhase(new FailingRebindPhase(bindingChanges.Add, injected));
        var config = new TestConfig();
        var source = pipeline.Start(config, new SkillPipelineContext());
        var saved = SkillPipelineRollback.Capture(source, config);
        SkillPipelineRollback.Detach(source);
        var starts = 0;
        pipeline.Events.OnPipelineStart += _ => starts++;

        var failure = Assert.Throws<InvalidOperationException>(() =>
            SkillPipelineRollback.Restore(pipeline, config, new SkillPipelineContext(), saved));

        Assert.Same(injected, failure);
        Assert.Equal(0, registry.ActiveCount);
        Assert.Equal(new[] { 1, -1 }, bindingChanges);
        Assert.Equal(0, starts);
    }

    private sealed class FailingRebindPhase(Action<int> updateBindings, Exception failure)
        : SkillRollbackPhaseBase(new AbilityPipelinePhaseId("failing-rebind")),
            IAbilityPipelinePhaseInstanceFactory<SkillPipelineContext>,
            IAbilityPhaseRollbackRebind<SkillPipelineContext>, IAbilityPhaseRollbackDetach
    {
        private bool _bound;

        protected override void OnExecute(SkillPipelineContext context) { }

        public IAbilityPipelinePhase<SkillPipelineContext> CreateRunPhase() => new FailingRebindPhase(updateBindings, failure);

        public void RebindAfterRollback(SkillPipelineContext context)
        {
            _bound = true;
            updateBindings(1);
            throw failure;
        }

        public void DetachForRollback()
        {
            if (!_bound) return;
            _bound = false;
            updateBindings(-1);
        }
    }

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "await-rollback";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}

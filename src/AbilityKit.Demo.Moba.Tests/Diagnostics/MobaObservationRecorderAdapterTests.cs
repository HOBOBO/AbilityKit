using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Lifecycle;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaObservationRecorderAdapterTests
{
    [Fact]
    public void TriggerObservation_MapsCorrelationDefinitionAndPayload()
    {
        var sink = new RecordingSink(BattleDiagnosticEventChannel.Trigger);
        var hook = (IMobaTriggerAnalysisHook)new MobaBattleObservationRecorderAdapter(sink);
        var observation = new MobaTriggerAnalysisObservation(
            triggerId: 701,
            contextKind: 2,
            originKind: 3,
            stage: MobaTriggerAnalysisStage.Budget,
            result: MobaTriggerAnalysisResult.Blocked,
            sourceActorId: 7,
            targetActorId: 9,
            contextId: 8001L,
            rootContextId: 500L,
            detailCode: 11,
            currentDepth: 1,
            currentFrameCount: 2,
            currentRootCount: 3,
            currentSameTriggerCount: 4,
            failureKey: "DepthLimit",
            reason: "Trigger budget blocked execution.");

        Assert.True(hook.IsEnabled);
        hook.OnObserved(in observation);

        var draft = Assert.Single(sink.Drafts);
        Assert.Equal(BattleDiagnosticEventKind.TriggerAnalysis, draft.Kind);
        Assert.Equal(BattleDiagnosticEventChannel.Trigger, draft.Channel);
        Assert.Equal(BattleDiagnosticDefinitionKind.Trigger, draft.DefinitionKind);
        Assert.Equal(701, draft.ConfigId);
        Assert.Equal(500L, draft.RootContextId);
        Assert.Equal(8001L, draft.ContextId);
        Assert.True(draft.Payload.TryGetTriggerAnalysis(out var payload));
        Assert.Equal(BattleDiagnosticTriggerAnalysisStage.Budget, payload.Stage);
        Assert.Equal(BattleDiagnosticTriggerAnalysisResult.Blocked, payload.Result);
        Assert.Equal("DepthLimit", payload.FailureKey);
    }

    [Fact]
    public void EffectObservations_MapStartAndEndWithoutLeakingRecorderTypesToProducer()
    {
        var sink = new RecordingSink(BattleDiagnosticEventChannel.Effect);
        var hook = (IMobaEffectLifecycleHook)new MobaBattleObservationRecorderAdapter(sink);
        var started = new MobaEffectLifecycleObservation(
            MobaEffectLifecycleStage.Started,
            effectConfigId: 801,
            triggerId: 802,
            sourceActorId: 7,
            targetActorId: 9,
            effectContextId: 8001L,
            rootContextId: 500L);
        var ended = new MobaEffectLifecycleObservation(
            MobaEffectLifecycleStage.Ended,
            effectConfigId: 801,
            triggerId: 802,
            sourceActorId: 7,
            targetActorId: 9,
            effectContextId: 8001L,
            rootContextId: 500L,
            succeeded: true);

        hook.OnObserved(in started);
        hook.OnObserved(in ended);

        Assert.Collection(
            sink.Drafts,
            draft =>
            {
                Assert.Equal(BattleDiagnosticEventKind.EffectStarted, draft.Kind);
                Assert.Equal(BattleDiagnosticDefinitionKind.Effect, draft.DefinitionKind);
                Assert.Equal(500L, draft.RootContextId);
                Assert.Equal(8001L, draft.ContextId);
            },
            draft =>
            {
                Assert.Equal(BattleDiagnosticEventKind.EffectEnded, draft.Kind);
                Assert.Equal(BattleDiagnosticEventOutcome.Succeeded, draft.Outcome);
                Assert.Equal(BattleDiagnosticDefinitionKind.Effect, draft.DefinitionKind);
            });
    }

    [Fact]
    public void BuffObservation_MapsRuntimeHandleDurationsAndLifecyclePayload()
    {
        var sink = new RecordingSink(BattleDiagnosticEventChannel.Buff);
        var hook = (IMobaBuffLifecycleHook)new MobaBattleObservationRecorderAdapter(sink);
        var runtime = new MobaSkillCastRuntimeHandle(41L, 3, 500L);
        var observation = new MobaBuffLifecycleObservation(
            MobaBuffLifecycleStage.StackChanged,
            buffId: 901,
            sourceActorId: 7,
            targetActorId: 9,
            rootContextId: 500L,
            contextId: 8001L,
            in runtime,
            stackCount: 2,
            previousStackCount: 1,
            durationSeconds: 10f,
            remainingSeconds: 8.5f,
            intervalRemainingSeconds: 0.75f,
            maxStacks: 3,
            modifierBindingCount: 2,
            modifierSourceId: 77);

        hook.OnObserved(in observation);

        var draft = Assert.Single(sink.Drafts);
        Assert.Equal(BattleDiagnosticEventChannel.Buff, draft.Channel);
        Assert.Equal(BattleDiagnosticDefinitionKind.Buff, draft.DefinitionKind);
        Assert.Equal(41L, draft.SkillRuntime.RuntimeId);
        Assert.Equal(3, draft.SkillRuntime.Generation);
        Assert.True(draft.Payload.TryGetBuffLifecycle(out var payload));
        Assert.Equal(BattleDiagnosticBuffLifecycleStage.StackChanged, payload.Stage);
        Assert.Equal(10000, payload.DurationMilliseconds);
        Assert.Equal(8500, payload.RemainingMilliseconds);
        Assert.Equal(750, payload.IntervalRemainingMilliseconds);
        Assert.Equal(2, payload.ModifierBindingCount);
    }

    [Fact]
    public void DisabledHookAndThrowingRecorder_DoNotEscapeIntoGameplay()
    {
        var disabledSink = new RecordingSink(BattleDiagnosticEventChannel.None);
        var disabledHook = (IMobaEffectLifecycleHook)new MobaBattleObservationRecorderAdapter(disabledSink);
        var observation = new MobaEffectLifecycleObservation(
            MobaEffectLifecycleStage.Started,
            801,
            802,
            7,
            9,
            8001L,
            500L);

        Assert.False(disabledHook.IsEnabled);
        disabledHook.OnObserved(in observation);
        Assert.Empty(disabledSink.Drafts);

        var throwingAdapter = (IMobaEffectLifecycleHook)new MobaBattleObservationRecorderAdapter(
            new ThrowingSink());
        var error = Record.Exception(() => throwingAdapter.OnObserved(in observation));
        Assert.Null(error);
    }

    [Fact]
    public void BuffProducer_IsolatesThrowingHookAndBusinessServicesDoNotDependOnDraftSink()
    {
        var notifier = new BuffLifecycleNotifier(null, null, null, new ThrowingBuffHook());
        var buff = new BuffMO(new BuffDTO
        {
            Id = 901,
            Name = "observation-test",
            DurationMs = 10000,
            MaxStacks = 3,
        });
        var runtime = new BuffRuntime
        {
            BuffId = buff.Id,
            SourceId = 7,
            StackCount = 1,
            Remaining = 10f,
            SourceContextId = 500L,
        };

        var error = Record.Exception(() => notifier.AppliedNew(buff, 7, 9, 10f, runtime));

        Assert.Null(error);
        Assert.DoesNotContain(
            typeof(MobaEffectExecutionService).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(IMobaBattleDiagnosticEventSink));
        Assert.DoesNotContain(
            typeof(BuffLifecycleNotifier).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(IMobaBattleDiagnosticEventSink));
        Assert.DoesNotContain(
            typeof(BuffContinuousIntervalHandler).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(IMobaBattleDiagnosticEventSink));
    }

    private sealed class RecordingSink :
        IMobaBattleDiagnosticEventSink,
        IMobaBattleDiagnosticEventGate
    {
        private readonly BattleDiagnosticEventChannel _enabledChannels;

        public RecordingSink(BattleDiagnosticEventChannel enabledChannels)
        {
            _enabledChannels = enabledChannels;
        }

        public List<MobaBattleDiagnosticEventDraft> Drafts { get; } = new();

        public bool IsEnabled(BattleDiagnosticEventChannel channel) =>
            channel != BattleDiagnosticEventChannel.None &&
            (_enabledChannels & channel) != 0;

        public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
        {
            Drafts.Add(draft);
            return true;
        }
    }

    private sealed class ThrowingSink : IMobaBattleDiagnosticEventSink
    {
        public bool TryCollect(in MobaBattleDiagnosticEventDraft draft) =>
            throw new InvalidOperationException("recorder failed");
    }

    private sealed class ThrowingBuffHook : IMobaBuffLifecycleHook
    {
        public bool IsEnabled => true;

        public void OnObserved(in MobaBuffLifecycleObservation observation) =>
            throw new InvalidOperationException("hook failed");
    }
}

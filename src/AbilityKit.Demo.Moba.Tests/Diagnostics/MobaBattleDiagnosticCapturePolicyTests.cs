using AbilityKit.Core.Observability;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaBattleDiagnosticCapturePolicyTests
{
    private static readonly BattleDiagnosticSessionScope Scope =
        new("capture-policy", "world", 1);

    [Fact]
    public void RecommendedPlayerDefault_KeepsOnlyMetricsAndOneReadView()
    {
        var options = BattleDiagnosticCaptureOptions.RecommendedDefault;

        Assert.Equal(BattleDiagnosticCaptureMode.Metrics, options.Mode);
        Assert.True(options.CapturesMetrics);
        Assert.False(options.CapturesEvents);
        Assert.False(options.CapturesState);
        Assert.Equal(BattleDiagnosticEventChannel.None, options.EnabledChannels);
        Assert.Equal(1, options.RetainedReadViewCount);
    }

    [Fact]
    public void MetricsMode_RejectsEventsBeforeCallingProviders()
    {
        var providerCalls = 0;
        var options = new BattleDiagnosticCaptureOptions(
            BattleDiagnosticCaptureMode.Metrics,
            BattleDiagnosticEventChannel.All,
            eventCapacity: 8,
            retainedReadViewCount: 1);
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            options,
            () => ++providerCalls,
            () => ++providerCalls);
        var draft = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.EffectStarted,
            BattleDiagnosticEventChannel.Effect);
        var observationSink = (IObservationSink<MobaBattleDiagnosticEventDraft>)collector;

        Assert.False(collector.IsEventEnabled(BattleDiagnosticEventChannel.Effect));
        Assert.False(observationSink.IsEnabled);
        Assert.False(collector.TryCollect(in draft));
        Assert.Equal(0, providerCalls);
        Assert.Equal(0, collector.Store.Count);
    }

    [Fact]
    public void EventsMode_WritesEnabledChannelsButDoesNotSampleState()
    {
        var options = new BattleDiagnosticCaptureOptions(
            BattleDiagnosticCaptureMode.Events,
            BattleDiagnosticEventChannel.Trigger | BattleDiagnosticEventChannel.Effect,
            stateSampleIntervalFrames: 5,
            eventCapacity: 8,
            retainedReadViewCount: 1);
        var collector = new MobaBattleDiagnosticEventCollector(Scope, options, () => 10, () => 20L);
        var effect = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.EffectStarted,
            BattleDiagnosticEventChannel.Effect);
        var buff = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.BuffAdded,
            BattleDiagnosticEventChannel.Buff);

        Assert.True(collector.TryCollect(in effect));
        Assert.False(collector.TryCollect(in buff));
        Assert.False(collector.ShouldSampleState(10));
        Assert.Equal(1, collector.Store.Count);
    }

    [Fact]
    public void FullMode_UsesConfiguredStateSamplingInterval()
    {
        var options = new BattleDiagnosticCaptureOptions(
            BattleDiagnosticCaptureMode.Full,
            BattleDiagnosticEventChannel.All,
            stateSampleIntervalFrames: 5,
            eventCapacity: 8,
            retainedReadViewCount: 1);
        var collector = new MobaBattleDiagnosticEventCollector(Scope, options);

        Assert.True(collector.ShouldSampleState(0));
        Assert.False(collector.ShouldSampleState(4));
        Assert.True(collector.ShouldSampleState(5));
    }

    [Fact]
    public void TriggerEffectBuffDrafts_PreserveRootCorrelationAndTypedChannels()
    {
        const long root = 900L;
        var trigger = MobaEffectDiagnosticProducer.CreateTriggerAnalysisDraft(
            301,
            contextKind: 1,
            originKind: 2,
            BattleDiagnosticTriggerAnalysisStage.Conditions,
            BattleDiagnosticTriggerAnalysisResult.Passed,
            sourceActorId: 7,
            targetActorId: 9,
            contextId: 901L,
            rootContextId: root);
        var effect = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
            401,
            triggerId: 301,
            sourceActorId: 7,
            targetActorId: 9,
            effectContextId: 902L,
            rootContextId: root);
        var origin = new MobaGameplayOrigin(
            7,
            9,
            MobaTraceKind.EffectExecution,
            401,
            immediateContextId: 902L,
            parentContextId: 901L,
            rootContextId: root,
            ownerContextId: root);
        var request = new BuffApplyRequest
        {
            SourceActorId = 7,
            TargetActorId = 9,
            BuffId = 501,
            SourceContextId = 902L,
            Origin = BuffOriginContext.FromOrigin(in origin)
        };
        var buff = MobaBuffService.CreateBuffAddedDraft(in request);

        Assert.Equal(BattleDiagnosticEventChannel.Trigger, trigger.Channel);
        Assert.Equal(BattleDiagnosticEventChannel.Effect, effect.Channel);
        Assert.Equal(BattleDiagnosticEventChannel.Buff, buff.Channel);
        Assert.Equal(root, trigger.RootContextId);
        Assert.Equal(root, effect.RootContextId);
        Assert.Equal(root, buff.RootContextId);
        Assert.Equal(301, trigger.ConfigId);
        Assert.Equal(BattleDiagnosticDefinitionKind.Trigger, trigger.DefinitionKind);
        Assert.Equal(root, trigger.Trace.RootId);
        Assert.Equal(301, trigger.Definition.Id);
        Assert.Equal(401, effect.ConfigId);
        Assert.Equal(BattleDiagnosticDefinitionKind.Effect, effect.DefinitionKind);
        Assert.Equal(501, buff.ConfigId);
        Assert.Equal(BattleDiagnosticDefinitionKind.Buff, buff.DefinitionKind);
    }
}

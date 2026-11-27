using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Effect;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaBattleDiagnosticSnapshotCaptureTests
{
    [Fact]
    public void EffectApplication_RoundTripsOnlyTheObservedInstanceAndKeepsUnobservedSourceMissing()
    {
        using var container = CreateBuilder().Build();
        using var scope = container.CreateScope();
        var collector = scope.Resolve<MobaBattleDiagnosticEventCollector>();
        var observer = new MobaEffectApplicationObserver(collector, null, null);
        var provider = new EffectObserverProvider(observer);
        var clock = new FrameTime();
        clock.StepTo(new FrameIndex(5), 0.125f);
        var source = new EntitasUnitFacade(10);
        var firstHost = new EntitasUnitFacade(20);
        var secondHost = new EntitasUnitFacade(30);
        var spec = new GameplayEffectSpec(EffectDurationPolicy.Infinite, 0f, 0f, default, null, null);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Full;
        var first = firstHost.Effects.Apply(spec, new EffectExecutionContext(provider, clock, source, firstHost, 42, firstHost, null));
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        var second = secondHost.Effects.Apply(spec, new EffectExecutionContext(provider, clock, firstHost, secondHost, 77, secondHost, null));
        Assert.Equal(first.Id, second.Id);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Full;
        var effects = new List<BattleDiagnosticActorEffect>();
        Assert.True(MobaBattleDiagnosticStateSampler.TrySampleActorEffects(collector.Scope, 8, 20, firstHost, effects, observer));
        Assert.True(MobaBattleDiagnosticStateSampler.TrySampleActorEffects(collector.Scope, 8, 30, secondHost, effects, observer));
        Assert.True(scope.Resolve<IBattleDiagnosticActorEffectStore>().TryReplaceSnapshot(8, new long[] { 20, 30 }, effects));
        var snapshot = scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>().CaptureSnapshot();
        var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot);
        var restored = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json);
        var captured = Assert.Single(restored.Effects.Items, effect => effect.Application.IsCaptured);
        Assert.Equal(20, captured.ActorId);
        Assert.Equal(5, captured.Application.Frame);
        Assert.Equal(10, captured.Application.SourceActor.RuntimeId);
        Assert.Equal(42, captured.Application.SourceContextId);
        Assert.Contains(restored.Effects.Items, effect => effect.ActorId == 30 && !effect.Application.IsCaptured);
    }

    private sealed class EffectObserverProvider(IEffectApplicationObserver observer) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEffectApplicationObserver) ? observer : null;
    }

    [Fact]
    public void WithoutTraceAdapter_ResolvesAndCapturesEventsWithEmptyTrace()
    {
        using var container = CreateBuilder().Build();
        using var scope = container.CreateScope();
        Assert.False(container.IsRegistered(typeof(IBattleDiagnosticTraceSnapshotSource)));

        var collector = scope.Resolve<MobaBattleDiagnosticEventCollector>();
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        var draft = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.Damage,
            BattleDiagnosticEventChannel.DamageAndHeal);
        Assert.True(scope.Resolve<IMobaBattleDiagnosticEventSink>().TryCollect(in draft));

        var capture = scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>();
        var snapshot = capture.CaptureSnapshot();

        Assert.Same(capture, scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>());
        Assert.Equal(collector.Scope, snapshot.SessionInfo.Scope);
        Assert.Single(snapshot.Events.Events);
        Assert.False(snapshot.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace));
        Assert.Empty(snapshot.Trace.Nodes);
        Assert.Equal(0L, snapshot.Trace.Revision);
        Assert.False(snapshot.Trace.Truncated);
        Assert.True(snapshot.Trace.IsStable);
    }

    [Fact]
    public void WithTraceAdapter_CapturesRegisteredTraceNodes()
    {
        using var container = CreateBuilder().AddModule(new MobaTraceAdapterModule()).Build();
        using var scope = container.CreateScope();
        var registry = scope.Resolve<MobaTraceRegistry>();
        var rootId = registry.CreateObservationRoot(MobaExecutionKind.SkillCast, 501);

        var snapshot = scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>().CaptureSnapshot();

        Assert.True(snapshot.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace));
        Assert.Equal(rootId, Assert.Single(snapshot.Trace.Nodes).ContextId);
        Assert.Equal(registry.Revision, snapshot.Trace.Revision);
    }

    [Fact]
    public void WithTraceSourceFromDifferentSession_RejectsScopeMismatch()
    {
        using var container = CreateBuilder()
            .RegisterExternalInstance<IBattleDiagnosticTraceSnapshotSource>(new MismatchedTraceSource())
            .Build();
        using var scope = container.CreateScope();

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>());

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static WorldContainerBuilder CreateBuilder() =>
        new WorldContainerBuilder()
            .AddModule(new AttributeWorldServicesModule(
                WorldServiceProfile.Default,
                new[] { typeof(MobaBattleDiagnosticSnapshotCapture).Assembly },
                new[] { "AbilityKit.Demo.Moba.Services" }))
            .RegisterExternalInstance(new MobaBattleDiagnosticEventCollector(
                new BattleDiagnosticSessionScope("snapshot-test", "world", 1),
                16,
                () => 0,
                () => 0L));

    private sealed class MismatchedTraceSource : IBattleDiagnosticTraceSnapshotSource
    {
        public BattleDiagnosticSessionScope Scope => new("different-session", "different-world", 1);

        public BattleDiagnosticTraceTrackSnapshot CaptureTraceSnapshot() =>
            throw new InvalidOperationException("A mismatched trace source must not be captured.");
    }
}

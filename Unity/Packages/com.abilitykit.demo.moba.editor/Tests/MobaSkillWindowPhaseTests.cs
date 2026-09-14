using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillWindowPhaseTests
    {
        [Test]
        public void AwaitEvent_ReceivesEventAndDisposesSubscription()
        {
            using var scope = new PhaseTestScope();
            const string eventId = "test.skill.confirmed";
            var phase = new SkillAwaitEventPhase(
                Id("await-confirmed"),
                new SkillAwaitEventPhaseDTO { EventId = eventId, TimeoutMs = 1000 });
            var key = new EventKey<object>(TriggeringIdUtil.GetEventEid(eventId));

            phase.Execute(scope.Context);
            Assert.That(scope.EventBus.HasSubscribers(key), Is.True);

            object payload = new object();
            scope.EventBus.Publish(key, in payload);
            phase.OnUpdate(scope.Context, 0f);

            Assert.That(phase.IsComplete, Is.True);
            Assert.That(scope.Context.IsAborted, Is.False);
            Assert.That(scope.EventBus.HasSubscribers(key), Is.False);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var snapshot), Is.True);
            Assert.That(snapshot.EventSequence, Is.EqualTo(1));
        }

        [Test]
        public void Race_TimeoutWindowWinsAndUnsubscribesAwaitEvent()
        {
            using var scope = new PhaseTestScope();
            const string eventId = "test.skill.never-arrives";
            var key = new EventKey<object>(TriggeringIdUtil.GetEventEid(eventId));
            var wait = new SkillAwaitEventPhase(
                Id("await-event"),
                new SkillAwaitEventPhaseDTO { EventId = eventId, TimeoutMs = 5000 });
            var timeout = new SkillWindowPhase(
                Id("timeout"),
                new SkillWindowPhaseDTO
                {
                    WindowId = "event-timeout",
                    Kind = (int)SkillWindowKind.Timed,
                    DurationMs = 100,
                    CompleteOnTimeout = true,
                },
                executor: null);
            var race = new AbilityRacePhase<SkillPipelineContext>(Id("event-or-timeout"));
            race.AddSubPhase(wait);
            race.AddSubPhase(timeout);

            race.Execute(scope.Context);
            race.OnUpdate(scope.Context, 0.1f);

            Assert.That(race.IsComplete, Is.True);
            Assert.That(wait.IsComplete, Is.True);
            Assert.That(scope.EventBus.HasSubscribers(key), Is.False);
            Assert.That(scope.Context.IsAborted, Is.False);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var snapshot), Is.True);
            Assert.That(snapshot.EndReason, Is.EqualTo(MobaSkillWindowEndReason.Timeout));
        }

        [Test]
        public void ChargeRelease_PersistsResolvedTierAndReleaseReason()
        {
            using var scope = new PhaseTestScope();
            var phase = new SkillWindowPhase(
                Id("charge"),
                new SkillWindowPhaseDTO
                {
                    WindowId = "charge-primary",
                    Kind = (int)SkillWindowKind.Charge,
                    DurationMs = 1200,
                    CompleteOnTimeout = true,
                    ChargeTierThresholdMs = new[] { 200, 500, 900 },
                },
                executor: null);

            phase.Execute(scope.Context);
            scope.Context.MarkInputReleased();
            phase.OnUpdate(scope.Context, 0.65f);

            Assert.That(phase.IsComplete, Is.True);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var snapshot), Is.True);
            Assert.That(snapshot.ElapsedMs, Is.EqualTo(650));
            Assert.That(snapshot.ChargeTier, Is.EqualTo(2));
            Assert.That(snapshot.EndReason, Is.EqualTo(MobaSkillWindowEndReason.InputReleased));
        }

        [Test]
        public void ChannelInterrupt_StopsFutureTicks()
        {
            using var scope = new PhaseTestScope();
            var phase = new SkillWindowPhase(
                Id("channel"),
                new SkillWindowPhaseDTO
                {
                    WindowId = "channel-primary",
                    Kind = (int)SkillWindowKind.Channel,
                    DurationMs = 1000,
                    CompleteOnTimeout = true,
                    ChannelIntervalMs = 100,
                },
                executor: null);

            phase.Execute(scope.Context);
            phase.OnUpdate(scope.Context, 0.25f);
            phase.OnInterrupt(scope.Context);
            phase.OnUpdate(scope.Context, 0.5f);

            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var snapshot), Is.True);
            Assert.That(snapshot.ChannelTick, Is.EqualTo(2));
            Assert.That(snapshot.EndReason, Is.EqualTo(MobaSkillWindowEndReason.Cancelled));
        }

        [Test]
        public void RaceLoserWindow_DoesNotExecuteBusinessCloseTriggers()
        {
            using var scope = new PhaseTestScope();
            var window = new SkillWindowPhase(
                Id("loser-window"),
                new SkillWindowPhaseDTO
                {
                    WindowId = "loser-window",
                    Kind = (int)SkillWindowKind.Timed,
                    DurationMs = 1000,
                    CompleteOnTimeout = true,
                    CloseTriggerIds = new[] { 999999 },
                    AbortOnTriggerFailure = true,
                },
                executor: null);
            var race = new AbilityRacePhase<SkillPipelineContext>(Id("window-race"));
            race.AddSubPhase(window);
            race.AddSubPhase(new InstantWinnerPhase("winner"));

            race.Execute(scope.Context);

            Assert.That(race.IsComplete, Is.True);
            Assert.That(scope.Context.IsAborted, Is.False);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var snapshot), Is.True);
            Assert.That(snapshot.EndReason, Is.EqualTo(MobaSkillWindowEndReason.Cancelled));
        }

        [Test]
        public void RecastSignal_TraversesRegistryAndRunningPipeline()
        {
            using var scope = new PhaseTestScope();
            var clock = new WorldClock();
            var registry = new SkillRunnerRegistry(clock, null, null, null);
            var runner = registry.GetOrCreate(PhaseTestScope.CasterActorId);
            var phase = new SkillWindowPhase(
                Id("recast"),
                new SkillWindowPhaseDTO
                {
                    WindowId = "recast-primary",
                    Kind = (int)SkillWindowKind.Recast,
                    DurationMs = 1000,
                    CompleteOnTimeout = true,
                },
                executor: null);
            var phases = new IAbilityPipelinePhase<SkillPipelineContext>[] { phase };
            var config = new TestPipelineConfig();

            Assert.That(runner.Start(null, null, config, phases, new object(), in scope.Request, scope.TriggerContext), Is.True);
            Assert.That(registry.TrySignalRecast(PhaseTestScope.CasterActorId, PhaseTestScope.SkillSlot), Is.True);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out var signalled), Is.True);
            Assert.That(signalled.RecastSequence, Is.EqualTo(1));

            runner.Step(0.01f);

            Assert.That(runner.HasRunning, Is.False);
            Assert.That(scope.Windows.TryGetSnapshot(in scope.Handle, out _), Is.False,
                "The completed runner must finalize its skill runtime after consuming recast.");
        }

        [Test]
        public void AutoModule_ResolvesWindowServiceWithItsRuntimeDependency()
        {
            AttributeWorldServicesModule.ClearCache();
            var builder = new WorldContainerBuilder()
                .AddModule(new MobaServicesAutoModule(typeof(MobaSkillWindowRuntimeService).Assembly));

            using var container = builder.Build();
            using var scope = container.CreateScope();
            var runtimes = scope.Resolve<MobaSkillCastRuntimeService>();
            var windows = scope.Resolve<MobaSkillWindowRuntimeService>();

            Assert.That(runtimes, Is.Not.Null);
            Assert.That(windows, Is.Not.Null);
            Assert.That(scope.Resolve<MobaSkillWindowRuntimeService>(), Is.SameAs(windows));
        }

        private static AbilityPipelinePhaseId Id(string value)
        {
            return new AbilityPipelinePhaseId(value);
        }

        private sealed class PhaseTestScope : IDisposable
        {
            public const int SkillId = 801100;
            public const int SkillSlot = 2;
            public const int CasterActorId = 10;
            private const int TargetActorId = 20;

            private readonly WorldContainer _container;
            public readonly EventBus EventBus;
            public readonly MobaSkillWindowRuntimeService Windows;
            public readonly MobaSkillCastRuntimeHandle Handle;
            public readonly SkillCastRequest Request;
            public readonly SkillCastContext TriggerContext;
            public readonly SkillPipelineContext Context;

            public PhaseTestScope()
            {
                var runtimes = new MobaSkillCastRuntimeService();
                Windows = new MobaSkillWindowRuntimeService(runtimes);
                EventBus = new EventBus();
                var frameTime = new FixedFrameTime();
                _container = new WorldContainerBuilder()
                    .RegisterExternalInstance(runtimes)
                    .RegisterExternalInstance(Windows)
                    .RegisterExternalInstance<IFrameTime>(frameTime)
                    .Build();

                var aim = Vec3.Zero;
                var direction = Vec3.Forward;
                var runtimeRequest = new MobaSkillCastRuntimeCreateRequest(
                    SkillId, SkillSlot, 1, 1, CasterActorId, TargetActorId, in aim, in direction, 901100L);
                var runtime = runtimes.Create(in runtimeRequest);
                Handle = runtime.Handle;
                Request = new SkillCastRequest(
                    SkillId, SkillSlot, CasterActorId, TargetActorId, in aim, in direction,
                    _container, EventBus, null, null);
                TriggerContext = SkillCastContextBuilder.Create()
                    .FromRequest(in Request)
                    .WithSkillLevel(1)
                    .WithSequence(1)
                    .WithSourceContext(901100L)
                    .WithRuntime(in Handle)
                    .Build();
                Context = new SkillPipelineContext();
                Context.Initialize(new object(), in Request, TriggerContext);
            }

            public void Dispose()
            {
                _container.Dispose();
            }
        }

        private sealed class InstantWinnerPhase : AbilityInstantPhaseBase<SkillPipelineContext>
        {
            public InstantWinnerPhase(string phaseId) : base(phaseId)
            {
            }

            protected override void OnInstantExecute(SkillPipelineContext context)
            {
            }
        }

        private sealed class TestPipelineConfig : IAbilityPipelineConfig
        {
            public int ConfigId => 801100;
            public string ConfigName => "p1-window-runtime-test";
            public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
            public bool AllowInterrupt => true;
            public bool AllowPause => true;
        }

        private sealed class FixedFrameTime : IFrameTime
        {
            public FrameIndex Frame => new FrameIndex(7);
            public float DeltaTime => 1f / 30f;
            public float Time => Frame.Value * DeltaTime;
            public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
            public FrameIndex TimeToFrame(float time) => new FrameIndex((int)Math.Round(time / DeltaTime));
        }
    }
}

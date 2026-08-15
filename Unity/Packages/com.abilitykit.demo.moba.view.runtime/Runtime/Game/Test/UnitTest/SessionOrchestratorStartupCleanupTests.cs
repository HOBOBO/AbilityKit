using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Common.Rooms;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionOrchestratorStartupCleanupTests
    {
        [Test]
        public void BattleSessionRuntime_RejectsNullOrchestratorHost()
        {
            var runtime = new BattleSessionRuntime();

            Assert.Throws<ArgumentNullException>(() => runtime.ConfigureOrchestrator(null));
            Assert.That(runtime.Orchestrator, Is.Null);
        }

        [Test]
        public void BattleSessionRuntime_RejectsOrchestratorReconfiguration()
        {
            var runtime = new BattleSessionRuntime();
            var host = new FailureInjectingHost(CreatePlan());

            runtime.ConfigureOrchestrator(host);

            Assert.That(runtime.Orchestrator, Is.Not.Null);
            Assert.That(runtime.Orchestrator, Is.SameAs(runtime.Orchestrator));
            Assert.Throws<InvalidOperationException>(() => runtime.ConfigureOrchestrator(host));
        }

        private static readonly string[] StartupFailureSteps =
        {
            "start-logic",
            "subscribe",
            "start-remote",
            "start-confirmed",
            "pipeline-start",
            "replay-setup",
        };

        private static readonly string[] CleanupOrder =
        {
            "record-writer",
            "pipeline-stop",
            "snapshot-routing",
            "confirmed-view",
            "destroy-worlds",
            "confirmed-world",
            "remote-world",
            "remote-interpolation",
            "unsubscribe",
            "stop-logic",
            "reset-handles",
        };

        [TestCaseSource(nameof(StartupFailureSteps))]
        public void StartSession_FailureAtEveryHostPhase_CleansUpAndFaults(string failureStep)
        {
            var fixture = CreateFixture();
            fixture.Host.FailNext(failureStep);

            Assert.Throws<InvalidOperationException>(() => fixture.Orchestrator.StartSession());

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Faulted));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Not.Null);
            Assert.That(fixture.State.Generation, Is.EqualTo(1));
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            var expectedCleanup = new List<string>(CleanupOrder);
            if (Array.IndexOf(StartupFailureSteps, failureStep) < 4)
            {
                expectedCleanup.Remove("pipeline-stop");
            }
            Assert.That(fixture.Host.CleanupCalls, Is.EqualTo(expectedCleanup));
        }

        [Test]
        public void StopSession_WhenCleanupStepThrows_ContinuesAndResumesOnlyFailedWork()
        {
            var fixture = CreateFixture();
            fixture.Orchestrator.StartSession();
            fixture.Host.FailNext("confirmed-world");

            Assert.Throws<AggregateException>(() => fixture.Orchestrator.StopSession());

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Faulted));
            Assert.That(fixture.Host.Calls, Does.Contain("remote-world"));
            Assert.That(fixture.Host.Calls, Does.Contain("stop-logic"));
            Assert.That(fixture.Host.ResetHandlesCount, Is.Zero);
            Assert.That(fixture.Host.CountCalls("confirmed-world"), Is.EqualTo(1));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));

            fixture.Orchestrator.StopSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Stopped));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Null);
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
            Assert.That(fixture.Host.CountCalls("confirmed-world"), Is.EqualTo(2));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            var callCount = fixture.Host.Calls.Count;
            fixture.Orchestrator.StopSession();
            Assert.That(fixture.Host.Calls.Count, Is.EqualTo(callCount));
        }

        [Test]
        public void StopSession_WhenRecordWriterCleanupFails_RetriesOnlyWriterStep()
        {
            var fixture = CreateFixture();
            fixture.Orchestrator.StartSession();
            fixture.Host.FailNext("record-writer");

            Assert.Throws<AggregateException>(() => fixture.Orchestrator.StopSession());

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Faulted));
            Assert.That(fixture.Host.CountCalls("record-writer"), Is.EqualTo(1));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));
            Assert.That(fixture.Host.CountCalls("stop-logic"), Is.EqualTo(1));
            Assert.That(fixture.Host.ResetHandlesCount, Is.Zero);

            fixture.Orchestrator.StopSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Stopped));
            Assert.That(fixture.Host.CountCalls("record-writer"), Is.EqualTo(2));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));
            Assert.That(fixture.Host.CountCalls("stop-logic"), Is.EqualTo(1));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));
        }

        [Test]
        public void StartSession_AfterStartupFailure_RetriesWithNewGeneration()
        {
            var fixture = CreateFixture();
            fixture.Host.FailNext("replay-setup");

            Assert.Throws<InvalidOperationException>(() => fixture.Orchestrator.StartSession());
            fixture.Orchestrator.StartSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Running));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Null);
            Assert.That(fixture.State.Generation, Is.EqualTo(2));
            Assert.That(fixture.Host.LogicSessionActive, Is.True);
            Assert.That(fixture.Host.CountCalls("start-logic"), Is.EqualTo(2));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            fixture.Orchestrator.StopSession();
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
        }

        [Test]
        public void StopSession_AfterSuccessfulStop_IsIdempotent()
        {
            var fixture = CreateFixture();
            fixture.Orchestrator.StartSession();

            fixture.Orchestrator.StopSession();
            var callCount = fixture.Host.Calls.Count;
            fixture.Orchestrator.StopSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Stopped));
            Assert.That(fixture.Host.Calls.Count, Is.EqualTo(callCount));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
        }

        [Test]
        public void DestroyBattleWorlds_WhenRemoteDestroyFails_StillDestroysConfirmedAndPropagates()
        {
            var calls = new List<string>();
            var failure = new InvalidOperationException("remote destroy failed");

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                SessionSimRuntimeDisposer.DestroyBattleWorlds(
                    () =>
                    {
                        calls.Add("remote");
                        throw failure;
                    },
                    () => calls.Add("confirmed")));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(new[] { "remote", "confirmed" }));
        }

        [Test]
        public void DestroyBattleWorlds_WhenBothDestroyOperationsFail_AggregatesBothFailures()
        {
            var calls = new List<string>();

            var thrown = Assert.Throws<AggregateException>(() =>
                SessionSimRuntimeDisposer.DestroyBattleWorlds(
                    () =>
                    {
                        calls.Add("remote");
                        throw new InvalidOperationException("remote destroy failed");
                    },
                    () =>
                    {
                        calls.Add("confirmed");
                        throw new InvalidOperationException("confirmed destroy failed");
                    }));

            Assert.That(calls, Is.EqualTo(new[] { "remote", "confirmed" }));
            Assert.That(thrown.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(thrown.InnerExceptions[0].Message, Is.EqualTo("remote destroy failed"));
            Assert.That(thrown.InnerExceptions[1].Message, Is.EqualTo("confirmed destroy failed"));
        }

        [Test]
        public void ExecuteCleanupSteps_WhenMiddleStepFails_ContinuesAndPropagatesOriginalFailure()
        {
            var calls = new List<string>();
            var failure = new InvalidOperationException("middle failed");

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "cleanup failed",
                    () => calls.Add("first"),
                    () =>
                    {
                        calls.Add("second");
                        throw failure;
                    },
                    () => calls.Add("third")));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void ExecuteCleanupSteps_WhenMultipleStepsFail_AggregatesInExecutionOrder()
        {
            var calls = new List<string>();

            var thrown = Assert.Throws<AggregateException>(() =>
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "cleanup failed",
                    () =>
                    {
                        calls.Add("first");
                        throw new InvalidOperationException("first failed");
                    },
                    () => calls.Add("second"),
                    () =>
                    {
                        calls.Add("third");
                        throw new InvalidOperationException("third failed");
                    }));

            Assert.That(calls, Is.EqualTo(new[] { "first", "second", "third" }));
            Assert.That(thrown.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(thrown.InnerExceptions[0].Message, Is.EqualTo("first failed"));
            Assert.That(thrown.InnerExceptions[1].Message, Is.EqualTo("third failed"));
        }

        [Test]
        public void ResetSessionResources_PreservesAttachmentOwnedPhaseAndGatewayBindings()
        {
            var handles = new BattleSessionHandles();
            var context = new BattleContext();
            var owner = new object();
            handles.Phase.Ctx = context;
            handles.GatewayRoom.ConnectionOwner = owner;

            handles.ResetSessionResources();

            Assert.That(handles.Phase.Ctx, Is.SameAs(context));
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.SameAs(owner));
        }

        private static Fixture CreateFixture()
        {
            var state = new BattleSessionState();
            var handles = new BattleSessionHandles();
            var host = new FailureInjectingHost(CreatePlan());
            return new Fixture(state, host, new SessionOrchestrator(state, handles, host));
        }

        private static BattleStartPlan CreatePlan(
            string gatewaySessionToken = "token",
            bool gatewayAutoCreateRoom = false,
            bool gatewayAutoJoinRoom = false,
            int timeSyncIntervalMs = 1000)
        {
            return new BattleStartPlan(
                worldId: "world",
                worldType: "moba",
                clientId: "client",
                playerId: "1",
                tickRate: 30,
                inputDelayFrames: 0,
                hostMode: BattleHostMode.GatewayRemote,
                useGatewayTransport: true,
                gatewayHost: "127.0.0.1",
                gatewayPort: 4000,
                numericRoomId: 1,
                gatewaySessionToken: gatewaySessionToken,
                gatewayRegion: "test",
                gatewayServerId: "server",
                gatewayAutoCreateRoom: gatewayAutoCreateRoom,
                gatewayAutoJoinRoom: gatewayAutoJoinRoom,
                gatewayJoinRoomId: string.Empty,
                gatewayCreateRoomOpCode: 110,
                gatewayJoinRoomOpCode: 111,
                autoConnect: false,
                autoCreateWorld: false,
                autoJoin: false,
                autoReady: false,
                syncMode: BattleSyncMode.SnapshotAuthority,
                viewEventSourceMode: BattleViewEventSourceMode.SnapshotOnly,
                enableClientPrediction: true,
                enableConfirmedAuthorityWorld: true,
                enableInputRecording: false,
                inputRecordOutputPath: string.Empty,
                enableInputReplay: false,
                inputReplayPath: string.Empty,
                runMode: BattleRunMode.Normal,
                createWorldOpCode: 0,
                createWorldPayload: null,
                timeSyncIntervalMs: timeSyncIntervalMs);
        }

        private readonly struct Fixture
        {
            public readonly BattleSessionState State;
            public readonly FailureInjectingHost Host;
            public readonly SessionOrchestrator Orchestrator;

            public Fixture(BattleSessionState state, FailureInjectingHost host, SessionOrchestrator orchestrator)
            {
                State = state;
                Host = host;
                Orchestrator = orchestrator;
            }
        }

        private sealed class FailureInjectingHost : ISessionOrchestratorHost
        {
            private readonly Dictionary<string, int> _failures = new Dictionary<string, int>();

            public FailureInjectingHost(BattleStartPlan plan)
            {
                Plan = plan;
            }

            public BattleStartPlan Plan { get; }
            public BattleContext Context { get; } = new BattleContext();
            public List<string> Calls { get; } = new List<string>();
            public List<string> CleanupCalls { get; } = new List<string>();
            public bool LogicSessionActive { get; private set; }
            public bool FrameSubscriptionActive { get; private set; }
            public bool RemoteWorldActive { get; private set; }
            public bool ConfirmedWorldActive { get; private set; }
            public bool PipelineActive { get; private set; }
            public bool ReplayActive { get; private set; }
            public int ResetHandlesCount { get; private set; }

            public bool HasActiveSessionResources =>
                LogicSessionActive || FrameSubscriptionActive || RemoteWorldActive ||
                ConfirmedWorldActive || PipelineActive || ReplayActive;

            public void FailNext(string step)
            {
                _failures[step] = 1;
            }

            public int CountCalls(string step)
            {
                var count = 0;
                for (var i = 0; i < Calls.Count; i++)
                {
                    if (Calls[i] == step) count++;
                }
                return count;
            }

            public void StartBattleLogicSession(BattleLogicSessionOptions opts)
            {
                LogicSessionActive = true;
                Call("start-logic");
            }

            public void SubscribeFrameReceived()
            {
                FrameSubscriptionActive = true;
                Call("subscribe");
            }

            public void UnsubscribeFrameReceived()
            {
                CleanupCall("unsubscribe");
                FrameSubscriptionActive = false;
            }

            public void StopBattleLogicSession()
            {
                CleanupCall("stop-logic");
                LogicSessionActive = false;
            }

            public void InvokeSessionStartingPipeline()
            {
                PipelineActive = true;
                Call("pipeline-start");
            }

            public void InvokeSessionStoppingPipeline()
            {
                CleanupCall("pipeline-stop");
                PipelineActive = false;
            }

            public void InvokeReplaySetupPipeline()
            {
                ReplayActive = true;
                Call("replay-setup");
            }

            public void StartRemoteDrivenLocalWorld()
            {
                RemoteWorldActive = true;
                Call("start-remote");
            }

            public void StartConfirmedAuthorityWorld()
            {
                ConfirmedWorldActive = true;
                Call("start-confirmed");
            }

            public void DisposeReplayRecordWriter() => CleanupCall("record-writer");
            public void TryDestroyBattleWorlds() => CleanupCall("destroy-worlds");
            public void DisposeSnapshotRouting() => CleanupCall("snapshot-routing");
            public void DisposeConfirmedView() => CleanupCall("confirmed-view");

            public void DisposeRemoteDrivenWorld()
            {
                CleanupCall("remote-world");
                RemoteWorldActive = false;
            }

            public void DisposeConfirmedWorld()
            {
                CleanupCall("confirmed-world");
                ConfirmedWorldActive = false;
            }

            public void DisposeRemoteInterpolation() => CleanupCall("remote-interpolation");

            public void ResetSessionHandles()
            {
                CleanupCall("reset-handles");
                ResetHandlesCount++;
                ReplayActive = false;
                LogicSessionActive = false;
                FrameSubscriptionActive = false;
                RemoteWorldActive = false;
                ConfirmedWorldActive = false;
                PipelineActive = false;
            }

            private void CleanupCall(string step)
            {
                CleanupCalls.Add(step);
                Call(step);
            }

            private void Call(string step)
            {
                Calls.Add(step);
                if (!_failures.TryGetValue(step, out var remaining) || remaining <= 0) return;

                _failures[step] = remaining - 1;
                throw new InvalidOperationException($"Injected failure: {step}");
            }
        }
    }
}

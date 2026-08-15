using System;
using System.Collections;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MultiplayerGatewayRecoveryRuntimeTests
    {
        [UnityTest]
        public IEnumerator ReconnectLifecycle_RestoresRoomThroughFrameworkActionRouter()
        {
            var restoreCalls = 0;
            var runtime = new MultiplayerGatewayRecoveryRuntime(
                _ =>
                {
                    restoreCalls++;
                    return Task.FromResult(Restored(MultiplayerRoomPhase.Lobby));
                });

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionLost);
            yield return Wait(runtime.PendingExecution);
            Report(runtime, NetworkSessionRecoverySignalKind.ReconnectScheduled);
            yield return Wait(runtime.PendingExecution);
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.ReconnectScheduled));

            Report(runtime, NetworkSessionRecoverySignalKind.ReconnectAttemptStarted);
            yield return Wait(runtime.PendingExecution);
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.ReconnectAttempt));

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionRestored);
            yield return Wait(runtime.PendingExecution);

            Assert.That(restoreCalls, Is.EqualTo(1));
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.Recovered));
            Assert.That(runtime.Decision.Signal.Kind,
                Is.EqualTo(NetworkSessionRecoverySignalKind.Recovered));
            Assert.That(runtime.Decision.HasAction, Is.False);
        }

        [UnityTest]
        public IEnumerator ReconnectExhausted_ExposesManualRetryStateWithoutRestoringRoom()
        {
            var restoreCalls = 0;
            var runtime = new MultiplayerGatewayRecoveryRuntime(
                _ =>
                {
                    restoreCalls++;
                    return Task.FromResult(Restored(MultiplayerRoomPhase.Lobby));
                });

            Report(runtime, NetworkSessionRecoverySignalKind.ReconnectExhausted);
            yield return Wait(runtime.PendingExecution);

            Assert.That(restoreCalls, Is.Zero);
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.ReconnectExhausted));
            Assert.That(runtime.Decision.Action,
                Is.EqualTo(NetworkSessionRecoveryAction.RebuildSession));
        }

        [UnityTest]
        public IEnumerator LoadingRoomRestore_CompletesWhenRoomFlowReachesBattle()
        {
            var runtime = new MultiplayerGatewayRecoveryRuntime(
                _ => Task.FromResult(Restored(MultiplayerRoomPhase.Loading)));

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionRestored);
            yield return Wait(runtime.PendingExecution);

            Assert.That(runtime.State,
                Is.EqualTo(MultiplayerRecoveryState.RestoringLoadingBarrier));
            Assert.That(runtime.Decision.HasAction, Is.True);

            runtime.ObserveRoomFlowState(MultiplayerRoomFlowState.InBattle);

            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.Recovered));
            Assert.That(runtime.Decision.Signal.Kind,
                Is.EqualTo(NetworkSessionRecoverySignalKind.Recovered));
        }

        [UnityTest]
        public IEnumerator RoomRestoreFailure_EscalatesToReconnectExhausted()
        {
            var failure = new InvalidOperationException("restore-failure");
            Exception observed = null;
            var runtime = new MultiplayerGatewayRecoveryRuntime(
                _ => Task.FromException<MultiplayerRoomRestoreResult>(failure),
                failure: exception => observed = exception);

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionRestored);
            yield return Wait(runtime.PendingExecution);

            Assert.That(observed, Is.SameAs(failure));
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.ReconnectExhausted));
            Assert.That(runtime.Decision.Signal.Kind,
                Is.EqualTo(NetworkSessionRecoverySignalKind.ReconnectExhausted));
        }

        [UnityTest]
        public IEnumerator Reset_PreventsStaleRoomRestoreFromMutatingNewLifecycle()
        {
            var completion = new TaskCompletionSource<MultiplayerRoomRestoreResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new MultiplayerGatewayRecoveryRuntime(_ => completion.Task);

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionRestored);
            var staleExecution = runtime.PendingExecution;
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.RestoringRoom));

            runtime.Reset();
            completion.SetResult(Restored(MultiplayerRoomPhase.Lobby));
            yield return Wait(staleExecution);

            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.None));
            Assert.That(runtime.Decision.HasDecision, Is.False);
        }

        [UnityTest]
        public IEnumerator ReconnectExhausted_PreventsStaleRoomRestoreFromOverwritingManualRetry()
        {
            var completion = new TaskCompletionSource<MultiplayerRoomRestoreResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new MultiplayerGatewayRecoveryRuntime(_ => completion.Task);

            Report(runtime, NetworkSessionRecoverySignalKind.ConnectionRestored);
            var staleExecution = runtime.PendingExecution;
            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.RestoringRoom));

            Report(runtime, NetworkSessionRecoverySignalKind.ReconnectExhausted);
            yield return Wait(runtime.PendingExecution);
            completion.SetResult(Restored(MultiplayerRoomPhase.Lobby));
            yield return Wait(staleExecution);

            Assert.That(runtime.State, Is.EqualTo(MultiplayerRecoveryState.ReconnectExhausted));
            Assert.That(runtime.Decision.Signal.Kind,
                Is.EqualTo(NetworkSessionRecoverySignalKind.ReconnectExhausted));
        }

        private static void Report(
            MultiplayerGatewayRecoveryRuntime runtime,
            NetworkSessionRecoverySignalKind kind)
        {
            var severity = kind == NetworkSessionRecoverySignalKind.ConnectionRestored
                ? SyncHealthSeverity.Info
                : SyncHealthSeverity.Warning;
            var signal = new NetworkSessionRecoverySignal(
                kind,
                severity,
                correlationContext: "room-1");
            Assert.That(runtime.TryReport(in signal, out _), Is.True);
        }

        private static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            task.GetAwaiter().GetResult();
        }

        private static MultiplayerRoomRestoreResult Restored(MultiplayerRoomPhase phase)
        {
            return new MultiplayerRoomRestoreResult(
                "room-1",
                1UL,
                7u,
                phase,
                MultiplayerRoomRestoreNextStep.None,
                MultiplayerRoomEntryKind.Reconnect,
                canStart: true,
                "restored",
                MultiplayerRoomRestoreStatus.Restored,
                MultiplayerRoomRestoreErrorCode.None);
        }
    }
}

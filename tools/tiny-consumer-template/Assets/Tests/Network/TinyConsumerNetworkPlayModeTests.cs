using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TinyConsumer.Tests
{
    public sealed class TinyConsumerNetworkPlayModeTests
    {
        [Serializable]
        private sealed class Evidence
        {
            public List<ModeEvidence> modes = new List<ModeEvidence>();
        }

        [Serializable]
        private sealed class ModeEvidence
        {
            public string mode;
            public string roomId;
            public bool sceneRootReady;
            public bool ownerProjected;
            public bool guestConverged;
            public bool guestRestored;
            public bool returnedToLobby;
            public bool predictedBeforeConfirmation;
            public int authoritativeFrame;
            public int predictedFrame;
            public int localPredictions;
            public int rollbacks;
            public int snapshotCorrections;
            public int recoveryRequests;
        }

        private sealed class Login : IDisposable
        {
            public Login(RoomGatewayConnectionSession connection, string accountId,
                string sessionToken)
            {
                Connection = connection;
                AccountId = accountId;
                SessionToken = sessionToken;
            }

            public RoomGatewayConnectionSession Connection { get; }
            public string AccountId { get; }
            public string SessionToken { get; }
            public void Dispose() => Connection.Dispose();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator StandaloneLobbyRunsAllModesThroughSceneRoot()
        {
            var portText = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT");
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            var evidencePath = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_EVIDENCE");
            Assert.That(int.TryParse(portText, out var port) && port > 0, Is.True);
            Assert.That(prefix, Is.Not.Empty);
            Assert.That(evidencePath, Is.Not.Empty);

            var evidence = new Evidence();
            foreach (var mode in new[] { TinySyncMode.State, TinySyncMode.Frame, TinySyncMode.Hybrid })
                yield return RunMode(mode, port, prefix, evidence);
            File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
            Assert.That(evidence.modes.Count, Is.EqualTo(3));
        }

        private static IEnumerator RunMode(TinySyncMode mode, int port, string prefix,
            Evidence evidence)
        {
            SceneManager.LoadScene("ConsumerLobby", LoadSceneMode.Single);
            yield return null;
            var lobby = UnityEngine.Object.FindObjectOfType<TinyConsumerLobby>();
            Assert.That(lobby, Is.Not.Null);

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75));
            var accountPrefix = prefix + "-consumer-" + mode.ToString().ToLowerInvariant();
            Login ownerLogin = null;
            Login guestLogin = null;
            TinyBattleSession guest = null;
            try
            {
                var ownerLoginTask = LoginAsync(port, accountPrefix + "-owner", deadline.Token);
                yield return Await(ownerLoginTask);
                ownerLogin = ownerLoginTask.Result;
                var guestLoginTask = LoginAsync(port, accountPrefix + "-guest", deadline.Token);
                yield return Await(guestLoginTask);
                guestLogin = guestLoginTask.Result;

                lobby.Enter(Launch(ownerLogin, port));
                TinyGameplayRoot root = null;
                var rootDeadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < rootDeadline)
                {
                    root = UnityEngine.Object.FindObjectOfType<TinyGameplayRoot>();
                    if (root != null && root.IsReady) break;
                    if (root != null && !string.IsNullOrEmpty(root.LastError))
                        Assert.Fail(root.LastError);
                    yield return null;
                }
                Assert.That(root, Is.Not.Null);
                Assert.That(root.IsReady, Is.True, "The consumer scene root did not restore its session.");
                var owner = root.Session;
                Assert.That(owner, Is.Not.Null);

                var guestGateway = TinyGatewayClient.ConnectAsync("127.0.0.1", port, deadline.Token);
                yield return Await(guestGateway);
                guest = new TinyBattleSession(Launch(guestLogin, port), guestGateway.Result);
                yield return Await(guest.RestoreAsync(deadline.Token));

                yield return Await(owner.CreateRoomAsync(mode, deadline.Token));
                yield return Await(guest.JoinRoomAsync(owner.RoomId, deadline.Token));
                yield return Await(owner.SetReadyAsync(deadline.Token));
                yield return Await(guest.SetReadyAsync(deadline.Token));
                var readyDeadline = Time.realtimeSinceStartup + 15f;
                while (!owner.CanStart && Time.realtimeSinceStartup < readyDeadline)
                    yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(owner.CanStart, Is.True);
                yield return Await(owner.BeginLoadingAsync(deadline.Token));

                var baselineDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < baselineDeadline)
                {
                    yield return Await(guest.PollAsync(deadline.Token));
                    guest.Tick(0.025f);
                    guest.TryGetNewSnapshot(out _);
                    if (owner.CanSubmitInput && guest.CanSubmitInput &&
                        Actor(root, guest.PlayerId) != null) break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                Assert.That(owner.SyncMode, Is.EqualTo(mode));
                Assert.That(guest.SyncMode, Is.EqualTo(mode));
                Assert.That(owner.CanSubmitInput && guest.CanSubmitInput, Is.True,
                    mode + " did not reach a usable baseline through the scene root.");

                var submission = owner.SubmitInputAsync(new TinyInput(1, 0, true), deadline.Token);
                var predicted = owner.Telemetry.LocalPredictions > 0;
                Assert.That(predicted, Is.EqualTo(mode == TinySyncMode.Hybrid));
                yield return Await(submission);

                var ownerProjected = false;
                var guestConverged = false;
                var convergeDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < convergeDeadline)
                {
                    guest.Tick(0.025f);
                    if (guest.TryGetNewSnapshot(out var snapshot) && snapshot.Actors != null)
                        foreach (var actor in snapshot.Actors)
                            if (actor.ActorId == guest.PlayerId && actor.Hp == 90)
                                guestConverged = true;
                    var view = Actor(root, guest.PlayerId);
                    ownerProjected |= view != null &&
                        Mathf.Abs(view.transform.localScale.y - 0.9f) < 0.0001f;
                    if (ownerProjected && guestConverged) break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                Assert.That(ownerProjected && guestConverged, Is.True,
                    mode + " did not project and converge on the same attack.");

                guest.Dispose();
                guest = null;
                guestLogin.Dispose();
                guestLogin = null;
                var restoredLoginTask = LoginAsync(port, accountPrefix + "-guest", deadline.Token);
                yield return Await(restoredLoginTask);
                guestLogin = restoredLoginTask.Result;
                var restoredGateway = TinyGatewayClient.ConnectAsync("127.0.0.1", port, deadline.Token);
                yield return Await(restoredGateway);
                guest = new TinyBattleSession(Launch(guestLogin, port), restoredGateway.Result);
                yield return Await(guest.RestoreAsync(deadline.Token));
                Assert.That(guest.RoomId, Is.EqualTo(owner.RoomId));
                Assert.That(guest.BattleId, Is.EqualTo(owner.BattleId));
                var guestRestored = false;
                var restoreDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < restoreDeadline)
                {
                    guest.Tick(0.025f);
                    if (guest.TryGetNewSnapshot(out var restored) && restored.Actors != null)
                        foreach (var actor in restored.Actors)
                            if (actor.ActorId == guest.PlayerId && actor.Hp == 90)
                                guestRestored = guest.CanSubmitInput;
                    if (guestRestored) break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                Assert.That(guestRestored, Is.True, mode + " guest did not restore its baseline.");

                var telemetry = owner.Telemetry;
                var result = new ModeEvidence
                {
                    mode = mode.ToString(), roomId = owner.RoomId,
                    sceneRootReady = root.IsReady, ownerProjected = ownerProjected,
                    guestConverged = guestConverged, guestRestored = guestRestored,
                    predictedBeforeConfirmation = predicted,
                    authoritativeFrame = telemetry.AuthoritativeFrame,
                    predictedFrame = telemetry.PredictedFrame,
                    localPredictions = telemetry.LocalPredictions,
                    rollbacks = telemetry.Rollbacks,
                    snapshotCorrections = telemetry.SnapshotCorrections,
                    recoveryRequests = telemetry.RecoveryRequests
                };
                root.SendMessage("ReturnToLobbyAsync");
                var lobbyDeadline = Time.realtimeSinceStartup + 15f;
                while (SceneManager.GetActiveScene().name != "ConsumerLobby" &&
                    Time.realtimeSinceStartup < lobbyDeadline)
                    yield return null;
                result.returnedToLobby = SceneManager.GetActiveScene().name == "ConsumerLobby";
                Assert.That(result.returnedToLobby, Is.True, root.LastError);
                evidence.modes.Add(result);
            }
            finally
            {
                guest?.Dispose();
                guestLogin?.Dispose();
                ownerLogin?.Dispose();
            }
        }

        private static GameObject Actor(TinyGameplayRoot root, uint playerId) =>
            root.transform.Find("Tiny Actor " + playerId)?.gameObject;

        private static DemoMultiplayerLaunchRequest Launch(Login login, int port) =>
            new DemoMultiplayerLaunchRequest("127.0.0.1", port, "dev", "local",
                login.AccountId, login.SessionToken, TimeSpan.FromSeconds(10));

        private static async Task<Login> LoginAsync(int port, string accountId,
            CancellationToken token)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync("127.0.0.1", port,
                timeout: TimeSpan.FromSeconds(10), cancellationToken: token);
            try
            {
                var result = await connection.RoomClient.AccountLoginAsync(accountId,
                    kickExisting: true, TimeSpan.FromSeconds(10), token);
                if (!result.Success) throw new InvalidOperationException(result.Message);
                return new Login(connection, result.AccountId, result.SessionToken);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static IEnumerator Await(Task task)
        {
            var timeout = Time.realtimeSinceStartup + 30f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Gateway request timed out.");
            task.GetAwaiter().GetResult();
        }
    }
}

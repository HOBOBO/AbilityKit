using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyGatewayPlayModeTests
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
            public bool ownerProjected;
            public bool guestProjected;
            public bool guestRestored;
            public int authoritativeFrame;
        }

        private sealed class LoginSession : IDisposable
        {
            public LoginSession(RoomGatewayConnectionSession connection, string accountId,
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
        [Timeout(180000)]
        public IEnumerator RealGatewayProjectsAllModesAndRestoresGuest()
        {
            var portText = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT");
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            var evidencePath = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_EVIDENCE");
            Assert.That(int.TryParse(portText, out var port) && port > 0, Is.True,
                "A live Tiny Gateway port is required.");
            Assert.That(prefix, Is.Not.Empty);
            Assert.That(evidencePath, Is.Not.Empty);

            var evidence = new Evidence();
            foreach (var mode in new[] { TinySyncMode.State, TinySyncMode.Frame, TinySyncMode.Hybrid })
                yield return VerifyMode(mode, port, prefix, evidence);
            File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
            Assert.That(evidence.modes.Count, Is.EqualTo(3));
        }

        private static IEnumerator VerifyMode(TinySyncMode mode, int port, string prefix,
            Evidence evidence)
        {
            var accountPrefix = prefix + "-" + mode.ToString().ToLowerInvariant();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            LoginSession ownerLogin = null;
            LoginSession guestLogin = null;
            TinyBattleSession owner = null;
            TinyBattleSession guest = null;
            GameObject ownerRoot = null;
            GameObject guestRoot = null;
            TinyActorViewModule ownerView = null;
            TinyActorViewModule guestView = null;
            TinyViewModuleContext ownerContext = default;
            TinyViewModuleContext guestContext = default;
            try
            {
                var ownerLoginTask = LoginAsync(port, accountPrefix + "-owner", deadline.Token);
                yield return Await(ownerLoginTask);
                ownerLogin = ownerLoginTask.Result;
                var guestLoginTask = LoginAsync(port, accountPrefix + "-guest", deadline.Token);
                yield return Await(guestLoginTask);
                guestLogin = guestLoginTask.Result;

                var ownerGateway = TinyGatewayClient.ConnectAsync("127.0.0.1", port, deadline.Token);
                yield return Await(ownerGateway);
                owner = NewSession(ownerLogin, port, ownerGateway.Result);
                var guestGateway = TinyGatewayClient.ConnectAsync("127.0.0.1", port, deadline.Token);
                yield return Await(guestGateway);
                guest = NewSession(guestLogin, port, guestGateway.Result);

                ownerRoot = new GameObject("Tiny Gateway Owner " + mode);
                guestRoot = new GameObject("Tiny Gateway Guest " + mode);
                ownerView = new TinyActorViewModule();
                guestView = new TinyActorViewModule();
                ownerContext = new TinyViewModuleContext(ownerRoot.transform, () => owner,
                    deadline.Token, exception => throw exception);
                guestContext = new TinyViewModuleContext(guestRoot.transform, () => guest,
                    deadline.Token, exception => throw exception);
                ownerView.OnAttach(in ownerContext);
                guestView.OnAttach(in guestContext);

                yield return Await(owner.CreateRoomAsync(mode, deadline.Token));
                yield return Await(guest.JoinRoomAsync(owner.RoomId, deadline.Token));
                yield return Await(owner.SetReadyAsync(deadline.Token));
                yield return Await(guest.SetReadyAsync(deadline.Token));
                yield return Await(owner.PollAsync(deadline.Token));
                Assert.That(owner.CanStart, Is.True);
                yield return Await(owner.BeginLoadingAsync(deadline.Token));

                var baselineDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < baselineDeadline)
                {
                    yield return Await(Task.WhenAll(owner.PollAsync(deadline.Token),
                        guest.PollAsync(deadline.Token)));
                    TickViews(owner, guest, ownerView, guestView, in ownerContext, in guestContext);
                    if (owner.CanSubmitInput && guest.CanSubmitInput &&
                        Actor(ownerRoot, guest.PlayerId) != null &&
                        Actor(guestRoot, guest.PlayerId) != null) break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                Assert.That(owner.CanSubmitInput && guest.CanSubmitInput, Is.True,
                    mode + " did not receive a battle baseline.");
                Assert.That(owner.SyncMode, Is.EqualTo(mode));
                Assert.That(guest.SyncMode, Is.EqualTo(mode));

                var guestActorId = guest.PlayerId;
                var ownerActorId = owner.PlayerId;
                var submission = owner.SubmitInputAsync(new TinyInput(1, 0, true), deadline.Token);
                if (mode == TinySyncMode.Hybrid)
                {
                    ownerView.Tick(in ownerContext, 0.025f);
                    Assert.That(Actor(ownerRoot, guestActorId).transform.localScale.y,
                        Is.EqualTo(0.9f).Within(0.0001f),
                        "Hybrid did not project the local prediction before confirmation.");
                }
                yield return Await(submission);

                var convergenceDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < convergenceDeadline)
                {
                    TickViews(owner, guest, ownerView, guestView, in ownerContext, in guestContext);
                    var ownerGuest = Actor(ownerRoot, guestActorId);
                    var guestGuest = Actor(guestRoot, guestActorId);
                    if (ownerGuest != null && guestGuest != null &&
                        Mathf.Abs(ownerGuest.transform.localScale.y - 0.9f) < 0.0001f &&
                        Mathf.Abs(guestGuest.transform.localScale.y - 0.9f) < 0.0001f &&
                        Mathf.Abs(Actor(ownerRoot, ownerActorId).transform.position.x) < 0.0001f &&
                        Mathf.Abs(Actor(guestRoot, ownerActorId).transform.position.x) < 0.0001f)
                        break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                var ownerProjected = Mathf.Abs(Actor(ownerRoot, guestActorId)
                    .transform.localScale.y - 0.9f) < 0.0001f;
                var guestProjected = Mathf.Abs(Actor(guestRoot, guestActorId)
                    .transform.localScale.y - 0.9f) < 0.0001f;
                Assert.That(ownerProjected && guestProjected, Is.True,
                    mode + " clients did not project the same attack.");

                guest.Dispose();
                guest = null;
                guestLogin.Dispose();
                guestLogin = null;
                var restoredLoginTask = LoginAsync(port, accountPrefix + "-guest", deadline.Token);
                yield return Await(restoredLoginTask);
                guestLogin = restoredLoginTask.Result;
                var restoredGateway = TinyGatewayClient.ConnectAsync("127.0.0.1", port, deadline.Token);
                yield return Await(restoredGateway);
                guest = NewSession(guestLogin, port, restoredGateway.Result);
                yield return Await(guest.RestoreAsync(deadline.Token));
                Assert.That(guest.RoomId, Is.EqualTo(owner.RoomId));
                Assert.That(guest.BattleId, Is.EqualTo(owner.BattleId));

                var restoreDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < restoreDeadline)
                {
                    TickViews(owner, guest, ownerView, guestView, in ownerContext, in guestContext);
                    var restoredActor = Actor(guestRoot, guest.PlayerId);
                    if (guest.CanSubmitInput && restoredActor != null &&
                        Mathf.Abs(restoredActor.transform.localScale.y - 0.9f) < 0.0001f)
                        break;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                var guestRestored = guest.CanSubmitInput &&
                    Mathf.Abs(Actor(guestRoot, guest.PlayerId).transform.localScale.y - 0.9f) < 0.0001f;
                Assert.That(guestRestored, Is.True, mode + " guest view did not recover.");
                evidence.modes.Add(new ModeEvidence
                {
                    mode = mode.ToString(), roomId = owner.RoomId,
                    ownerProjected = ownerProjected, guestProjected = guestProjected,
                    guestRestored = guestRestored,
                    authoritativeFrame = owner.Telemetry.AuthoritativeFrame
                });
            }
            finally
            {
                if (ownerView != null) ownerView.OnDetach(in ownerContext);
                if (guestView != null) guestView.OnDetach(in guestContext);
                owner?.Dispose();
                guest?.Dispose();
                ownerLogin?.Dispose();
                guestLogin?.Dispose();
                if (ownerRoot != null) UnityEngine.Object.Destroy(ownerRoot);
                if (guestRoot != null) UnityEngine.Object.Destroy(guestRoot);
            }
        }

        private static GameObject Actor(GameObject root, uint playerId) =>
            root.transform.Find("Tiny Actor " + playerId)?.gameObject;

        private static void TickViews(TinyBattleSession owner, TinyBattleSession guest,
            TinyActorViewModule ownerView, TinyActorViewModule guestView,
            in TinyViewModuleContext ownerContext, in TinyViewModuleContext guestContext)
        {
            owner.Tick(0.025f);
            guest.Tick(0.025f);
            ownerView.Tick(in ownerContext, 0.025f);
            guestView.Tick(in guestContext, 0.025f);
        }

        private static TinyBattleSession NewSession(LoginSession login, int port,
            TinyGatewayClient gateway) => new TinyBattleSession(
                new DemoMultiplayerLaunchRequest("127.0.0.1", port, "dev", "local",
                    login.AccountId, login.SessionToken, TimeSpan.FromSeconds(10)), gateway);

        private static async Task<LoginSession> LoginAsync(int port, string accountId,
            CancellationToken token)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync("127.0.0.1", port,
                timeout: TimeSpan.FromSeconds(10), cancellationToken: token);
            try
            {
                var result = await connection.RoomClient.AccountLoginAsync(accountId,
                    kickExisting: true, TimeSpan.FromSeconds(10), token);
                if (!result.Success) throw new InvalidOperationException(result.Message);
                return new LoginSession(connection, result.AccountId, result.SessionToken);
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

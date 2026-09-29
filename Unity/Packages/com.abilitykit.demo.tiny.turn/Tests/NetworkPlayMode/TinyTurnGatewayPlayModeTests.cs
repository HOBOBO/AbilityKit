#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Turn.View;
using AbilityKit.Network.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Tiny.Turn.Tests
{
    public sealed class TinyTurnGatewayPlayModeTests
    {
        [UnityTest]
        public IEnumerator CancelledOperationCannotClearReplacement()
        {
            var gameObject = new GameObject("Turn operation test");
            gameObject.SetActive(false);
            var root = gameObject.AddComponent<TinyTurnGameplayRoot>();
            var lifetime = new CancellationTokenSource();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TinyTurnGameplayRoot).GetField("_lifetime", flags)!.SetValue(root, lifetime);
            var run = typeof(TinyTurnGameplayRoot).GetMethod("Run", flags, null,
                new[] { typeof(Func<CancellationToken, Task>), typeof(bool) }, null)!;
            var cancel = typeof(TinyTurnGameplayRoot).GetMethod("CancelActiveRequest", flags)!;
            var old = new TaskCompletionSource<bool>();
            var replacement = new TaskCompletionSource<bool>();
            var started = 0;
            try
            {
                run.Invoke(root, new object[] { (Func<CancellationToken, Task>)(_ => old.Task), false });
                cancel.Invoke(root, null);
                run.Invoke(root, new object[]
                {
                    (Func<CancellationToken, Task>)(_ => replacement.Task), true
                });
                old.SetException(new InvalidOperationException("stale response"));
                yield return null;
                Assert.That(root.LastError, Is.Empty);
                run.Invoke(root, new object[]
                {
                    (Func<CancellationToken, Task>)(_ => { started++; return Task.CompletedTask; }), false
                });
                Assert.That(started, Is.Zero, "Old completion cleared the replacement operation.");
                replacement.SetResult(true);
                yield return null;
                run.Invoke(root, new object[]
                {
                    (Func<CancellationToken, Task>)(_ => { started++; return Task.CompletedTask; }), false
                });
                Assert.That(started, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                lifetime.Dispose();
            }
        }

        [Serializable]
        private sealed class Evidence
        {
            public string roomId = string.Empty;
            public int turn;
            public uint winnerId;
            public bool guestRestored;
            public bool returnedToLobby;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TurnSceneUsesGatewayAndRestoresGuest()
        {
            var portText = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT");
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            var evidencePath = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_EVIDENCE");
            Assert.That(int.TryParse(portText, out var port) && port > 0, Is.True);
            Assert.That(prefix, Is.Not.Empty);
            Assert.That(evidencePath, Is.Not.Empty);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var ownerAccount = prefix + "-turn-owner";
            var guestAccount = prefix + "-turn-guest";
            var ownerLoginTask = LoginAsync(port, ownerAccount, deadline.Token);
            yield return Await(ownerLoginTask);
            using var ownerLogin = ownerLoginTask.Result;
            var guestLoginTask = LoginAsync(port, guestAccount, deadline.Token);
            yield return Await(guestLoginTask);
            using var guestLogin = guestLoginTask.Result;

            TinyTurnProjectLaunch.Prepare(Launch(ownerLogin, port), "StarterScene");
            SceneManager.LoadScene(TinyTurnProjectLaunch.SceneName, LoadSceneMode.Single);
            TinyTurnGameplayRoot? root = null;
            var startDeadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < startDeadline)
            {
                root = UnityEngine.Object.FindObjectOfType<TinyTurnGameplayRoot>();
                if (root != null && root.IsReady) break;
                if (root != null && root.LastError.Length != 0) Assert.Fail(root.LastError);
                yield return null;
            }
            Assert.That(root?.IsReady, Is.True);
            var owner = root!.Session!;
            var guestTask = TinyTurnSession.ConnectAsync(Launch(guestLogin, port), deadline.Token);
            yield return Await(guestTask);
            TinyTurnSession? guest = guestTask.Result;
            try
            {
                yield return Await(guest.RestoreAsync(deadline.Token));
                yield return Await(owner.CreateRoomAsync(deadline.Token));
                yield return Await(guest.JoinRoomAsync(owner.RoomId, deadline.Token));
                yield return Await(owner.SetReadyAsync(deadline.Token));
                yield return Await(guest.SetReadyAsync(deadline.Token));
                var readyDeadline = Time.realtimeSinceStartup + 15f;
                while (!owner.CanStart && Time.realtimeSinceStartup < readyDeadline)
                    yield return null;
                Assert.That(owner.CanStart, Is.True);
                yield return Await(owner.BeginLoadingAsync(deadline.Token));
                yield return WaitForTurn(owner, guest, 0, deadline.Token);
                Assert.That(guest.CanAct, Is.False);
                yield return Await(owner.SubmitActionAsync(deadline.Token));
                Assert.That(owner.ActionPending, Is.True);
                Assert.That(owner.CanAct, Is.False);
                yield return Await(owner.RetryPendingActionAsync(deadline.Token));
                yield return WaitForTurn(owner, guest, 1, deadline.Token);
                Assert.That(guest.CanAct, Is.True);

                guest.Dispose();
                guest = null;
                var replacementLoginTask = LoginAsync(port, guestAccount, deadline.Token);
                yield return Await(replacementLoginTask);
                using var replacementLogin = replacementLoginTask.Result;
                var restoredTask = TinyTurnSession.ConnectAsync(
                    Launch(replacementLogin, port), deadline.Token);
                yield return Await(restoredTask);
                using var restored = restoredTask.Result;
                yield return Await(restored.RestoreAsync(deadline.Token));
                Assert.That(restored.NeedsFullSnapshot, Is.True);
                var requestCount = restored.SnapshotRequestCount;
                yield return Await(restored.RequestFullSnapshotAsync(
                    "Turn recovery test refresh", deadline.Token));
                Assert.That(restored.SnapshotRequestCount, Is.EqualTo(requestCount + 1));
                yield return WaitForTurn(owner, restored, 1, deadline.Token);
                Assert.That(restored.CanAct, Is.True);
                yield return Await(restored.SubmitActionAsync(deadline.Token));
                yield return WaitForTurn(owner, restored, 2, deadline.Token);
                yield return Await(owner.SubmitActionAsync(deadline.Token));
                yield return WaitForTurn(owner, restored, 3, deadline.Token);
                Assert.That(owner.State!.Value.WinnerId, Is.EqualTo(1u));
                Assert.That(restored.State!.Value.WinnerId, Is.EqualTo(1u));
                var evidence = new Evidence
                {
                    roomId = owner.RoomId,
                    turn = owner.State.Value.Turn,
                    winnerId = owner.State.Value.WinnerId,
                    guestRestored = true
                };
                SceneManager.LoadScene("StarterScene", LoadSceneMode.Single);
                yield return null;
                evidence.returnedToLobby = SceneManager.GetActiveScene().name == "StarterScene";
                File.WriteAllText(evidencePath!, JsonUtility.ToJson(evidence, true));
                Assert.That(evidence.returnedToLobby, Is.True);
            }
            finally { guest?.Dispose(); }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OwnerActsAgainAfterRecreatingSession()
        {
            var portText = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT");
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            Assert.That(int.TryParse(portText, out var port) && port > 0, Is.True);
            Assert.That(prefix, Is.Not.Empty);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var ownerAccount = prefix + "-resume-owner";
            var ownerLoginTask = LoginAsync(port, ownerAccount, deadline.Token);
            yield return Await(ownerLoginTask);
            using var ownerLogin = ownerLoginTask.Result;
            var guestLoginTask = LoginAsync(port, prefix + "-resume-guest", deadline.Token);
            yield return Await(guestLoginTask);
            using var guestLogin = guestLoginTask.Result;
            var ownerTask = TinyTurnSession.ConnectAsync(Launch(ownerLogin, port), deadline.Token);
            yield return Await(ownerTask);
            var owner = ownerTask.Result;
            var guestTask = TinyTurnSession.ConnectAsync(Launch(guestLogin, port), deadline.Token);
            yield return Await(guestTask);
            using var guest = guestTask.Result;
            try
            {
                yield return Await(owner.RestoreAsync(deadline.Token));
                yield return Await(guest.RestoreAsync(deadline.Token));
                yield return Await(owner.CreateRoomAsync(deadline.Token));
                yield return Await(guest.JoinRoomAsync(owner.RoomId, deadline.Token));
                yield return Await(owner.SetReadyAsync(deadline.Token));
                yield return Await(guest.SetReadyAsync(deadline.Token));
                var readyDeadline = Time.realtimeSinceStartup + 15f;
                while (!owner.CanStart && Time.realtimeSinceStartup < readyDeadline)
                {
                    yield return Await(owner.PollAsync(deadline.Token));
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                Assert.That(owner.CanStart, Is.True);
                yield return Await(owner.BeginLoadingAsync(deadline.Token));
                yield return WaitForManualTurn(owner, guest, 0, deadline.Token);
                yield return Await(owner.SubmitActionAsync(deadline.Token));
                yield return WaitForManualTurn(owner, guest, 1, deadline.Token);
                yield return Await(guest.SubmitActionAsync(deadline.Token));
                yield return WaitForManualTurn(owner, guest, 2, deadline.Token);
                owner.Dispose();

                var replacementLoginTask = LoginAsync(port, ownerAccount, deadline.Token);
                yield return Await(replacementLoginTask);
                using var replacementLogin = replacementLoginTask.Result;
                var restoredTask = TinyTurnSession.ConnectAsync(
                    Launch(replacementLogin, port), deadline.Token);
                yield return Await(restoredTask);
                using var restored = restoredTask.Result;
                yield return Await(restored.RestoreAsync(deadline.Token));
                yield return WaitForManualTurn(restored, guest, 2, deadline.Token);
                yield return Await(restored.SubmitActionAsync(deadline.Token));
                Assert.That(restored.ActionPending, Is.True);
                yield return WaitForManualTurn(restored, guest, 3, deadline.Token);
                Assert.That(restored.State!.Value.WinnerId, Is.EqualTo(1u));
            }
            finally { owner.Dispose(); }
        }

        private static IEnumerator WaitForManualTurn(TinyTurnSession owner,
            TinyTurnSession guest, int turn, CancellationToken token)
        {
            var until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                token.ThrowIfCancellationRequested();
                yield return Await(owner.PollAsync(token));
                yield return Await(guest.PollAsync(token));
                owner.Tick(0.025f);
                guest.Tick(0.025f);
                if (owner.State?.Turn == turn && guest.State?.Turn == turn) yield break;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.Fail("Tiny Turn sessions did not reach turn " + turn);
        }

        private static IEnumerator WaitForTurn(TinyTurnSession owner, TinyTurnSession guest,
            int turn, CancellationToken token)
        {
            var until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                token.ThrowIfCancellationRequested();
                yield return Await(guest.PollAsync(token));
                guest.Tick(0.025f);
                if (owner.State?.Turn == turn && guest.State?.Turn == turn) yield break;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.Fail("Tiny Turn authoritative state did not reach turn " + turn);
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception!.GetBaseException();
            if (task.IsCanceled) throw new OperationCanceledException();
        }

        private sealed class Login : IDisposable
        {
            public Login(RoomGatewayConnectionSession connection, string accountId, string token)
            { Connection = connection; AccountId = accountId; Token = token; }
            public RoomGatewayConnectionSession Connection { get; }
            public string AccountId { get; }
            public string Token { get; }
            public void Dispose() => Connection.Dispose();
        }

        private static async Task<Login> LoginAsync(int port, string accountId, CancellationToken token)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync(
                "127.0.0.1", port, cancellationToken: token);
            var login = await connection.RoomClient.AccountLoginAsync(
                accountId, true, TimeSpan.FromSeconds(10), token);
            if (!login.Success) throw new InvalidOperationException(login.Message);
            return new Login(connection, login.AccountId, login.SessionToken);
        }

        private static DemoMultiplayerLaunchRequest Launch(Login login, int port) => new(
            "127.0.0.1", port, "dev", "local", login.AccountId, login.Token,
            TimeSpan.FromSeconds(10));
    }
}

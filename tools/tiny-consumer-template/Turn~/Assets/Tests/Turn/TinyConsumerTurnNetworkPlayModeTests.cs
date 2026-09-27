#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Turn.View;
using AbilityKit.Network.Room;
using NUnit.Framework;
using TinyConsumer.Turn;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TinyConsumer.Tests
{
    public sealed class TinyConsumerTurnNetworkPlayModeTests
    {
        [Serializable]
        private sealed class Evidence
        {
            public string roomId = string.Empty;
            public int turn;
            public uint winnerId;
            public bool sceneRootReady;
            public bool returnedToLobby;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OwnTurnLobbyCompletesGatewayBattleAndReturns()
        {
            Assert.That(int.TryParse(Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT"),
                out var port) && port > 0, Is.True);
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            var evidencePath = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_EVIDENCE");
            Assert.That(prefix, Is.Not.Empty);
            Assert.That(evidencePath, Is.Not.Empty);
            SceneManager.LoadScene("ConsumerTurnLobby", LoadSceneMode.Single);
            yield return null;
            var lobby = UnityEngine.Object.FindObjectOfType<TinyConsumerTurnLobby>();
            Assert.That(lobby, Is.Not.Null);

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var ownerLoginTask = LoginAsync(port, prefix + "-owner", deadline.Token);
            yield return Await(ownerLoginTask);
            using var ownerLogin = ownerLoginTask.Result;
            var guestLoginTask = LoginAsync(port, prefix + "-guest", deadline.Token);
            yield return Await(guestLoginTask);
            using var guestLogin = guestLoginTask.Result;
            lobby!.Enter(Launch(ownerLogin, port));
            TinyTurnGameplayRoot? root = null;
            var until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until)
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
            using var guest = guestTask.Result;
            yield return Await(guest.RestoreAsync(deadline.Token));
            yield return Await(owner.CreateRoomAsync(deadline.Token));
            yield return Await(guest.JoinRoomAsync(owner.RoomId, deadline.Token));
            yield return Await(owner.SetReadyAsync(deadline.Token));
            yield return Await(guest.SetReadyAsync(deadline.Token));
            until = Time.realtimeSinceStartup + 15f;
            while (!owner.CanStart && Time.realtimeSinceStartup < until)
                yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(owner.CanStart, Is.True);
            yield return Await(owner.BeginLoadingAsync(deadline.Token));
            yield return WaitForTurn(owner, guest, 0, deadline.Token);
            yield return Await(owner.SubmitActionAsync(deadline.Token));
            yield return WaitForTurn(owner, guest, 1, deadline.Token);
            yield return Await(guest.SubmitActionAsync(deadline.Token));
            yield return WaitForTurn(owner, guest, 2, deadline.Token);
            yield return Await(owner.SubmitActionAsync(deadline.Token));
            yield return WaitForTurn(owner, guest, 3, deadline.Token);
            Assert.That(owner.State!.Value.WinnerId, Is.EqualTo(1u));
            var evidence = new Evidence
            {
                roomId = owner.RoomId, turn = owner.State.Value.Turn,
                winnerId = owner.State.Value.WinnerId, sceneRootReady = root.IsReady
            };
            root.SendMessage("ReturnToLobby");
            until = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != "ConsumerTurnLobby" &&
                Time.realtimeSinceStartup < until)
                yield return null;
            evidence.returnedToLobby = SceneManager.GetActiveScene().name == "ConsumerTurnLobby";
            Assert.That(evidence.returnedToLobby, Is.True, root.LastError);
            File.WriteAllText(evidencePath!, JsonUtility.ToJson(evidence, true));
        }

        private static IEnumerator WaitForTurn(TinyTurnSession owner, TinyTurnSession guest,
            int turn, CancellationToken token)
        {
            var until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until)
            {
                token.ThrowIfCancellationRequested();
                yield return Await(guest.PollAsync(token));
                guest.Tick(0.025f);
                if (owner.State?.Turn == turn && guest.State?.Turn == turn) yield break;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.Fail("Turn consumer did not reach turn " + turn);
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
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
            try
            {
                var login = await connection.RoomClient.AccountLoginAsync(
                    accountId, true, TimeSpan.FromSeconds(10), token);
                if (!login.Success) throw new InvalidOperationException(login.Message);
                return new Login(connection, login.AccountId, login.SessionToken);
            }
            catch { connection.Dispose(); throw; }
        }

        private static DemoMultiplayerLaunchRequest Launch(Login login, int port) => new(
            "127.0.0.1", port, "dev", "local", login.AccountId, login.Token,
            TimeSpan.FromSeconds(10));
    }
}

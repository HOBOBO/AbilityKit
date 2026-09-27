#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Turn.View;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using NUnit.Framework;
using TinyConsumer.Turn;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TinyConsumer.Tests
{
    public sealed class TinyConsumerCrossProcessPlayModeTests
    {
        [Serializable]
        private sealed class Evidence
        {
            public string mode = string.Empty;
            public string role = string.Empty;
            public string roomId = string.Empty;
            public string battleId = string.Empty;
            public int processId;
            public bool sceneRootReady;
            public bool authoritativeResult;
            public bool returnedToLobby;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator IndependentOwnerUsesGateway() => Run("owner");

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator IndependentGuestUsesGateway() => Run("guest");

        private static IEnumerator Run(string role)
        {
            var mode = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_CROSS_MODE");
            var roomFile = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_CROSS_ROOM_FILE");
            var evidenceDirectory = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_CROSS_EVIDENCE_DIR");
            var prefix = Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PREFIX");
            Assert.That(int.TryParse(Environment.GetEnvironmentVariable("ABILITYKIT_TINY_UNITY_PORT"),
                out var port) && port > 0, Is.True);
            Assert.That(mode, Is.EqualTo("State").Or.EqualTo("Turn"));
            Assert.That(roomFile, Is.Not.Empty);
            Assert.That(evidenceDirectory, Is.Not.Empty);
            Assert.That(prefix, Is.Not.Empty);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            var loginTask = LoginAsync(port, prefix + "-" + role, deadline.Token);
            yield return Await(loginTask);
            using var login = loginTask.Result;
            var evidence = new Evidence
            {
                mode = mode!, role = role!,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            if (mode == "Turn")
                yield return RunTurn(role!, roomFile!, login, port, deadline.Token, evidence);
            else
                yield return RunState(role!, roomFile!, login, port, deadline.Token, evidence);
            File.WriteAllText(Path.Combine(evidenceDirectory!, role + ".json"),
                JsonUtility.ToJson(evidence, true));
        }

        private static IEnumerator RunState(string role, string roomFile, Login login,
            int port, CancellationToken token, Evidence evidence)
        {
            SceneManager.LoadScene("ConsumerLobby", LoadSceneMode.Single);
            yield return null;
            var lobby = UnityEngine.Object.FindObjectOfType<TinyConsumerLobby>();
            Assert.That(lobby, Is.Not.Null);
            lobby!.Enter(Launch(login, port));
            TinyGameplayRoot? root = null;
            var until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                root = UnityEngine.Object.FindObjectOfType<TinyGameplayRoot>();
                if (root != null && root.IsReady) break;
                if (root != null && root.LastError.Length != 0) Assert.Fail(root.LastError);
                yield return null;
            }
            Assert.That(root?.IsReady, Is.True);
            evidence.sceneRootReady = true;
            var session = root!.Session!;
            if (role == "owner")
            {
                yield return Await(session.CreateRoomAsync(TinySyncMode.State, token));
                PublishRoom(roomFile, session.RoomId);
            }
            else
            {
                yield return ReadRoom(roomFile, token);
                yield return Await(session.JoinRoomAsync(File.ReadAllText(roomFile), token));
            }
            evidence.roomId = session.RoomId;
            yield return Await(session.SetReadyAsync(token));
            if (role == "owner")
            {
                yield return WaitUntil(() => session.CanStart, () => root.LastError, token);
                yield return Await(session.BeginLoadingAsync(token));
            }
            yield return WaitUntil(() => session.CanSubmitInput, () => root.LastError, token);
            evidence.battleId = session.BattleId;
            yield return ReadyBarrier(roomFile, role, token);
            if (role == "owner")
                yield return Await(session.SubmitInputAsync(new TinyInput(1, 0, true), token));
            yield return WaitUntil(() =>
            {
                var guestId = role == "guest" ? session.PlayerId :
                    FindOtherPlayer(session.Room, session.PlayerId);
                var actor = root.transform.Find("Tiny Actor " + guestId);
                return actor != null && Mathf.Abs(actor.localScale.y - 0.9f) < 0.0001f;
            }, () => root.LastError, token);
            evidence.authoritativeResult = true;
            root.SendMessage("ReturnToLobbyAsync");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "ConsumerLobby",
                () => root.LastError, token);
            evidence.returnedToLobby = true;
        }

        private static IEnumerator RunTurn(string role, string roomFile, Login login,
            int port, CancellationToken token, Evidence evidence)
        {
            SceneManager.LoadScene("ConsumerTurnLobby", LoadSceneMode.Single);
            yield return null;
            var lobby = UnityEngine.Object.FindObjectOfType<TinyConsumerTurnLobby>();
            Assert.That(lobby, Is.Not.Null);
            lobby!.Enter(Launch(login, port));
            TinyTurnGameplayRoot? root = null;
            var until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                root = UnityEngine.Object.FindObjectOfType<TinyTurnGameplayRoot>();
                if (root != null && root.IsReady) break;
                if (root != null && root.LastError.Length != 0) Assert.Fail(root.LastError);
                yield return null;
            }
            Assert.That(root?.IsReady, Is.True);
            evidence.sceneRootReady = true;
            var session = root!.Session!;
            if (role == "owner")
            {
                yield return Await(session.CreateRoomAsync(token));
                PublishRoom(roomFile, session.RoomId);
            }
            else
            {
                yield return ReadRoom(roomFile, token);
                yield return Await(session.JoinRoomAsync(File.ReadAllText(roomFile), token));
            }
            evidence.roomId = session.RoomId;
            yield return Await(session.SetReadyAsync(token));
            if (role == "owner")
            {
                yield return WaitUntil(() => session.CanStart, () => root.LastError, token);
                yield return Await(session.BeginLoadingAsync(token));
            }
            yield return WaitUntil(() => session.State?.Turn == 0,
                () => root.LastError, token);
            evidence.battleId = session.BattleId;
            yield return ReadyBarrier(roomFile, role, token);
            if (role == "owner") yield return Await(session.SubmitActionAsync(token));
            yield return WaitUntil(() => session.State?.Turn == 1,
                () => root.LastError, token);
            yield return ReadyBarrier(roomFile + ".turn1", role, token);
            if (role == "guest") yield return Await(session.SubmitActionAsync(token));
            yield return WaitUntil(() => session.State?.Turn == 2,
                () => root.LastError, token);
            yield return ReadyBarrier(roomFile + ".turn2", role, token);
            if (role == "owner") yield return Await(session.SubmitActionAsync(token));
            yield return WaitUntil(() => session.State?.Turn == 3 &&
                session.State?.WinnerId == 1u, () => root.LastError, token);
            evidence.authoritativeResult = true;
            root.SendMessage("ReturnToLobby");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "ConsumerTurnLobby",
                () => root.LastError, token);
            evidence.returnedToLobby = true;
        }

        private static uint FindOtherPlayer(RoomGatewaySnapshot? room, uint self)
        {
            if (room == null) return 0;
            foreach (var player in room.Players)
                if (player.PlayerId != self) return player.PlayerId;
            return 0;
        }

        private static void PublishRoom(string path, string roomId)
        {
            var temporary = path + ".pending";
            File.WriteAllText(temporary, roomId);
            File.Move(temporary, path);
        }

        private static IEnumerator ReadRoom(string path, CancellationToken token)
        {
            var until = Time.realtimeSinceStartup + 60f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until)
            {
                token.ThrowIfCancellationRequested();
                yield return null;
            }
            Assert.That(File.Exists(path), Is.True, "Owner did not publish a room.");
        }

        private static IEnumerator ReadyBarrier(string roomFile, string role, CancellationToken token)
        {
            File.WriteAllText(roomFile + ".ready-" + role, "ready");
            yield return ReadRoom(roomFile + ".ready-" +
                (role == "owner" ? "guest" : "owner"), token);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, Func<string> error,
            CancellationToken token)
        {
            var until = Time.realtimeSinceStartup + 45f;
            while (!condition() && Time.realtimeSinceStartup < until)
            {
                token.ThrowIfCancellationRequested();
                if (error().Length != 0) Assert.Fail(error());
                yield return null;
            }
            Assert.That(condition(), Is.True, "Cross-process scene did not reach its expected state.");
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

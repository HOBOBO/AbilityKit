#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Protocol.Shooter;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Shooter 无头双实例多人验证的 MonoBehaviour 入口。
    /// 挂在 ShooterMultiplayerScene 的任意 GameObject 上。
    ///
    /// Owner: 创建房间 → 写 roomId → 等待 battle → 记录 stateHash
    /// Member: 等 roomId → 加入房间 → 等待 battle → 记录 stateHash
    /// </summary>
    public sealed class ShooterHeadlessClient : MonoBehaviour
    {
        private bool _started;

        private async void Start()
        {
            if (_started) return;
            _started = true;

            try { await RunAsync(); }
            catch (Exception ex) { Finish(false, ex.ToString(), 0, 0); }
        }

        private static async Task RunAsync()
        {
            var args = Environment.GetCommandLineArgs();
            var role = RequireArg(args, "-shooterHeadlessRole");
            var account = RequireArg(args, "-shooterHeadlessAccount");
            var host = ValArg(args, "-gatewayHost") ?? "127.0.0.1";
            var port = IntArg(args, "-gatewayPort", 4000);
            var region = ValArg(args, "-gatewayRegion") ?? "dev";
            var serverId = ValArg(args, "-gatewayServerId") ?? "local";
            var roomPath = FullPath(RequireArg(args, "-shooterHeadlessRoomPath"));
            var resultPath = FullPath(RequireArg(args, "-shooterHeadlessResult"));
            var timeout = IntArg(args, "-shooterHeadlessTimeoutSeconds", 240);

            var isOwner = string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase);
            Debug.Log($"[ShooterHeadless] role={role} account={account} host={host}:{port}");

            using var launcher = ShooterClientNetworkLauncher.Create(
                ShooterClientConnectionFactory.Tcp());

            try
            {
                // Login via room gateway TCP (same as MOBA headless)
                var loginResult = await DemoRoomGatewayAccountClient.LoginTcpAsync(
                    host, port, account, TimeSpan.FromSeconds(30));
                if (!loginResult.Success)
                {
                    Finish(false, $"Login failed: {loginResult.Message}", 0, 0);
                    return;
                }
                Debug.Log($"[ShooterHeadless] Logged in as {loginResult.AccountId}");

                var profile = CreateProfile(isOwner ? 1 : 2, 2);
                var sessionOptions = profile.BuildSessionOptions();
                var launchSpec = profile.BuildRoomLaunchSpec(sessionOptions, region, serverId);
                var endpoint = new ShooterClientNetworkEndpoint(host, port);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

                if (isOwner)
                {
                    var runtime = new ShooterBattleRuntimePort();
                    var presentation = new ShooterPresentationFacade();
                    var result = await launcher.CreateReadyStartAndSubscribeAsync(
                        endpoint,
                        runtime,
                        presentation,
                        CreateStartPayload(sessionOptions),
                        loginResult.SessionToken,
                        launchSpec,
                        playerId: 1,
                        tickRate: sessionOptions.TickRate,
                        timeout: TimeSpan.FromSeconds(120),
                        cancellationToken: cts.Token);

                    if (string.IsNullOrWhiteSpace(result.Flow.RoomId))
                    {
                        Finish(false, $"Room creation failed: {result.Flow.Message}", 0, 0);
                        return;
                    }

                    File.WriteAllText(roomPath,
                        JsonConvert.SerializeObject(new { roomId = result.Flow.RoomId }));
                    Debug.Log($"[ShooterHeadless] Room created: {result.Flow.RoomId}");

                    await WaitForBattleAsync(launcher, result.Session, runtime, TimeSpan.FromSeconds(90));
                    var (frame, hash) = GetStateHash(result.Session, runtime);
                    Finish(true, $"Owner completed. room={result.Flow.RoomId}", frame, hash);
                }
                else
                {
                    var roomId = await WaitForRoomFileAsync(roomPath, TimeSpan.FromSeconds(30));
                    if (roomId == null)
                    {
                        Finish(false, "Timed out waiting for room file", 0, 0);
                        return;
                    }

                    Debug.Log($"[ShooterHeadless] Joining room: {roomId}");
                    var runtime = new ShooterBattleRuntimePort();
                    var presentation = new ShooterPresentationFacade();
                    var result = await launcher.JoinReadyStartAndSubscribeAsync(
                        endpoint,
                        runtime,
                        presentation,
                        CreateStartPayload(sessionOptions),
                        loginResult.SessionToken,
                        roomId,
                        launchSpec,
                        playerId: 2,
                        tickRate: sessionOptions.TickRate,
                        timeout: TimeSpan.FromSeconds(120),
                        cancellationToken: cts.Token);

                    await WaitForBattleAsync(launcher, result.Session, runtime, TimeSpan.FromSeconds(90));
                    var (frame, hash) = GetStateHash(result.Session, runtime);
                    Finish(true, $"Member completed. room={roomId}", frame, hash);
                }
            }
            catch (Exception ex)
            {
                Finish(false, ex.ToString(), 0, 0);
            }
        }

        private static async Task WaitForBattleAsync(
            ShooterClientNetworkLauncher launcher,
            ShooterClientSession session,
            ShooterBattleRuntimePort runtime,
            TimeSpan maxWait)
        {
            var deadline = DateTime.UtcNow + maxWait;
            while (DateTime.UtcNow < deadline)
            {
                launcher.Tick(1f / 30f);
                session.Tick(1f / 30f);
                var (frame, _) = GetStateHash(session, runtime);
                if (frame > 10) break; // Battle is running
                await Task.Delay(100);
            }

            // Let battle run for 15 more seconds to accumulate state
            var runUntil = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < runUntil)
            {
                launcher.Tick(1f / 30f);
                session.Tick(1f / 30f);
                await Task.Delay(50);
            }
        }

        private static (int frame, uint hash) GetStateHash(
            ShooterClientSession session,
            ShooterBattleRuntimePort runtime)
        {
            return (session.CurrentFrame, runtime.ComputeStateHash());
        }

        private static async Task<string?> WaitForRoomFileAsync(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var coord = JsonConvert.DeserializeAnonymousType(json, new { roomId = "" });
                        if (!string.IsNullOrWhiteSpace(coord?.roomId)) return coord.roomId;
                    }
                    catch { }
                }
                await Task.Delay(500);
            }
            return null;
        }

        private static void Finish(bool success, string message, int frame, uint hash)
        {
            var resultPath = FullPath(RequireArg(Environment.GetCommandLineArgs(), "-shooterHeadlessResult"));
            var result = new { success, message, frame, stateHash = $"0x{hash:X8}" };
            File.WriteAllText(resultPath, JsonConvert.SerializeObject(result, Formatting.Indented));
            Debug.Log($"[ShooterHeadless] Done: success={success} frame={frame} hash=0x{hash:X8} msg={message}");
        }

        private static ShooterMultiplayerProfileSO CreateProfile(int playerId, int playerCount)
        {
            var profile = ScriptableObject.CreateInstance<ShooterMultiplayerProfileSO>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ShooterMultiplayerProfileSO).GetField("syncTemplateId", flags)!
                .SetValue(profile, ShooterSyncTemplateIds.PredictRollbackAuthority);
            typeof(ShooterMultiplayerProfileSO).GetField("controlledPlayerId", flags)!
                .SetValue(profile, playerId);
            typeof(ShooterMultiplayerProfileSO).GetField("playerCount", flags)!
                .SetValue(profile, playerCount);
            typeof(ShooterMultiplayerProfileSO).GetField("autoReady", flags)!.SetValue(profile, true);
            typeof(ShooterMultiplayerProfileSO).GetField("autoStart", flags)!.SetValue(profile, true);
            return profile;
        }

        private static ShooterStartGamePayload CreateStartPayload(
            ShooterPlayModeSessionOptions options)
        {
            var players = new ShooterStartPlayer[options.PlayerCount];
            for (var i = 0; i < players.Length; i++)
            {
                players[i] = new ShooterStartPlayer(i + 1, $"P{i + 1}", i * 4f, 0f);
            }

            return new ShooterStartGamePayload(
                $"shooter-headless-{options.RandomSeed}",
                options.TickRate,
                options.RandomSeed,
                players);
        }

        private static string RequireArg(string[] args, string name) =>
            ValArg(args, name) ?? throw new ArgumentException($"Required argument missing: {name}");

        private static string? ValArg(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static int IntArg(string[] args, string name, int fallback) =>
            int.TryParse(ValArg(args, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : fallback;

        private static string FullPath(string value) => Path.GetFullPath(value);

        /// <summary>无头运行时端口：仅满足接口，不做实际渲染。</summary>
        /// <summary>无头表现层：仅满足接口，不做渲染。</summary>
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;

namespace AbilityKit.Samples.ServerRuntime;

internal static class Program
{
    private const string WorldType = "server.battle";
    private const int TotalFrames = 30;
    private const float DeltaTime = 1f / 30f;

    private static void Main()
    {
        Log.SetSink(new ConsoleLogSink());

        Log.Info("=== AbilityKit ServerRuntime Starter（host + host.extension）===");
        Log.Info("演示权威服闭环：客户端输入 → 服务器帧循环 → 权威世界推进 → 帧包广播回客户端\n");

        // 1. 世界工厂注册：HostRuntime 通过 WorldManager 创建权威世界。
        var registry = new WorldTypeRegistry();
        registry.Register(WorldType, options => new ServerBattleWorld(options.Id, options.WorldType));
        var manager = new WorldManager(new RegistryWorldFactory(registry));

        // 2. 服务器运行时 + 扩展模块装配。
        var options = new HostRuntimeOptions();
        var server = new HostRuntime(manager, options);

        var modules = new HostRuntimeModuleHost();
        var frameSync = new FrameSyncDriverModule();
        modules.Add(frameSync);
        modules.Add(new ServerFrameTimeModule(DeltaTime));
        modules.InstallAll(server, options);

        // 3. 模拟客户端接入，然后创建世界（WorldCreated 广播会在 PostTick 前送达）。
        var client = new LoopbackClient("client-A");
        server.Connect(client);

        var world = server.CreateWorld(new WorldCreateOptions(new WorldId("battle-1"), WorldType));

        // 4. 服务器主循环：每帧模拟客户端提交输入，服务器 Tick 驱动整个闭环。
        var clientId = new ServerClientId("client-A");
        var worldId = world.Id;
        var playerId = new PlayerId("player-1");

        for (int f = 0; f < TotalFrames; f++)
        {
            // 客户端输入经 IFrameSyncInputHub 进入服务器（生产中来自网络层）。
            frameSync.SubmitInput(clientId, worldId, new PlayerInputCommand(
                new FrameIndex(f + 1), playerId, ServerInput.Move, Array.Empty<byte>()));
            if (f > 0 && f % 5 == 0)
            {
                frameSync.SubmitInput(clientId, worldId, new PlayerInputCommand(
                    new FrameIndex(f + 1), playerId, ServerInput.Hit, Array.Empty<byte>()));
            }

            // PreTick：输入 flush 进世界 → 世界 Tick 推进 → PostTick：快照广播给客户端。
            server.Tick(DeltaTime);
        }

        // 5. 结算：服务器权威状态 vs 客户端收到的帧包。
        var battle = (ServerBattleWorld)world;
        Log.Info($"\n[Server] 最终权威状态 —— x={battle.X:0.00}, hp={battle.Health:0}");
        Log.Info($"[Client] 共收到 {client.ReceivedFrames} 个帧包（含每帧广播的权威快照）");
        Log.Info("[结论] 客户端只发输入、只收帧包 —— 权威逻辑全部在服务器侧闭环。");

        server.DestroyWorld(worldId);
        Log.Info("=== Starter 完成 ===");
    }

    private sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message) => Console.WriteLine($"[INFO ] {message}");

        public void Warning(string message) => Console.WriteLine($"[WARN ] {message}");

        public void Error(string message) => Console.WriteLine($"[ERROR] {message}");

        public void Exception(Exception exception, string message = null!)
            => Console.WriteLine($"[EXCPT] {message} {exception}");
    }
}

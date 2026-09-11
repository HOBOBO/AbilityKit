#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// WorldCrossCommunication - World 跨世界通信进阶示例
    /// 演示 World 之间的通信、消息传递、服务共享、数据同步等高级特性
    /// </summary>
    [Sample]
    public sealed class WorldCrossCommunication : SampleBase
    {
        public override string Title => "World Cross-Communication";
        public override string Description => "演示 World 间通信、消息广播、服务共享、跨世界数据同步";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            Log("=== World 跨世界通信进阶示例 ===");
            Output.Divider();

            // 1. World 通信架构
            Log("【1】World 通信架构");
            Output.Bullet("WorldHost - World 容器的宿主，管理多个 World");
            Output.Bullet("WorldMessage - 世界间消息的载体");
            Output.Bullet("IMessageRouter - 消息路由器");
            Output.Bullet("IServiceBridge - 服务桥接器");
            Output.Bullet("IWorldResolver - 世界服务解析器");
            Log("");
            Log("  ┌─────────────────────────────────────────────────┐");
            Log("  │                   WorldHost                      │");
            Log("  │  ┌──────────┐  ┌──────────┐  ┌──────────┐      │");
            Log("  │  │  Lobby   │  │  Battle  │  │  Lobby   │      │");
            Log("  │  │  World   │  │  World   │  │  World   │      │");
            Log("  │  └────┬─────┘  └────┬─────┘  └────┬─────┘      │");
            Log("  └────────┼──────────────┼──────────────┼────────────┘");
            Log("           │              │              │");
            Log("           └──────────────┼──────────────┘");
            Log("                          │");
            Log("                   ┌──────▼──────┐");
            Log("                   │  IMessageRouter │");
            Log("                   └──────────────┘");
            Log("");

            // 2. 消息类型
            Log("【2】消息类型");
            Output.Bullet("WorldMessage - 基础消息类型");
            Output.Bullet("PlayerJoinMessage - 玩家加入");
            Output.Bullet("PlayerLeaveMessage - 玩家离开");
            Output.Bullet("StateSyncMessage - 状态同步");
            Output.Bullet("CommandMessage - 命令消息");
            Output.Bullet("EventMessage - 事件消息");
            Log("");

            // 3. 消息发送
            Log("【3】消息发送");
            Log("");
            Log("  // 发送到特定 World");
            Log("  host.SendMessage(new WorldId(\"battle\"), new PlayerJoinMessage");
            Log("  {");
            Log("      PlayerId = \"player_001\",");
            Log("      PlayerName = \"Alice\",");
            Log("      TeamId = 1");
            Log("  });");
            Log("");
            Log("  // 广播到所有 World");
            Log("  host.Broadcast(new WorldEventMessage");
            Log("  {");
            Log("      EventType = \"ServerShutdown\",");
            Log("      Reason = \"Maintenance\"");
            Log("  });");
            Log("");
            Log("  // 广播到特定类型的所有 World");
            Log("  host.BroadcastToType(\"LobbyWorld\", new ChatMessage");
            Log("  {");
            Log("      Sender = \"System\",");
            Log("      Content = \"Welcome to server!\"");
            Log("  });");
            Log("");

            // 4. 消息接收
            Log("【4】消息接收");
            Log("");
            Log("  // 在 World 中订阅消息");
            Log("  public class BattleWorld : IWorld");
            Log("  {");
            Log("      public void Initialize()");
            Log("      {");
            Log("          // 订阅玩家加入消息");
            Log("          Services.Resolve<IMessageRouter>()");
            Log("              .Subscribe<PlayerJoinMessage>(OnPlayerJoin);");
            Log("          ");
            Log("          // 订阅玩家离开消息");
            Log("          Services.Resolve<IMessageRouter>()");
            Log("              .Subscribe<PlayerLeaveMessage>(OnPlayerLeave);");
            Log("      }");
            Log("      ");
            Log("      private void OnPlayerJoin(PlayerJoinMessage msg)");
            Log("      {");
            Log("          Log($\"玩家 {msg.PlayerName} 加入战斗世界\");");
            Log("          CreatePlayerEntity(msg.PlayerId);");
            Log("      }");
            Log("  }");
            Log("");

            // 5. 服务桥接
            Log("【5】服务桥接 (IServiceBridge)");
            Log("");
            Log("  // 跨 World 共享服务");
            Log("  public interface ISharedGameService");
            Log("  {");
            Log("      int OnlinePlayerCount { get; }");
            Log("      void NotifyAll(string message);");
            Log("  }");
            Log("");
            Log("  // 在 Host 级别注册共享服务");
            Log("  host.RegisterSharedService<ISharedGameService>(new SharedGameService());");
            Log("");
            Log("  // 在 World 中访问共享服务");
            Log("  var sharedService = world.Services");
            Log("      .GetShared<ISharedGameService>();");
            Log("  int count = sharedService.OnlinePlayerCount;");
            Log("");

            // 6. World 生命周期事件
            Log("【6】World 生命周期事件");
            Log("");
            Log("  // 配置 World 生命周期回调");
            Log("  var options = new HostRuntimeOptions");
            Log("  {");
            Log("      OnBeforeCreateWorld = opts => Log($\"创建: {opts.Id}\"),");
            Log("      OnWorldCreated = world => Log($\"已创建: {world.Id}\"),");
            Log("      OnWorldDestroying = world => Log($\"销毁: {world.Id}\"),");
            Log("      OnWorldDestroyed = id => Log($\"已销毁: {id}\"),");
            Log("      OnPreTick = dt => UpdateGlobalSystems(dt),");
            Log("      OnPostTick = _ => SyncAcrossWorlds()");
            Log("  };");
            Log("");

            // 7. 数据同步
            Log("【7】跨 World 数据同步");
            Log("");
            Log("  // 全局数据管理器");
            Log("  public class GlobalDataManager");
            Log("  {");
            Log("      private readonly Dictionary<string, object> _globalData = new();");
            Log("      ");
            Log("      // 跨 World 共享数据");
            Log("      public T GetGlobal<T>(string key);");
            Log("      public void SetGlobal<T>(string key, T value);");
            Log("      ");
            Log("      // 带版本的数据（用于冲突检测）");
            Log("      public VersionedData<T> GetVersioned<T>(string key);");
            Log("  }");
            Log("");
            Log("  // 注册为共享服务");
            Log("  host.RegisterSharedService<GlobalDataManager>(new GlobalDataManager());");
            Log("");

            // 8. Player 跨 World 迁移
            Log("【8】Player 跨 World 迁移");
            Log("");
            Log("  // Player 迁移流程");
            Log("  // 1. 在源 World 保存 Player 状态");
            Log("  var playerState = battleWorld.SavePlayerState(playerId);");
            Log("  ");
            Log("  // 2. 从源 World 移除 Player");
            Log("  battleWorld.RemovePlayer(playerId);");
            Log("  ");
            Log("  // 3. 发送迁移消息");
            Log("  host.SendMessage(lobbyWorld.Id, new PlayerMigrateMessage");
            Log("  {");
            Log("      PlayerId = playerId,");
            Log("      State = playerState,");
            Log("      TargetLobbyId = \"lobby_1\"");
            Log("  });");
            Log("  ");
            Log("  // 4. 目标 World 接收并恢复 Player");
            Log("  lobbyWorld.RestorePlayer(playerState);");
            Log("");

            // 9. 异步 World 操作
            Log("【9】异步 World 操作");
            Log("");
            Log("  // 异步创建 World");
            Log("  var world = await host.CreateWorldAsync(new WorldCreateOptions");
            Log("  {");
            Log("      Id = new WorldId(\"async_battle\"),");
            Log("      WorldType = \"BattleWorld\",");
            Log("      InitializationTimeout = 5f");
            Log("  });");
            Log("  ");
            Log("  // 异步销毁 World");
            Log("  await host.DestroyWorldAsync(new WorldId(\"old_battle\"));");
            Log("  ");
            Log("  // 等待所有 World 就绪");
            Log("  await host.WaitForAllWorldsReadyAsync();");
            Log("");

            // 10. 典型应用场景
            Log("【10】典型应用场景");
            Output.Bullet("大厅-战斗世界切换 - 玩家从大厅进入战斗");
            Output.Bullet("全局排行榜 - 跨 World 聚合数据");
            Output.Bullet("跨服聊天 - 全局消息广播");
            Output.Bullet("断线重连 - 保存/恢复玩家状态");
            Output.Bullet("服务器迁移 - 世界热迁移");
            Output.Bullet("GM 命令 - 全局广播执行");
            Log("");

            // 11. 消息序列化
            Log("【11】消息序列化");
            Log("");
            Log("  // 消息可序列化为 JSON 用于网络传输");
            Log("  public class WorldMessage");
            Log("  {");
            Log("      public string MessageId { get; set; }");
            Log("      public WorldId SourceWorld { get; set; }");
            Log("      public WorldId? TargetWorld { get; set; }");
            Log("      public long Timestamp { get; set; }");
            Log("      public string Payload { get; set; }  // JSON 序列化");
            Log("  }");
            Log("");
            Log("  // 序列化发送");
            Log("  var message = new WorldMessage");
            Log("  {");
            Log("      MessageId = Guid.NewGuid().ToString(),");
            Log("      SourceWorld = currentWorld.Id,");
            Log("      Payload = JsonSerializer.Serialize(data)");
            Log("  };");
            Log("");

            // 12. 错误处理
            Log("【12】错误处理和容错");
            Log("");
            Log("  // 消息发送失败处理");
            Log("  try");
            Log("  {");
            Log("      host.SendMessage(targetWorld, message);");
            Log("  }");
            Log("  catch (WorldNotFoundException)");
            Log("  {");
            Log("      Log($\"目标世界不存在: {targetWorld}\");");
            Log("  }");
            Log("  catch (MessageSendException ex)");
            Log("  {");
            Log("      Log($\"消息发送失败: {ex.Message}\");");
            Log("      // 重试或降级处理");
            Log("  }");
            Log("");
            Log("  // World 销毁时的消息处理");
            Log("  host.OnWorldDestroyed += worldId =>");
            Log("  {");
            Log("      // 取消该世界的待处理消息");
            Log("      CancelPendingMessages(worldId);");
            Log("      // 重路由目标为该世界的消息");
            Log("      ReRouteMessages(worldId, fallbackWorld);");
            Log("  };");
            Log("");

            // 13. 性能考虑
            Log("【13】性能考虑");
            Output.Bullet("消息队列大小限制，避免内存溢出");
            Output.Bullet("消息频率限制，防止刷屏");
            Output.Bullet("异步处理避免阻塞主循环");
            Output.Bullet("批量消息合并减少开销");
            Output.Bullet("关键消息优先级调度");
            Log("");

            // 14. API 参考
            Log("【14】关键 API 参考");
            Output.Bullet("AbilityKit.Ability.World.Management.HostRuntime");
            Output.Bullet("AbilityKit.Ability.World.Services.IMessageRouter");
            Output.Bullet("AbilityKit.Ability.World.Services.IServiceBridge");
            Output.Bullet("AbilityKit.Ability.World.Abstractions.IWorld");
            Output.Bullet("AbilityKit.Ability.World.Messages");
            Log("");

            Output.Divider();
            Log("【总结】World 跨世界通信是构建大规模多人游戏的核心能力，需要完善的消息路由和服务共享机制");
        }
    }
}

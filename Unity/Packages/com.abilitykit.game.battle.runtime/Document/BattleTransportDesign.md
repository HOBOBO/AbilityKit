> 文档类型：历史设计记录；现行使用契约已取代本文。
> 状态说明（2026-11-25）：以下正文保留原路径与历史方案，未作为当前 API 教程继续维护。旧命名空间、无依赖声明、`game.battle.transport.runtime`/StateSyncAdapter 关系及“struct 即零 GC”等描述不适用于当前实现。
> 当前入口：[包首页](../README.md)、[文档索引](../Documentation~/README.md)、[使用指南](../Documentation~/Usage.md)。本文不构成新增机制或恢复退役接口的依据。

# com.abilitykit.game.battle：战斗传输层

> **阅读对象**：希望了解战斗逻辑传输层如何设计的开发者
>
> **文档目标**：让你理解"战斗传输层与具体网络实现如何解耦"、"两层包的关系"、"MOBA 场景下的完整连接流程"

---

## 一、设计理念

### 1.1 为什么需要传输层抽象？

不同游戏可能使用不同的底层网络（TCP、UDP、KCP、WebSocket），也可能连接不同的网关（自研网关、Orleans、Mirror）。传输层抽象让战斗逻辑与具体网络实现解耦。

### 1.2 两层包的关系

| 包 | 职责 | 依赖 |
|----|------|------|
| `game.battle.runtime` | 定义通用接口（`IBattleLogicTransport`、请求/响应类型） | 无 |
| `game.battle.transport.runtime` | 实现传输（NetworkTransport、StateSyncAdapter） | 依赖 runtime |

```
game.battle.runtime              game.battle.transport.runtime
┌──────────────────┐           ┌──────────────────────────────┐
│ IBattleLogicTransport │◀──────│ NetworkTransport             │
│ IBattleLogicRequest  │        │   - INetworkClient (底层)     │
│ IBattleLogicResponse│        │   - 请求序列化/反序列化       │
└──────────────────┘        │   - 服务器推送处理              │
                             └──────────────────────────────┘
```

---

## 二、game.battle.runtime

### 2.1 核心接口

```csharp
namespace AbilityKit.Ability.Game.Battle
{
    /// <summary>
    /// 战斗逻辑传输接口
    /// 抽象了战斗逻辑与具体网络的交互
    /// </summary>
    public interface IBattleLogicTransport
    {
        /// <summary>帧推送事件</summary>
        event Action<FramePacket> FramePushed;

        void Connect();
        void Disconnect();

        /// <summary>请求创建世界</summary>
        void SendCreateWorld(CreateWorldRequest request);

        /// <summary>请求加入世界</summary>
        void SendJoin(JoinWorldRequest request);

        /// <summary>请求离开世界</summary>
        void SendLeave(LeaveWorldRequest request);

        /// <summary>提交输入</summary>
        void SendInput(SubmitInputRequest request);
    }
}
```

### 2.2 请求类型

| 类型 | 字段 | 说明 |
|------|------|------|
| `CreateWorldRequest` | `WorldCreateOptions`, `OpCode`, `Payload` | 创建战斗世界 |
| `JoinWorldRequest` | `WorldId`, `PlayerId`, `OpCode`, `Payload` | 加入已有世界 |
| `LeaveWorldRequest` | `WorldId`, `PlayerId`, `OpCode`, `Payload` | 离开世界 |
| `SubmitInputRequest` | `WorldId`, `PlayerInputCommand` | 提交玩家输入 |

---

## 三、game.battle.transport.runtime

### 3.1 目录结构

```
Battle/Transport/
├── GenericNetworkClient.cs           # 基于 INetworkClient 的网络客户端
├── INetworkClient.cs                 # 底层网络接口
├── NetworkTransport.cs               # IBattleLogicTransport 实现
├── NetworkTransportOptions.cs        # 配置选项
├── NullBattleLogicTransport.cs       # 空实现（用于测试）
├── Moba/
│   ├── Client/
│   │   ├── IBattleStartConfig.cs     # 战斗启动配置（本地玩家ID、TickRate）
│   │   ├── IStateSyncAdapter.cs      # 统一帧/状态同步适配器
│   │   ├── StateSyncAdapter.cs       # Orleans Gateway 连接、登录/房间、快照推送
│   │   ├── StateSyncCodec.cs
│   │   └── SnapshotModels.cs
│   ├── NetworkSession.cs
│   ├── NetworkOpCodes.cs
│   ├── NetworkProtocol.cs
│   ├── StateSyncModels.cs
│   ├── TransportModels.cs             # 登录、创房、加房、提交帧输入（请求/响应）
│   └── TcpNetworkClient.cs
└── *.asmdef
```

### 3.2 底层网络接口

```csharp
namespace AbilityKit.Ability.Game.Battle.Transport
{
    /// <summary>
    /// 底层网络客户端接口
    /// 支持请求-响应和服务器推送两种模式
    /// </summary>
    public interface INetworkClient
    {
        bool IsConnected { get; }

        Task<NetworkResult> ConnectAsync(string host, int port);
        void Disconnect();

        /// <summary>请求-响应模式（等待服务器回复）</summary>
        Task<ResponseResult<TResponse>> SendRequestAsync<TRequest, TResponse>(
            uint opCode, TRequest request)
            where TRequest : class
            where TResponse : class;

        /// <summary>服务器推送（单向，无需等待）</summary>
        void SendServerPush(uint opCode, byte[] payload);

        // 事件
        event Action OnConnected;
        event Action<int> OnDisconnected;
        event Action<string> OnError;
        event Action<uint, byte[]> OnServerPush;
    }
}
```

### 3.3 IBattleLogicTransport 实现

`NetworkTransport` 将 `IBattleLogicTransport` 的方法调用转换为 `INetworkClient` 的请求/推送：

```
SendInput(request)
  → Serialize SubmitInputRequest
  → INetworkClient.SendRequestAsync(OpSubmitInput, request)
  → 服务器响应

Connect()
  → INetworkClient.ConnectAsync(host, port)
  → 注册 OnServerPush 回调
  → 服务器推送 FramePushed
  → 反序列化 FramePacket
  → FramePushed?.Invoke(packet)
```

### 3.4 MOBA 专用适配器

`StateSyncAdapter` 连接 Orleans Gateway，提供完整的登录→创房→加房→战斗流程：

```
客户端                          网关                           战斗服务器
  │                               │                                │
  │ ConnectAsync()                │                                │
  │───────────────────────────────▶│                                │
  │                               │                                │
  │ GuestLogin                    │                                │
  │───────────────────────────────▶│ GuestLogin                    │
  │                               │──────────────────────────────▶│
  │                               │◀──────────────────────────────│
  │◀──────────────────────────────│ LoginResponse                 │
  │                               │                                │
  │ CreateRoom / JoinRoom         │                                │
  │───────────────────────────────▶│ ...                           │
  │◀──────────────────────────────│ RoomResponse                  │
  │                               │                                │
  │ SubmitFrameInput              │                                │
  │───────────────────────────────▶│ ...                           │
  │                               │                                │
  │◀─ ServerPush (Snapshot) ─────│                                │
  │    FramePushed?.Invoke()       │                                │
```

---

## 四、与 world.statesync 的关系

```
IBattleLogicTransport.Receive FramePacket
  │
  ▼
world.statesync
  ├── ClientPredictionModule (per-entity 预测)
  │     └── 客户端本地预测 + 回滚
  └── IPredictionCoordinator (global ECS 预测)
        └── 帧同步 + 回滚

network 层
  │
  ▼
game.battle.transport.runtime
  │
  ▼
game.battle.runtime
  │
  ▼
world.statesync
```

---

## 五、设计原则

| 原则 | 说明 |
|------|------|
| **接口隔离** | `IBattleLogicTransport` 只暴露战斗逻辑所需的操作 |
| **传输无关** | 底层可替换（TCP/UDP/KCP/Orleans）而不影响战斗逻辑 |
| **零 GC** | 请求/响应类型使用结构体，避免 GC |
| **双工通信** | 同时支持请求-响应（命令）和服务器推送（帧数据） |

# com.abilitykit.network.sdk

> AbilityKit 多人联网 SDK 的**组装根与生命周期持有者**。传输无关（transport-independent）：不绑定任何具体网络协议，具体传输由 `ITransport` 实现注入。

- **版本**：0.1.0（Beta）
- **程序集**：`AbilityKit.Network.Sdk`（纯 C#，`noEngineReferences`，不依赖 UnityEngine）
- **依赖**：仅 `com.abilitykit.network.runtime` 0.1.0
- **公共类型**：2 个 —— `NetworkSdkBuilder`、`NetworkSdkClient`

## 在整个网络栈中的位置

```
ITransport        原始字节传输（com.abilitykit.network.runtime：TcpTransport / 你的自定义实现）
  ↓
IConnection       成帧 / 心跳 / 重连 / 分发（ConnectionManager，或 GameFrameworkNetworkChannelConnection）
  ↓
NetworkSdkClient  ← 本包：组装根 + 生命周期 + 单一 RequestClient（请求/响应 + 推送）
  ↓
能力包            com.abilitykit.network.room（房间会话）… 以及未来的 battle 能力包
```

本包**刻意做窄**：只负责传输/连接/生命周期，不感知"房间""战斗"等业务语义。房间能力由 `com.abilitykit.network.room` 用一个扩展方法（`sdk.CreateRoomClient()`）挂接，复用同一条请求链，**不**新建第二个 `RequestClient`。

## NetworkSdkBuilder

流式组装器，把一个传输/连接组装成 `NetworkSdkClient`。

```csharp
public sealed class NetworkSdkBuilder
{
    // 二选一（互斥，后设者清掉先设者）
    public NetworkSdkBuilder UseConnectionFactory(Func<IConnection> connectionFactory);
    public NetworkSdkBuilder UseTransportFactory(Func<ITransport> transportFactory);

    // 仅当用 transport factory 时生效（内部 new ConnectionManager 时传入）
    public NetworkSdkBuilder ConfigureConnection(Action<ConnectionOptions> configure);

    // 回调/IO 派发器；默认 InlineDispatcher
    public NetworkSdkBuilder UseDispatchers(IDispatcher callbackDispatcher, IDispatcher? ioDispatcher = null);

    public NetworkSdkClient Build();  // 未设任何 factory 抛 InvalidOperationException
}
```

- `UseTransportFactory` —— 你只给传输（如 `() => new TcpTransport()`），SDK 内部用 `ConnectionManager` 包成 `IConnection`。
- `UseConnectionFactory` —— 你直接给一个现成的 `IConnection`（例如桥接 Unity GameFramework 的 `GameFrameworkNetworkChannelConnection`），SDK 不再创建 `ConnectionManager`。

## NetworkSdkClient

拥有一条连接及其单一 `RequestClient`，实现 `IReconnectableConnection, IDisposable`。

```csharp
public sealed class NetworkSdkClient : IReconnectableConnection, IDisposable
{
    // 状态
    public ConnectionState State { get; }
    public bool IsConnected { get; }
    public bool SupportsReconnect { get; }       // 底层连接是否实现 IReconnectableConnection
    public bool IsReconnectExhausted { get; }

    // 生命周期
    public void Open(string host, int port);
    public void Close();
    public void Tick(float deltaTime);            // 每帧驱动（必须在主循环或 PlayerLoop 调用）
    public void ResetReconnect();
    public void Dispose();

    // 发送
    public void SendPacket(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0);                  // 原始信封，不带请求跟踪
    public Task<ArraySegment<byte>> SendRawRequestAsync(uint opCode, ArraySegment<byte> payload,                        // 请求/响应（seq 配对）
        TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // 事件
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<Exception>? Error;
    public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;       // (opCode, seq, payload)
    public event Action<uint, ArraySegment<byte>>? ServerPushReceived;         // (opCode, payload)
    public event Action<string, string>? Kicked;                               // (sessionToken, reason)
    public event Action<int, float>? ReconnectScheduled;                        // (attempt, delaySec)
    public event Action<int>? ReconnectAttemptStarted;
    public event Action<int>? ReconnectExhausted;
}
```

## 最小用法

```csharp
// 1. 组装（注入 TCP 传输）
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new TcpTransport())
    .ConfigureConnection(o => { /* heartbeat / reconnect 选项 */ })
    .Build();

// 2. 建连 + 每帧驱动
sdk.Open(host, port);
// ...每帧：sdk.Tick(deltaTime);

// 3. 请求/响应
var resp = await sdk.SendRawRequestAsync(MyOpCodes.Login, payload);

// 4. 接收服务端推送
sdk.ServerPushReceived += (opCode, payload) => { /* dispatch */ };

// 5. 挂接房间能力（见 com.abilitykit.network.room）
var room = sdk.CreateRoomClient();
```

## 自定义传输（替换网络底层）

实现 `ITransport`（来自 `com.abilitykit.network.runtime`），再用 `UseTransportFactory(() => new YourTransport())` 注入即可。仓库内已有第二个传输实现的先例：`com.abilitykit.gameframework.network` 把 Unity GameFramework 的 `INetworkChannel` 桥接成了 `IConnection`（`GameFrameworkNetworkChannelConnection`），shooter demo 的 `ShooterClientConnectionFactory.FromGameFrameworkNetwork(...)` 即在用。当前仓库只内置 `TcpTransport`（裸 TCP），WebSocket/KCP/UDP 等按需新增 `ITransport` 实现即可，上层无需改动。

## 相关

- 房间会话能力 → `com.abilitykit.network.room`
- 战斗数据面引擎（输入上行 + 快照/事件下行 + 可靠事件 + resync）→ `com.abilitykit.network.battle`（`NetworkTransport` / `NetworkTransportOptions`）
- 传输/连接原语（`IConnection`/`ConnectionManager`/`TcpTransport`/`RequestClient`/重连/时钟同步/插值）→ `com.abilitykit.network.runtime`
- 两个示例的完整接入（shooter / moba）→ `Docs/design/07-NetworkSynchronization/` 接入清单

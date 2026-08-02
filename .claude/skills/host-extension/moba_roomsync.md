# Moba RoomSync（C/S 房间状态同步）

> ⚠ **MIGRATION (2026-07-31)**：本文档描述的代码已从 `com.abilitykit.host.extension/Runtime/Moba/` 提取至 `com.abilitykit.demo.moba.host/Runtime/Moba/`（version 0.1.0）。所有 API 命名空间和 asmdef 名称未变，仅物理位置与企业级依赖声明变更。


源文件：`Runtime/Moba/Client/RoomSync/*.cs` + `Runtime/Moba/Server/RoomSync/*.cs`

## Client 侧

### IMobaRoomSyncClient

`Runtime/Moba/Client/RoomSync/IMobaRoomSyncClient.cs`

```csharp
public interface IMobaRoomSyncClient {
    void ApplySnapshot(in MobaRoomSnapshotMessage message);
    void ApplyCommandResult(in MobaRoomCommandResultMessage message);
    void ApplyDelta(in MobaRoomChangedMessage message);
    bool TryBuildRequestSnapshot(string clientId, out MobaRoomRequestSnapshotMessage message);
}
```

### MobaRoomSyncClient

`Runtime/Moba/Client/RoomSync/MobaRoomSyncClient.cs`

```csharp
public sealed class MobaRoomSyncClient : IMobaRoomSyncClient {
    public int LastSeenRevision { get; }
    public int LastAckClientSeq { get; }
    public int PendingDeltaCount { get; }
    public bool NeedSnapshot { get; }       // delta 跨度 > MaxDeltaRevisionGap 时为 true

    public void SetLastAckClientSeq(int seq);
    public bool TryDequeueDelta(out MobaRoomChangedMessage message);
    public MobaRoomHelloMessage BuildHello(string clientId);
}
```

配置：`MaxDeltaRevisionGap`（默认 1）。当 delta 跨度超过此值时触发 `NeedSnapshot=true`，客户端需要请求全量 snapshot。

## Server 侧

### IMobaRoomSyncServer

`Runtime/Moba/Server/RoomSync/IMobaRoomSyncServer.cs`

```csharp
public interface IMobaRoomSyncServer {
    bool TryHandleHello(MobaRoomHelloMessage hello, ...);
    bool TryHandleRequestSnapshot(MobaRoomRequestSnapshotMessage request, ...);
    MobaRoomCommandResult HandleCommand(MobaRoomCommandMessage command, ...);
    MobaRoomSnapshotMessage BuildSnapshotMessage();
}
```

### MobaRoomSyncServer

`Runtime/Moba/Server/RoomSync/MobaRoomSyncServer.cs`，包 `IMobaRoomOrchestrator`。

按 `LastSeenRevision < snap.Revision` 决定是否回全量。

### Outbox 体系

- `IMobaRoomSyncServerOutbox` / `MobaRoomSyncServerOutbox`：`Queue<MobaRoomSnapshotMessage>`（全量）
- `IMobaRoomSyncServerDeltaOutbox` / `MobaRoomSyncServerDeltaOutbox`：`Queue<MobaRoomChangedMessage>`（增量）

接口：`Enqueue / TryDequeue / Clear`

### Broadcaster（订阅 room 事件，转化为消息入 outbox）

- `MobaRoomSyncServerBroadcaster : IDisposable`：订阅 `IMobaRoomOrchestrator.AddChanged`，每次变更把全量 `MobaRoomSnapshotMessage` 入 outbox（按 revision 去重）
- `MobaRoomSyncServerDeltaBroadcaster : IDisposable`：同上但入 delta（`MobaRoomChangedMessage.FromArgs`）

**IDisposable**：Dispose 时取消订阅 room 事件，避免悬空订阅。

## 典型同步流

```
Client 改变意图 → 发送 MobaRoomCommandMessage
        ↓
Server MobaRoomSyncServer.HandleCommand
        ↓
MobaRoomOrchestrator.Apply(command)
        ↓
OnChanged 触发
        ↓
Broadcaster 把 Snapshot/Delta 入 Outbox
        ↓
Server 网络层从 Outbox TryDequeue → 广播
        ↓
Client MobaRoomSyncClient.ApplySnapshot / ApplyDelta / ApplyCommandResult
        ↓
若 delta gap > MaxDeltaRevisionGap → 触发 NeedSnapshot → 请求全量
```

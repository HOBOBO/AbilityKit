# InMemory Transport 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖同步投递、连接标记与所有权；不模拟 socket 行为。

安装条件见[包入口](../README.md)。`CreateConnectedPair()` 创建互相引用的两个 transport；方法名不表示已经触发 `Connected`。

## 完整本地字节示例

```csharp
using System;
using AbilityKit.Network.Transport.InMemory;

var pair = InMemoryTransport.CreateConnectedPair();
using var a = pair.A;
using var b = pair.B;
byte[] received = null;
b.BytesReceived += bytes =>
{
    received = new byte[bytes.Count];
    Array.Copy(bytes.Array, bytes.Offset, received, 0, bytes.Count);
};
a.Connect("inmemory", 1);
b.Connect("inmemory", 1);
a.Send(new ArraySegment<byte>(new byte[] { 7 }));
if (received == null || received.Length != 1 || received[0] != 7)
    throw new InvalidOperationException("同步投递失败");
```

## 同步行为和边界

`Send` 在调用线程直接触发对端 `BytesReceived`，不复制数组、不排队、不隔离订阅者异常。回调可能重入发送方；异常也可能直接返回到 `Send` 调用方。跨回调保存数据时复制所需片段，避免发送方复用数组。

当前 `Send` 不检查任一端的 `IsConnected`。`Close()` 只清除本端连接标记，在本端此前连接时发出本端 `Disconnected`；它不切断 peer 引用、不通知对端断连、不清除订阅。`Dispose()` 等同 `Close()`，不能用它验证其他 transport 的终态和断网语义。调用方负责释放两端及自己的订阅。

原始 transport 的 `Connect` 忽略 host/port；上层 `ConnectionManager.Open` 或 SDK 仍要求非空 host 和 1..65535 端口，可使用 `"inmemory", 1`。

## 接入 SDK 时

factory 的每次调用通常必须返回新 transport，以便首次 Open 和重连分别取得独立实例。固定返回同一 pair 端点只适合明确限制为单次打开的测试，应关闭自动重连，并由测试管理对端。`InMemoryTransport` 不是 `InProcessChannelListener`：后者还经过 NetworkHost 服务端 session、帧协议及 pipeline，并复制交付字节。

本轮现有 `AbilityKit.Network.Transport.InMemory.Tests` 的 1 项回环通过（E3）；没有延迟、乱序、丢包、IO 线程或真实断网证据。

源码：[InMemoryTransport](../Runtime/Transport/InMemoryTransport.cs)。

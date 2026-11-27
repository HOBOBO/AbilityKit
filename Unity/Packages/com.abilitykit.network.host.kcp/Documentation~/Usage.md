# KCP 服务端接入指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖 listener/channel 生命周期及本地回环，未验证部署网络和 Unity Player。

安装条件见[包入口](../README.md)。本包为 `NetworkHost` 提供 KCP UDP listener；请求路由、认证、世界调度由上层装配。

## 完整本地监听示例

下例只在回环地址绑定临时端口并停止，不发送业务消息。客户端地址应读取运行时绑定的端点，不能把监听配置中的 `Port = 0` 当成目标端口。

```csharp
using System;
using System.Net;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.Kcp;

var listener = new KcpChannelListener(new KcpChannelListenerOptions
{
    Address = IPAddress.Loopback,
    Port = 0,
    ConversationId = 1
});
using var host = new NetworkHost(listener, new NetworkHostOptions());
host.Start();
await host.StopAsync(TimeSpan.FromSeconds(1));
```

## 配置与建连

- `Address` 决定绑定地址，`Port` 支持 0（系统分配）；`ConversationId` 必须非零，接收缓冲区和更新间隔必须为正。
- 客户端必须使用相同 `ConversationId` 与兼容的 KCP 参数，并承载相同 AbilityKit 帧协议。该 ID 是 KCP 会话参数，不是玩家身份、认证票据或恢复代次。
- listener 以远端 endpoint 区分 channel；收到 conversation 匹配的有效初始数据报才创建 channel。客户端 UDP 初始化成功不能证明服务端已接受。
- 内部接收与 KCP 更新在后台执行；应用仍需驱动 `NetworkHost.Tick()` 的超时检测，并在自己的执行线上推进世界。不能让多个 session handler 无约束地并发修改同一世界。

## 所有权与停止

将 listener 交给 `NetworkHost` 后，由 host 管理 listener、session 和已接受的 channel。先等待 `host.StopAsync`，再释放 host；应用不要同时释放它持有的 channel。

直接使用 listener 时，调用方接管已接受 channel 的生命周期。`listener.Stop()` 关闭 UDP socket 并清空 channel 索引，不能据此假定每个已经交付的 channel 都调用过 `Dispose()`。网络停止也不代替房间退出或世界销毁。

关闭、取消和错误可能与后台回调交错；订阅回调应短小，并把数据复制或转交到明确的宿主调度入口。NAT endpoint 迁移、互联网弱网、认证及攻击防护不在本地回环证据中。

## 证据与源码

本轮现有 `AbilityKit.Network.Host.Kcp.Tests` 的 1 项本地 UDP 回环通过（E3）；上述监听示例另外验证最小装配。不代表真实 Gateway 业务、跨平台或发布验收。

- [KcpChannelListener](../Runtime/Kcp/KcpChannelListener.cs)：端点索引、接收与停止。
- [KcpChannelListenerOptions](../Runtime/Kcp/KcpChannelListenerOptions.cs)：监听参数。
- [KcpServerChannel](../Runtime/Kcp/KcpServerChannel.cs)：发送、更新与 channel 释放。

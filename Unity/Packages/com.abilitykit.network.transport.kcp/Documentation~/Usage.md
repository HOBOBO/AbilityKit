# KCP Transport 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖客户端参数、事件语义和本地 UDP 回环；未验证互联网部署。

安装与直接依赖见[包入口](../README.md)。本包把 AbilityKit 的成帧字节交给 KCP/UDP；服务端需要匹配的 KCP peer，普通 TCP 或裸 UDP echo endpoint 不能替代它。

## 工厂装配片段

下面省略 endpoint 与宿主循环，需由应用接入；不作为在线服务示例执行。使用此组合需另外安装 `com.abilitykit.network.sdk`，引入 `AbilityKit.Network.Sdk` 与 `AbilityKit.Network.Transport.Kcp` 命名空间；直接使用 transport 不依赖 SDK。

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new KcpTransport(new KcpTransportOptions
    {
        ConversationId = 1,
        Mtu = 1200,
        UpdateIntervalMilliseconds = 10
    }))
    .Build();
```

每次 factory 调用创建新 transport，不要捕获并反复返回一个已经关闭的实例。SDK 持有 connection，connection 持有 transport，业务最终释放 SDK。`KcpTransport` 保留传入 Options 的引用，构造后不要并发修改或复用为动态配置。

## 参数与成功信号

构造时检查 conversation 非零、更新间隔和窗口大于零、MTU 在 576..65507、接收缓冲区不小于 MTU。参数通过只表示本地配置可构造；不能据此推导对端兼容或吞吐性能。

`Connect(host, port)` 要求非空 host 与 1..65535 端口，初始化 UDP socket 和 KCP 后就发出 `Connected`；没有远端握手确认。业务仍需请求响应、认证和协议层探活。双方 `ConversationId` 应相同，但该参数不是用户身份或恢复 generation。endpoint 变化后的 NAT 迁移不在当前契约中。

## 时钟、回调与失败

KCP 的接收和更新使用内部后台任务；宿主仍必须以真实墙钟间隔驱动上层 SDK `Tick`，否则心跳、超时与重连不会按预期工作。KCP 内部更新不能代替它。

未连接时非空发送会失败；错误通过 `Error` 暴露，接收循环异常会进入关闭路径。`Close` 取消后台工作、关闭 socket、释放 KCP，并在原来持有 socket 时发出 `Disconnected`。不要依赖所有 transport 都具有统一的可重用或终态语义，重连交由 connection 的新实例 factory。

回调不保证 Unity 主线程；SDK 应注入适合宿主的 dispatcher，异步保存 `ArraySegment<byte>` 时复制数据。不能把 KCP 可靠有序传输当成业务请求的幂等、恰好一次执行或世界恢复保证。

## 证据与源码

本轮现有 `AbilityKit.Network.Transport.Kcp.Tests` 的 1 项本地 UDP echo 通过（E3）；另有服务端 listener/channel 回环。未做性能比较、弱网、移动端、Unity Player 或正式 Gateway 验收。

[KcpTransport 与 Options](../Runtime/Transport/KcpTransport.cs) 是本包接入契约入口。底层含 ET 派生实现；保留随附版权声明并核对相应上游许可证，不从本地测试推导再分发授权。

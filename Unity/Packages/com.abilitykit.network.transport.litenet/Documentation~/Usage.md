# LiteNet Transport 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖客户端装配、线程和本机 UDP echo，未验收完整服务端网关。

安装要求见[包入口](../README.md)。Unity 包已经随附 [LiteNetLib.dll](../Runtime/Plugins/LiteNetLib.dll)，不需要按旧说明再次手工复制同名 DLL；应避免项目重复引用不同版本。仓库 .NET 镜像的依赖以对应工程为准，不能据此保证所有 Unity 目标兼容。

## 装配片段

下例组合额外需要 `com.abilitykit.network.sdk` 及 `AbilityKit.Network.Sdk`、`AbilityKit.Network.Transport.LiteNet` 命名空间；它们不是由本 transport 自动安装的上层依赖。

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new LiteNetTransport("abilitykit"))
    .Build();
```

此片段省略 endpoint、dispatcher、Tick 和释放，由宿主补齐；每次 factory 调用都创建新实例。服务端必须运行 LiteNetLib 或兼容实现，使用相同 connection key，并支持 AbilityKit 帧协议。默认 key 为 `abilitykit`，它只是连接协商参数，不是用户认证或保密机制。

## 运行契约

- `Connect` 校验 host 与 1..65535 端口，创建并启动 `NetManager`；`Connected` 随 peer 连接事件触发。连接成功后仍需业务登录或鉴权。
- `Send` 使用 `DeliveryMethod.ReliableOrdered`；未得到 peer 时非空发送失败。该选项不能证明比 TCP 更低延迟，性能与弱网行为需另测。
- `UnsyncedEvents = true`：LiteNetLib 在内部线程触发事件，不需要业务调用 `PollEvents`。访问 Unity 对象前仍需 dispatcher；上层 SDK 的心跳和重连仍依赖宿主墙钟 `Tick`。
- 接收实现取得字节后同步发布，跨回调持有数据依照通用 transport 契约自行复制；不要把当前分配方式当作所有 transport 的统一保证。
- `Close` 停止 manager 并清除本地 manager/peer 引用；`Dispose` 调用 Close。connection 管理的重连应重新创建 transport。业务管理 SDK 的释放，不同时单独释放其 transport。

发送异常会报告 `Error`、关闭并重新抛出；订阅者自身异常也可能影响该路径。不要在 IO 回调内直接执行耗时世界逻辑，或把一个 session 的处理串行性外推为全世界串行。

## 验证和源码

本轮现有 `AbilityKit.Network.Transport.LiteNet.Tests` 的 1 项本机 LiteNetLib UDP echo 通过（E3）。尚未核验 AbilityKit 完整 LiteNet Gateway、跨平台 Player、真实弱网和发布环境。

源码：[LiteNetTransport](../Runtime/Transport/LiteNetTransport.cs)。

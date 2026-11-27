# WebSocket Transport 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖 URI 装配、分支选择和本地桌面回环；浏览器/TLS/代理未验收。

安装与依赖见[包入口](../README.md)。本包以二进制 WebSocket 承载 AbilityKit 的成帧字节；服务端必须支持相同帧协议，普通文本 WebSocket 服务不能直接复用。

## URI 与工厂装配片段

此 SDK 组合额外需要 `com.abilitykit.network.sdk`，引入 `AbilityKit.Network.Sdk` 与 `AbilityKit.Network.Transport.WebSocket` 命名空间。直接使用 transport 的依赖仍以包声明为准。

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new WebSocketTransport("/game", secure: true))
    .Build();
sdk.Open("gateway.example.invalid", 443);
```

这是参数说明片段，未连接或执行；真实 endpoint、dispatcher、Tick 与最终释放由应用提供。传给 Open 的 host 不包含 scheme 或路径。构造参数默认 path 为 `/`、secure 为 false；上例组合为 `wss://gateway.example.invalid:443/game`。secure 仅选择 `wss`，不提供自定义证书、鉴权 header 或反向代理配置接口。每次 factory 调用必须创建新 transport。

## 平台、线程与成帧

`UNITY_WEBGL && !UNITY_EDITOR` 使用包内 JavaScript bridge；其他分支使用 `ClientWebSocket` 与后台任务。存在平台分支只代表实现入口，不能证明浏览器策略、移动端或 IL2CPP 已通过验收。

WebSocket 消息边界不能代替 AbilityKit 帧边界，应用不要假定每次 transport 回调就是一个完整业务包。保留 connection 的帧解码与请求分发。桌面接收实现可能使用池化缓冲区；WebGL bridge 的复制行为也不改变通用约定：跨回调保存 payload 前复制需要的数据。

回调不统一保证主线程；业务通过 SDK dispatcher 对接宿主，持续以墙钟间隔驱动 SDK `Tick`。`Connect` 开始异步连接，不能将方法返回视为已经连接、业务鉴权或下行订阅完成。

## 失败和释放

未连接时非空发送失败；建立或接收异常由 `Error` 和断连路径报告。Close/Dispose 负责本地 transport 资源，业务会话、订阅恢复与服务端退出由上层管理。不要在 SDK 外再取得同一 transport 的释放所有权。

浏览器混合内容、TLS 证书、代理升级和超时均需在真实目标环境独立验收。仓库已有可配置的 Gateway WebSocket 服务端实现，默认关闭；本包本地 echo 不等于已启用或验收该网关。

## 证据与源码

本轮现有 `AbilityKit.Network.Transport.WebSocket.Tests` 的 1 项本机 `HttpListener` echo 通过（E3）。未运行 Unity、WebGL 浏览器、TLS 或代理测试。

- [WebSocketTransport](../Runtime/Transport/WebSocketTransport.cs)：条件编译、URI 与桌面收发。
- [WebGlWebSocketBridge](../Runtime/Transport/WebGL/WebGlWebSocketBridge.cs)：浏览器桥接。

# Network Runtime 首次使用与边界

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校本页成帧、会话所有权与选择边界；未验证外部网络或所有同步辅助原语。

## 无 socket 的最小成帧

完整示例在内存构造并解析本仓库格式，只验证成帧。示例操作码不用于正式协议。

```csharp
using System;
using AbilityKit.Network.Protocol;

byte[] body = { 42 };
var header = new NetworkPacketHeader((NetworkPacketFlags)0, 1, 0, (uint)body.Length);
var frame = new byte[NetworkFrameCodec.GetFrameSize(body.Length)];
NetworkFrameCodec.WriteFrame(frame, header, body);
if (!NetworkFrameCodec.TryParseFrame(frame, out var parsed, out var payload)
    || parsed.OpCode != 1 || payload.Length != 1 || payload[0] != 42)
    throw new InvalidOperationException("本地成帧往返失败");
```

`TryParseFrame` 返回的 Span 借用输入存储，不提供新数组；不要跨其所有者生命周期保留。`Try` 方法也不是任意字节均不抛错的安全边界：例如不支持的 header size 会由 `NetworkPacketHeader.Read` 抛错。网络接收需使用配置了长度预算、协议目录校验和错误收敛的会话路径，不直接把此例当作不可信输入解析器。

## 从原语到会话

| 需要 | 选择和责任 |
| --- | --- |
| 自定义字节通道 | 实现 `ITransport`，声明缓冲和回调线程；默认使用本包 TcpTransport 或另装可选 transport |
| 连接、成帧、心跳、重连 | `ConnectionManager`；宿主持续以真实墙钟 delta（秒）调用 Tick，串行管理 Open/Close/Dispose |
| 借用现有连接做请求 | `RequestClient` 或 `NetworkProtocolSession`；保存并释放会话，连接仍归创建方 |
| 统一持有连接及请求链 | 使用上层 `com.abilitykit.network.sdk` 组合，不在同一连接上无规划地重复创建请求序号空间 |
| 类型化玩法恢复 | 由所属状态/帧同步会话明确准入、输入历史及世界导入；不能仅安装 Sync 辅助类就视为已接通 |

默认 dispatcher 可在调用线程或 IO 线程直接运行；访问引擎对象前显式选择线程派发。请求响应、事件推送、按码 handler 的载荷所有权不同；权威细节见[请求会话生命周期](../README.md#请求会话生命周期)与[主动关闭](../README.md#主动关闭与调度队列)。

## 关闭与恢复排错

1. 停止新业务请求，先释放借用连接的请求会话，再关闭/释放拥有的连接；主动 Close 不保证发 Disconnected。
2. 断线、超时、取消或释放终结旧请求，不自动重发。重连后重新建立身份、目录协商和业务恢复资格，不能重用旧请求任务作完成证明。
3. 推送如果要跨回调保存，按对应入口契约在回调内复制；不能把结构体 ArraySegment 当作数组 owner。
4. Close/Dispose 使旧队列通知失效，但不等待所有在途回调结束；宿主还须按执行线关闭业务消费者。

快照 ID、可靠事件水位和两种同步模式的输入确认分别由所属链持有。FastReconnectSession 等状态机只提供原语，不代替成功导入及提交证明。条件模拟位于协议包层，不证明 socket 故障、拥塞或实际弱网行为。

## 来源与验证

源码：[成帧](../Runtime/Network/Protocol/NetworkFrameCodec.cs)、[包头](../Runtime/Network/Protocol/NetworkPacketHeader.cs)、[连接](../Runtime/Network/Runtime/ConnectionManager.cs)、[请求](../Runtime/Network/Runtime/RequestResponse/RequestClient.cs)。本批 .NET 局部结果由仓库日期化记录维护；没有运行 SDK/传输矩阵、外部服务、Unity/WebGL 或生产网络。

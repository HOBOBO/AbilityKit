# GameFramework Network Bridge 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.gameframework.network` 0.1.0，2026-11-25 工作区源码及 .NET 镜像
> 核校状态：部分核校；五个桥接源码与 GameFramework 接收/Update 路径已作 E0 核对，本地 E3 范围见第 7 节
> 事实基线：2026-11-25；未验收真实远端 Gateway 与 Unity Player

## 1. 目标与前置条件

本包适用于已有 GameFramework 网络 manager，需要把专用 channel 交给 AbilityKit 连接/SDK 的宿主。它不创建或驱动整个 GameFramework 应用，也不负责 Room/Battle 协议、认证、业务恢复。

[package.json](../package.json) 声明 Unity 2022.3，直接依赖只列 GameFramework；[运行程序集](../Runtime/com.abilitykit.gameframework.network.asmdef) 还引用 `AbilityKit.Network.Runtime`，安装时必须额外满足该运行时依赖。SDK 组合另需 SDK 包；包声明缺口未在本轮修改。.NET 镜像成功不代替独立 UPM 安装验收。

源码入口：[factory](../Runtime/Network/GameFrameworkGatewayConnectionFactory.cs)、[wrapper](../Runtime/Network/GameFrameworkNetworkChannelConnection.cs)、[helper](../Runtime/Network/AbilityKitGatewayNetworkChannelHelper.cs)、[header](../Runtime/Network/AbilityKitGatewayPacketHeader.cs)、[packet](../Runtime/Network/AbilityKitGatewayPacket.cs)。

## 2. 完整示例：本地成帧往返

完整 Console 示例，引用已装配本包、GameFramework 及 Network Runtime 的工程。仅在 MemoryStream 中验证完整 header/body，不创建 manager、不访问 DNS 或网络；预期输出 `gameframework frame roundtrip passed`。

```csharp
using System;
using System.IO;
using System.Linq;
using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Protocol;

var input = new byte[] { 3, 5, 8 };
var header = new NetworkPacketHeader((NetworkPacketFlags)0, 101, 7, (uint)input.Length);
var packet = new AbilityKitGatewayPacket(header, new ArraySegment<byte>(input));
input[0] = 99; // packet 构造时复制，原始数组的变化不应传入。
var helper = new AbilityKitGatewayNetworkChannelHelper();
using var stream = new MemoryStream();
if (!helper.Serialize(packet, stream))
    throw new InvalidOperationException("Packet not accepted");
var frame = stream.ToArray();
using var headerStream = new MemoryStream(frame.Take(helper.PacketHeaderLength).ToArray());
var parsedHeader = helper.DeserializePacketHeader(headerStream, out var headerError);
if (parsedHeader == null || headerError != null)
    throw new InvalidOperationException("Header rejected");
using var bodyStream = new MemoryStream(frame.Skip(helper.PacketHeaderLength).ToArray());
var parsed = helper.DeserializePacket(parsedHeader, bodyStream, out var bodyError)
    as AbilityKitGatewayPacket;
if (parsed == null || bodyError != null || parsed.Header.OpCode != 101 ||
    parsed.Header.Seq != 7 || !parsed.Payload.SequenceEqual(new byte[] { 3, 5, 8 }))
    throw new InvalidOperationException("Body roundtrip failed");
Console.WriteLine("gameframework frame roundtrip passed");
```

packet 复制输入不等于不可变：其 Payload 暴露数组片段，消费者仍可修改底层数组；不要共享给会原地改写的处理者。

## 3. 在线装配与通道所有权

1. 宿主创建 GameFramework manager 并按其正常生命周期持续 Update；选择仅由此 wrapper 使用的 channel 名。
2. 调用 `GameFrameworkGatewayConnectionFactory.Create(manager, channelName)`，默认 ServiceType.Tcp；或把已配置正确 helper 的专用 channel 交给 `Wrap(channel)`。
3. 先绑定所需事件，再 Open；连接/SDK 所有者持续 Tick。SDK 的 owned factory 可返回此 wrapper，但不能让多个 SDK 对同一个命名 channel 各建 wrapper。
4. 关闭时先停止业务输入与解绑业务订阅，再由唯一 owner 关闭/Dispose wrapper，最后由外部 owner 处理 manager。

factory 发现同名 channel 时直接复用，**不检查已有 service/helper 是否匹配**。wrapper 构造时调用 `SetDefaultHandler`，覆盖原 default handler；同 channel 只能有一个 wrapper owner。GameFramework 已注册的对应 packet ID handler 可能先消费消息，默认 handler 不能保证收到所有包，专用 channel 不应混装会截获这些 opcode 的 handler。

wrapper 只实现 `IConnection`。构造时按 channel.Connected 设置初始 State，不为已连接 channel 补发 Connected。若需要 SDK 自动重连能力，先核对 SDK 与 `IReconnectableConnection` 要求，不能把类型包装等同于重连能力。

## 4. 驱动、事件与失败

| 操作/属性 | 当前行为与限制 |
| --- | --- |
| `Open(host, port)` | 同步 DNS 解析后只取首个地址，设置 Connecting 并调用 channel.Connect；没有地址回退。校验、DNS 或 Connect 异常可直接抛出，不保证恢复 State 或触发 Error |
| `Tick(deltaTime)` | 仅观察 Connecting→Connected、Connected→Disconnected；deltaTime 未参与运算。不调用 GameFramework Update，不提供心跳、超时或重连 |
| `IsConnected`/`State` | 前者直接查询未 disposed 的 channel，后者由构造/Open/Tick/Close 维护；两者可暂时不一致 |
| `Send` | 检查 disposed，但不检查 connected；构造 GatewayPacket 并交 channel.Send。发送、长度或第三方异常可传播 |
| 接收 | 带 ServerPush 标志时只发 ServerPushReceived，否则发 PacketReceived；不内建 kick DTO 解码或业务过滤 |
| `Error`、`Kicked` | 事件虽声明，当前 wrapper 无调用点。宿主另订阅 GameFramework 网络错误；不能等待这两个事件来完成所有失败收敛 |
| `Close` | 调用 channel.Close 后标记 Disconnected；channel 抛出时可能跳过标记。disposed 后 Close/Tick 无操作，Open/Send 抛 ObjectDisposedException |

网络模块真正的收发与接收事件派发由外部 GameFramework Update 驱动；只 Tick wrapper 或 SDK 会造成状态/回调停滞。默认 handler 与业务回调无本包并发隔离保证，应保持同一受控线程推进，耗时/重入/异常由宿主明确处理。

## 5. 成帧与缓冲区

helper 对齐 NetworkFrameCodec：4 字节小端帧长度前缀，后接 NetworkPacketHeader，再接 payload；前缀长度不含自身。当前固定网络头 16 字节，helper 的 PacketHeaderLength 为 20，PacketLength 是 payload 长度。

Serialize 只接受 AbilityKitGatewayPacket，其他 packet 返回 false；每次创建成帧数组。DeserializeHeader 检查最小长度与 payload length 一致，头解析与 checked uint→int 转换仍可抛异常，不是所有坏输入均返回 null；这里也没有完整的最大帧长策略，须由接入边界限制预算。

ReadAll 将 stream.Position 置 0，单次 Read 必须得到全部请求字节，否则抛 EndOfStreamException；因此针对 GameFramework 提供完整 header/body 缓冲流的调用路径，不是通用的不可寻址流、任意短读或分片流解析器。Packet 构造复制 payload，DeserializePacket 读取后还会再次复制，不能声称零 GC。

helper 的 Initialize/Shutdown/PrepareForConnecting 为空，SendHeartBeat 返回 false。需要心跳时由所属网络方案明确实现，不能只配置间隔就假定本 helper 已发送应用心跳。

## 6. 释放与迁移

Dispose 先设置 disposed，再清空 channel default handler 并关闭 channel，finally 设置 State=Disconnected；不发 Disconnected，不恢复旧 handler，不从 manager 删除 channel，不 Shutdown manager，也不清空外部事件委托。若清 default handler 抛异常，Close 可能尚未执行；再次 Dispose 不补偿，应由外部 owner 记录并收敛。

SDK owned factory 会接管 wrapper 的关闭/释放，因而影响其底层 channel，但不接管 GameFramework manager。不要把原先由其他业务拥有的 channel 交给 owned wrapper 后继续独立操作。

排错先看：是否持续驱动 GameFramework Update；是否用了冲突 channel/helper/ID handler；是否只在等待未桥接的 Error/Kicked；是否混用了 State 和即时 IsConnected；是否将非完整流交给 helper。原[设计文档](../Document/GameFrameworkNetwork网络适配模块开发设计文档.md)保留背景与路径，本文持有现行使用契约。

## 7. 验证范围与关联文档

本轮核对五个源码及 GameFramework Update/事件池路径，运行本文本地完整示例 1/1 与现有 Shooter `ClientConnectionFactoryCanWrapGameFrameworkNetworkChannel` 测试 1/1 通过。后者使用 fake channel，只验证局部包装、Open/Tick/Close，不能据此宣称真实 TCP/Gateway 已通或两条同步链已恢复；本包未发现独立测试 suite。

实际命令、结果及未覆盖范围见仓库 D3 支撑批次记录。未运行完整 Shooter/network gate、真实 Gateway、Unity、独立安装、浏览器、弱网、性能或 CI；证据仅为 E0 与局部 E3，不新增 E4/E5。

返回[文档索引](README.md)；环境前提见[包入口](../README.md)。

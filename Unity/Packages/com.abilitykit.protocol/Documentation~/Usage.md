# Protocol 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校下述目录、解码及序列化边界；领域 codec、生成工具和 AOT 未完整验证。

## 本地目录约束解码

以下完整示例只使用本地构造的一字节测试载荷与示例目录定义，演示在调用 decoder 前检查大小。正式消息必须使用所属协议包提供的生成目录和 codec，不复制此示例分配操作码或发明线上布局。

```csharp
using System;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;

var definition = new ProtocolMessageDefinition(
    "value.push", 1, ProtocolDirection.ServerToClient, ProtocolPacketKind.Push,
    "System.Int32", "custom-binary", maximumPayloadBytes: 1);
var decoders = new ProtocolPayloadDecoderRegistry();
decoders.Register("local.example", definition.Id, payload =>
{
    if (payload.Array == null || payload.Count != 1) throw new FormatException();
    return (int)payload.Array[payload.Offset];
});
var result = decoders.Decode("local.example", definition,
    new ArraySegment<byte>(new byte[] { 42 }), schemaVersion: 1);
if (!result.Success || result.Value is not int value || value != 42)
    throw new InvalidOperationException(result.Error);
var oversized = decoders.Decode("local.example", definition,
    new ArraySegment<byte>(new byte[] { 1, 2 }), schemaVersion: 1);
if (oversized.FailureKind != ProtocolDecodeFailureKind.PayloadTooLarge)
    throw new InvalidOperationException("未拒绝超长载荷");
```

## 注册和失败分类

decoder 键为 catalog ID + message ID；`Register` 重复抛异常，`TryRegister` 遇已有键返回假，不比较委托是否相同。目录、metadata、decoder 注册是不同步骤；有目录不代表已有 decoder，更不代表完成连接握手。

网络接收使用 `Decode(catalogId, definition, payload, schemaVersion)`：先核版本范围与最大字节数，再找 decoder。版本传 null 只跳过版本检查，仍查大小；不能把“不带版本”当作已协商。旧的字符串 message ID 重载没有目录约束，只适合兼容或本地可信调用。

decoder 异常转换为 `ProtocolDecodeResult` 的失败，不以异常文本作策略分支，使用 `FailureKind`。此注册表不从 definition 自动选择 codec、不验证业务状态、不自动复制输入字节；具体 decoder 保证载荷结构、分配预算及返回对象的所有权。注册表同步只覆盖表访问，不保证自定义 decoder 线程安全。

## 连接协商与版本

`ProtocolCatalogNegotiationSession` 为一条连接保存单个目录的协商结果；每次新物理连接或重连调用 `Reset`，不能延用旧协商结果。多目录连接由应用组合相应目录的协商结果。身份校验、交换声明、保存选定 schema 版本并在接收边界启用门禁，均由连接/会话层负责。

YAML 文档格式的 `schemaVersion: 2`、catalog 的 `revision` 和各消息的线上 schema 范围是三种不同版本，不互相代用。正式系统目录通告采用所属生成契约与有界 codec；不因注册辅助类存在就宣称握手已运行。

## 序列化后端和生命周期

| 入口 | 契约 |
| --- | --- |
| 所属协议包的强类型 facade/codec | 使用该协议声明的布局与版本；热路径优先沿现有直调入口，不能换格式而不评审兼容性 |
| `WireSerializer` / `IWireSerializer` | 包提供可替换抽象，使用前必须显式 Install；未安装读取 Current 会抛异常 |
| `WireSerializer.Install` | 默认拒绝覆盖；显式 `replaceExisting: true` 才替换。配置为进程级，不是会话级，活动收发期间不要更换 |
| `MemoryPackWireSerializerInstaller` | 仅安装可选反射 adapter，不随包提供或验证 MemoryPack；缺后端在实际编解码时失败，不承诺裁剪/AOT |
| `ITextSerializer` / JsonTextSerializer | 文本序列化用途，与 wire codec 分开；不能用 JSON 偷换已经约定的线上二进制载荷 |

本基础包的直接依赖为包声明中的 Newtonsoft JSON；消费者不应因本包存在就假设 MemoryPack 已安装。自定义后端可用于明确适配或本地测试，但已发布的协议布局必须保持所属契约。

## 核校来源与限制

源码：[目录模型](../Runtime/Protocol/Catalog/ProtocolCatalogModel.cs)、[解码注册表](../Runtime/Protocol/Catalog/ProtocolPayloadDecoderRegistry.cs)、[协商状态](../Runtime/Protocol/Catalog/ProtocolCatalogNegotiationSession.cs)、[WireSerializer](../Runtime/Serialization/WireSerializer.cs)。仓库创作流程由包入口指明的权威 YAML 与工具维护；不手改随包生成的 catalog、metadata 或领域 DTO。

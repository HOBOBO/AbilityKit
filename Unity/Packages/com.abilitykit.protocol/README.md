# com.abilitykit.protocol — 协议基础与序列化抽象

本包维护消息信封、操作码描述、协议目录和协商、序列化抽象；不维护连接、房间或具体玩法的载荷。Unity 运行时程序集不引用引擎或 Core；直接依赖为 `com.unity.nuget.newtonsoft-json`（`.NET` 镜像对应 `Newtonsoft.Json`），用于文本序列化。线上消息遵循所属协议段声明的 MemoryPack 或 custom-binary 等契约，不能用文本序列化替换。

`Runtime/Generated/` 的内置 catalog 和 metadata 是随包编译的投影，不要求消费者安装生成器。其权威输入在仓库 `Protocols/Catalogs/`；维护者用 `tools/compile-protocol-catalogs.ps1` 生成，并用 `-Check` 核验，禁止手改生成文件。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本与直接依赖见 [package.json](package.json)。随包 catalog/metadata 是编译投影，包消费者无需安装仓库生成器。先读[文档索引](Documentation~/README.md)；[使用指南](Documentation~/Usage.md)提供本地目录约束解码完整示例及失败分类。

## 序列化后端与协议布局

| 项 | 说明 |
|---|---|
| **后端** | 基础包提供可替换抽象；具体消息遵循所属 catalog 的 codec，不擅自更换线上格式 |
| **wire 类型** | 由所属分组 Wire Schema 生成；class/struct、顺序/版本容忍和成员形式以该 schema 为准 |
| **op-code 绑定** | 目录生成 metadata 与属性投影，不能由业务另分配相冲突的数字 |
| **生成** | catalog 与 Wire Schema 分别由正式协议工具投影；custom-binary 由对应专用 codec 管理 |

## 三层 API（按所属协议选择）

### 1. 直接 MemoryPack（按协议声明使用）
```csharp
byte[] bytes = MemoryPackSerializer.Serialize(value);
T value = MemoryPackSerializer.Deserialize<T>(bytes);
```
这是已配置 MemoryPack 且有对应 formatter 时的调用片段，不是基础包的安装示例；是否适合热路径需按所属协议和实际测试决定。

### 2. per-segment `Wire*Binary` facade（类型安全便捷封装）
```csharp
ArraySegment<byte> seg = WireRoomGatewayBinary.Serialize(in wireReq);     // room-gateway 段
T req = WireRoomGatewayBinary.Deserialize<T>(seg);                        // 泛型
// 或 typed overload（moba-framesync 段）：
var frameReq = WireCustomBinary.DeserializeSubmitFrameInputReq(payload);
```
- `WireRoomGatewayBinary`（`protocol.room`）—— 泛型 `Serialize<T>/Deserialize<T>` + 瞬态 buffer（`SerializeTransient`）+ `ArraySegment` 返回。
- `WireCustomBinary` 等属于领域协议包的 facade，不属于本基础包；具体生成或手写归属及兼容边界以所属包为准。
- facade 不能仅凭名称推断线格式；使用与 catalog codec、schema 及版本匹配的实现。

### 3. pluggable 抽象（`WireSerializer` / `IWireSerializer`）—— 可替换后端
```csharp
byte[] bytes = WireSerializer.Serialize(value);   // → WireSerializer.Current
T value = WireSerializer.Deserialize<T>(bytes);
```
- `protocol` 定义 `IWireSerializer` 和静态 `WireSerializer.Current`，保留可选的 MemoryPack 反射安装入口，不依赖或随包提供 MemoryPack。
- `MemoryPackWireSerializerInstaller.InstallAsCurrent()` 只安装序列化器；调用方负责准备可用后端。未找到后端时，实际编解码抛出 `InvalidOperationException`。反射入口不作为裁剪或 AOT 支持承诺；生产协议优先使用所属协议段的直调入口。
- **用途**：明确需要可替换后端的适配或测试。抽象本身不强制反射；可选 MemoryPack adapter 才采用反射，不把活动连接上的后端替换当作兼容迁移。

## 何时用哪层

| 场景 | 用 |
|------|---|
| 战斗/快照/输入热路径 | 使用所属 catalog 声明的 codec；MemoryPack 消息可用第 1 层或匹配的第 2 层 facade，custom-binary 消息使用专用 codec |
| 房间/会话 RPC | 第 2 层（`WireRoomGatewayBinary`）提供泛型编解码和 ArraySegment 返回；具体 API 以 `protocol.room` 为准 |
| 需要可替换后端（测试/alt） | 第 3 层（`WireSerializer`）|
| 游戏状态 payload（需 delta/AOI/特殊编解码）| 使用对应游戏的专用 codec；内部格式由该 codec 的契约决定 |

## 新增一个 wire 类型的标准流程

本节面向仓库维护者；这些路径不是独立安装包中的本地链接。

1. 在 `Protocols/Catalogs/*.protocol.yaml` 定义消息身份、方向、响应关系及运行时限制。
2. 目录管理的 MemoryPack 传输载荷在 `Protocols/WireSchemas/*.wire.yaml` 使用分组 v2：`schemaVersion: 2`、project/group/namespace 与 `types[]`。保留既有字段 ID 和布局，不手写第二份序列化字段。
3. 用 `tools/compile-protocol-catalogs.ps1` 与 `tools/export-protocol-wire.ps1` 生成并执行其只读 Check；详细命令、兼容评审与项目范围以仓库 `Protocols/README.md`、`Docs/ProtocolNetworkCodegenGuide.md` 为准。
4. custom-binary、兼容旧布局和进程本地持久化由已有专用 codec 管理，不强行转成 Wire DTO。为修改过的载荷补对应本地黄金字节与往返验证。

禁止手改生成 DTO、metadata、catalog、注册表、清单及兼容性基线。手写 partial 只承载构造、校验、转换等非序列化定义。

## 不要

- 不绕过 catalog 的 codec 和版本约束替换线上格式；基础包可插拔不代表线上消息可任意换格式。
- 新网络边界使用带 definition 的受约束 Decode，或在运行时路由前安装 `ProtocolPacketBoundaryValidator`；旧字符串重载仅供兼容与本地可信调用。
- 不把目录注册当成握手完成，不把 YAML 格式版本、目录 revision 和消息 schema 版本混用；重连须重置连接作用域协商。
- 不在业务里复制 op-code 数字；使用生成目录或 `ProtocolMessageDescriptor<T>.RequireOpCode(direction)` 等所属协议入口。

## 相关包

- `com.abilitykit.protocol.room` —— room-gateway wire 类型 + `WireRoomGatewayBinary`
- `com.abilitykit.protocol.moba` / `.shooter` —— 各 demo 的 wire 类型 + codec
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`

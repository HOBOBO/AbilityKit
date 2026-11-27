# Room Protocol

`com.abilitykit.protocol.room` 提供 Room Gateway、可靠战斗事件及 Gateway 时间同步的共享 DTO、操作码和二进制辅助工具。

## 安装与首次使用

从项目已配置的包来源安装；版本、Unity 声明及直接依赖见 [package.json](package.json)。当前包为 0.1.0，声明 Unity 2022.3。运行程序集还引用 MemoryPack，使用者须提供可用的 MemoryPack 程序集及匹配的生成代码；包声明本身不会安装该第三方依赖。完整依赖组合和独立安装仍需目标工程验证。

先运行[使用指南的本地编解码示例](Documentation~/Usage.md#2-完整示例本地编解码)，预期得到相同账号字段及固定基线信封身份。示例不建立网络连接。在线接入还需连接、认证、请求匹配、业务结果检查及同步会话所有者。

## 文档与兼容

- [文档索引](Documentation~/README.md)：阅读顺序和源码入口。
- [使用指南](Documentation~/Usage.md)：消息身份、借用缓冲区、固定基线与失败行为。

操作码以 [RoomGatewayOpCodes](Runtime/Room/RoomGatewayOpCodes.cs) 和随包生成目录为准，不在 README 维护另一份完整表。旧说明中的 `LeaveRoom = 112` 错误，当前为 119，112 是 `BeginLoading`。本包不拥有房间、世界或同步时钟，不应因 DTO 可反序列化就认定请求成功或会话已恢复。

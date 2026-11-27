# GameFramework Network Bridge

`com.abilitykit.gameframework.network` 把外部 GameFramework 网络 channel 适配为 AbilityKit `IConnection`，并提供 Gateway 成帧 helper。

## 安装与首次使用

从项目已配置的包来源安装。版本及 Unity 声明见 [package.json](package.json)。该声明只列 `com.abilitykit.gameframework`，但运行程序集还引用 `AbilityKit.Network.Runtime`；使用者必须额外安装相容的 `com.abilitykit.network.runtime`。使用 SDK 注入时还需 SDK 包。此处记录声明缺口，本批不修改包依赖。

先运行[使用指南中的 MemoryStream 成帧示例](Documentation~/Usage.md#2-完整示例本地成帧往返)。在线使用时，为 wrapper 预留独占 channel，由应用持续驱动 GameFramework 模块，再由连接/SDK 所有者 Tick 与释放 wrapper。

## 文档与限制

- [文档索引](Documentation~/README.md)
- [使用指南](Documentation~/Usage.md)：通道复用、事件、Tick、成帧及释放责任。
- [原适配设计](Document/GameFrameworkNetwork网络适配模块开发设计文档.md)：保留设计背景，当前使用契约以 Usage 为准。

wrapper 不实现 `IReconnectableConnection`；Error/Kicked 没有自动桥接，Tick 不驱动 GameFramework Update。完整在线接入、Unity 和独立安装未由本轮本地测试验收。

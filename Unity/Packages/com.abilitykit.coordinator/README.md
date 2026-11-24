# AbilityKit Coordinator

本包提供战斗会话的**配置、数据和世界驱动契约**，不创建连接，也不包含可直接运行的 `SessionCoordinator` 或 `LocalSyncAdapter` / `RemoteSyncAdapter`。实际连接、认证、重连与同步策略由 SDK 和项目接入层装配。

| 源码目录 | 职责 |
| --- | --- |
| `Runtime/Session` | `SessionConfig`、会话标识与环境策略、项目宿主接口、端点配置 |
| `Runtime/WorldBridge` | 世界驱动、步进门控、状态缓冲与出生服务的契约 |
| `Runtime/Data` | 输入、实体和快照数据，以及项目负载的编码入口 |

`NetworkEndpoint` 只保存配置中的主机文本和端口；`IsValid` 只检查已填写主机且端口为正。不解析 DNS、不拨号、不验证传输可达性，也不负责监听端点。`SessionConfig` 的远程模式不会自动创建网络连接。

纯 C#，Unity 2022.3，.NET 10 镜像引用同一份 Runtime 源码。直接依赖和版本见 [package.json](package.json)，包括 `core`、`world.di`、`host`、`protocol`、MemoryPack 和 Unity 侧 Unsafe 包；不依赖 `host.network` 或任何具体传输。

历史装配见[早期设计](Document/Coordinator会话协调模块开发设计文档.md)和 [ET 接入资料](docs/ET-Integration-Guide.md)，不能作为当前 API 清单。

# com.abilitykit.game.battle.transport.runtime

> **遗留 moba 传输子树载体包。** 通用战斗数据面引擎（`NetworkTransport`/`NetworkTransportOptions`/`INetworkClient`/`GenericNetworkClient`/`Projection/*`）已在 2026-08-06（P2.1）迁出到中立包 **[`com.abilitykit.network.battle`](../com.abilitykit.network.battle/README.md)**。新代码请用那个包，不要依赖本包。

本包现在只保留 moba 专属/遗留的 `Runtime/Battle/Transport/Moba/` 子树：

- `TcpNetworkClient`（独立 TCP 客户端 + 自带成帧）
- `NetworkProtocol`（guest login / 建房 / 加入 / 帧输入 的 MemoryPack 编解码）
- `NetworkOpCodes`、`StateSyncModels`（`ClientStateSyncPayloadCodec` 等）、`TransportModels` DTO、`IBattleStartConfig`

这些目前仅被 `src/AbilityKit.Demo.Moba.Console` 使用（Console demo 的 `StateSyncAdapter` 直接用 `TcpNetworkClient`）。本包依赖 `com.abilitykit.network.battle` 以获得 `INetworkClient` 接口（`TcpNetworkClient : INetworkClient`）。

## 待清理（后续独立 task）

以下文件经核对外部零引用，可安全删除（不属 SDK 契约、无生产路径使用）：`Moba/NetworkSession.cs`（及其 `SessionState`/`INetworkSession` 等辅助类型）、`Moba/Client/StateSyncAdapter.cs`、`Moba/Client/IStateSyncAdapter.cs`、`Moba/Client/SnapshotModels.cs`、`Moba/client/StateSyncCodec.cs`。

> 已知小瑕疵：`Moba/TransportModels.cs` 与 `Moba/Client/IBattleStartConfig.cs` 因命名空间块提前闭合，类型实际落在全局命名空间。搬迁/清理时一并修。

## 相关

- 战斗数据面引擎（请用此包）→ [`com.abilitykit.network.battle`](../com.abilitykit.network.battle/README.md)
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`

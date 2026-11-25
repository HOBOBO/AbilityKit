# Changelog — com.abilitykit.network.runtime

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [未发布]

- 请求登记与请求客户端释放互斥；断开或错误移除当时的待响应请求，不重发，也不接管连接所有权。
- 协议会话释放时解绑推送订阅，跳过尚未进入的后续回调。释放后的 `IsConnected` 为 `false`，新增推送订阅会报 `ObjectDisposedException`。
- 主动关闭或停止时隔离旧接收、解码器残留和排队通知；关闭后重新打开只消费当前连接的回调。`NetworkSession.Dispose()` 不重复释放传输。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。表示该包的公共网络抽象与时序（Clock / 帧快照流 / 重连 / 滞后补偿 / 网络条件模拟 / 准入）已被两个生产级 demo（MOBA、Shooter）端到端验证，并通过独立的契约测试覆盖（`src/AbilityKit.Network.Runtime.Tests`，~150 用例）。

### API 边界（本包承诺稳定的部分）
- 网络运行时抽象：传输、时钟、连接生命周期、收发时序。
- 快照流（snapshot stream）、重连（reconnect）、滞后补偿（lag compensation）、网络条件模拟（conditioning）、准入控制（admission）的公共契约。

### 已知限制 / 不在 0.1.0 承诺范围
- 仅承诺 .NET (`net10.0`) 与 Unity (`2021.3+`) 两面编译镜像一致；未承诺传输实现的字节级跨版本兼容（线协议稳定性由各 `protocol.*` 包负责）。
- 性能基线尚未固化（无纳入门禁的性能回归测试）。
- 暂未对外承诺跨 major 版本的二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 建立 CHANGELOG 与发版基线。

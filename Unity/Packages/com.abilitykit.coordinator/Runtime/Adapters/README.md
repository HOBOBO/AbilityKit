# SyncAdapter 体系状态说明

## 决策（2026-07-24）

**本目录的 `ISyncAdapter` 体系（`LocalSyncAdapter` / `RemoteSyncAdapter` / `HybridSyncAdapter` + `SyncAdapterFactory`）当前不被任何 demo 使用，属于未被验证的空壳抽象。**

### 证据

| Demo | 实际同步路径 | 是否经过 SyncAdapter |
|------|------------|---------------------|
| MOBA | `host.extension/ClientPredictionDriverModule` + view.runtime `RemoteDrivenWorldInstaller` | **否** |
| Shooter | `ShooterClientPredictionRuntimeAdapter` + `ShooterPackedSnapshotRollbackProvider` | **否** |

两个 demo 各自实现了不同的预测回滚路径，都绕过了 coordinator 的 SyncAdapter 体系。

### HybridSyncAdapter 的 4 处 TODO

`HybridSyncAdapter.cs` 内 4 处 TODO 全部未实现（`SubmitInput` / `Tick` / `Reconcile` / `GetAllEntityStates`）。它只在 `SyncAdapterFactory.Create(SyncMode.Hybrid)` 时被实例化，而该 factory 只被 `SessionCoordinator` 调用——但没有任何 demo 在生产路径上使用 `SessionCoordinator` 的 Hybrid 模式。

### 不移除的原因

1. `LocalSyncAdapter` 和 `RemoteSyncAdapter` 有完整实现，不是空壳
2. `ISyncAdapter` 接口设计本身合理（Attach/Tick/SubmitInput/GetAllEntityStates），未来可作为通用同步框架的基础
3. 移除是破坏性改动，需要确认没有外部消费者

### 使用建议

- **不要基于 SyncAdapter 体系做新功能**，除非你同时打算让至少一个 demo 接入它
- **MOBA/Shooter 的预测回滚实现**请参考各自的实际路径（`RemoteDrivenPredictionStateFactories` / `ShooterClientPredictionRuntimeAdapter`），不要参考 HybridSyncAdapter
- 如果未来要补全 HybridSyncAdapter，必须先解决：
  1. 状态哈希覆盖范围（当前 MOBA 只覆盖位置+InGame，不足以发现血量/Buff 漂移）
  2. 回滚边界（当前只 Transform+WorldRandom+FrameTime，血量/Buff/CD 不可复原）
  3. 让 `SessionCoordinator` 实际接入某个 demo 的战斗路径（当前两个 demo 都不用它）

### 相关文档

- HybridSyncAdapter.cs 顶部 XML 注释（2026-07-20 加）
- `.claude/skills/coordinator/sync_adapters.md`（2026-07-20 更新）
- `.claude/skills/coordinator/source_vs_readme.md`（README 9 处偏差对照）

# AbilityKit World StateSync

本包提供纯 C# 的本地状态历史、实体检查点和可选预测组件。适合先在单进程接入，再由宿主接入权威状态流；它不是网络会话或完整世界恢复系统。

- **推荐起点**：只保存已提交的快照元数据时用 `SnapshotBuffer`；需要捕获和恢复注册实体的本地状态时才用 `StateManager`。默认历史容量是 128 帧，按实际捕获频率与需要的窗口调整。
- **依赖与环境**：直接代码依赖 `AbilityKit.Core`、`AbilityKit.World.Snapshot` 和 `MemoryPack`；Unity 清单另声明由 OpenUPM 提供的 `org.nuget.system.runtime.compilerservices.unsafe@5.0.0`。最低 Unity 版本沿用 `2022.3`；仓库 .NET 工程从同一 Runtime 源码编译，以 `net10.0` 验证。
- **不负责**：房间接入、快照准入与固定基线重建、逻辑世界对象创建或销毁、表现插值、帧同步检查点、输入确认。状态同步与帧同步可共享世界状态和执行规则，但各自拥有权威游标。

```csharp
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;

// 接入片段：entity 须由调用方实现 IRollbackable，simulationFrame 是宿主模拟帧。
var history = new SnapshotBuffer(maxBufferSize: 128);
var checkpoints = new StateManager(history);
checkpoints.RegisterRollbackable(entity); // 由业务实现 IRollbackable
checkpoints.CaptureState(simulationFrame);
bool found = checkpoints.TryRestore(simulationFrame);
```

`found` 表示找到本地检查点，并不保证恢复所有实体。`GetFullState` 不包含逐实体检查点，不是完整权威快照。

## 目录

| 目录 | 内容 |
| --- | --- |
| `Runtime/StateSync/Checkpoints`、`Buffer` | 本地捕获/恢复与有界历史 |
| `Runtime/StateSync/Snapshot`、`Diff`、`Hash` | 快照模型、独立差分及哈希 |
| `Runtime/StateSync/Prediction/Replay`、`Prediction/Entity` | 输入重演协调器与独立的实体级预测适配 |
| `Runtime/StateSync/Aoi`、`Network`、`Compression`、`Events` | 可见集合、旧打包契约、独立压缩工具、客户端事件；不包含会话驱动 |
| [`Documentation~/`](Documentation~/README.md) | 接入契约、稳定性评审与限制 |
| [`Tests/`](Tests/README.md) | 包级验收入口 |
| [`Samples~/LocalSnapshotHistory`](Samples~/LocalSnapshotHistory/README.md)、[`Samples~/LocalCheckpoints`](Samples~/LocalCheckpoints/README.md) | 可导入的离线快照历史与实体检查点示例 |
| [`Samples~/LocalStateCommit`](Samples~/LocalStateCommit/README.md) | 纯内存提交示例：先校验、再导入、成功后才发布状态并推进提交游标 |

从 [接入与契约](Documentation~/StateSyncDesign.md) 开始，发布边界见 [稳定性评审](Documentation~/StabilityReview.md) 与 [CHANGELOG](CHANGELOG.md)。

# AbilityKit Diagnostics

DebugDraw 的运行时契约和 Unity SceneView 适配器由本包统一拥有：运行时代码使用
`AbilityKit.Diagnostics.DebugDraw`，编辑器代码使用
`AbilityKit.Diagnostics.Editor.DebugDraw`。DebugDraw 复用 Core 的纯 C# 数学类型，
因此本包现在显式依赖 `com.abilitykit.core`；不再是零内部依赖包。

开发期性能剖析与调试工具：Profiler 抽象、火焰图数据、分析工件导出与 Unity Editor 窗口。
不依赖任何其他 AbilityKit 包，可独立选装。

## 为什么需要它

战斗逻辑需要在纯 C# 逻辑层（脱离 Unity Profiler 的服务器/测试环境）测量热点，
并把结果带回 Unity 或导出为工件离线分析。本包提供与宿主无关的 Profiler 抽象和一套
Editor 侧消费工具。

## 能力清单

| 分类 | 类型 | 说明 |
|---|---|---|
| 核心 | `IProfiler` / `ProfilerHub` / `NullProfiler` | Profiler 抽象、全局入口、零开销空实现 |
| 核心 | `EditorProfiler` | 编辑器/开发期实现 |
| 数据 | `FlameData` | 火焰图数据结构（可导出给 speedscope 等工具） |
| 分析 | `AnalysisArtifact` / `AnalysisProfilerBuilder` / `AnalysisBattleDiagnosticSection` | 战斗分析工件与构建 |
| Editor | `DiagnosticsWindow` | Unity 编辑器诊断窗口 |
| Editor | `Exporters` / `AdvancedExporters` / `AnalysisArtifactJsonExporter` | 工件导出：json / csv / folded / speedscope / chrome trace / **flamegraph SVG** / 分析工件 JSON |

## 典型用法

```csharp
using (ProfilerHub.Current.Sample("battle.tick"))
{
    // ... 战斗帧逻辑
}
```

未挂载实现时走 `NullProfiler`，生产环境零开销；需要观测时替换 Hub 中的实现即可。

## Collection levels

Diagnostics is optional. The core runtime uses `NullProfiler` until a project explicitly installs
and registers a diagnostics implementation. `ProfilerOptions.CollectionLevel` controls the amount
of work performed by a running `EditorProfiler`:

| Level | Collected data |
|---|---|
| `Off` | No metrics are collected. Session lifecycle APIs remain available. |
| `Summary` | Counters, gauges, rates and duration summaries; no flame tree, raw samples, events or spans. |
| `Standard` | Full flame tree, samples, threshold events and span timeline. |
| `Verbose` | Reserved for detailed adapters; currently equivalent to `Standard`. |

Use `NullProfiler` or `Off` in production by default. Enable `Summary`, `Standard` or `Verbose`
only for a test, profiling or CI session.

When an `EditorProfiler` remains installed but collection is `Off` (or `Enabled` is false),
hot-path `Begin`, `Record` and event calls return through an immutable gate without taking the
aggregation lock. This keeps runtime feature switches cheap as well as keeping `NullProfiler`
available for deployments that do not install diagnostics at all.

Detailed span records are written to bounded thread-local buffers and merged when a snapshot,
session save or stop is requested. This keeps the high-frequency span path away from the central
span ring lock; the merged timeline preserves per-buffer completion order, while cross-thread
ordering is intentionally treated as diagnostic evidence rather than a synchronization contract.

Counter and gauge writes use bounded thread-local operation buffers and are merged before reads,
snapshots, session saves and stop. If a buffer fills, the newest operations replace the oldest;
the resulting loss is exposed as `ProfilerSnapshot.MetricOperationsDropped` and in JSON/CSV exports.

Raw `Sample` writes use the same bounded thread-local operation buffers. They are merged before
`GetSamples`, snapshots, session saves and stop, so the hot path does not take the profiler's
aggregation lock. Buffer overflow is reported through the same `MetricOperationsDropped` field.
The per-thread budget defaults to 256 and can be tuned with
`ProfilerOptions.MaxMetricOperationsPerThread`; changing it while a session is running keeps
the newest pending operations and counts evicted entries as dropped.

Counter rates maintain incremental 1-second, 5-second and 60-second rolling totals. A counter
write only expires samples that crossed a window boundary; reading a rate or snapshot performs
the same bounded expiry pass. This avoids rescanning the full 60-second history on every high-rate
counter operation while preserving `TotalCount`, peak rate and threshold event behavior.

`SaveSession` records only the session summary metadata it needs and does not clone the flame tree,
sample buffers or span timeline. Use `GetSnapshot` when a complete exportable copy is required;
this keeps frequent session checkpoints inexpensive for large captures.

The Unity diagnostics window reuses its display snapshot for a short refresh interval and takes a
fresh copy for export. Repainting the editor window therefore does not repeatedly clone a large
flame tree while a capture is running.

File exports for profiler JSON/CSV/folded/FlameGraph/0x formats and analysis artifacts write directly to a UTF-8 stream.
`ExportToString` remains available for tests and integrations that need an in-memory value, while
large file exports avoid constructing a second complete output string.

SpeedScope and Chrome Trace file exports serialize their final JSON directly to a UTF-8 stream,
while `ExportToString` remains available. Timeline events are staged as compact value records to
preserve ordering, and JSON is generated one event at a time. This avoids retaining a second string
for every event; sorting buffers and event records still require memory proportional to capture size.
FlameGraph SVG output is also written directly to the file stream after its layout is computed.

Probe tokens are tagged with the session generation in which they were created. Completing a
token after `Stop`/`Start` or `Clear` is ignored, preventing asynchronous work from contaminating
the next session.

## 依赖

无 AbilityKit 内部依赖。Unity 2022.3+（Editor 部分仅编辑器可用，Runtime 部分纯 C#）。

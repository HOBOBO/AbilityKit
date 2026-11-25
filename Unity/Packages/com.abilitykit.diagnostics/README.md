# AbilityKit Diagnostics

DebugDraw 的运行时契约和 Unity SceneView 适配器由本包统一拥有：运行时代码使用
`AbilityKit.Diagnostics.DebugDraw`，编辑器代码使用
`AbilityKit.Diagnostics.Editor.DebugDraw`。DebugDraw 复用 Core 的纯 C# 数学类型，
因此本包现在显式依赖 `com.abilitykit.core`；不再是零内部依赖包。

开发期性能剖析与调试工具：Profiler 抽象、火焰图数据、分析工件导出与 Unity Editor 窗口。

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
| Unity 运行时 | `UnityRuntimeDiagnostics` | 显式采集 Unity 帧耗时与内存指标；不会自动注册 PlayerLoop |
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

## 采集等级

诊断能力为可选项。在项目显式安装并注册诊断实现前，核心运行时使用 `NullProfiler`。`ProfilerOptions.CollectionLevel` 控制活动 `EditorProfiler` 执行的工作量：

| 等级 | 采集数据 |
|---|---|
| `Off` | 不采集指标；会话生命周期 API 仍可用。 |
| `Summary` | 计数器、量值、速率和时长摘要；不采集火焰树、原始采样、事件或跨度。 |
| `Standard` | 完整火焰树、采样、阈值事件和跨度时间轴。 |
| `Verbose` | 为详细适配器预留，目前等同于 `Standard`。 |

生产默认使用 `NullProfiler` 或 `Off`，仅在测试、性能分析或 CI 会话启用 `Summary`、`Standard` 或 `Verbose`。

保留 `EditorProfiler` 但采集等级为 `Off`（或 `Enabled` 为 false）时，热路径 `Begin`、`Record` 和事件调用通过不可变门禁直接返回，不获取聚合锁，使运行时开关保持低开销；完全不安装诊断的部署仍可使用 `NullProfiler`。

详细跨度记录写入有界线程本地缓冲，在请求快照、保存或停止会话时合并，避免高频路径争用中央跨度环形锁。合并时间轴保留每个缓冲内的完成顺序；跨线程顺序仅为诊断证据，不构成同步契约。

计数器及量值写入有界线程本地操作缓冲，在读取、快照、会话保存和停止前合并。缓冲满时新操作替换最旧操作；丢失量通过 `ProfilerSnapshot.MetricOperationsDropped` 及 JSON/CSV 导出公开。

原始 `Sample` 写入使用相同的有界线程本地操作缓冲，在 `GetSamples`、快照、保存和停止前合并，热路径不获取聚合锁。溢出通过相同的 `MetricOperationsDropped` 报告。每线程预算默认 256，可由 `ProfilerOptions.MaxMetricOperationsPerThread` 调整；活动会话中修改时保留最新待处理操作，将淘汰项计为丢失。

计数器速率增量维护 1 秒、5 秒和 60 秒滚动总量。写入仅清理越过窗口边界的采样；读速率或快照执行相同有界过期处理，避免每次高频操作都重扫完整 60 秒历史，同时保留 `TotalCount`、峰值速率及阈值事件行为。

`SaveSession` 仅记录所需会话摘要元数据，不复制火焰树、采样缓冲或跨度时间轴。需要完整可导出副本时使用 `GetSnapshot`，使大型采集的频繁会话检查点保持低开销。

Unity 诊断窗口在短刷新间隔内复用显示快照，导出时取得新副本，避免采集期间每次窗口重绘都复制大型火焰树。

性能分析的 JSON、CSV、folded、FlameGraph、0x 格式及分析工件直接写入 UTF-8 流。`ExportToString` 继续供测试和需要内存值的集成使用；大型文件导出避免构建第二份完整输出字符串。

SpeedScope 和 Chrome Trace 文件将最终 JSON 直接序列化到 UTF-8 流，同时保留 `ExportToString`。时间轴事件以紧凑值记录暂存以保序，逐事件生成 JSON，避免为每个事件保留第二份字符串；排序缓冲及事件记录仍需与采集规模成正比的内存。FlameGraph SVG 在完成布局计算后同样直接写入文件流。

探针令牌标记其创建时的会话代际。`Stop`/`Start` 或 `Clear` 后完成的旧令牌被忽略，防止异步工作污染下一会话。

## 依赖

- `AbilityKit.Diagnostics`：纯 C# 运行时，依赖 Core 的数学类型；`.NET` 工程复用同一源码。
- `AbilityKit.Diagnostics.Unity`（`Runtime/Unity/`）：Unity 2022.3+ 运行时适配，单向依赖纯 C# 运行时。
- `AbilityKit.Diagnostics.Editor`（`Editor/`）：窗口、SceneView 绘制、Editor 导出；JSON 工件导出使用 `com.unity.nuget.newtonsoft-json`。运行时程序集不引用该 JSON 库。

`UnityRuntimeDiagnostics` 的命名空间和方法不变。自定义 asmdef 若调用它，需引用 `AbilityKit.Diagnostics.Unity`；仅消费 `ProfilerHub` 等通用能力不需要增加引用。程序集限定反射名称或预编译二进制中持有旧程序集引用的消费者需要迁移。

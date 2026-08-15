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
| Editor | `Exporters` / `AdvancedExporters` / `AnalysisArtifactJsonExporter` | 工件导出（JSON 等） |

## 典型用法

```csharp
using (ProfilerHub.Current.Sample("battle.tick"))
{
    // ... 战斗帧逻辑
}
```

未挂载实现时走 `NullProfiler`，生产环境零开销；需要观测时替换 Hub 中的实现即可。

## 依赖

无 AbilityKit 内部依赖。Unity 2022.3+（Editor 部分仅编辑器可用，Runtime 部分纯 C#）。

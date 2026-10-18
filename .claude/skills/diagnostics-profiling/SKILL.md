---
name: diagnostics-profiling
description: AbilityKit 性能分析（com.abilitykit.diagnostics）快速上手与火焰图工作流——ProfilerHub/EditorProfiler 接入、ABILITYKIT_MOBA_PROFILE 冒烟采样、七种导出格式（含 flamegraph SVG）、world.tick 根作用域、MOBA 埋点地图、ProfilerHub 进程单例与并行测试坑、已知边界（speedscope 聚合粒度/damage 场景缺口）。触发场景：性能分析、火焰图、profiler、ProfilerHub、EditorProfiler、探针埋点、慢帧排查、冒烟采样、导出产物、speedscope、folded、采样指标命名。
---

# 性能分析（diagnostics）快速上手

基于源码核校（2026-10-10，框架修复后实测）。首次真实接入是 MOBA console 冒烟（会话记录含完整评估）。

## 30 秒上手（MOBA console 冒烟采样）

```powershell
$env:ABILITYKIT_MOBA_PROFILE = "1"
$env:ABILITYKIT_MOBA_PROFILE_DETAIL = "1"   # 明细模式：探针名带配置 ID/动作名（L1）
dotnet test src/AbilityKit.Demo.Moba.Tests --filter "FullyQualifiedName~Console_entry_full_battle_scenario"
# 产物：local/Logs/moba-console-profile/<label>-<时间戳>-<id>.{flame.svg,folded,speedscope.json,chrome-perf.json,json}
```

- 开关在 `ConsoleBattleBootstrapper.Initialize/Dispose`（harness：`src/AbilityKit.Demo.Moba.Console/Bootstrap/MobaConsoleProfiling.cs`）；`ABILITYKIT_MOBA_PROFILE_DIR` 可改输出目录。
- **必须过滤到单用例**：ProfilerHub 是进程级单例，并行测试类会互相顶掉 profiler 会话。
- 打开 `.flame.svg` 看聚合火焰图（悬停 tooltip）；**speedscope/chrome 现在是真实时间线**（逐事件、带帧号与关联上下文，L2/L3）——speedscope.app 时间线视图能直接看到哪一帧有尖峰。
- **方法级火焰图（L4）**：`powershell -ExecutionPolicy Bypass -File tools/profile_moba_dotnet_trace.ps1`（dotnet-trace 采样式，无插桩偏差，产物在 `local/Logs/moba-console-profile/dotnet-trace/`）。

## 框架结构（谁在哪）

| 层 | 位置 | 说明 |
|----|------|------|
| 探针 API | `com.abilitykit.diagnostics/Runtime/Core` | `IProfiler`（Begin/Record/Increment/SetGauge/Sample）+ `ProfilerHub` 静态入口 + `NullProfiler`（默认，零开销）+ `EditorProfiler`（内存聚合、火焰树、线程栈按 ManagedThreadId） |
| 导出器 | 同包 `Editor/Exporters`（纯 BCL，无 UnityEditor 依赖） | 7 种格式；Unity 侧归 editor asmdef，.NET 侧经 `src/AbilityKit.Diagnostics.Exporters` 投影工程 |
| 会话 harness | `src/AbilityKit.Demo.Moba.Console/Bootstrap/MobaConsoleProfiling.cs` | 安装/停止/导出/恢复 NullProfiler 一条龙；导出失败只告警不影响冒烟结果 |

## 导出格式（`ExporterFactory.Get(format)`，落盘一律无 BOM UTF-8）

| format | 扩展名 | 用途 |
|--------|--------|------|
| `flamegraph` | `.flame.svg` | **静态 SVG 火焰图**：块宽=累计耗时、向上分叉=调用链；8 槽类目色按指标族（名称第二段）首见顺序分配 |
| `speedscope` | `.speedscope.json` | speedscope.app 交互查看；事件对是**聚合**的，看层级占比别看逐帧时间轴 |
| `folded` | `.folded` | Brendan Gregg folded 栈（flamegraph.pl/inferno） |
| `json` | `.json` | 完整快照（指标目录/计数器/gauge/样本/耗时摘要/事件/火焰树） |
| `chrome` | `.chrome-perf.json` | Chrome trace-viewer（聚合粒度） |
| `csv` / `0x` | `.csv` / `.0x.json` | 表格化 / 树形 JSON |

## 埋点地图（已存在，直接消费）

- **框架根作用域**：`world.tick`（`EntitasWorld.Tick`，com.abilitykit.world.entitas）——全部系统执行挂到同一棵 per-frame 树下，并向 ProfilerHub 同步帧号（span.Frame 的来源）；世界外直接派发的工作（如施法入口同步段）保留为 `moba.*` 顶层根，两类根的占比本身就是诊断信息。
- **每系统行**：`world.system.<类型名>`（`WorldSystemBase.Execute`）——火焰图里每个 Entitas 系统一行，直接定位哪个系统吃帧。
- **MOBA 作用域探针**（进火焰树；明细模式追加标识）：`moba.trigger.dispatch/evaluate/execute`（MobaTriggerDiagnosticsAdapter，evaluate 带事件名、execute 带动作名）、`moba.effect.execute[<configId>]` / `moba.effect.action[<actionId>]`（MobaEffectExecutionService）。
- **MOBA RecordDuration**（挂在当前火焰栈上）：`moba.damage.pipeline/stage`、`moba.buff.drain`、`moba.skill.pipeline.step`、`moba.skill.runner.step`、`moba.effects.step`、`moba.continuous.tick`。
- 通道默认全开（`MobaBattleDiagnosticsService.CapturesMetrics` 无 policy 时为 true）；要关某通道用 `SetChannelEnabled`。

## 三个粒度维度的实现（2026-10-12 L1-L4 落地）

| 维度 | 机制 | 看在哪 |
|------|------|--------|
| 业务粒度（L1） | `MobaPerformanceProfiling.DetailMarkers`（宿主用 `ABILITYKIT_MOBA_PROFILE_DETAIL=1` 开）在探针名追加 `[configId]`/`:actionName`；per-system 行常开 | `.flame.svg` / folded |
| 时间粒度（L2） | EditorProfiler span 环形缓冲（`ProfilerOptions.MaxSpanEvents`，默认 16384，环形覆盖计 `SpansDropped`）+ 帧轴（`ProfilerHub.SetFrame`）；chrome/speedscope 导出真实时间线（无 span 时回退聚合）；json 有 `spanLog` 摘要 | `.chrome-perf.json` / `.speedscope.json` 时间线视图 |
| 身份粒度（L3） | `ProfilerHub.Begin(name, contextId)` 携带关联上下文（effect=父技能施法节点、action=动作 trace 节点、trigger=派发 TraceScope id），span 与 chrome 事件 args.cid 可与 trace jsonl 的 ContextId/ConfigId join | chrome args.cid × trace jsonl |
| 方法级（L4） | dotnet-trace 采样式（外部工具），`tools/profile_moba_dotnet_trace.ps1` 一键 | speedscope.app |

**探针带 contextId 的写法**：`ProfilerHub.Begin(name, contextId).ToScope()`；自定义实现 IProfiler 需实现 `Begin(string, long)` 重载。

## 指标命名约定（防再踩重复测量坑）

- 同一段工作**只允许一种测法**：作用域探针（`ProfilerHub.Begin`）完成时自动产出同名时长摘要+样本，**不要再对同一 span 调 `RecordDuration`**（历史教训：`moba.effect.action.durationMs` 与 `moba.effect.action` 并存，火焰树出现假兄弟帧，2026-10-10 已删）。
- 名称=小写点分层级，第一段是类目（火焰根），第二段是指标族（SVG 配色）；采样名要跨版本稳定（设计文档 §七 Metric Catalog）。

## 已知边界

- 时间线 span 有界（默认 16384 条环形覆盖）：超长战斗会丢最旧事件（json `spanLog.dropped` 可查）；需要更长窗口调大 `MaxSpanEvents`。
- 明细模式（DetailMarkers）会放大指标基数与开销，只在采样时开；方法级"真相"用 L4 采样式，插桩数字有测量扰动。
- `damage.pipeline`/`continuous.tick` 有指标定义，但 FullBattleScenario 不产生伤害链路——想看 damage 火焰要换有伤害的战斗脚本。
- EditorProfiler 每探针一次锁+字典操作，冒烟/dev 可用，别当生产遥测。
- Unity 侧用 DiagnosticsWindow 菜单导出（格式列表来自 `ExporterFactory.GetSupportedFormats`）。

## 验证入口

- 导出器契约测试：`src/AbilityKit.Diagnostics.Tests/ExporterTests.cs`（SVG 形状/无 BOM/folded 格式），挂 `foundation-units` gate。
- 冒烟采样管线：`src/AbilityKit.Demo.Moba.Tests/Smoke/MobaConsoleProfilingTests.cs`（Gate=MobaConsoleSmoke，P0）。
- 新埋点落地后：跑 `tools/run_test_gate.ps1 -Gate foundation-units` + `-Gate moba-console-smoke`；改了 Unity 包源码另需 Unity 编译验证（见 CLAUDE.md 已知工程坑）。

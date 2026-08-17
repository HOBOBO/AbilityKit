# MOBA 战斗诊断故障排查

> 适用版本状态：2026-07-19

本文按“运行前提 → Session → Capability → 查询状态 → Revision → Unity 工程”的顺序定位问题。

## 查询状态速查

查询结果同时包含 `Phase` 与 `Availability`。不要只检查结果集合是否为空。

### 阶段

| Phase | 含义 | 处理方式 |
| --- | --- | --- |
| `Idle` | 尚未发起查询 | 检查 ViewModel 是否进入刷新流程 |
| `Loading` | 查询进行中 | 等待完成；本地同步查询通常不会长期停留 |
| `Ready` | 有完整结果 | 正常显示 |
| `Empty` | 查询成功，但结果数为 0 | 正常空态，不应显示为错误 |
| `Partial` | 只有部分结果 | 检查 `Truncated` 或 `Evicted` 原因 |
| `Unavailable` | 当前无法提供结果 | 根据 Availability 定位 |
| `Error` | 查询执行失败 | 记录 ErrorCode、Message、Scope 和 Revision |

### 可用性

| Availability | 含义 | 常见原因 |
| --- | --- | --- |
| `Available` | 数据正常可用 | `Ready` 或 `Empty` |
| `NotProduced` | 对应 Store 尚未生产数据 | 采样系统未执行、事件未发生、Trace 根从未建立 |
| `NotCaptured` | 数据面存在，但请求对象或帧没有被当前快照捕获 | Actor 不在快照、请求了非最新帧 |
| `Evicted` | 历史数据或 revision 已被有界 Store 淘汰 | 事件分页过慢、Trace 根已淘汰 |
| `Truncated` | 超过查询或展示上限 | 缩小过滤范围或继续分页 |
| `Unsupported` | 当前 Session 没有该能力 | Store 未注入、功能未实现或环境不支持 |
| `Disconnected` | 会话连接不可用 | 主要供未来远端 Session 使用 |
| `Error` | 查询失败 | 查看错误码和消息，不应回退读取 Runtime 对象 |

## 窗口无法使用

### 提示“进入播放模式后才能使用”

原因：Battle Debug 窗口只在 Play Mode 下解析活动战斗会话。

处理：

1. 打开可启动 MOBA 战斗会话的场景。
2. 进入 Play Mode。
3. 等待场景和 World 初始化完成后重新打开或刷新窗口。

### 提示 `BattleDebugFacadeProvider.Current` 为空

原因：战斗调试 Facade 尚未注册，通常表示 `BattleLogicSessionHost.Start()` 未执行、启动失败或已停止。

处理：

1. 检查场景是否使用正确的 MOBA 启动入口。
2. 检查 Console 中会话启动前的首个异常。
3. 确认不是只启动了表现层而没有启动逻辑会话。
4. 修复启动问题后重新进入 Play Mode。

### 提示没有活动 `BattleLogicSession`

原因：Facade 存在，但当前没有可查询的逻辑会话。

处理：确认会话生命周期是否已经启动，或是否在切场景、退出战斗时提前停止。

## 诊断会话不可用

面板通过 `BattleDebugDiagnosticSessionResolver` 从当前 Facade、Battle Session、World Services 解析 `IBattleDiagnosticReadOnlySession`。

按以下顺序检查：

1. `BattleDebugContext.Facade` 是否有效。
2. `TryGetSession` 是否成功。
3. `TryGetWorld` 是否成功。
4. World Services 是否注册了 `IBattleDiagnosticReadOnlySession`。
5. SessionInfo 的 Scope 是否有效。

不要在 Panel 内直接解析具体 Store，也不要在 Session 解析失败后读取 `SelectedUnit` 作为兜底。这样的兜底会让同一面板同时展示不同时间边界的数据。

## 面板提示能力不支持

Capability 由 Session 根据实际可用端口声明。能力位缺失不一定是代码错误。

| 面板 | 必需能力 |
| --- | --- |
| 总览 | `ActorState | ActorTags | ActorEffects` |
| 属性 | `ActorAttributes` |
| Buff | `ActorBuffs` |
| 标签 | `ActorTags` |
| 效果 | `ActorEffects` |
| 诊断事件 | `Events` 查询表面 |
| 诊断状态 | `WorldState`、`ActorState` 查询表面 |

定位步骤：

1. 检查 `SessionInfo.Capabilities`。
2. 检查对应只读 Store 是否以相同 World Scope 注册。
3. 检查 Local Session 是否使用能够接收该 Store 的构造路径。
4. 检查 Store Scope 是否与 Event/State Store Scope 一致。
5. 新增端口后检查 World DI 是否仍解析到旧构造器或缺少模块注册。

不要因为 `BattleDiagnosticCapabilities.AllLocal` 包含某项，就假定每个 Local Session 都声明该项。

## 空结果与未采样

### `Empty`

表示查询成功且该 Actor 当前确实没有对应项。例如 Actor 已采样但没有 Buff，结果应为 `Empty`。

### `NotProduced`

表示 Store 从未收到快照或相关事实从未生产。例如：

- 状态采样系统尚未执行第一帧。
- Trace Registry 尚无任何根。
- 事件类型在当前战斗中从未发生。

### `NotCaptured`

表示 Store 已有快照，但请求不属于该快照。例如：

- 选择的 Actor 已在采样前销毁。
- Actor 不在当前 Registry。
- ViewModel 请求了非当前快照帧。
- 切换 World/Epoch 后仍保留旧选择。

latest-only Store 不支持任意历史帧查询。帧 `0` 表示最新；指定帧必须等于当前快照帧。

## 数据不刷新

### 先检查 Revision

各数据面使用独立 revision：

- `EventStoreRevision`
- `StateStoreRevision`
- `TraceStoreRevision`
- `ActorAttributeStoreRevision`
- `ActorBuffStoreRevision`
- `ActorTagStoreRevision`
- `ActorEffectStoreRevision`

如果数据变化但对应 revision 不变，问题通常在 Producer、Sampler 或 Store 提交链路。如果 revision 已变而面板不刷新，检查 ViewModel 缓存键和窗口重绘。

### 检查 ViewModel 缓存键

Actor 详情 ViewModel 至少应包含：

- Session Scope
- 对应 Store revision
- ActorId
- Frame

事件 ViewModel 还应包含所有过滤条件和选择状态。Overview 必须同时包含 State、Tag、Effect 三类 revision。Trace ViewModel 使用 Session Scope、`TraceStoreRevision` 和 Root Context ID。显式刷新应使缓存失效并请求 Repaint，不应修改 Store。

### 窗口刷新边界

主窗口每 0.25 秒刷新实体列表并 Repaint。若列表更新而面板数据不更新，优先检查对应 Store revision，而不是提高刷新频率。

## 事件缺失

按以下顺序检查：

1. 事件是否在当前战斗真实发生。
2. Producer 是否拿到 `IMobaBattleDiagnosticEventSink`。
3. 对应 `BattleDiagnosticEventChannel` 是否启用。
4. Draft 是否通过 Scope、Sequence 和字段校验。
5. Event Store 是否被冻结或容量淘汰了旧事件。
6. 面板过滤条件是否排除了事件。
7. 查询是否绑定到旧 Store revision。

关闭的通道不会写入事件，也不会消耗 Sequence。诊断提交异常按设计与战斗主流程隔离，因此应检查诊断日志和聚焦测试，不能依赖战斗逻辑异常暴露采集失败。

## Trace 查询问题

- `Unsupported`：当前 Session 没有同 Scope Trace Read Store。
- `NotProduced`：Trace Registry 尚未生产任何根。
- `Evicted`：Registry 已有历史，但目标 RootContextId 已不存在。
- 树结构错误：检查 ParentContextId、RootContextId、CreatedFrame 和显式 `IsEnded`，不要用 `EndedFrame == 0` 推断活动状态。

在战斗调试窗口切换到 `Trace` 面板，输入事件或日志中的 Root Context ID 后点击“加载”。面板会显示树层级、节点状态、孤儿标记和选中节点父链；若状态提示持续不变，检查 `TraceStoreRevision` 是否推进以及输入的根是否仍在 Store 保留范围内。

## Unity 编译与测试问题

### Unity 项目已被另一个 Editor 打开

不要启动第二个 Unity Editor 实例，也不要结束用户正在使用的 Editor 进程。可以执行不需要项目锁的范围化 `dotnet build`；EditMode Test Runner 应等待现有 Editor 可用后从该实例运行。

### 新脚本在 IDE 构建中缺失

Unity 生成的 `.csproj` 可能尚未同步。检查：

1. 新文件是否位于正确程序集目录。
2. `.asmdef` 引用是否正确。
3. 新脚本是否有唯一 `.meta` GUID。
4. 生成 `.csproj` 是否包含对应 `<Compile Include>`。
5. 必要时由 Unity 重新生成项目文件。

手工同步生成项目只用于当前工作区验证，不应把生成文件当作程序集边界的来源。

### 只有编译结果，没有 NUnit XML

`dotnet build` 只能证明程序集编译通过，不能证明 NUnit 测试已执行。只有 Unity Test Runner 或其他实际执行测试并产生明确结果的流程，才能宣称测试通过。

### 无关脏工作区错误阻塞验证

不要回退不属于当前任务的改动。先判断错误是否位于本次改动依赖链：

- 相关错误：与现有改动协同修复并补充验证。
- 无关错误：使用范围化项目和 `BuildProjectReferences=false` 验证当前程序集，并在结果中明确剩余限制。

## 报告问题所需信息

提交诊断工具问题时至少包含：

- Unity 版本和当前场景。
- 是否处于 Play Mode。
- Session Scope、Capabilities、ConnectionState、CaptureState。
- 面板名称、ActorId 和查询 Frame。
- Query Phase、Availability、ErrorCode、Message。
- 对应 Store revision。
- 可复现步骤和首个相关 Console 异常。
- 是否实际运行 EditMode 测试，以及结果文件路径。

# ActionTimeline 数据协议与播放边界

> 本文描述 `com.abilitykit.actionschema` 的真实职责：它是 ActionEditor 与运行时之间的时间线 DTO、JSON 加载器和最小播放 helper，不是 Triggering 的 Action 参数 Schema，也不是完整的动画或技能时间线引擎。

## 1. 能力定位

ActionTimeline 把编辑器资产压缩为纯 C# 数据协议，使 Unity 编辑器导出的逻辑 JSON 可以被 Unity Runtime 和 `.NET` 工程共同读取。

公共包提供：

- `SkillAssetDto`、`GroupDto`、`TrackDto`、`ClipDto` 等共享 DTO。
- `ActionTimelineJson` 文件与字符串反序列化入口。
- `TimelinePlayer` 单次触发扫描器。
- `ITimelineEventSink` 的 TriggerLog 输出边界。

公共包不提供：

- TriggerPlan 中 Action 参数的 schema、绑定或校验。
- 通用 clip handler 注册器和业务动作执行。
- clip 区间进入、持续、退出、blend 或嵌套时间线。
- seek、倒放、排序、循环、暂停和自动结束。
- 资源版本迁移、业务校验、异常隔离和完整回滚状态。

因此该模块应被声明为“动作时间线数据协议与最小触发 helper”。MOBA 的 handler registry、event buffer 和 Ability phase 属于领域层扩展。

| 层级 | 应负责 | 不应由该层统一规定 |
|------|--------|--------------------|
| ActionSchema 公共包 | DTO、JSON 读取、一次性 clip 扫描和最小 sink 协议 | 技能阶段、业务 handler、回滚、动画同步和大规模调度 |
| Editor/export 接入 | 将 authoring 资产稳定导出为协议数据，并维护兼容性 | 直接决定运行时副作用 |
| 项目应用层 | clip 类型语义、handler registry、phase 完成条件、异常与回滚策略 | 要求所有游戏使用 MOBA clip 目录 |
| MOBA/View 示例 | 展示 handler、event buffer 与 Ability Pipeline 接法 | 被视为公共播放器的默认应用套件 |

公共包保持最小，能够低成本复用数据协议；真正多变的技能、动画和表现编排继续留在项目中。开箱即用在这里表示“DTO 与最小播放器可立即接入”，不表示“完整战斗时间线不需要项目开发”。

## 2. 源码与验证入口

| 内容 | 路径 |
|------|------|
| Unity Runtime DTO | `Unity/Packages/com.abilitykit.actionschema/Runtime/Ability/Share/ActionSchema/ActionTimelineDtos.cs` |
| JSON 与公共播放器 | `Unity/Packages/com.abilitykit.actionschema/Runtime/Ability/Share/ActionSchema/ActionTimelineRuntime.cs` |
| .NET 镜像工程 | `src/AbilityKit.ActionSchema/AbilityKit.ActionSchema.csproj` |
| ActionEditor 导出 | `Unity/Packages/com.abilitykit.thirdparty.actioneditor/ActionEditor/Editor/LogicJsonExporter.cs` |
| MOBA 播放器 | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/ActionTimeline/MobaTimelinePlayer.cs` |
| MOBA handler registry | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/ActionTimeline/MobaClipHandlerRegistry.cs` |
| Ability Pipeline phase | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Ability/Pipeline/Timeline/AbilityTimelinePhase.cs` |
| View 直接播放示例 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/TimelineLogicRunner.cs` |
| View Pipeline 示例 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/SkillPipelineDemoRunner.cs` |

`.NET` 工程直接链接 package Runtime 源码，是公共协议的无 Unity 构建入口：

```powershell
dotnet build src/AbilityKit.ActionSchema/AbilityKit.ActionSchema.csproj -c Release
```

该命令只证明共享 DTO、JSON loader、公共播放器及其 Core/Newtonsoft.Json 依赖可以在 `net10.0` 下编译，不执行播放语义测试。ActionEditor、MOBA Runtime 和 View Runner 是 Unity 侧真实生产与消费证据。

## 3. 数据协议与所有权

```mermaid
flowchart LR
    Editor[ActionEditor Asset] --> Exporter[LogicJsonExporter]
    Exporter --> Legacy[兼容 .logic.json]
    Legacy --> Public[公共播放器或旧接入]
    Exporter --> Logic[.moba.logic.json]
    Exporter --> Presentation[.moba.presentation.json]
    Logic --> Compiler[SkillFlow 编译为时间线事件]
    Presentation --> View[View 表现时间线]
```

DTO 层级为 Asset、Group、Track、Clip。核心字段包括：

| 层级 | 数据 |
|------|------|
| Asset | `length`、`groups` |
| Group | `name`、`actorId`、active/locked/collapsed、`tracks` |
| Track | `type`、`name`、active/locked、`clips` |
| Clip | `type`、`start`、`length`、`blendIn`、`blendOut`、字符串 `args` |

DTO 使用可变 public 字段和可变集合。加载器和播放器不复制数据；调用方持有并共享同一对象图。播放期间修改 group、track、clip 或 args 会立即影响后续扫描，没有冻结、并发保护或版本戳。播放器因此借用 asset，不拥有 asset；发布方负责在播放期间保持对象图稳定。

普通 `LoadFromJson` / `LoadFromFile` 保留兼容反序列化语义，不做业务校验。分层产物由 `LoadForRuntime` 检查 `schemaVersion=1`、顶层运行类型、活动 Clip 的归属及时间字段；它不验证 Clip 业务支持、资源、重复身份或 `start + length` 上界。

## 4. 编辑器导出契约

`SkillAsset` 源 JSON 是编辑输入。`LogicJsonExporter` 映射层级，将 Track/Clip 的 CLR `FullName` 写入 `type`，仅从实现 `ILogicJsonExportable` 的 Clip 填充 `args`。

| 场景 | 导出和消费 |
| --- | --- |
| 未启用 MOBA 分层的兼容导出 | 普通导出写 `<name>.logic.json`；MOBA 专用菜单不接收未启用分层的源资产。 |
| 启用 `exportMobaRuntime` | 先按运行侧验证和划分活动 Clip，再写 `<name>.moba.logic.json` 和 `<name>.moba.presentation.json`；该分支不新写也不清理旧 `.logic.json`。 |
| MOBA 逻辑消费 | `SkillTimelinePhaseDef.MobaLogicTimeline` 在 SkillFlow 发布中交给 `MobaActionTimelineCompiler`；`ExecuteEffect` 编译为时间线事件，`TriggerLog` 不形成正式效果事件，其他不支持的逻辑 Clip 被拒绝。 |
| MOBA 表现消费 | `MobaSkillPresentationCastDriver` 严格加载表现侧 JSON，针对施法实例管理表现；不执行权威效果。 |

重新导出、SkillFlow 发布、运行时资源加载是不同步骤。不能按 JSON 是否存在或按后缀相似来替代消费验证；编辑输入、导出物、发布物不互相手改覆盖。

CLR 全名因此成为持久化协议。重命名命名空间、类型或程序集映射可能让旧 JSON 无法匹配 handler，必须作为数据迁移处理，而不能视为普通重构。

导出不是原子替换：没有临时文件、校验后 rename 或旧文件备份。进程中断和磁盘错误可能留下不完整文件；生产流水线应在外层增加临时输出、反序列化校验和原子发布。

## 5. 公共播放器的真实语义

```mermaid
flowchart TD
    Tick[Update delta] --> Clamp[Negative delta becomes zero]
    Clamp --> Advance[Advance current time]
    Advance --> Group[Scan groups in stored order]
    Group --> ActiveG{Group active?}
    ActiveG -->|no| Group
    ActiveG -->|yes| Track[Scan tracks in stored order]
    Track --> ActiveT{Track active?}
    ActiveT -->|no| Track
    ActiveT -->|yes| Clip[Scan clips in stored order]
    Clip --> Due{Not fired and start reached?}
    Due -->|no| Clip
    Due -->|yes| Fire[Try TriggerLog]
    Fire --> Mark[Mark key as fired]
```

`TimelinePlayer.Update` 先把负 delta 钳为零，再推进时间并全量扫描。所有满足 `clip.start <= Time + epsilon` 的未触发 clip 都会在当前 tick 触发，因此大步长会补触发跨过的 clip。

公共播放器只识别 `AbilityKit.ActionEditorImpl.TriggerLog` 或以 `.TriggerLog` 结尾的类型。未知类型、null sink 和不匹配类型都不会输出事件，但仍会被标记 fired，不会在注册 sink 或能力后重试。

播放器不读取 asset length 来完成或停止；View 示例在外部比较 `Time >= asset.length` 并选择 reset。`length`、blendIn、blendOut 和 clip length 在公共播放算法中均不参与计算。

## 6. Clip identity 与 Reset

公共播放器和 MOBA 播放器用以下字段拼接 fired key：

```text
group.name | track.name | clip.type | clip.start | clip.length
```

key 不包含 group/track/clip 索引、actorId、args 或稳定 GUID。相同 group name、track name、type、start、length 的不同 clip 会碰撞，只触发一次。实现使用 `ToString("R")`，但没有显式指定 `InvariantCulture`；在依赖本地文化的环境中，字符串身份仍不应被当作跨平台稳定 ID。协议也没有转义分隔符，名称中的 `|` 会进一步扩大歧义。

`Reset(time)` 只设置当前时间并清空 fired 集合。若 reset 到非零时间，下一次 update 即使 delta 为零，也会补触发所有 start 不晚于该时间的 active clip。它不是 seek 语义，也不保存“目标时间之前已消费”的状态。中断后重新使用同一 player 会重新允许旧 clip 触发。

需要稳定回放时，应在数据协议中加入不可变 clip ID，并显式区分 restart、seek 与 restore；在此之前不要依赖同字段 clip 的独立触发。

## 7. MOBA 领域播放器与 Ability Phase

`MobaTimelinePlayer` 复用公共播放器的扫描和 fired key 规则，但把执行扩展为 `MobaClipHandlerRegistry`：

- registry 按精确字符串键保存 handler，重复注册覆盖旧值。
- 默认 registry 只注册 `TriggerLogHandler`。
- handler 缺失、sink 为 null 或 handler 返回 false 时，clip 仍被标记 fired；`TryHandle` 的 false 不会自动重试。
- handler 异常没有统一捕获，会从 Update 向调用方传播。

`AbilityTimelinePhase` 负责把领域播放器接入技能 Pipeline：

1. 从构造参数或 context 取得 asset。
2. 从 context 取得 registry；缺失时创建默认 registry。
3. 从 context 取得 sink；缺失时创建并写回 `AbilityTimelineEventBuffer`。
4. 创建播放器并 reset 到零。
5. 每 tick 更新播放器。
6. 当 player time 不小于 asset length 时完成 phase。

asset length 的完成语义属于 Ability phase，不属于公共播放器。中断、错误和 reset 只清除 phase 对 player/asset 的引用；event buffer、外部 sink 已产生的副作用和 handler 内状态不会自动回滚。公共 `TimelinePlayer` 以 float 累计时间，而 MOBA player 目前以 Q32.32 raw 时间累计；两者并不因此获得端到端确定性，DTO 时间、handler、副作用和外部 tick 仍需单独约束。

## 8. 时间、顺序与确定性边界

公共播放器不按 start 排序，同 Tick 到期的 Clip 按对象图顺序扫描；未来 Clip 不会截断后续扫描。MOBA 正式逻辑编译器另行量化并排序时间线事件，保留同时间事件顺序。导出与迁移应保留原有顺序。

浮点时间使用累计加法和固定 epsilon，没有 tick index、定点数或量化。若用于帧同步、预测或回放：

- 上层必须固定 delta 和加载数据版本。
- handler 必须确定性且不依赖 Unity 非确定 API。
- sink 副作用必须可记录、去重或重演。
- 需要独立 hash/replay 证据，不能仅凭 DTO 为纯 C# 就声明确定性。

公共播放器每 tick 全量扫描所有 active clip，复杂度随时间线规模线性增长。它没有游标、按时间索引或已结束 track 裁剪，适合小型逻辑时间线和示例，不应未经基准直接用于大规模并发实体。

## 9. 失败传播与成熟度

| 环节 | 当前失败语义 |
|------|--------------|
| 文件读取 | IO 异常直接传播 |
| JSON 解析 | 空字符串返回 null；格式异常直接传播 |
| 分层校验 | `LoadForRuntime` 拒绝错误版本、运行侧或不合法的活动 Clip；逻辑编译和表现资源解析另有失败边界 |
| 公共 sink | 回调异常直接传播 |
| MOBA handler/sink | 异常直接传播 |
| 未知/不支持 clip | 静默跳过并标记 fired |
| handler 返回 false | MOBA 播放器仍标记 fired，不会自动重试 |
| phase error | Pipeline error 路径处理，但已产生副作用不回滚 |

2026-11-25 本地 `.NET` 的 `MobaActionTimelineCompilerTests` 12/12 通过，覆盖分层与错侧拒载、逻辑编译、表现恢复等；[ActionSchema 完整示例](../../../Unity/Packages/com.abilitykit.actionschema/Documentation~/README.md#完整示例)原样通过分层、序列化与严格加载。它们不执行 Unity 导出菜单、双文件写入或正式发布，也不是公共播放器专项 suite。

同日用 Unity 2022.3.62f1 在隔离工程导入临时时间线，直接调用 `ExportSelectedTimeline`。只生成两份 MOBA 分层文件，旧 `.logic.json` 不变；从磁盘读取实际导出文件后，逻辑侧编译出单个 `125 ms` 效果事件，表现侧验证了按施法实例派发粒子请求、携带追平偏移、向后定位后重播及停止。两侧都拒绝对侧文件，不支持的活动 Clip 在写入前被拒绝。报告在 `local/Logs/documentation/D4-TimelineUnity-20261125-145443/`；未运行实际效果、资源渲染、正式发布或战斗预测回滚。

优先补充：

1. DTO 缺字段、未知字段、null 集合和非法时间的兼容测试。
2. 跳帧、零/负 delta、非零 reset 和未排序 clip 测试。
3. fired key 碰撞与稳定 clip identity 测试。
4. null sink、未知 handler、handler false 和异常传播测试。
5. asset length 为零或负数，以及 phase 中断/错误后的 event buffer 语义。
6. 编辑器导出到 runtime load 的 golden JSON 契约测试。

## 10. 采用结论

ActionTimeline 的生产分层路径可复用版本 1 和严格加载；临时样例已验证实际 Editor 导出到两侧消费，稳定 EditMode 回归、稳定 Clip ID 和原子双文件发布仍是缺口。旧 DTO 和公共播放器不能替代正式 SkillFlow 编译或施法表现适配。

它不能被描述为通用 Timeline 引擎，也不能与 Triggering Action 参数 Schema 混为一谈。需要持续区间、blend、seek、倒放、动画同步或大规模调度时，应由独立执行层实现，并把 ActionTimeline DTO 仅作为输入格式或先升级协议。

## 11. 关联文档

- [配置系统](04-ConfigurationSystem.md)：运行时配置数据库、TriggerPlan 与 Action 参数 Schema。
- [CodeGen 与 Luban 生产链路](07-CodeGenAndLubanProductionPipeline.md)：配置生成、候选晋升与发布边界。
- [Pipeline 与 Ability Runtime](../08-GameplayModules/08-PipelineAndAbilityRuntime.md)：phase/run 生命周期与终止语义。
- [技能系统架构](../08-GameplayModules/01-SkillSystemArchitecture.md)：Timeline phase 在技能执行链中的位置。

---

文档类型：Canonical 设计 | 事实基线：旧播放器章节 2026-08-17；分层导出和消费章节 2026-11-25 | 证据等级：E0 源码、局部 E3（.NET 与隔离 Unity 导出）；未新增 E4/E5。

*文档版本：v3.1 | 最后更新：2026-08-17*

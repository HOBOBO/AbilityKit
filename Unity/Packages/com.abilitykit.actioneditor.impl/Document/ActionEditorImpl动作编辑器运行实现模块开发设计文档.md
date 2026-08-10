# Ability-Kit ActionEditorImpl 动作编辑器实现模块开发设计文档

## 一、文档定位

本文是 `com.abilitykit.actioneditor.impl` 的 package canonical 文档。该包为 third-party ActionEditor 提供 AbilityKit 项目侧的资产、Track 和 Clip 类型，主要属于 authoring schema，而不是完整的运行时 Timeline 引擎。

跨包链路必须区分四个所有者：

1. `com.abilitykit.actioneditor.impl`：编辑资产、轨道和片段类型；
2. `com.abilitykit.thirdparty.actioneditor`：编辑器和 logic JSON 导出器；
3. `com.abilitykit.actionschema`：可序列化 DTO、JSON 加载和基础播放器；
4. `com.abilitykit.demo.moba.runtime`：MOBA 专用播放器、Clip Handler 和技能流水线消费。

类型能够在编辑器中创建，不代表它能进入 logic JSON，更不代表运行时具备对应副作用。

## 二、模块边界

ActionEditorImpl 当前负责：

- 定义 `SkillAsset : Asset`；
- 定义 Action、Animation、Audio、Effect、Signal 等 Track；
- 定义 Animation、Audio、Particle、Transform、Visible 和 Signal Clip；
- 通过 `Name`、`Attachable`、`Color`、`ShowIcon` 等元数据接入 ActionEditor；
- 部分 Clip 通过 `ILogicJsonExportable` 填充 logic DTO 参数。

ActionEditorImpl 不负责：

- 编辑器窗口、序列化框架和 logic JSON 文件写入；
- `SkillAssetDto`、TrackDto 和 ClipDto 的契约所有权；
- 运行时时钟、Clip 调度、事件总线和具体副作用；
- 动画、音频、粒子、Transform 或 Signal 的完整播放闭环；
- 业务技能生命周期、目标选择、资源加载和回滚。

## 三、Authoring 类型

### 3.1 SkillAsset

`SkillAsset` 只继承 third-party ActionEditor 的 `Asset` 并提供名称和序列化元数据，没有自身执行逻辑。资产保存格式、编辑器行为和导出入口由 third-party ActionEditor 决定。

### 3.2 Track

当前 Track 包括：

- `ActionTrack`；
- `AnimationTrack`；
- `AudioTrack`；
- `EffectTrack`；
- `SignalTrack`。

Track 主要声明分类、颜色、图标和可附着关系。`ActionTrack` 仍含 `Test1`、`Test2`、`Test3` 示例字段，说明 schema 尚有实验痕迹；这些字段不应被视为稳定业务契约。

### 3.3 Clip

当前 Clip 类型包括：

- Animation：`PlayAnimation`；
- Audio：`PlayAudio`；
- Effect：`PlayParticle`；
- Transform：`MoveBy`、`MoveTo`、`RotateTo`、`ScaleTo`；
- GameObject：`VisibleTo`；
- Event：`TriggerEvent`、`TriggerLog`、`TriggerShake`。

`TriggerEvent` 是 `ClipSignal` 数据类型，`IsValid` 只检查 `eventName > 0`。它不会自行发布事件或执行运行时副作用。

## 四、导出链路

logic JSON 的真实导出器是 third-party ActionEditor 包中的 `LogicJsonExporter`，不是 ActionEditorImpl。

导出流程为：

1. 编辑器读取 ActionEditor `Asset`；
2. `LogicJsonExporter` 转换为 `SkillAssetDto`；
3. Group、Track 和 Clip 的完整类型名写入 DTO；
4. 只有 Clip 实现 `ILogicJsonExportable` 时，导出器才调用 `FillLogicArgs()` 写入参数；
5. 最终写出 `<name>.logic.json`。

当前搜索确认 `PlayAnimation` 和 `TriggerLog` 实现 `ILogicJsonExportable`。其他 Clip 即使会出现在结构 DTO 中，也可能没有业务参数进入 logic JSON。消费者必须按 Clip 类型建立导出支持矩阵，不能假定所有公开字段都会被序列化。

导出成功只证明 authoring 数据被转换为 DTO，不证明运行时识别该类型、能加载资源或完成副作用。

## 五、DTO 与基础运行时

ActionSchema 包拥有 `SkillAssetDto`、JSON Loader 和基础 `TimelinePlayer`。当前基础 Player：

- 按 deltaTime 推进时间；
- 通过 group、track、type、start、length 拼接一次性触发键；
- `Reset()` 清空已触发集合；
- 只把类型名以 `.TriggerLog` 结尾或等于 `AbilityKit.ActionEditorImpl.TriggerLog` 的 Clip 识别为可触发日志。

因此基础 Player 当前不执行 `PlayAnimation`、`PlayAudio`、`PlayParticle`、Transform、Visible、`TriggerEvent` 或 `TriggerShake` 的完整语义。即使 `PlayAnimation` 可导出参数，也不能据此宣称基础 Player 会播放动画。

字符串拼接键还意味着两个 Clip 若关键字段完全相同，可能共享一次性身份。Clip 稳定 id、重复 Clip 和重排语义需要通过测试进一步约束。

## 六、MOBA 运行链

MOBA Runtime 提供独立 `MobaTimelinePlayer`、`MobaClipHandlerRegistry` 和技能流水线阶段。该链消费 `SkillAssetDto`，而不是直接消费 Editor `SkillAsset`。

典型数据流为：

```text
ActionEditor SkillAsset
  -> LogicJsonExporter
  -> *.logic.json
  -> ActionTimelineJson / SkillAssetDto
  -> MobaTimelinePlayer + Clip Handler Registry
  -> IMobaTimelineEventSink / 业务副作用
```

MOBA 链的可执行范围由其 Handler Registry 决定，不由 ActionEditorImpl 的类型列表自动决定。新增 Clip 至少需要同步评估：

- Editor authoring 类型；
- logic 参数导出；
- DTO 兼容性；
- MOBA Handler 注册；
- 资源和业务上下文；
- 自动测试与回放确定性。

## 七、兼容性与所有权

Clip 和 Track 的完整类型名会进入 logic JSON，因此 namespace、类名和程序集变更都可能破坏消费方识别。重命名时需要：

- schema 版本或旧类型别名；
- 已有 JSON 迁移；
- Editor 资产兼容策略；
- 运行时 Handler 的双读窗口；
- 回滚可用的旧产物。

ActionEditorImpl 应只拥有 authoring 类型及其导出参数，不应反向依赖具体 MOBA Runtime。运行时通过 DTO 和类型字符串建立兼容边界。

## 八、失败矩阵

| 场景 | 当前行为或风险 | 所有者 |
|---|---|---|
| Clip 未实现 `ILogicJsonExportable` | 结构可导出但参数可能为空 | ActionEditorImpl |
| 导出路径或写入失败 | logic JSON 无法生成 | third-party ActionEditor |
| 类型名重命名 | 旧 JSON 或 Handler 匹配失效 | schema/发布治理 |
| 基础 Player 收到非 TriggerLog | 当前不执行副作用 | ActionSchema |
| MOBA 缺少 Handler | Clip 不形成业务行为 | Moba Runtime |
| `TriggerEvent.eventName <= 0` | Clip 无效 | authoring 校验 |
| 重复关键字段 Clip | 基础 Player 触发键可能冲突 | ActionSchema |
| 资源不存在 | 本包不负责加载和回退 | 具体运行时 |
| Editor 可预览 | 不能证明逻辑服或回放一致 | 测试与发布治理 |

## 九、采用证据与成熟度

已确认：

- third-party ActionEditor 导出 `SkillAssetDto`；
- ActionSchema 能加载 logic JSON，并有只支持 TriggerLog 的基础 Player；
- MOBA Runtime 的 `AbilityTimelinePhase`、`SkillPipelineBuilder` 和 `MobaTimelinePlayer` 消费 `SkillAssetDto`。

这形成跨包 E2 采用证据，但不代表所有 ActionEditorImpl Clip 已在业务主链执行，也不代表 Editor 预览与运行时语义一致。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Asset、Track 和 Clip 类型源码存在 |
| E1 | 已具备 | third-party ActionEditor 可编辑并导出结构 |
| E2 | 局部具备 | ActionSchema 和 MOBA Runtime 消费 DTO |
| E3 | 未确认 | 未确认所有 Clip 的 authoring-to-runtime 契约测试 |
| E4 | 未确认 | 未确认完整导出、加载、播放和回放 artifact |
| E5 | 未具备 | 类型版本、兼容迁移和发布门禁未闭合 |

## 十、源码阅读路径

1. [SkillAsset.cs](../Runtime/ActionEditorImpl/Runtime/SkillAsset.cs)：authoring 资产类型；
2. [ActionTrack.cs](../Runtime/ActionEditorImpl/Runtime/Directables/Tracks/ActionTrack.cs)：Track 元数据和示例字段；
3. [TriggerEvent.cs](../Runtime/ActionEditorImpl/Runtime/Directables/Clips/Event/TriggerEvent.cs)：Signal 数据与最小校验；
4. [TriggerLog.cs](../Runtime/ActionEditorImpl/Runtime/Directables/Clips/Event/TriggerLog.cs)：可导出的日志 Clip；
5. [PlayAnimation.cs](../Runtime/ActionEditorImpl/Runtime/Directables/Clips/Animation/PlayAnimation.cs)：可导出的动画 Clip；
6. [LogicJsonExporter.cs](../../com.abilitykit.thirdparty.actioneditor/ActionEditor/Editor/LogicJsonExporter.cs)：真实 DTO 导出桥；
7. [ActionTimelineDtos.cs](../../com.abilitykit.actionschema/Runtime/Ability/Share/ActionSchema/ActionTimelineDtos.cs)：DTO 契约；
8. [ActionTimelineRuntime.cs](../../com.abilitykit.actionschema/Runtime/Ability/Share/ActionSchema/ActionTimelineRuntime.cs)：基础 Player 支持范围；
9. [MobaTimelinePlayer.cs](../../com.abilitykit.demo.moba.runtime/Runtime/Domain/ActionTimeline/MobaTimelinePlayer.cs)：MOBA 专用执行链；
10. [AbilityTimelinePhase.cs](../../com.abilitykit.demo.moba.runtime/Runtime/Domain/Ability/Pipeline/Timeline/AbilityTimelinePhase.cs)：技能流水线消费者。

## 十一、后续治理顺序

1. 建立 Track/Clip 的 authoring、导出、基础播放和 MOBA Handler 支持矩阵；
2. 清理 `ActionTrack` 的测试字段，或将其显式版本化；
3. 为所有需要运行时参数的 Clip 实现并测试 logic 导出；
4. 为类型重命名建立 schema 版本、别名和迁移工具；
5. 为基础 Player 的重复键、Reset、边界时间和未知类型补测试；
6. 建立 SkillAsset 到 logic JSON、DTO、MOBA Player 的端到端 artifact；
7. 将兼容检查和支持矩阵接入发布门禁后，再升级 E4-E5。

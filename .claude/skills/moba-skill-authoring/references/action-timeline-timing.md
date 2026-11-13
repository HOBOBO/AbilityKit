# ActionEditor 时长与 Cast 接线检查

用于新英雄动作接入、动作提前切待机、延迟动作丢失、500ms 占位时长，以及修改动作素材/编排后的跨英雄检查。
沿用主 skill 的机制准入与安全发布规则；本流程不授权新增生命周期机制。

## 时长如何确定

ActionEditor 源顶层 `length` 表示该时间轴从 0 到结束的完整跨度。审阅该值是否覆盖所有本阶段负责的片段：

```text
片段结束 = 片段 start + 片段 length
所有本阶段片段的最晚结束 <= 时间轴总 length
逻辑导出 length == 表现导出 length == 源时间轴 length
引用该逻辑导出的 Timeline.DurationMs = 编译器对 length 的毫秒量化
```

例如动画在 0.4s 开始，时间轴片段占用 0.8s，本阶段至少需要覆盖到 1.2s；若作者另留 0.2s 尾段，导出总长是 1.4s。
并行片段取最晚结束，串行片段按各自已编排的起止时间；不能简单累计并行素材长度，也不能忽略开头空白。
有意保留的空白属于编排，不能把总长自动裁剪成最晚片段结束。

```mermaid
flowchart LR
    S[ActionEditor 源\n总长与片段起止] --> E[逻辑与表现导出\n保留相同总长]
    E --> B[Flow 阶段\n显式引用逻辑导出]
    B --> C[编译 DurationMs\n执行对应阶段]
    C --> H[核对 HFSM 退出\n与实际动作采样]
    M[模型动作目录\n素材帧数与时长] --> S
```

模型动作目录是编排时的参考资料；箭头不表示现有工具会自动把目录数据写入时间轴。

片段 `length` 是时间轴上的占用，不总等于素材长度：需核对裁剪、clipOffset、playbackSpeed、Controller speed、重复和循环。
blend 通常在片段占用内，不能无依据再加一遍。持续循环/架枪等长期模式由 HFSM/Modifier 管理，不能为了“播完”制造无限 Cast。
投射物飞行、区域存留和命中后特效若有独立所有者，不把它们的完整寿命自动并入施法时长。

## 当前代码的实际契约

以下路径以 `Unity/Packages/` 为前缀：

| 环节 | 文件与当前行为 |
|---|---|
| 源编辑 | `com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_*.json`；顶层 length 与轨道中的 startTime/length |
| 导出 | `com.abilitykit.thirdparty.actioneditor/ActionEditor/Editor/LogicJsonExporter.cs`：取 asset.Length，逐片段导出 StartTime 与 Length |
| 分流 | `com.abilitykit.actionschema/Runtime/Ability/Share/ActionSchema/ActionTimelineRuntime.cs`：logic/presentation 分流都保留源总长及片段原始 start/length，不按各自轨道重新裁剪总长 |
| Flow 引用 | `com.abilitykit.demo.moba.editor/Editor/Moba/SkillFlowDefs.cs`：SkillTimelinePhaseDef.MobaLogicTimeline 非空才调用编译器；为空仍使用手填 Timeline |
| 编译 | `com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/MobaActionTimelineCompiler.cs`：DurationMs 来自 asset.length，事件 AtMs 来自 clip.start；毫秒按 AwayFromZero 四舍五入，不是 ceil |
| 发布 | `com.abilitykit.demo.moba.editor/Editor/Moba/MobaSkillPipelineExporter.cs`：由 SkillFlowCO 编译并发布 Unity/Console，不根据同名文件自动建立引用 |

现有 ActionTimelinePartition 校验会检查片段开始时间不超过总长，但**不会完整拦截 start + length 越过总长**。
所以“导出/编译通过”不能代替结束边界检查。修复或新增自动校验时区分有意裁剪和必须完整播放的片段。

## 检查顺序

1. 从技能表取得 CastFlowId/PreCastFlowId，追到 SkillFlowCO 的真实阶段树。不要假定 SkillId=FlowId，也不要看到同名 timeline 文件就认定已经引用。
2. 确认动作实际由谁播放：ActionEditor presentation、HFSM 状态 Action，还是具有独立生命周期的投射物/区域。模型动作目录仅提供资料，本身不说明动作从何时开始或何时结束。
3. 检查 ActionEditor 源的总长、启用轨道和每个 start/end。空 groups、固定占位总长、缺少实际动作的时间轴列为待核，不能推断已覆盖 HFSM 动画。仅效果时间轴不必强行复制 HFSM 动画轨道；应明确对应的阶段占用和起点，避免重复播放。
4. 比较源、logic、presentation 的总长；分流后开始时间与片段跨度应保持。逻辑轨道最后事件早于动画结束时，不能据此缩短逻辑导出的顶层 length。
5. 在 SkillFlowCO 确认目标阶段的 MobaLogicTimeline 指向正确 `.moba.logic.json`。字段为空时导出结果不会接管手填 DurationMs；修正既有引用需同时维护会重新导入 FlowCO 的 Showcase/同步入口，避免下一次同步覆盖。重新导入使用 `SkillFlowDef.FromDto(dto, previous)` 按稳定 PhaseId 保留显式引用，不按数组位置或同名文件猜测。
6. 比较该阶段编译后 DurationMs、事件 AtMs 与两端发布物。多阶段总时长需分别看 Sequence、Parallel、Delay、Repeat、条件等待等运行语义；某个 Timeline 的 length 不等于整个 Cast 总长，动态等待不能强行算成一个常数。
7. 对 HFSM 动作检查相对 Cast/阶段的起点、Action 游标和状态退出原因。缩短 Cast 仍需独立正常收尾时，再提出收尾机制方案；先排除空源、旧导出和未接通引用，不能直接加第二套计时掩盖源问题。

可先运行只读预检，查看源与两个导出的总长、延迟起点、最晚片段结束及占位警告：

```powershell
python -P .claude/skills/moba-skill-authoring/scripts/audit_action_timing.py --skill-id 30020101 --output local/Logs/action-timing-audit.json
# 不指定 --skill-id 时扫描全部标记 exportMobaRuntime 的源时间轴。
```

该脚本不读 Unity AssetDatabase、不确认 Flow 引用、不验证素材与 Controller，不修改源或发布物；无错误不代表动作完整播放通过。
源中的已知信号片段按 ClipSignal 的零时长读取；其他源片段缺少 length 时列为未知并提示复核，不把缺省值猜成零。
源跨度盘点不替代 Unity 对具体片段类型的反序列化与 Length getter，最终核对正式导出。
它报告的越界/空轨道需要按所有权和作者意图复核，不会自动改时长。

## 修复、发布与行为验收

优先修正 ActionEditor 源编排、总长、已存在的 MobaLogicTimeline 引用及会覆盖它的导入来源。
正常路径使用既有导出与 Flow 发布器；修改取消、并行冲突、状态/终态恢复或新增时长策略须另行确认，不能把本流程当成机制扩展许可。

批量重建可以使用 `MobaActionTimelineBatch.Apply -mobaActionTimelineManifest <审阅后的映射文件>`，
入口由 Unity `-executeMethod AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaActionTimelineBatch.Apply` 调用。
映射显式列源文件、Flow、已有阶段位置/稳定 ID 及对应 Showcase；正式动画事实与裁剪决定先记录在审阅报告，不能靠此工具自动选择有效动作区间。
该入口调用正式 ActionEditor 导出器，比较新旧事件（包括时间、效果、失败策略），检查源文件并发变化，再接引用和正常发布；不会创建新的 Cast 阶段或推断英雄身份规则。
`ExecuteEffect.abortOnFailure` 经批准映射到既有 Timeline 事件字段；缺省 false，迁移时必须保留原有 true，不能为了接通导出而丢失失败策略。

1. ActionEditor 保存/导出目标源，或运行 `NBC.ActionEditor.LogicJsonExporter.ExportAllMobaTimelines`；涉及其他英雄并行修改时优先只导出当前目标。
2. 发布并检查 Flow：`MobaSkillPipelineExporter.Apply`、`MobaSkillPipelineExporter.Check`；需要时按主 skill 补齐 HFSM/Trigger 发布。
3. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published`，比较 Unity/Console；不直接改生成 JSON。
4. 配置/编译验收之外，使用有图形设备的本地 HUD 场景，观察延迟前、开始帧、动作最后有效采样和 Cast/HFSM 退出。动画晚开始的场景必须覆盖，而不是仅测 start=0。
5. 验证一次输入只提交一次伤害/弹体/模式变化；延长阶段后检查新技能输入、普通攻击、移动、取消、控制和死亡，不能为了播完改变原有操作响应而不记录。
6. 连续同动作、不同动作衔接与回滚恢复分别验证 Action 身份、起点和游标。循环/独立对象在自己的退出路径清理。

观察器的等待上限应按已发布阶段时长、模拟 tick 与渲染采样频率留出余量，并采集实际 Cast 终态；固定等待若干渲染帧不等于经过同样数量的逻辑帧。尚处于 Running/Cast 的最后一条样本不能证明动作被提前切走。核对真实 Animator 的末段采样、单次 Action/Cast 身份及退出，不只断言曾进入动画状态。

独立技能逐项验收应在前一个 Cast 实际结束后提交下一技能，避免效果已经生效但 Cast 尚未结束时触发既有冲突拒绝。快速连按、取消和打断另设有明确预期的场景；不能为了独立技能用例通过而改共享冲突规则或放宽有效断言。

每个技能的检查记录至少包含：SkillId/FlowId/阶段、源时间轴、总 length、实际播放入口与起点、最晚结束、Flow 引用、发布 DurationMs、实际 Cast/Action 起止和中断结果。分别报告“资料一致”“引用接通”“真实播放完成”，不能用一种证据代替另一种。

## 已观察案例与后续方向

2026-11-12 检查时，金克丝多份时间轴是 groups=[]、length=0.5，且 Q Showcase 阶段仍手填 DurationMs=500；实际模型动作走 HFSM。
这些是定位线索，后续检查应读取当前源，不把历史值作为修复目标。
优先完善真实 ActionEditor 编排及导出接线，再判断是否需要另设正常收尾窗口。

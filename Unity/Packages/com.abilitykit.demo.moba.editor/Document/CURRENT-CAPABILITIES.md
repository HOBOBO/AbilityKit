# MOBA 战斗诊断当前能力与限制

> 状态日期：2026-08-09
>
> 本文是当前实现状态的唯一事实入口。设计目标请查阅架构设计，历史批次请查阅实施历史。

## 状态标记

| 标记 | 含义 |
| --- | --- |
| 可用 | 本地 Play Mode 已有 Producer/Store/Session Query，且存在可消费入口 |
| 条件可用 | 代码已接入，但依赖当前 World 服务、真实事件来源或运行模式 |
| 仅契约 | Core 已定义能力位或 DTO，但尚无完整用户工作流 |
| 未实现 | 设计中存在，当前没有可用实现 |

## Session 与查询

当前统一只读入口是 `IBattleDiagnosticReadOnlySession`。本地实现按实际注入的 Store 动态声明 capability，不应使用 `BattleDiagnosticCapabilities.AllLocal` 推断当前会话支持全部能力。

| Capability | 查询 | Revision | 状态 | Editor 消费入口 |
| --- | --- | --- | --- | --- |
| `WorldState` | `QueryWorld` | `StateStoreRevision` | 可用 | 诊断状态 |
| `ActorState` | `QueryActors` | `StateStoreRevision` | 可用 | 总览、诊断状态 |
| `Events` | `QueryEvents` | `EventStoreRevision` | 可用 | 诊断事件 |
| `Trace` | `QueryTrace` | `TraceStoreRevision` | 条件可用 | Trace |
| `ActorAttributes` | `QueryActorAttributes`、`QueryActorAttributeModifiers` | `ActorAttributeStoreRevision` | 可用 | 属性 |
| `ActorBuffs` | `QueryActorBuffs` | `ActorBuffStoreRevision` | 可用 | Buff |
| `ActorTags` | `QueryActorTags` | `ActorTagStoreRevision` | 可用 | 标签、总览 |
| `ActorEffects` | `QueryActorEffects` | `ActorEffectStoreRevision` | 可用 | 效果、总览 |
| `SkillRuntime` | 无正式查询 | 无 | 仅能力位 | 无 |
| `FreezeCapture` | 不属于只读 Session 查询 | 无统一公开查询 | 仅内部控制能力 | 无正式 UI |
| `Clear` | 不属于只读 Session 查询 | 各 Store 独立推进 | 仅内部控制能力 | 无正式 UI |
| `PinTrace` | 无正式控制工作流 | 无 | 未实现 | 无 |
| `Export` | Runtime 可生成不可变快照并映射到标准 JSON Artifact；Battle Debug 可将当前实时会话导出到磁盘 | 各轨道独立 revision | 条件可用 | 战斗调试工具栏“导出” |
| `SelfMetrics` | 无正式查询 | 无 | 未实现 | 无 |

`StoreRevision` 是 `EventStoreRevision` 的兼容别名。新代码应使用各数据面的独立 revision，避免无关 Store 更新导致重复查询。

## 状态快照语义

World、Actor、Attributes、Buffs、Tags 和 Effects 当前使用 latest-only 快照：

- 查询帧 `0` 表示最新快照。
- 指定帧仅在等于 Store 当前快照帧时可用。
- Store 尚未提交过快照时返回 `NotProduced`。
- Actor 不在当前快照，或请求的帧不是当前帧时返回 `NotCaptured`。
- Actor 存在但对应集合为空时返回正常 `Empty`，不等同于未采样。
- 各 Store revision 独立推进，不提供跨 Store 原子事务。

Overview 同时依赖 `ActorState | ActorTags | ActorEffects`，并以 Session Scope、三类 revision、ActorId 和 Frame 作为联合缓存键。某一项 capability 缺失时，Overview 不回退读取 Runtime 对象。

## 不可变诊断快照与标准 Artifact

Runtime 当前提供独立于 Editor 查询面的内存快照源和本地快照协调器：

- 一次采集 Event、World/Actor、Trace、Attributes、Buffs、Tags 和 Effects，生成平台无关的只读 Session Snapshot。
- Event 轨道一次复制完整 Ring 保留区，不受查询页最大 500 条和 retained read view 数量限制；默认 20,000 条容量可完整复制。
- 每条轨道保留自己的 revision；latest-only 轨道保留采样 frame，并显式报告是否与 State frame 对齐。
- Trace 导出通过导出前后 revision 校验进行有限重试；无法取得稳定导出时以 `IsStable = false` 明示，不伪装为一致结果。
- 各 Store 在单次快照调用内执行防御性复制；后续写入、淘汰或 Clear 不改变已经生成的快照。
- 快照源使用独立窄接口，`IBattleDiagnosticReadOnlySession` 和 Editor 面板不因此获得整批导出或可变控制权限。

协调器按轨道顺序复制，不暂停采集，也不承诺跨 Store 原子事务。标准文件层复用顶层 `abilitykit-analysis.v1`，以可选 `battleDiagnostics` Section 承载独立版本 `abilitykit-battle-diagnostics.v1`；旧 Artifact 缺失该 Section 时仍可正常导入。Codec 显式映射全部八条轨道和已知结构化 Event Payload，并校验顶层版本、Section 版本、必需轨道、Event Metrics/Sequence、Actor 数量和领域构造约束。

`BattleDiagnosticOfflineSession` 将导入快照适配为 `IBattleDiagnosticReadOnlySession`，固定呈现为 `Disconnected` / `Frozen`，保留各轨道独立 revision、Event 固定 revision 分页、latest-only 状态查询和 Trace Partial 语义。Battle Debug 工具栏可在活动实时会话中捕获并导出标准 JSON，也可在 Play Mode 或 Edit Mode 打开文件并离线浏览；实时与离线来源互斥，损坏的新文件不会替换当前离线现场，“返回实时”会释放离线 Session。

## 录像驱动逻辑世界

Battle Debug 在本地 Play Mode 提供嵌入式录像控制区。当前 MVP 不是静态解析录像 DTO，而是复用活动战斗的完整 `BattleStartPlan`，将当前 `BattleLogicSession` 热切换为本地 Lockstep Replay，再由标准录像输入驱动真实逻辑世界：

- 支持加载录像、连续播放、暂停、单帧前进、单帧后退、进度 Slider 跳转和 `0.1x` 至 `8x` 播放速度。
- 加载入口提供“渲染表现”开关；关闭后 Detach `BattleHudFeature` 和 `BattleViewFeature`，不创建或驱动主 HUD、View、VFX 和相机，但保留完整逻辑 Session、World 和 Diagnostics。
- 暂停会冻结逻辑 Tick；到达录像末帧后自动暂停。
- 向前 Seek 逐帧提交录像输入并推进 Session。纯逻辑模式的向后 Seek 可优先使用 Rollback；渲染模式禁止只回滚逻辑，先按 HUD、View 顺序 Detach 表现，再重建 Session 并从头确定性重放到目标帧，最后按 View、HUD 顺序恢复表现，避免旧实体、特效、快照订阅或插值状态残留。
- Replay Session 继续注册为当前 Facade 和 Diagnostics Session，既有 Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 面板查询当前重演世界；纯逻辑模式也可在暂停或 Seek 到目标帧后执行这些查询。
- 成功加载录像时退出 Artifact 离线模式；Replay 与 Artifact 不混合为同一数据源。

当前录像文件不包含完整 Map、玩家 Loadout 和 LaunchSpec，因此加载录像要求 Play Mode 中已经存在活动 `BattleLogicSession`，并复用该 Session 的完整启动计划。Replay 与原实时 Session 不并行存在；加载会重启当前 Session。Artifact 仍是可在 Edit Mode 打开的单快照离线现场，不因 Replay 能力而获得历史 Timeline。

## Event Store

事件 Ring Store 当前具备：

- 固定容量和严格递增 Sequence。
- Scope 校验与有界淘汰。
- 按 Actor、Channel、结果和文本组合过滤；文本搜索覆盖结构化技能失败的 Code、Message 和 Slot。
- 基于 Store revision 的一致分页。
- 已丢弃 revision 的 `Evicted` 语义。
- Freeze、Clear 和采集通道开关的内部控制端口。
- 不可变查询结果和版本化强类型 Payload；当前正式覆盖同步状态哈希、触发分析和技能失败。

事件面板以每页 200 条的调查工作集展示结果，并将失败事件投影为案例导向的调查入口：

- 正式消费 Workspace 共享 Filter，并与 Events 局部调查条件合并。Channel 取交集；共享 Actor、Config、Correlation、Search 和 Trigger 标量条件存在时优先；局部条件只补共享未设置字段；`FailuresOnly` 取 OR；共享 Frame 与 `UnfinishedOnly` 保留。
- 面板持续显示共享与局部条件来源，提供“清除局部”“设为共享”和“清除共享”三个独立入口。`RecentFrameCount` 只是 Events 当前调查工作集窗口，不属于共享 Filter；提升可共享条件时会清除该局部窗口并明确提示。
- 首屏绑定查询时的 Event Store revision；“加载更多”只从同一固定 revision 和有效 Filter 追加更早结果，不把新的 live 数据混入已有工作集。
- 共享或局部筛选条件、或者 live revision 变化时重建首屏；固定 revision 被 Store 淘汰时保留已经加载的数据、停止继续分页并显示明确状态。
- 只有共享可靠 Root Context 的失败才合并；无 Root Trace 的失败按事件独立保留。
- 案例显式标注 `Confirmed`、`Inferred` 或 `InsufficientEvidence`，支持按置信度和根因组合过滤；增量加载后按稳定案例 Key 保持当前选择。
- 支持前后案例导航、直接选择证据事件、聚焦证据关联链、选择来源 Actor、打开 Trace 和复制证据摘要。
- 结构化技能失败按稳定 Code/Source/Stage 形成问题簇，不依赖 Summary 文案；问题簇统计整个已加载工作集的次数、首次帧、最近帧和跨度。
- 事件详情展示信封字段、Summary 和已知结构化 Payload，并可从单个事件继续关联调查或打开 Trace。

完整跨帧 Timeline 和 Bookmark 尚未落地；诊断快照导出由 Battle Debug 顶部 Artifact 工作流提供。

## Trace Store

Trace 面板按 Root Context ID 查询真实 Trace Registry 导出的树预序快照；既可手工输入根，也可由事件详情自动打开根并定位事件 Context。面板提供：

- 节点层级、状态、Actor、Config、起止帧和结束原因。
- 选中节点从根到当前节点的父链路径。
- 按 Kind、状态、结束原因、Context、Actor 和 Config 的不区分大小写搜索；结果保留命中节点的祖先路径。
- 在直接搜索命中之间循环前后导航，不把上下文祖先当作命中；程序化选择会将目标滚动回树视口。
- 分支折叠、保留当前选中父链的批量折叠和全部展开；搜索期间临时展开命中路径，清除搜索后恢复既有折叠状态。
- 当前树内的临时节点 Pin、返回 Pin 和 Pin 节点淘汰提示。
- 缺失 Parent 的孤儿节点标记，以及异常父链的环路防护。
- `Unsupported`、`NotProduced`、`Evicted` 和截断结果的显式状态提示。
- 以 Session Scope、`TraceStoreRevision` 和 Root Context ID 组成的查询缓存键。

Trace 数据仍受 Runtime Store 保留范围约束。Editor 临时 Pin 只是当前面板内的导航状态，不是 Session `PinTrace` 控制能力；面板仍不提供持久化 Pin、导出或修改 Trace 的能力。

## Producer 覆盖

| 事件来源 | 状态 | 说明 |
| --- | --- | --- |
| 技能生命周期 | 可用 | 开始、完成、失败、打断；输入、准备、战斗规则、Runner 与 Pipeline 失败额外保留 Slot、Source、Stage、Code、Message |
| Damage | 可用 | 管线最终伤害与直接伤害 |
| Heal | 可用 | 直接治疗 |
| Buff 生命周期 | 可用 | 添加与移除 |
| Projectile 生命周期 | 可用 | 生成、命中、结束 |
| Summon 生命周期 | 可用 | 生成与结束 |
| TraceNode 生命周期 | 可用 | 创建与结束 |
| Area 生命周期 | 可用 | 生成与结束 |
| Effect 执行生命周期 | 可用 | 开始与结束 |
| Warning/Exception | 可用 | 受现有限流策略约束 |
| 同步状态哈希快照 | 条件可用 | 仅采集真实收到的权威状态哈希 |
| Snapshot Gap | 未实现 | 同步层尚未暴露可靠事实来源 |
| Rollback/Replay 完成 | 未实现 | 不从空对账报告推断事件 |
| Full Snapshot 请求/应用 | 未实现 | 等待同步控制器正式事件 |

## SkillFlow Editor 与运行时诊断边界

`SkillFlowSO.dataList` 已提供与正式运行时 DTO 同构的递归树形编辑模型：支持 `RulePlan`、`Timeline`、`Sequence`、`Parallel`、`Repeat`、`Delay` 和 `WaitUntil`，并映射稳定 `PhaseId` 与 `PipelineContinuousTagTemplateId`。废弃的 `Checks` 编辑类型只为已有 SerializeReference 资产迁移保留，不再进入新增菜单；`Handlers` 不提供新的编辑类型。

Battle Debug 已建立只读配置源索引，通过类型化的 `BattleDebugConfigReference` 将运行时 ID 映射到权威 JSON，而不让诊断面板持有可变配置对象。索引使用结构化 JSON 解析和行号信息定位准确条目，支持普通 `Id` 数组表、`ability_trigger_plans.json` 的 `Triggers/TriggerId` 结构，以及 `skill_flows.json` 内按 `Id + PhaseId` 递归定位嵌套 Phase；缺失 ID 或 Phase 时明确失败，不回退为模糊文本命中。

当前 `Open Config` 入口覆盖：

- Skill Runtime 的 Skill 配置。
- Event 中语义明确的 Skill、TriggerPlan、Effect、Buff、Projectile、Area 和 Summon 配置。
- Trace 中语义明确的 Skill、Effect、Buff、Projectile、Area、Summon 和 PresentationTemplate 配置。
- Actor Buff 和实时 Projectile/Area 对象配置。

权威配置源位于 `com.abilitykit.demo.moba.view.runtime/Resources/moba` 和 `Resources/ability`。普通配置跳转会选中并 Ping 对应 `TextAsset`，再调用 Unity 外部编辑器打开准确行；SkillFlow Trace 则在 JSON 精确索引校验通过后选中正式 `SkillFlowSO` 资产。`EffectAction` 的 `ConfigId` 是动作模块 ID；Damage/Heal Event 的 `ConfigId` 可能代表 Skill、Buff、Item 或环境原因参数，Damage Trace 的 ID 语义也不稳定。因此这些入口不会生成配置跳转，避免碰巧同号导致误定位。

运行中的 Cast Pipeline Phase 已通过正式 `PhaseStart/PhaseComplete/PhaseError` 事件生成独立 Trace 子节点，并把 `SkillId -> CastFlowId -> PhaseId` 写入只读 Diagnostics DTO 与离线 Artifact。Battle Debug 会把该节点映射为 `SkillFlow(CastFlowId, PhaseId)`，选中正式 `SkillFlowSO`，递归解析 Sequence/Parallel/Repeat 节点，自动展开序列化路径，并在 Inspector 与 Odin 节点位置显示 Trace 高亮。该链路不从 Summary、EndReason 或日志文本解析身份；PreCast 在正式启用独立 Flow 契约前保持技能级定位，不会错误复用 CastFlowId。

## Editor 工作区与面板边界

主窗口使用稳定 Actor ID 保存选择，实体列表重建、排序或过滤不会把选择静默切换到其他 Actor。实体工具栏显示可见/总实体数，支持清除过滤；左栏支持按 ID 跳转、在当前可见实体间循环前后选择和清除选择。面板分为 `Actor` 和 `Diagnostics` 两个工作区，二级面板通过下拉框选择；拥有大型列表或树的面板自行管理滚动，避免窗口外层滚动嵌套。实体栏支持拖拽调整宽度，栏宽、工作区和各工作区面板索引通过 EditorPrefs 持久化；Actor 选择属于会话状态，不跨窗口持久化。周期刷新仅在过滤后的 Actor ID 序列变化时替换实体列表快照，并可通过窗口“自动刷新”开关暂停轮询；该开关不等同于 Diagnostics Freeze，不停止采集，也不修改 Store。

Diagnostics 工作区维护与 Session Scope 绑定的稳定选择、可见 Frame Cursor 和有界导航历史，当前覆盖 Actor、Event、Trace Root 与 Trace Node。窗口顶部状态条显示当前帧、跟随最新状态、变更来源和稳定 Selection，并支持手工固定帧、恢复跟随最新；Event、Trace 节点和历史恢复会通过 Workspace State 更新帧游标。Replay 提供 Seek 端口时，游标定位可以进一步驱动真实 Replay；Live 和 Artifact 只维护诊断帧上下文，不伪装具备历史 Seek 能力。Event 历史恢复受当前查询工作集限制：目标 Sequence 不在已加载页时不会隐式扫描完整保留区。

窗口已接入持久 Inspector，Runtime 诊断投影以 Workspace Selection 为唯一事实源，支持 Actor、Event、Trace Root 和 Trace Node。Actor 按选择帧与 State Store revision 查询，frame 0 保持 latest-only 语义；Event 使用选择帧精确过滤和固定 Event Store revision，按最新优先分页恢复稳定 Sequence，单次最多扫描 4 页、2,000 条，超限明确显示 Partial/Truncated；Trace 使用 Selection 的 RelatedId 作为 Root Context，并按选中 Context ID 投影节点。查询结果按 Session Scope、Selection 和对应 Store revision 缓存；淘汰、未生产、未捕获、不支持、断开和查询失败均保留真实状态，不合成详情。

Config 已作为 Editor-only 持久投影接入同一 Inspector。点击 Event、Trace 或其他现有“打开配置”入口时，Inspector 保存完整 `BattleDebugConfigReference(Kind, Id, PhaseId)` 及来源 Workspace Selection，不修改只有数值字段的 Core Selection DTO，也不把 SkillFlow `PhaseId` 编码为数字。配置通过权威 JSON Source Index 解析，成功时持续显示 Kind、Id、PhaseId、源路径和行号，失败时持续显示源缺失、JSON、ID 或 Phase 错误；支持重新解析、复制引用和原有配置打开动作。来源 Workspace Selection 改变后 Config 投影自动清除，避免把旧配置误认为当前诊断选择；该投影不进入 Core Navigation History，也不依赖 Runtime Session 或 Store revision。

宽度不小于 960 时 Inspector 为可拖拽、可持久化宽度的右栏；窄窗口降级到主工作区下方，并可通过工具栏收起。Inspector 支持 Actor、Trace、Config 和帧导航以及 Event/Trace 复制；Skill Runtime 独立选择尚未接入。

Overview 在未选择 Actor 时也可显示数据源与 Health 摘要，包括连接/捕获状态、Event/State/Trace 独立 revision、最后状态帧、最后事件序列、冻结状态和有界最近错误。“最近失败”会打开 Events 的失败预设，“全部事件”会打开无 Actor 限制的全局事件视图，两者不再指向同一未筛选结果。

Session Resolver 显式区分 Offline、FacadeMissing、LogicSessionMissing、WorldMissing、ServicesMissing、DiagnosticSessionMissing 和 Connected。实时 Health 来自 scoped Runtime 健康只读端口；离线 Health 由 Artifact 已存在轨道保守投影，不把缺失轨道伪装为已生产。

Events、State、Trace、Attributes、Buffs、Tags 和 Effects 已使用同一空态投影语义，区分缺少选择、过滤无匹配、普通空结果、NotProduced、NotCaptured、Evicted、Truncated、Unsupported、Disconnected 和 Failed。Trace 使用 Root Context 作为选择主体；各 ViewModel 保留真实 QueryStatus，并在缓存失效时清除旧状态。Partial/Truncated 查询只要仍有可显示项就继续展示数据及状态，不用空态覆盖有效结果。

以下详情面板已经通过只读 Diagnostics Session 消费不可变 DTO：

- 总览
- 属性
- 标签
- 效果
- Buff
- 诊断事件
- Trace
- 诊断状态

实时模式下主窗口仍有活动对象依赖：

- 通过 `IBattleDebugFacade` 枚举实体。
- 通过活动 Facade 解析当前选择。
- 左侧实体列表通过 `IUnitFacade` 显示 Tag/Effect 简要计数。
- 帧同步系列面板使用既有帧同步调试接口，不属于上述 Actor Store DTO 化链路。

离线模式从 Artifact State Actor 快照建立实体列表，只向 DTO 面板注入离线只读 Session，不伪造 Runtime Unit；帧同步系列面板在离线模式隐藏。因此，当前准确表述是“主要诊断详情面板和离线文件浏览已与活动 Runtime 对象解耦”，而不是“整个 Battle Debug 实时链路完全 DTO 化”。

## 环境支持

| 环境 | 状态 | 限制 |
| --- | --- | --- |
| 本地 Unity Play Mode | 可用 | 实时模式必须存在活动 `BattleLogicSession` 和对应 World Services；已有活动 Session 时可加载录像并选择渲染表现或纯逻辑 Replay，也可切换到离线文件 |
| Unity Edit Mode 非运行状态 | 条件可用 | 可打开并浏览离线 Artifact；不能捕获实时快照，也不能启动 Replay 逻辑世界 |
| 远端权威服 | 未实现 | 没有连接、鉴权、协议和远端 Session Adapter 工作流 |
| 标准录像文件 | 条件可用 | Play Mode 中复用活动 Session 的完整启动计划，支持播放、暂停、单步、速度和按模式确定性 Seek；纯逻辑模式仍可查询 Diagnostics，但不能脱离启动上下文独立创建战斗 |
| 离线诊断文件 | 可用 | Battle Debug 支持打开标准 JSON、来源状态、离线 Actor 导航和返回实时；Artifact 自身为单快照，不提供历史 Timeline |
| Web/CLI 客户端 | 仅契约基础 | Core 可复用，但没有产品化客户端 |

## 已知限制

- Local Session 的 Scope 默认仍可能使用临时本地身份，稳定外部 Session/World/Epoch 配置入口尚未完整产品化。
- Actor 状态类 Store 不保留历史帧；历史查看依赖录像确定性重演，不能从单个 Store 或 Artifact 直接查询任意过去帧。
- Replay 当前要求活动战斗提供完整 `BattleStartPlan`，尚不能仅凭录像文件在 Edit Mode 或空场景中独立创建逻辑世界，也未实现 Live 与 Replay 并行对照；“渲染表现”是当前 Replay 控制器的运行时选择，尚未写入启动计划或录像文件。
- 强类型 Event Payload 当前正式覆盖同步状态哈希、触发分析和技能失败；其他事件仍主要依赖稳定信封字段和 Summary。
- 调查工作集仍是当前筛选下的显式增量读取，不是完整保留区的后台索引；不提供趋势图、跨 Artifact 问题合并或 Bookmark。
- Workspace Filter 已正式驱动 Events 查询、缓存和固定 revision 分页，并与 Events 局部调查条件区分来源和清除语义；State、Trace 和 Actor 详情尚未消费其不适用或待设计字段，因此还不是跨全部面板的统一可见 Filter。统一 Frame Cursor、Actor/Event/Trace 持久 Inspector 和 Editor-only Config 持久投影已形成可见闭环，但 Timeline、Skill Runtime 独立选择和跨面板 Filter 仍需继续收敛；Config 尚未进入 Core Navigation History，清除 Actor 选择、Artifact 自动选择和超出当前 Event 工作集的历史恢复也仍需继续收敛。
- Freeze、Clear 和通道控制存在内部端口，但没有完整面板操作与权限模型。
- Battle Debug 已提供实时快照磁盘导出、离线 Artifact 导入和 Play Mode 录像重演，但尚无文件大小限制、来源信任策略、自监控指标或远端能力；Editor 当前树内的临时导航 Pin 不跨文件持久化。
- Replay 控制会重启、推进或回滚当前逻辑 Session；除该显式工作流外，Diagnostics 查询面板仍保持只读，不建立任意可变 Runtime 旁路。

## 更新规则

当能力发生变化时，应在同一变更中更新本文：

1. 修改 capability、查询、Store 或 Session 路由时，更新“Session 与查询”。
2. 新增或移除 Producer 时，更新“Producer 覆盖”。
3. 新增面板或迁移数据边界时，更新“Editor 面板边界”。
4. 接入远端、离线、导出或历史状态时，更新“环境支持”和“已知限制”。
5. 将状态日期更新为验证变更的日期。

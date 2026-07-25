# Battle Debug 可用性优化计划

> 制定日期：2026-07-22
>
> 范围：将 Battle Debug 从“存在多个查询面板”提升为本地 Unity Play Mode 中可完成日常战斗、技能、状态和 Replay 排查的工具。
>
> 本文是后续优化批次的执行基线，不描述已经完成的能力；现状以 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md) 为准。

## 1. 问题定义

当前 Runtime 已存在 Event Collector、状态采样器、Local Diagnostic Session、Actor Store、Trace Store 和多类 Producer；Battle Debug 也已有 Actor、Attributes、Buffs、Tags、Effects、Events、Trace、State 与 FrameSync 面板。

但实际使用体验仍不可接受：

- 实体栏主要显示 Actor ID 与 Tag/Effect 计数，不能快速判断对象身份、阵营、生命或当前状态。
- 实时 Diagnostics Session 由各面板临时解析；窗口没有统一报告 Facade、Logic Session、World、Services、Local Session 任一环节的失效原因。
- 采样、采集、可选服务注入和面板构造多处静默吞异常；事件或状态为空时用户无法判断是尚未采样、Producer 未触达、通道关闭、采集冻结、Actor 过滤排空，还是查询故障。
- Actor 与 Diagnostics 由工作区和下拉框两级隐藏；一次只能看一个面板，无法低成本关联“技能事件 -> Trace -> 受影响 Actor 状态”。
- 诊断事件默认按选中 Actor 过滤，事件关联字段不完整或用户切换 Actor 时会把实际事件隐藏；空结果只显示“无事件”。
- World Summary 的活动技能运行时和 Trace Root 数当前未真实采样，固定为 0，降低用户对诊断数据的信任。

优化目标不是增加更多静态字段，而是让用户能够在一次技能释放后，在同一个窗口确认数据是否进入系统、事件发生了什么、因果 Trace 在哪里、Actor 状态改变了什么，以及空数据应如何处理。

## 2. 产品目标与完成定义

### 2.1 首要用户工作流

1. 打开 Battle Debug 后，用户在 5 秒内能够看出数据源是实时、Replay、离线还是未连接，并知道未连接的具体断点。
2. 本地战斗开始后，用户在不输入 Actor ID 的情况下可从实体列表识别己方/敌方、名称或类型、生命状态与存活状态，并选中目标。
3. 释放一次可成功执行的技能后，用户可在默认视图看到 Skill 生命周期事件；选择事件后可打开对应 Trace，并可跳转到来源或目标 Actor。
4. 释放失败、无事件或无状态时，用户能从健康视图区分“未安装/未解析”“未采样”“Producer 未产出”“过滤无匹配”“通道关闭”“Frozen”“查询失败”，而不是只看到空列表。
5. 加载纯逻辑 Replay 并跳到目标帧后，用户可复用同一工作流检查该帧的 Actor、事件、Trace 和状态；不能假装具备 latest-only Store 不支持的历史快照能力。

### 2.2 非目标

- 本计划不在 P0/P1 引入远端诊断协议、鉴权或 Remote Session Adapter。
- 不把 latest-only State Store 改造成无限历史数据库；历史状态继续通过 Replay 确定性重演获得。
- 不允许 Editor 面板绕过 `IBattleDiagnosticReadOnlySession` 直接写入 Store 或战斗 Runtime。
- 不以吞掉异常保持主循环稳定为代价丢失健康证据；错误记录必须有界、低频且不携带大对象引用。
- 不承诺跨 Store 原子快照或事件/状态严格同帧一致性，必须继续以各轨道 revision 与 frame 明示边界。

### 2.3 发布门槛

P1 完成后，本地 Play Mode 必须满足：

- 健康面板能显示 Session 解析路径、capability、各 Store revision、最后采样帧、Event sequence、通道和冻结状态。
- 成功施放一个项目内基准技能时，Event revision 增长且能看到至少开始与结束事件；若对应技能路径没有 Trace，也必须明确显示“事件无 Root Context”，不显示空白。
- Actor 详情首页在不依赖 Runtime `IUnitFacade` 的情况下展示可用的状态、属性/资源摘要和最近相关事件。
- Events、Trace 与 Actor 之间至少具备事件 -> Trace、事件 -> Actor、Trace 节点 -> Actor 的单向或双向导航闭环。
- 全部新增空态按状态码区分，并提供可执行的排查提示。
- 聚焦 EditMode 测试、范围化构建和至少一次 Play Mode 手工验收有明确结果记录。

## 3. 设计原则

1. 先证明数据链路健康，再优化展示。没有 Session、Store revision 或 Producer 增长证据时，不以“无事件”作为结论。
2. 一个窗口只维护一份当前数据源和 Diagnostics Session 解析结果。面板读取同一不可变上下文，避免实时与离线逻辑分叉。
3. 对用户默认展示全局事实，再允许缩小到选中 Actor、当前 Trace、失败或指定 Channel。过滤必须可见、可一键清除。
4. 首屏服务高频排查，深度字段放进 Inspector。Actor、事件和 Trace 应通过稳定 ID 关联，而不依赖临时对象引用。
5. 采集稳定性和可观测性分离。采集异常仍不能中断战斗 Tick，但健康服务必须记录有界的失败计数、错误码、最后错误时间和上下文。
6. 性能按 revision 驱动。刷新只在 Session 身份、Store revision、选择或过滤改变时查询；大列表保持上限、分页或虚拟化，不在每次 `OnGUI` 全量分配。

## 4. 分阶段实施计划

### P0：诊断链路自检与数据打通

目标：将“Diagnostics 下没有东西”变成可定位的状态，并在真实技能路径中验证 Event/State/Trace revision 是否增长。

Runtime：

- 新增只读 Runtime 健康 DTO 和 `IBattleDiagnosticHealthReadStore`，包含 Session Scope、采集状态、capability、各 Store revision、最新 State frame、Event last sequence、EnabledChannels、Frozen、采样成功/失败计数、采集成功/拒绝/失败计数、最近错误码和时间戳。
- 将 `BattleDebugDiagnosticSessionResolver` 扩展为有阶段结果的解析器，至少区分 Offline、FacadeMissing、LogicSessionMissing、WorldMissing、ServicesMissing、DiagnosticSessionMissing、Connected；保留现有布尔入口作为兼容包装或统一迁移调用方。
- 在 `MobaDiagnosticStateSampleSystem`、`MobaBattleDiagnosticStateSampler`、`MobaBattleDiagnosticEventCollector` 和技能生命周期采集边界保留主循环隔离，但将吞掉的异常归档到健康服务。错误信息应归类、限频、截断，不能每帧写 Unity Console。
- 明确 Collector 对 draft 的拒绝原因：Frozen、ChannelDisabled、InvalidDraft、StoreRejected、Exception。Producer 调用只记录聚合计数，不把高频正常路径写日志。
- 修正 World Summary：从真实技能 Runtime/Trace Registry 获取活动数量；若依赖不存在，Health 中标记 unavailable，不能固定填 0 伪装为事实。
- 以真实本地 World 解析验证 `IBattleDiagnosticReadOnlySession`、`IMobaBattleDiagnosticEventSink`、`IMobaBattleDiagnosticCaptureControl`、Sampler 与各 Actor Store 的 Scope 一致性。

Editor：

- Battle Debug 窗口在构造 `BattleDebugContext` 前统一解析实时或离线 Session，并把解析结果、Health Snapshot 和数据源身份注入上下文。
- 顶部来源状态升级为紧凑健康条：来源、World、Session、连接阶段、Capture 状态、capabilities、Event/State/Trace revision 与最后帧；异常时显示精确失败阶段及建议动作。
- 新增固定的“健康”入口，不依赖反射面板发现。显示通道、Frozen、上次成功采样、计数器、最近错误和缺失服务；提供只读复制诊断摘要操作。
- `BattleDebugPanelRegistry` 记录发现/构造失败的 Panel 类型和异常摘要，并在健康页展示；不能再静默忽略。
- Events 空态按 Health 和查询结果显示“尚未产生事件”“当前 Actor 过滤无匹配”“Skill 通道关闭”“采集已冻结”“Producer 调用失败”“查询失败”，并提供“清除 Actor 过滤/查看全部事件”等局部操作。

验收：

- 无活动 Session、缺少 Local Session、无 State 采样、Frozen、关闭 Skill Channel、选中 Actor 过滤无匹配、Producer 抛错均产生不同健康状态和可读提示。
- 成功技能的 Event Store revision 和 LastSequence 增长；状态采样后 State revision 和 Snapshot frame 增长；有 Trace 时 Trace revision 增长。
- P0 不改变现有业务事件语义，只补健康证据、失败分类与真实活动数量。

### P1：技能与 Actor 核心调试工作流

目标：让一名玩法开发能从“选中目标”或“释放技能”出发完成第一轮根因定位。

信息架构：

- 将现有“Actor / Diagnostics + 下拉框”的两级隐藏改为稳定一级主 Tab：Overview、Actor、Events、Trace、Network、Health。Actor 内部再使用紧凑子 Tab 或可折叠区承载 Attributes、Buffs、Tags、Effects。
- 保持选中 Actor、选中 Event、选中 Trace Root/Node 为窗口状态；跨 Tab 切换不得丢失关联上下文。
- 首屏 Overview 使用主从布局：左侧实体列表，中间 Actor Summary/状态卡，右侧 Recent Events 或 Event Inspector。窄窗口自动降为纵向布局，避免工具栏和关键字段截断。

实体与 Actor：

- 实体列表从诊断 Actor 快照投影名称、Kind、Team、HP、存活/失效状态、Tag/Effect/Buff 计数；实时 Unit 只作为可选增强，离线与 Replay 必须得到一致的 DTO 视图。
- Actor Summary 至少展示 ID、名称、类型、队伍、当前 HP、位置或不可用状态、Tag/Effect/Buff 摘要、最近相关 Event、当前/最近 Root Context。
- Attributes 支持搜索、仅变化项、Base/Final/差值、来源与优先级排序；Buff/Tag/Effect 支持按名称搜索和状态分组。空集合继续是正常 Empty，但必须显示采样帧和 Store 状态。
- State 页面显示 World、Actor 列表、最近采样帧、采样延迟/过期提示和状态 revision；不是只有一组静态摘要。

事件与 Trace：

- Events 默认显示全部 Channel，明确显示当前过滤条件；选择 Actor 后由用户主动启用“仅该 Actor”，并可一键恢复全部。
- 增加 Channel 快速过滤：Skill、Damage/Heal、Buff、Effect、Projectile、Summon、Warning/Exception、All；默认保留 Skill 和 Failure 的高信号入口。
- 事件行显示 Frame、Sequence、Channel、Kind、Outcome、Source/Target、Skill/Config、Summary；详情展示所有可用关联 ID 和强类型 Payload。
- Event Inspector 支持打开 Trace、选择 Source/Target Actor、按 Root Context 查看相关事件。没有 Root Context 时明确提示。
- Trace 页面由事件或 Actor 最近 Root 自动带入；显示根摘要、节点树、状态、帧区间、结束原因和关联 Actor，并支持节点 -> Actor 跳转。

验收：

- 玩家在 3 次以内点击完成“从技能事件打开 Trace，再定位受击 Actor”。
- 选择一个有 Buff、Tag、Effect 的 Actor 时，不需要切换多个下拉面板即可看到其核心摘要和最近事件。
- 默认 Events 不会因残留 Actor 选择而错误显示空列表；所有 active filter 均可见。

### P2：Replay、时间关联与生产效率

目标：让实时问题可迁移到 Replay，在目标帧附近高效比较因果与状态。

- 在 Event、Trace 节点和 Actor 状态显示可跳转的 Replay frame；仅在活动 Replay Session 时启用，Live/Artifact 必须显示不支持原因。
- 提供前后帧状态对比：HP、Buff、Tag、Effect、关键 Attribute 的新增/移除/变化。对 latest-only Store 通过 Seek 后重新采样取得两端，不伪造持续历史。
- 增加 Bookmark、已保存过滤预设和“仅当前技能链”视图；持久化内容只保存稳定 ID、过滤与 Replay frame，不保存 Runtime 对象。
- 支持导出选中的 Event/Trace/Actor 摘要到标准 Artifact 附加信息或独立文本，明确其不是完整诊断快照替代品。
- 引入长事件列表的分页、增量加载或虚拟化；记录查询耗时、保留区淘汰与 UI 刷新开销。

验收：

- 纯逻辑 Replay 跳到技能帧并暂停后，可从事件、Trace、Actor 状态完成与实时相同的排查闭环。
- Event 超过显示上限时，用户能看到保留范围、当前页/过滤和淘汰提示，而非误以为数据丢失。

### P3：规模化与远端准备

目标：为大规模战斗、远端数据源和团队共享准备稳定边界，不提前耦合传输实现。

- 定义远端 Health/Session Adapter 所需的最小字段、权限和降级语义；优先复用 P0 的 Health DTO 和 Session Resolver 状态码。
- 增加 Diagnostics 自监控：Store 容量/淘汰、采样耗时、Collector 拒绝率、UI 查询耗时、对象分配预算。
- 提供容量配置、采样频率、Channel 配置和安全的控制权限模型；Capture Control 的 Freeze/Clear 不直接暴露为无确认的日常按钮。
- 为大型 Actor/Trace 数据引入虚拟化、搜索索引和节流，并建立性能回归场景。

验收：

- 远端或大数据量未接入时，UI 仍能按 capability 和连接状态明确降级。
- 诊断自身开销可测量，且不会因全量刷新、异常日志或无限保留造成明显战斗帧抖动。

## 5. 代码边界

预期核心修改区域：

- Runtime Diagnostics：`Runtime/Application/Services/Diagnostics`、`Runtime/Application/Systems/Diagnostics`、技能生命周期及其他 Producer 边界。
- Diagnostics Core：只新增平台无关 Health DTO、状态码和只读查询契约；不引用 Unity 或 Editor。
- Editor Battle Debug：`Editor/BattleDebug` 下的 Context、Session Resolver、Window、Registry、Panels 和 ViewModels。
- 测试：现有 Diagnostics Core Tests、Moba Editor Tests、Game Unit Tests；新增真实 World 健康与技能事件端到端 fixture。
- 文档：当前能力、测试指南、实施历史，以及本文的状态和批次勾选。

明确禁止：

- Editor 直接访问 Collector 的可变 Store 或绕过 Session 查询。
- 为修复 UI 空态而在业务路径伪造 Event、Trace 或状态数据。
- 将每帧异常的完整堆栈无限存入内存或 Console。
- 为了显示历史而改变 latest-only Store 语义却不建立容量、淘汰与一致性设计。

## 6. 测试矩阵

| 层级 | P0 | P1 | P2/P3 |
| --- | --- | --- | --- |
| Core/Runtime 单元测试 | Resolver 阶段、Health 状态码、采样/采集失败分类、channel/frozen/revision | DTO 投影、过滤、空态分类 | Bookmark/比较模型、容量与淘汰语义 |
| 真实 World 集成测试 | 自动服务/系统安装、Local Session Scope、技能 Producer 到 Event/Trace/State revision 增长 | Actor/事件/Trace 关联 ID 完整性 | Replay seek 后状态与事件查询 |
| Editor ViewModel 测试 | 统一 Context、健康视图、过滤无匹配与 producer 未产出区分 | Tab/selection 状态、Actor summary、Event/Trace 导航 | 预设、时间关联、分页/虚拟化投影 |
| Editor Window 手工验收 | 未连接与缺服务可诊断、健康条更新 | 一次技能释放闭环、窄窗口和默认过滤 | Replay 跳帧、前后状态比较、大列表性能 |
| 构建与静态检查 | Runtime、Editor、Diagnostics Tests 0 errors；`git diff --check` | 同左 | 增加性能场景和发布配置检查 |

每个新增异常分支至少验证：正常路径、Unavailable/NotProduced/NotCaptured/Empty/Failed 语义、revision 变化、缓存失效、日志限频与不影响战斗 Tick。

## 7. 风险与控制

- 服务解析存在可选依赖：Health 必须报告“未解析”而不是把能力位或固定 0 当成真实数据。
- Producer 频率高：健康计数应使用轻量数值和有限最近错误，不在成功路径分配字符串。
- UI 重构风险：P1 先迁移现有 Panel 和 ViewModel，不同时重写全部查询逻辑；保持旧面板可在过渡开关或测试中验证后移除。
- Replay 与 Live 数据源互斥：任何跳转操作都要检查当前来源，离线 Artifact 不得伪装可 Seek。
- 多 Store 异步更新：Summary 必须显示各 revision/frame，不能承诺单一原子时刻。
- Unity Test Runner 被运行中的 Editor 占用：构建结果与 NUnit 结果分别记录，不能以编译通过代替测试通过。

## 8. 推荐执行顺序

1. 先执行 P0 的 Runtime Health DTO、Collector/Sampler 证据记录和统一 Session Resolver。
2. 接入窗口健康条与 Health 页面，先在用户当前场景复现“释放技能无事件”并给出确切断点。
3. 用真实 World 集成测试锁定技能 Event/Trace/State revision 增长后，再开始 P1 信息架构迁移。
4. P1 完成一次技能闭环的 Play Mode 验收后，进入 P2 的 Replay 时间关联。
5. P3 仅在本地流程稳定且性能数据表明需要时启动。

## 9. 计划维护规则

- 每完成一个阶段，在本文标记完成项，并同步更新 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md)、[TESTING.md](TESTING.md) 和 [IMPLEMENTATION-HISTORY.md](IMPLEMENTATION-HISTORY.md)。
- 任何“可用”声明必须附带实际环境、聚焦测试、构建与手工验收边界。
- 若真实场景证明某 Producer 未进入当前技能路径，应先修复或明确其覆盖边界，再优化相关 UI；不得把未产出的数据伪造成已采集。

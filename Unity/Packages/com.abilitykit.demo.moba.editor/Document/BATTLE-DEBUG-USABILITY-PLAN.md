# Battle Debug 产品优化与功能完善路线图

> 规划版本：2026-08-08
>
> 目标：将 Battle Debug 从“多个诊断查询面板”建设为面向战斗模块开发的调查、定位、验证和证据沉淀工作台。
>
> 当前实现事实以 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md) 为准；已交付批次以 [IMPLEMENTATION-HISTORY.md](IMPLEMENTATION-HISTORY.md) 为准；本文只描述产品方向、缺口和后续执行路线。

## 1. 产品判断

Battle Debug 的主要问题已经不是缺少基础采集能力，而是已有能力没有围绕开发任务形成完整闭环。

当前工具已经能够采集并查询 World、Actor、Attributes、Buffs、Tags、Effects、Events 和 Trace，也具备标准 Artifact、离线浏览、真实逻辑 Replay、Trace 导航和技能失败调查工作集。但用户仍然需要在多个面板之间手工拼接事实，工具更多是在“展示数据”，还不能稳定回答以下问题：

- 哪里首先出现异常？
- 这个异常影响了谁？
- 最终伤害、Buff 或技能结果是如何产生的？
- 这是配置问题、输入问题、规则拒绝、执行链中断、状态问题，还是同步问题？
- 修改代码或配置后，行为是否真的改善？
- 如何把现场和结论交给其他开发者复现？

因此后续工作必须从“继续增加面板”转为“建设开发工作流”。优先级不以功能数量为标准，而以减少定位步骤、提高结论可信度、缩短修复验证时间为标准。

## 2. 产品目标

### 2.1 愿景

开发者在本地 Play Mode、Replay 或离线 Artifact 中，能够围绕一个战斗问题完成以下闭环：

1. 发现：工具主动指出异常、失败、异常密度或数据链路缺失。
2. 定位：从总览进入 Actor、Frame、Event 或 Investigation，找到第一个异常点。
3. 解释：通过 Trace、技能阶段、Damage、Effect、Buff 和 Attribute 来源解释结果。
4. 验证：对比修改前后的帧、Actor、Replay 或 Artifact，确认行为和性能变化。
5. 沉淀：保存筛选、书签、证据范围和结论，生成可复现、可分享的诊断材料。

### 2.2 成功指标

以下指标用于每个阶段的手工验收和回归评审，不要求一开始全部自动化，但必须逐步形成可测量基线：

| 指标 | 目标 |
| --- | --- |
| 首次判断数据源 | 打开窗口后 5 秒内确认 Live、Replay、Artifact、Disconnected 及原因 |
| 技能失败定位 | 从失败案例到第一个可疑阶段不超过 3 次主要交互 |
| 技能结果解释 | 从 Damage/Heal/Effect 事件到来源技能、Trace、Source/Target Actor 不超过 3 次主要交互 |
| 空数据判定 | 所有常见空态都能区分 NotProduced、NotCaptured、FilteredEmpty、Empty、Unavailable、Failed、Evicted |
| 修复验证 | 同一问题可保存相同筛选和证据范围，并完成修改前后对比 |
| 证据复现 | 导出的证据包包含来源、Scope、Frame、Revision、稳定 ID、筛选和结论，不依赖 Editor 对象引用 |
| 大数据可用性 | 2 万条事件保留区下，滚动、筛选、选择不因全量 OnGUI 查询造成明显卡顿 |
| 诊断侵入性 | 采集异常不打断战斗 Tick；健康信息有界、限频、可追踪 |

指标若因真实场景无法测量，必须记录阻塞原因，而不是用“看起来可用”替代结果。

### 2.3 非目标与边界

- P0-P2 不引入远端协议、鉴权、跨机器控制或云端存储。
- 不把 latest-only 状态 Store 直接改成无限历史数据库；历史状态优先通过确定性 Replay 获取。
- 不允许 Editor 绕过 `IBattleDiagnosticReadOnlySession` 直接读取可变 Store 或修改战斗 Runtime。
- 不用固定 0、空列表或日志文本伪造缺失诊断事实。
- 不承诺多个 Store 具备跨轨道原子快照；每条轨道继续展示独立 revision、frame 和稳定性边界。
- 不在第一阶段制作自由布局的复杂 Trace 图；优先做可搜索、可折叠、可定位的路径视图。
- 不把控制采集、Freeze、Clear 等高风险能力直接暴露为无确认的日常按钮。

## 3. 已完成、需整合与真正缺口

| 分类 | 当前内容 | 后续动作 |
| --- | --- | --- |
| 已完成 | 统一只读 Session、独立 Store revision、不可变 DTO、事件 Ring Store、固定 revision 分页 | 作为所有新功能的基础契约，不重复建设 |
| 已完成 | 标准 Artifact 导出/导入、离线 Session、实时快照导出 | 补充证据范围和来源元数据，不改变现有格式兼容性 |
| 已完成 | 真实逻辑 Replay、暂停、单步、Seek、纯逻辑/表现模式 | 增加围绕问题的帧定位和比较工作流 |
| 已完成 | Actor、Events、Trace、Attributes、Buffs、Tags、Effects 面板 | 迁移到统一工作区和持久 Inspector |
| 已完成 | 技能失败调查案例、问题簇、置信度、证据聚焦、Trace/Actor 导航 | 扩展为通用 Investigation，而非只服务技能失败 |
| 已完成 | Phase 0 的 Health DTO、阶段化 Session Resolver、Overview Health 和首批空态投影 | 继续补齐 Producer/Panel 健康聚合与真实场景验收 |
| 需整合 | Actor/Diagnostics 二级工作区、面板下拉选择、分散的直接导航回调 | 持久 Inspector 已完成 Actor/Event/Trace 与 Editor-only Config 投影接入；继续扩展 Skill Runtime 和一级工作区整合；统一 Frame Cursor 可见闭环已完成首批迁移 |
| 需整合 | Core Workspace Selection/Filter/History 与局部面板筛选 | 让共享 Filter 真正驱动 Events、State、Trace 和 Actor，并统一清除/恢复语义 |
| 需整合 | Overview、事件筛选、Trace 搜索和当前临时 Pin | 统一筛选、关联上下文、书签和证据范围 |
| 需整合 | 空态和查询状态已有部分 DTO/状态码支持 | 将统一投影迁移到 Trace、Actor 详情和更多可查询面板 |
| 真正缺口 | 首屏异常总览、事件密度和趋势 | P1 优先完成 |
| 真正缺口 | 跨帧 Timeline、Damage/Effect/Buff/Modifier 解释链 | P1/P2 完成；Frame Cursor 已完成窗口可见闭环的首批迁移 |
| 真正缺口 | Frame/Actor/Run/Artifact 对比、回归摘要、证据包 | P2 完成 |
| 真正缺口 | 背景索引、性能预算、远端权威会话、Local/Authority 对账 | P3，按实际需求启动 |

## 4. 核心开发工作流

### 4.1 技能无法释放

入口应是 Overview 的失败热点或 Events 的技能失败案例。用户需要依次看到 Input、Preparation、Rule、Runner、Pipeline 等阶段，失败 Code、Message、Source、Slot、Target、Config 和关联 Trace。最终结论应区分输入错误、目标不可用、资源/冷却、规则拒绝、配置缺失、Pipeline 中断和诊断未采集。

### 4.2 技能释放了但结果不对

用户从技能事件进入 Timeline 和 Trace，再进入 Damage/Heal Inspector。工具应展示 Source、Target、技能阶段、最终结果、每个计算阶段、Modifier 来源和关键状态变化，避免只显示一个最终数值。

### 4.3 Buff、属性或标签不符合预期

用户选择 Actor 后，应能按当前帧查看属性 Base/Final/Delta、Modifier 优先级和来源 Buff/Effect/Config，并能反向跳转到添加、移除或修改它的 Event/Trace。无历史帧时必须引导使用 Replay，而不是暗示 Store 能查询历史。

### 4.4 Effect、触发器或技能链中断

用户应能从 Trigger/Effect 事件进入 Trace Path，看到条件、Action、Phase、Context、Parent Context、结束状态和终止原因。没有完整结构化 Payload 时显示证据不足，并指出需要补充的 Producer 字段。

### 4.5 战斗状态或同步出现异常

Health 和 Sync 工作区应先显示数据源、采样状态、权威状态哈希和 revision，再定位第一个可观测差异。对没有可靠事实来源的 Snapshot Gap、Rollback 或 Full Snapshot，不生成空的“无差异”结论。

### 4.6 修改后是否变好

用户应能保存一次问题的 Frame、Actor、Event、Trace、Filter 和备注，重新运行或打开另一份 Artifact 后执行前后对比。结果至少包含行为差异、失败数、事件数、关键状态差异、耗时和诊断覆盖差异，并明确哪些数据不可比较。

## 5. 产品架构方向

### 5.1 一级工作区

将现有面板组织为固定一级入口：

- Overview：异常、热点、健康、最近事件和推荐入口。
- Timeline：按 Frame 展示事件密度、技能链、Actor、Damage、Buff、Effect 和同步轨道。
- Actor：Actor Summary、Attributes、Buffs、Tags、Effects。
- Trace：Trace Tree、Trace Path、阶段和配置定位。
- Compare：Frame、Actor、Replay、Artifact 和 Run 对比。
- Evidence：书签、调查案例、证据范围、导出和备注。
- Health：Session、Capability、Store、Producer、查询和 UI 自身健康。

一级入口名称可以按现有中文界面规范落地，但不能继续让用户通过面板下拉框猜测功能位置。

### 5.2 统一上下文

窗口级状态应逐步收敛到一个只读上下文，至少包括：Session、Selection、Frame Cursor、Filters、Navigation History、Health Snapshot 和 repaint 请求。所有面板从同一上下文读取，导航只修改稳定 ID 和帧，不传递临时 Runtime 对象。

建议逐步建立以下边界：

- `BattleDebugSelection`：Actor、Event Sequence、Root Context、Trace Context、Skill Runtime、Config Reference。
- `BattleDebugFrameCursor`：当前帧、范围、是否可 Seek、来源和不可用原因。
- `BattleDebugFilterState`：全局筛选、局部筛选、保存预设和筛选来源。
- `BattleDebugNavigationHistory`：前进、后退和入口原因。
- `BattleDebugHealthSnapshot`：数据链路和工具自身的只读状态。

### 5.3 数据解释优先于数据堆积

新增 DTO 和 Producer 字段必须服务于一个可执行问题。每个新事件至少回答：发生时间、来源、目标、稳定关联 ID、阶段、结果、原因、配置来源和可继续导航的位置。无法提供完整因果链时要提供置信度和缺失证据，而不是增加一行不具备解释性的文本。

## 6. 分阶段路线图

### Phase 0：信任基础与产品骨架

**目标**：让用户先相信工具显示的状态，并让后续工作有统一上下文。

**Runtime/Core 交付物**：

- Health DTO、Health Read Store 和统一状态码，覆盖 Session 解析、Capability、Capture、Store revision/frame、通道、冻结、淘汰、采样/采集成功失败和最近错误。
- 统一 Session Resolver 阶段结果：Offline、FacadeMissing、LogicSessionMissing、WorldMissing、ServicesMissing、DiagnosticSessionMissing、Connected。
- 修正 World Summary 的真实活动 Skill Runtime、Trace Root 数量；缺少来源时标记 Unavailable。
- 统一事件拒绝分类：Frozen、ChannelDisabled、InvalidDraft、StoreRejected、Exception，并采用计数、限频和有界最近错误。

**Editor 交付物**：

- 固定 Health 入口和顶部健康条，不依赖反射 Panel Registry 才能发现。
- `BattleDebugContext` 注入来源身份、Health 和统一 revision 状态。
- Events、State、Trace 和 Actor 空态统一显示状态码、原因和建议动作。
- Panel 发现/构造异常进入 Health，而不是静默丢弃。

**验收标准**：

- 无 Session、缺 World、未采样、Frozen、通道关闭、过滤无匹配、正常 Empty、Evicted 和查询 Failed 可被明确区分。
- 一次基准技能释放后，Event/State/Trace revision 和最后帧变化可被确认；没有 Trace 时明确显示无 Root Context。
- 诊断采集失败不会中断战斗 Tick，也不会每帧刷完整异常日志。

**测试与风险**：

- 覆盖 Resolver 状态机、Health 状态转移、拒绝分类、revision 和日志限频。
- 增加真实 World fixture 验证 Scope、服务安装和技能 Producer 链路。
- 风险是健康 DTO 侵入高频路径；控制方式是无字符串成功路径、聚合计数和固定容量错误记录。

### Phase 1：Overview、统一选择与 Inspector

**目标**：从“打开哪个面板”转为“从问题入口开始调查”。

**Editor/UX 交付物**：

- 固定 Overview、Actor、Timeline、Trace、Health 一级入口，逐步淘汰工作区加下拉框的隐藏层级。
- Overview 显示数据源、健康摘要、异常热点、最近失败、事件密度、活跃 Actor 和推荐调查入口。
- 建立统一 Selection、Frame Cursor、Filter State 和 Navigation History。
- 引入持久右侧 Inspector：选中 Actor、Event、Trace Root 或 Trace Node 后始终显示详情，不因切换主视图丢失上下文；Runtime 诊断以真实 Session 查询、固定 revision 和有界 Event 分页为边界。Config 已使用完整 Editor-only Reference 持久投影，保留 SkillFlow PhaseId 并绑定来源 Workspace Selection；后续再扩展 Skill Runtime 独立选择，并评估 Config 是否需要进入 Core History。
- Actor Summary 统一显示身份、队伍、生命/存活、Tag/Buff/Effect 数、最近事件和最近 Root Context。
- 全局筛选可见，支持一键清除 Actor、Frame、Channel 和搜索条件；局部筛选不能静默污染其他工作区。

**Runtime/Core 交付物**：

- 为 Actor Summary 和 Overview 补充缺失的稳定字段，但不让 Editor 回退读取 Runtime 对象。
- 建立最近异常和热点的只读投影模型；初期可基于分页工作集，后续再增加后台索引。

**验收标准**：

- 用户打开窗口后能在 5 秒内判断来源和健康状态。
- 从 Overview 选 Actor、Event 或失败案例后，切换 Timeline、Trace、Actor 不丢失选择。
- 从一个技能失败案例到证据 Event、Trace 和 Source/Target Actor 不超过 3 次主要交互。
- 在窄窗口下关键状态、选择和操作不重叠，列表和树保持独立滚动。

**测试与风险**：

- ViewModel 测试覆盖 Selection、Filter、Frame Cursor、History 和缓存失效。
- 手工验收覆盖 Live、Replay、Artifact、Disconnected 和 720/960/1180 宽度；Frame Cursor 需要验证手工固定帧、跟随最新、Selection/History 帧恢复和 Replay Seek 边界。
- 风险是一次性重写窗口造成回归；采取渐进迁移，保留旧 Panel 作为兼容实现直到新入口完成闭环。

### Phase 2：Timeline 与战斗因果分析

**目标**：让用户可以按时间理解战斗，而不是在事件列表和 Trace 树之间手工拼接。

**Runtime/Core 交付物**：

- 扩充 Event Payload：Damage/Heal、Buff、Effect、Projectile、Summon、Skill Phase、Condition、Action 和 Modifier 的稳定结构化字段。
- 引入 Timeline 查询和分段聚合契约，支持按 Frame、Actor、Channel、Root Context 和 Skill Runtime 查询。
- 增加 Damage Pipeline DTO，至少描述输入、修正、吸收、抗性、暴击、最终结果和每项 Modifier 来源。
- 增加 Buff/Attribute provenance DTO，支持来源 Event、Effect、Buff、Config 和优先级。

**Editor/UX 交付物**：

- Timeline 支持帧游标、范围选择、缩放聚合、泳道、事件密度、异常标记和关键事件跳转。
- Trace 增加 Trace Path 模式：展示从 Root 到当前节点的父链和关键阶段，不制作首版自由图。
- Damage/Heal Inspector 使用 waterfall 展示计算过程；Effect/Condition/Action 展示输入、结果和中断原因。
- Timeline、Event、Trace、Actor 和 Config 之间建立双向导航。

**验收标准**：

- 对一个错误伤害案例，用户能从 Timeline 找到首个异常事件，再进入 Damage waterfall 和 Modifier 来源。
- 对一个触发器失败案例，用户能看到条件/Action/Phase 的执行结果和停止原因。
- 没有结构化 Payload 时，界面明确标记“当前事件只能解释到信封层”，不推断不存在的因果关系。

**测试与风险**：

- Core 测试覆盖 Payload 版本化、Timeline 排序/聚合、Damage waterfall 和 provenance 关联。
- 真实技能场景覆盖成功、失败、免疫、护盾、Buff 叠加、Projectile 命中和跨 Actor 触发。
- 风险是 Producer 字段扩张导致采集开销上升；采用按 Channel 配置、固定容量和离线导出兼容策略。

### Phase 3：Replay、比较与修复验证

**目标**：把诊断工具从“定位问题”提升为“证明修改有效”。

**Editor/UX 交付物**：

- Event、Trace Node、Investigation 和 Bookmark 都能跳转到 Replay Frame；不支持 Seek 的来源显示明确原因。
- Bookmark 保存稳定 ID、Frame/Range、Filter、来源、标签、备注和结论，不保存 Runtime 引用。
- 支持前后帧对比：HP、关键 Attribute、Buff、Tag、Effect、Event、Trace 和 Skill Runtime。
- 支持 Actor 对比、Run/Artifact 对比和同一 Investigation 的修复前后对比。
- 提供回归摘要：失败数量、首个异常帧、技能完成率、Damage/Heal 结果、关键状态差异、事件覆盖和不可比较项。
- 支持导出聚焦证据包，包含来源、Scope、revision、frame、稳定 ID、筛选、关键记录和人工结论。

**Runtime/Core 交付物**：

- 设计统一 Comparison Model，逐字段声明 Equal、Changed、Added、Removed、Unavailable 和 NotComparable。
- 记录 Replay 启动上下文、配置版本、场景/模式标识和诊断能力，避免不同条件被误判为回归。
- 扩展 Artifact 元数据和证据附加信息，同时保持旧 Artifact 可导入。

**验收标准**：

- 用户可从实时问题导出现场，使用同一 Bookmark/Filter 在 Replay 或另一 Artifact 中复查。
- 修改前后至少能看到首个异常帧、失败数和关键结果差异，并说明不可比较原因。
- 证据包在无活动 Runtime 的 Edit Mode 中可打开，且不依赖原始对象引用。

**测试与风险**：

- 测试 Seek 后 latest-only 状态的重新采样语义，不允许把单快照伪装成历史快照。
- Codec 测试覆盖旧版本、缺失附加信息、损坏证据和不兼容能力降级。
- 风险是 Replay 重启 Session 带来副作用；所有跳转必须明确来源、暂停状态和重建行为。

### Phase 4：规模化、自监控与团队协作

**目标**：在本地工作流稳定后，支撑大型战斗、批量回归和远端准备。

**交付物**：

- SelfMetrics：Store 容量/淘汰、采样耗时、Collector 拒绝率、查询耗时、Timeline 索引耗时、UI 刷新次数和分配预算。
- 长列表、Actor 树、Trace 树和 Timeline 的虚拟化、增量索引和后台聚合。
- Capture 配置、容量、采样频率和 Channel 的受控入口，带权限、确认、变更记录和恢复策略。
- 定义远端 authoritative Session Adapter 的最小字段、Capability 协商、权限和降级语义。
- 为 CI/CLI 消费设计只读 Artifact 和回归摘要接口，优先复用标准 Artifact，不提前绑定传输协议。
- Local/Authority 对账工作区，只有在同步层提供可靠 Snapshot Gap、Rollback、Full Snapshot 事实后才生成结论。

**验收标准**：

- 2 万事件、数百 Actor、深层 Trace 下，查询和滚动保持可用，性能指标可查看。
- 工具自身的内存、查询和 UI 成本能够通过固定场景回归。
- 远端或大数据量能力缺失时按 Capability 明确降级，不显示伪造的空结果。

**测试与风险**：

- 建立性能基准、内存上限、分配检查和大数据 Artifact 回归。
- 对控制入口做权限和误操作测试。
- 远端能力延后到本地数据模型稳定后实现，避免把当前 Editor 设计锁死在传输细节上。

## 7. 依赖关系与执行批次

建议执行顺序如下：

1. **批次 A：Phase 0 信任基础**。先完成 Health、Resolver 阶段状态、真实 World 数量和空态语义。
2. **批次 B：Phase 1 工作区骨架**。统一 Context、Selection、Filter、Frame Cursor、Overview 和 Inspector。
3. **批次 C：Phase 1 核心闭环**。完成技能失败、事件、Trace、Actor 的稳定导航和 Play Mode 手工验收。
4. **批次 D：Phase 2 解释能力**。先补 Damage/Heal、Skill Phase、Effect 和 Modifier 的结构化事实，再实现 Timeline。
5. **批次 E：Phase 3 验证能力**。Bookmark、证据包、前后帧和 Artifact/Run 比较。
6. **批次 F：Phase 4 规模化**。只根据真实性能数据启动索引、虚拟化、SelfMetrics、CI 和远端准备。

依赖原则：Health 是 Overview 和空态的前置；统一 Selection/Frame/Filter 是 Timeline、Inspector 和 Compare 的前置；结构化 Payload 是因果解释的前置；Artifact 元数据是跨 Run 比较的前置；性能索引不能早于查询契约稳定。

## 8. 测试矩阵与发布门槛

| 层级 | Phase 0 | Phase 1 | Phase 2 | Phase 3/4 |
| --- | --- | --- | --- | --- |
| Diagnostics Core | Health、Resolver、拒绝分类、revision | Summary/Filter/Selection 模型 | Payload、Timeline、Waterfall、Provenance | Compare、Bookmark、SelfMetrics、容量 |
| Runtime 集成 | 服务安装、Scope、采样和技能事件增长 | 真实 Actor/事件/Trace 关联 | Damage、Buff、Effect、Phase 完整链路 | Replay、批量 Artifact、性能场景 |
| Editor ViewModel | 状态投影、空态、缓存失效 | Context、导航、Inspector、筛选 | Timeline、Trace Path、详情模型 | 比较、证据包、虚拟化 |
| 手工验收 | 未连接、未采样、Frozen、过滤无匹配 | 一次技能问题闭环、窄窗口 | 伤害和技能链解释 | 修复前后对比、大数据滚动 |
| 工程验证 | 目标项目静态检查、`git diff --check` | 同左，另记录 Unity 编译边界 | 同左，增加 Artifact 兼容性 | 性能报告、CI/CLI 输出 |

每个新状态分支至少验证正常、Unavailable、NotProduced、NotCaptured、Empty、FilteredEmpty、Evicted、Failed；每次修改必须分别记录静态检查、生成项目构建和 Unity NUnit/Play Mode 结果，不能用其中一项替代另一项。

## 9. 代码边界与变更纪律

预期修改区域：

- Diagnostics Core：平台无关 DTO、状态码、查询、Timeline、Comparison 和证据契约。
- Runtime Diagnostics：采样、Collector、Producer、Health 和快照元数据。
- Editor Battle Debug：Context、Session Resolver、Window、Overview、Timeline、Inspector、Trace、Compare、Evidence 和 ViewModel。
- Tests：Diagnostics Core、Runtime 集成、Editor ViewModel、Artifact Codec 和性能基准。
- Documents：同步 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md)、[TESTING.md](TESTING.md) 和 [IMPLEMENTATION-HISTORY.md](IMPLEMENTATION-HISTORY.md)。

禁止事项：

- Editor 直接持有或修改 Collector、Store、World Service 和 Runtime 对象。
- 为了让 UI 有内容而伪造 Event、Trace、State 或活动数量。
- 用 Summary 文案、日志文本或临时对象引用推断稳定关联关系。
- 无限保留事件、错误堆栈、索引或 UI 缓存。
- 在未确认当前工作区用户修改的情况下覆盖 `BattleDebugWindow.cs` 或其他无关文件。

## 10. 第一批实现状态

第一批最小闭环已完成代码接入和源码编译验证，当前状态如下：

| 交付物 | 状态 | 说明 |
| --- | --- | --- |
| Health DTO 和 Session Resolver 阶段状态 | 已接入 | Live/Offline Health、Resolver 阶段和 Overview 展示已接入 |
| Overview 来源/健康/异常入口 | 已接入 | “最近失败”和“全部事件”使用不同语义路由 |
| Selection、Filter、Navigation History 最小模型 | 部分接入 | Selection/History 已接入窗口；Workspace Filter 已驱动 Events 查询、缓存和分页，State/Trace/Actor 尚未消费待设计字段 |
| Event、失败案例、Trace、Actor 稳定导航 | 已接入 | 支持稳定 ID、Trace/Actor 跳转和窗口前进/后退 |
| 持久 Selection Inspector 首批闭环 | 已接入 | Actor/Event/Trace Root/Trace Node 通过稳定 Selection 查询；桌面右栏、窄窗口下方布局、宽度/显示状态持久化、不可用状态和有界 Event 扫描已接入 |
| Config 持久 Inspector 投影 | 已接入 | 保存完整 Editor-only Kind/Id/PhaseId 与来源 Selection；权威 JSON 路径/行号、持续错误、重新解析和复制已接入，来源选择改变后自动退出；不进入 Core History |
| Events 共享与局部 Filter 语义 | 已接入 | 显示来源并提供清除局部、设为共享、清除共享；共享标量优先，Channel 交集，最近帧不共享 |
| 统一空态组件及 ViewModel 测试 | 已接入 | Events、State、Trace、Attributes、Buffs、Tags、Effects 已统一；Partial/Truncated 有结果时保留内容；Inspector 投影新增缓存、revision、分页、淘汰和 Config 身份/解析生命周期测试 |
| 真实技能场景 Play Mode 验收 | 待执行 | 已写入 `TESTING.md`；Config 本批尚未获得 Unity Test Runner 结果，也未完成窗口视觉和真实技能交互验收 |

当前 Battle Debug 已具备“先确认链路，再进入问题”的基础入口，Events 已形成共享与局部筛选边界，Actor/Event/Trace 拥有持久 Inspector，Config 也具备不丢失 PhaseId 的 Editor-only 持久投影；一级工作区、跨全部面板的统一可见 Filter、Skill Runtime 独立选择、Config Core History、Timeline 和真实技能场景证据闭环仍未完成。

## 11. 计划维护规则

- 每完成一个阶段，更新本文的阶段状态，并同步 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md)、[TESTING.md](TESTING.md) 和 [IMPLEMENTATION-HISTORY.md](IMPLEMENTATION-HISTORY.md)。
- 已实现能力只能进入“已完成”或“需整合”，不得继续作为未来功能重复列出。
- 每个“可用”声明必须附带运行模式、能力限制、测试结果和手工验收边界。
- 若真实场景证明某 Producer 未进入当前技能路径，先修复或明确覆盖边界，再优化 UI；不能把未产出的数据包装成已采集。
- 规划评审以成功指标和开发者完成任务的步骤数为依据，不以面板数量或 DTO 数量作为完成标准。

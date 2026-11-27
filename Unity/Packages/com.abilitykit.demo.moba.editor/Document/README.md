# AbilityKit Demo MOBA Editor 工具指南

> 适用包：`com.abilitykit.demo.moba.editor`
>
> Unity 版本：2022.3 或更高的 2022.3 LTS 版本
>
> 最后更新：2026-07-25

本包提供 MOBA 示例的配置生产、场景预览、战斗诊断、帧同步检查、Scene Gizmo 和热重载等 Unity Editor 工具。本文是工具使用与维护文档的统一入口。

## 快速开始

### 打开战斗调试窗口

1. 在 Unity 中打开 MOBA 示例工程。
2. 打开可启动 `BattleLogicSession` 的 MOBA 战斗场景。
3. 进入 Play Mode，并等待战斗会话启动。
4. 选择菜单 `Tools/AbilityKit/Demos/Moba/Battle/战斗调试`。
5. 在“角色”的实体导航选择 Actor；顶部可切换“技能与对象”“事件与 Trace”“帧同步”“会话”工作区。窄窗口使用下拉菜单切换。

窗口只在 Play Mode 下工作。未进入 Play Mode、`BattleDebugFacadeProvider.Current` 为空或没有活动 `BattleLogicSession` 时，窗口会显示对应提示。

### 常用操作

- 在实体导航顶部输入框按实体标识筛选；计数显示“可见/总数”，可点击 `×` 清除过滤。
- 在左侧输入 Actor ID 后点击“跳转”，或使用 `<` / `>` 在当前可见实体间循环定位；`×` 清除当前 Actor 选择。实体被过滤时选择保持不变并显示提示；实体离开当前世界时不会自动切换到相邻对象。
- 点击顶部刷新图标立即检查实体列表；窗口默认每 0.25 秒自动检查，可关闭“自动”暂停窗口轮询。窄窗口的刷新频率位于“数据与工具”菜单。该开关不冻结也不停止底层诊断采集。
- 拖动实体列表和详情区之间的分隔条可调整左栏宽度；栏宽、当前工作区和各工作区面板位置会在窗口关闭后保留。
- 在“角色”工作区查看“总览”“属性”“标签”“效果”“Buff”等选中实体快照。
- 实体导航和选择检查器在角色与诊断工作区分别记忆开关；诊断工作区默认收起实体导航，角色工作区默认收起右侧检查器。窄窗口开启检查器时，使用“主视图 / 检查器”切换。实体导航仅显示 Actor ID，标签与效果详情在对应面板查看。
- “技能与对象”集中显示技能运行时、活跃对象和对象目录；“事件与 Trace”用于失败调查和因果追踪；“帧同步”用于预测、回滚与网络检查；“会话”显示诊断健康和状态。诊断工作区可选择主、辅分析面板，并从布局菜单切换调查预设。
- “数据与工具”菜单集中放置 Artifact 导入导出、回放、状态区和扩展命令。主工具栏保留数据源、工作区视图开关、选择历史与刷新操作。
- 在“诊断事件”的“失败调查”区优先处理失败案例：按置信度和根因过滤，使用前后按钮切换案例，直接选择证据事件，并可聚焦其 Root Context、Skill Runtime、Attack 或 Context 关联链。
- 调查工作集首屏加载 200 条，可点击“加载更多”在同一固定 Event Store revision 上追加更早结果；筛选或 live revision 变化时重建首屏，固定快照淘汰时保留已加载结果并显示明确提示。
- 调查案例只按可靠 Root Trace 聚合；没有 Root Trace 的失败保持独立，避免仅凭 Actor 或配置建立错误因果关系。案例结论、证据摘要和结构化技能失败字段可复制。
- 问题簇按整个已加载工作集统计次数、首次帧、最近帧和跨度；事件列表仍支持按选中 Actor、失败结果、结构化技能失败 Code/Message/Slot 和普通文本条件过滤。
- 事件或调查案例带有 Root Context ID 时，可点击“打开 Trace”，自动切换并定位对应 Trace 节点；手工输入 Root Context ID 仍作为辅助入口。
- Trace 支持按 Kind、状态、结束原因、Context、Actor 或 Config 搜索；搜索结果保留祖先路径。可在直接命中之间循环前后定位、批量展开/折叠分支，也可 Pin 当前节点并在浏览其他节点后快速返回。程序化定位会将目标节点滚动回树视口。
- 在“诊断状态”中点击 Actor ID，可同步更新窗口实体选择。
- 面板提供复制按钮时，复制内容来自诊断 DTO 在采样时固化的数据。

## 当前面板

| 面板 | 主要用途 | 数据边界 |
| --- | --- | --- |
| 总览 | Actor 类型、名称、Tag 和 Effect 数量 | Actor、Tag、Effect 只读诊断查询 |
| 属性 | 属性基础值、最终值、属性投影和时间曲线；修改器页按持续来源查看各类目标与计算详情 | 属性来自只读诊断查询；实时修改器详情来自当前 World 的持续实例查询 |
| 标签 | Actor 当前 Tag | Actor Tags 只读诊断查询 |
| 效果 | Ability Effect 实例和计时状态 | Actor Effects 只读诊断查询 |
| Buff | MOBA Buff 实例和上下文 | Actor Buffs 只读诊断查询 |
| 诊断事件 | 固定 revision 调查工作集、失败案例、证据与关联导航、问题簇范围和事件明细 | Event Ring Store 一致分页与只读案例投影 |
| Trace | 按 Root Context ID 查看、搜索与命中导航、批量折叠 Trace 树，检查节点状态、父链路径和临时 Pin | Trace Read Store 查询 |
| 诊断状态 | World 与 Actor 最新状态 | State Store 查询 |
| 帧同步/* | 帧、预测、回滚、对账和网络状态 | 既有帧同步调试接口；按运行环境动态显示 |

大部分诊断面板消费 `IBattleDiagnosticReadOnlySession` 的只读 DTO。“属性 > 修改器”的实时持续实例详情另外查询当前 World 的 `IMobaContinuousRuntimeQueryService`；离线 Artifact 只能展示已导出的属性投影，不能还原持续实例或非属性修改器。主窗口以稳定 Actor ID 保存选择，并通过窄导航命令支持 State → Actor 和 Event → Trace；实体枚举和选择解析仍依赖活动 `IBattleDebugFacade` 和 `IUnitFacade`。实体导航不查询每个 Actor 的标签或效果；标签页的详细运行时快照由用户点击“抓取”时读取。

### 角色修改器与属性曲线

在 `Actor > 属性` 中切换“属性 / 修改器”。修改器页按持有它的持续实例分组，显示 Buff、发射器、子弹等来源的生命周期、层数、施加者、上下文与修改器条目；类型筛选覆盖属性、状态机参数、状态标记、领域参数、技能槽位候选、引用候选和其他目标。条目详情显示操作、优先级、数值来源、属性公式、阶段筛选、计算与捕获值，未知目标类型仍保留原始类型 ID。属性投影与持续实例无法关联时单独列出，不重复计入已关联来源。属性型条目的“曲线”可跳至对应属性。

新增修改器目标类型时，列表仍会以原始类型/目标 ID 显示在“其他”筛选下；需要领域专属解释时，在运行时视图中补充只读字段，并扩展共用的 `BattleDebugModifierDetails` 渲染和筛选名称。属性曲线只表示角色属性的实际基础值/最终值，其他领域参数应按其消费阶段提供对应诊断值，不把它们当作角色属性绘图。

属性页选择某条属性的“曲线”，可设置 1–600 帧的采样间隔、暂停记录或清空历史；图中显示基础值、最终值及可选的常量预期值。预期值与容差用于判断所选点，并汇总已记录点的符合数、超差数和最大绝对偏差。实时 World 在战斗帧末尾只读取当前选中的属性，采样不受普通状态快照间隔限制；曲线最多保留 240 个点，切换 Actor、属性、间隔或会话后重新记录，帧回退也会清空旧历史。

普通属性 Store 仍只保留最新帧。实时曲线从启用记录后的下一次战斗帧末尾开始，记录相邻点至少相隔指定帧数；若战斗帧本身跳号、诊断被冻结或角色属性不存在，相应帧没有采样点。窗口通过 World 服务读取独立的有界历史，窗口刷新频率不会造成历史帧丢失。离线 Artifact 只有单帧属性快照，不提供跨帧曲线；无法取得实时采样服务时，面板仅按观察到的最新快照取点，并可能遗漏两次窗口刷新之间的变化。常量预期值比较只判断已记录点，不证明未记录帧符合预期。

## 使用边界

- 当前可用入口是本地 Unity Play Mode。
- 当前状态类 Actor Store 使用 latest-only 语义：帧 `0` 表示最新快照；指定帧只有等于当前快照帧时才可查询。
- 事件 Store 有界保留历史，并以 revision 保证同一次分页读取的一致性。
- 远端会话、离线文件、导出、完整 Timeline 和完整 Skill Runtime 检查尚未形成可用工作流。
- 本工具是只读诊断客户端，不应修改生命、属性、Buff、Effect、技能运行时或 Trace。
- 工具不替代 Unity Profiler、Memory Profiler、正式 Record/Replay 或网络抓包工具。

## 其他 Editor 工具入口

从下表进入各领域工作台或场景命令；Scene Gizmo 是 Scene View 叠加层，不会打开新窗口。

### 窗口与场景命令

| 类型 | 工具 | 菜单入口 | 使用前提 |
| --- | --- | --- | --- |
| 窗口 | 战斗调试 | `Tools/AbilityKit/Demos/Moba/Battle/战斗调试` | 场景叠加可在编辑模式配置；实时诊断需启动战斗 |
| 窗口 | MOBA 状态机编辑器 | `Window/AbilityKit/MOBA 状态机编辑器` | 在工作台保存、只读校验已保存文件或预检后发布；不从战斗调试发布 |
| 窗口 | MOBA 角色动画同步 | `Window/AbilityKit/MOBA 角色动画同步`；也可从状态机编辑器带角色打开 | 在工作台扫描差异和检查已绑定动画；同步并发布须确认完整 Luban 发布范围 |
| 项目子菜单 | MOBA 地图工具 | `Tools/AbilityKit/Demos/Moba/Map` | 地图与兵线校验只读；刷新地图 Prefab、兵线发布和建筑发布各自预检并确认，不互相触发 |
| Inspector | MOBA 生产技能流程 | 选择生产 `SkillFlowCO.asset` →「MOBA 生产技能流程」 | 编辑模式下先保存源资产；校验不修改正式产物，发布先预检再确认 |
| 命令 | 打开现有 Demo 场景 | `Tools/AbilityKit/Demos/Moba/Open Demo Scene` | 只打开已有场景，不生成装配；切换场景前由用户决定是否保存当前修改 |
| 命令 | 播放现有 Demo 场景 | `Tools/AbilityKit/Demos/Moba/Play Existing Demo Scene` | 打开已有场景并进入本地 Play Mode，不生成装配 |
| 命令 | 生成两套 Demo 装配 | `Tools/AbilityKit/Demos/Build Package-Owned Gameplay Composition` | 预检并确认后才重建 MOBA 与 Shooter；可能迁移或删除旧资产、更新 Starter 和替换 Build Settings 场景列表 |
| 窗口 | Editor Flow Pump | 战斗调试 → 数据与工具 → 预览 → 编辑器驱动（Flow Pump） | 打开不启动；在编辑模式下从窗口显式启动预览 |
| 窗口 | 帧同步测试 | 战斗调试 → 数据与工具 → 同步诊断 → 帧同步测试 | 打开只查找场景中的 Harness；创建对象、启动会话、连接均由窗口内按钮触发 |
| 命令 | 编译 Hotfix | `Tools/AbilityKit/Hot Reload/Compile Hotfix` | 工程需具备 Hotfix 编译环境 |
| 命令 | 重载 Hotfix | `Tools/AbilityKit/Hot Reload/Reload Hotfix` | 先完成 Hotfix 编译 |
| 窗口 | 创建英雄 | `AbilityKit/Moba/Hero/Create New Hero…` | 按向导填写英雄资源信息 |

### 场景辅助图形

在「战斗调试」窗口的顶部工具栏点击「场景叠加」，即可独立开关碰撞世界、攻击范围、Buff 范围、出生区域、导航网格、寻路路径和活动 AOE 区域。开关沿用原有个人编辑器偏好，在编辑模式也可调整；关闭面板不会关闭已启用的叠加层。

启动战斗后在 Scene View 查看实际绘制。叠加层读取逻辑战斗 World，表示判定位置和范围；表现模型启用插值时，移动中的模型可能与逻辑位置存在短暂视觉差异。「恢复默认设置」只重置开关；「清空出生点缓存」让绘制器在下次采集时重新记录当前角色位置，不改变开关、场景或战斗状态。

### P1/P2 示例同步

在编辑模式打开「战斗调试 → 数据与工具 → 示例维护」，选择相应的 P1 或 P2 预检。弹窗显示读取的 JSON、将新建或覆盖的资产，以及 P2 可能更新的触发器项目资产。预检不写入，也不做字段级比较；必须点「确认同步」才会调用原同步实现。缺失输入、目标类型错误或目标有未保存修改时不能确认。

P1/P2 的 `SyncBatch` 原样保留，批处理直接调用时没有确认弹窗。同步复用现有 `AssetDatabase.SaveAssets()`，可能保存其他脏资产，而且不是事务；执行前先处理其他改动，失败后按版本控制检查并恢复受影响的文件。本入口只维护示例，不用于生产发布。

### 表现资源维护与离线复核

- **头顶 HUD 结构**：从 `Tools/AbilityKit/Demos/Moba/HUD/预检并重建头顶 HUD Prefab` 查看新建或覆盖目标，确认后才生成。播放、导入或 Prefab Mode 下不可用，目标有未保存修改时阻止重建。批处理仍可直接调用 `AbilityKit.Demo.Moba.Editor.MobaOverheadPrefabBuilder.Build`。生成会覆盖 `Resources/ui/battle/hud_overhead.prefab` 的手工结构调整，并可能保存其他未保存资产；布局和数值配置不用重建 Prefab。失败后按版本控制核对差异再重试。
- **Lux 表现审计**：`AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.LuxPresentationAudit.Capture` 只用于独立 Unity 工程的批处理，不提供菜单。`-luxReviewOutput` 必须指向 `local/Logs/` 下新的或空的绝对路径；另用 `-logFile` 指定 Unity 日志路径，不要把日志写在采样目录里。默认只采样已发布资源，产生 PNG 与 `bounds.json`；仅显式传入 `-luxRebuild` 才会写入派生 Prefab，须使用可丢弃且资源不链接回主工程的副本。
- **角色动画时序审计**：批处理 `AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaCharacterAnimationTimingAudit.Capture` 接受 `-mobaAnimationAuditHeroes <逗号分隔的模型ID>`，可选 `-mobaAnimationAuditSamplePoses`。用 `-mobaAnimationAuditOutput <绝对目录>` 指定 `local/Logs/` 下新的或空的目录，`-logFile` 指向该目录之外的日志。历史调用省略输出参数时会在 `local/Logs/character-animation-timing/` 下创建独立运行目录；以本次日志中的路径为准。`imported-clips.json` 和可选的 `sampled-poses-<模型ID>.json` 在同一目录，只读采样不发布资源。

### 历史英雄资源检查

`KarthusSkillSync.Inspect`、`LuxSkillSync.Inspect`、`GragasSkillSync.Inspect` 和 `GragasSkillSync.InspectQAnimationSegments` 是历史迁移或验收复现所用的只读批处理，不是日常发布入口。方法位于 `AbilityKit.Ability.Impl.BattleDemo.Moba.Editor`；用 Unity `-executeMethod` 调用时，传 `-mobaResourceFactsOutput <绝对目录>` 和 `-logFile <日志路径>`，均放在 `local/Logs/` 下。报告文件名和 JSON 结构不变，已存在的同名报告不会被覆盖。

四个方法省略输出参数时统一在 `local/Logs/moba-resource-facts/` 下分配独立运行目录，不再写 `local/` 根下的固定 JSON。以本次 Unity 日志打印的路径为准；不要用新的审计报告直接触发历史迁移。

### 单英雄工具与生产发布

Annie、Jinx、Veigar 不再提供独立的发布或检查菜单。改动技能流程后，保存生产 `SkillFlowCO.asset`，使用 Inspector 的「MOBA 生产技能流程」校验或预检发布；触发器、时间轴、HFSM 和配置表分别走各自正式工具。历史同步方法不作为日常入口，可能改写聚合资产或派生 Prefab，也不保证写入只限一个英雄。

### 特效模板检查与生产配置发布

- **只读检查**：从 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates` 打开检查器，查看已发布的模板估算和运行时实例。无需在 Project 窗口选目录；检查不会扫描目录导出 JSON、修改配置或触发发布。
- **生产发布**：在 AbilityKit 仓库中维护 `LubanConfig/Moba/Production/Datas/T-特效表.xlsx`。确认版本控制状态后，在仓库根目录运行 `python LubanConfig/Moba/export_pipeline.py --apply`。这是 MOBA 生产配置的统一发布命令，会写入 Unity 和 Console 的生成物，不是仅导出特效的命令。

## 文档导航

- [当前能力与限制](CURRENT-CAPABILITIES.md)：查询、能力位、面板、Producer 及未实现范围的当前快照。
- [Battle Debug 产品优化与功能完善路线图](BATTLE-DEBUG-USABILITY-PLAN.md)：围绕发现、定位、解释、验证和证据沉淀的产品目标、阶段依赖与验收基线。
- [故障排查](TROUBLESHOOTING.md)：Session、Capability、查询状态、刷新和 Unity 工程问题。
- [扩展开发指南](EXTENDING.md)：新增 DTO、Store、Session Query、ViewModel 和 Panel 的标准步骤。
- [测试与验证](TESTING.md)：构建、EditMode 测试、元数据和提交前检查。
- [架构设计](Moba战斗诊断与溯源工具设计.md)：长期目标、架构约束、数据模型与设计决策。
- [实施历史](IMPLEMENTATION-HISTORY.md)：按批次记录已完成的诊断正式化工作和当时验证结果。
- [Editor 包模块设计](DemoMobaEditor示例编辑器工具模块开发设计文档.md)：整个 Editor 包的职责和依赖边界。

文档中“当前能力”以 [当前能力与限制](CURRENT-CAPABILITIES.md) 为准；架构设计和实施历史中的阶段性描述不替代当前能力快照。

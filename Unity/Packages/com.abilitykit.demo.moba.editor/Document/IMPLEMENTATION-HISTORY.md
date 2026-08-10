# MOBA 战斗诊断实施历史

> 本文记录已经完成的实施批次及当时的验证边界。
>
> 当前能力以 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md) 为准。历史记录中的“当前”“尚未”等措辞只描述对应批次结束时的状态。
>
> 最后整理：2026-08-09

## 阅读规则

- 本文用于追踪设计如何落地，不作为当前 API 或能力清单。
- 每批只记录目标、关键结果和验证边界。
- 历史测试数量不代表当前分支持续通过。
- 没有实际执行 Unity Test Runner 的批次，不以项目编译代替 NUnit 结果。
- 更详细的第一至第二十五批原始记录暂保留在架构设计第 39 节内嵌归档中，后续可在不丢失审计信息的前提下逐步精简。

## 第一至四批：Core 地基与本地 Session

### 第一批：交互与查询语义

建立平台无关 Diagnostics Core，定义 Session/World/Epoch 身份、稳定选择、帧游标、保留范围、过滤、分页、查询状态和 Workspace 控制器。Core 不引用 Unity、Editor 或活动战斗对象。

### 第二批：DTO 与 Event Ring Store

建立 World、Actor、Event、Trace 基础不可变 DTO，以及统一只读 Session/Query 契约。实现固定容量 Event Ring Store、严格 Sequence、有界淘汰、Freeze、Clear、组合过滤和 revision 一致分页。

### 第三批：Runtime Collector 与首批 Producer

将 Diagnostics Core 提升到 Runtime 包，建立 `MobaBattleDiagnosticEventCollector` 和 Draft 提交边界，接入技能生命周期与最终伤害。Producer 不感知 Ring Store，采集失败与战斗主流程隔离。

### 第四批：State Store、Sampler 与 Local Session

建立 World/Actor 状态快照 Store、Runtime 状态采样器和 `MobaBattleDiagnosticLocalSession`，统一路由 State 与 Event 查询。状态 Store 与事件历史形成互补数据面。

## 第五至十二批：Producer 覆盖

### 第五批：自动状态采样与 Buff

增加 Late 阶段自动状态采样系统，并接入 Buff 添加和移除事件。

### 第六批：Projectile 生成

在弹丸成功生成和链接后提交 `ProjectileSpawned`，保留来源、目标、配置、Trace 与 Skill Runtime 关联。

### 第七批：Projectile 结束

在弹丸链接移除前捕获来源上下文，提交 `ProjectileEnded`。

### 第八批：Heal 与直接伤害

补齐不经过完整伤害管线的直接 Damage 和 Heal 事件。

### 第九批：Summon 生命周期

接入 Summon 生成与销毁事件，保留稳定 Actor、Config 和上下文关联。

### 第十批：TraceNode 生命周期

从统一 Trace Registry 网关采集节点创建和结束，避免逐业务服务重复插桩。

### 第十一批：Area 生命周期

接入 Area 生成和结束；将纯映射器与 Entitas 系统类分离，保持聚焦测试边界。

### 第十二批：Effect 执行生命周期

接入 Effect 开始、成功结束和异常 Dispose 路径结束事件。

## 第十三至十六批：首批 Editor 与补充 Producer

### 第十三批：首批诊断面板

在既有 Panel Registry 上新增“诊断事件”和“诊断状态”，统一通过只读 Session 查询，不从面板直接访问 Store。该批没有迁移既有 Actor 详情面板。

### 第十四批：Projectile Hit 与 ViewModel 分层

接入 `ProjectileHit`，并把诊断事件、诊断状态的查询和缓存逻辑从 Panel 抽到不依赖 UnityEditor 的 ViewModel。

### 第十五批：Warning 与 Exception

接入结构化 Warning/Exception Producer，复用诊断服务既有限流，避免异常路径重复采集。

### 第十六批：首批同步事件

只采集真实收到的权威状态哈希快照。没有从空对账报告推断 Snapshot Gap、Rollback 或 Full Snapshot 事件。

## 第十七至二十批：契约正式化

### 第十七批：独立 Revision 与原子状态快照

拆分 Event 和 State revision，完善 ViewModel 缓存键。State Store 使用原子 World/Actor 提交并正式定义 latest-only 查询语义。Local Session capability 收敛到真实公开查询表面。

### 第十八批：强类型 Event Payload

建立版本化纯值类型 `BattleDiagnosticEventPayload` 判别联合，首个 schema 覆盖同步状态哈希，并保留 Summary 文本兼容。

### 第十九批：Collector 窄端口

将职责混杂接口拆分为 Producer Sink、只读 Store、状态写入和采集控制端口，通过共享适配器保证同一 World Scope 内各端口指向同一 Collector。

### 第二十批：Trace 图只读查询

从真实 Trace Registry 导出节点图，增加独立 Trace revision、明确结束状态和 Local Session 动态 Trace capability，不从事件 Summary 重建因果图。

## 第二十一至二十五批：Actor 详情 DTO 化

### 第二十一批：Actor Attributes

增加 Attribute 与 Modifier DTO、latest-only Store、Runtime 采样、动态 capability 和只读属性面板。面板不再读取活动 Attribute 实例。

### 第二十二批：Actor Buffs

增加 Buff DTO、独立 Store、Runtime 投影、Capture 生命周期和独立 Buff 面板。规范化非有限和负时间。

### 第二十三批：Actor Tags

增加 Tag DTO 和独立 Store，采样时固化名称；既有标签面板迁移到只读 Session。

### 第二十四批：Actor Effects

增加 Effect DTO 和独立 Store，显式表达计时字段适用性；既有效果面板迁移到只读 Session。

### 第二十五批：Overview 聚合

Overview 组合 Actor、Tag 和 Effect 查询，移除面板对活动 Unit、Tag Container 和 Effect Container 的直接读取。联合缓存键包含 Session Scope、三类 revision、ActorId 和 Frame。

验证边界：Diagnostics Tests 和 Editor 生成项目完成范围化构建且为 0 errors；Unity Editor 当时正在运行，因此未启动第二实例、未结束用户进程、未运行 EditMode Test Runner，也未宣称 NUnit 通过。

## 第二十六批：Trace Tree/Path Editor 面板

目标：为第二十批已经完成的 Trace 只读数据链建立正式 Editor 消费入口。

关键结果：新增 Trace 面板与无 UnityEditor 依赖的 ViewModel；支持按 Root Context ID 查询、树层级、节点状态、孤儿标记、节点选择、根到选中节点的父链，以及不可用和截断状态提示。缓存键使用 Session Scope、独立 Trace revision 和 Root Context ID。

未改变的边界：Runtime Trace DTO、Store、Producer、Session Query 和 capability 契约保持不变；面板不读取活动 Trace Registry，不提供 Pin、导出或可变操作。

测试：已补 ViewModel 聚焦测试，覆盖缓存键、深度、孤儿、选择父链、revision 刷新回退、不可用清理和循环 Parent 防护。测试程序集源码编译通过；未运行 Unity Test Runner，不宣称 NUnit 通过。

构建：通过临时 MSBuild 导入将尚未被 Unity 生成项目刷新的两个新脚本纳入编译；Editor 生成项目 0 errors、8 个既有弃用 warning，Diagnostics Tests 生成项目 0 errors、2 个既有弃用 warning。临时导入文件已删除，生成项目未保留改动。

Unity 手工验收：未执行；未启动第二 Unity 实例，也未结束用户现有 Unity 进程。

已知限制：需要从事件或日志取得 Root Context ID 后手工输入；Trace Store 保留策略可能使旧根返回 Evicted；当前不支持搜索、折叠、Pin、导出、远端或离线 Trace。

当前能力文档更新：Trace 已登记正式 Editor 面板入口，并移除“无独立 Trace 图面板”限制。

## 第二十七批：Battle Debug Workspace UX 第一批

目标：优先修复日常诊断中的选择正确性、跨面板链路和面板扩展性，不进行无关视觉重做。

关键结果：主窗口改为保存稳定 Actor ID，实体刷新、排序和过滤不再导致选择静默漂移；新增 Actor/Diagnostics 两级工作区和面板下拉选择；Context 提供窄选择与 Trace 导航命令；事件支持选择详情、已知结构化 Payload 展示、来源/目标 Actor 选择和一键打开 Trace；诊断状态 Actor 行支持反向选择。

滚动边界：面板通过可选布局接口声明工作区与滚动所有权。Events、Trace、State 及大型 Actor 集合面板自行管理滚动，短内容面板继续使用窗口外层滚动，消除外层与内部列表的嵌套滚动。

未改变的边界：Runtime DTO、Store、Producer、Session 查询和 capability 契约均未修改；实体枚举与活动 Unit 解析仍由既有 Facade 提供；第三方面板不实现布局接口时保持默认 Actor 工作区和窗口滚动行为。

测试与构建：交互变更集中在 IMGUI EditorWindow，没有向纯 ViewModel fixture 添加私有窗口反射测试。Editor 生成项目范围化编译通过，0 errors；`git diff --check` 通过。Unity Test Runner 与手工 Play Mode UI 验收尚未执行。

已知限制：Trace 树搜索、折叠、Pin 和导出仍未实现；左右栏宽度和窗口状态尚未持久化；实体列表仍按 0.25 秒周期重建。

## 第二十八批：Battle Debug Workspace UX 第二批

目标：降低长期诊断时的导航与布局成本，补齐大型 Trace 树的高频定位能力，并减少稳定战场中的列表刷新扰动。

关键结果：主窗口增加可拖拽实体栏分隔条，通过 EditorPrefs 保存栏宽、工作区和两个工作区的面板索引；Actor 选择仍保持会话级，不跨窗口持久化。实体刷新使用复用缓冲区构建候选快照，仅在过滤后的 Actor ID 序列变化时替换可见列表。

Trace UX：ViewModel 增加搜索投影、命中计数、折叠状态和临时 Pin。搜索覆盖 Kind、状态、结束原因、Context、Actor 与 Config，显示命中节点及其祖先；搜索期间穿透折叠，清除搜索后恢复折叠状态。Pin 支持固定当前节点、浏览后返回，以及节点淘汰后的明确不可用状态。

未改变的边界：Runtime Trace DTO、Store、Session Query 和 capability 未修改；Editor Pin 只是当前面板内的导航状态，不等同于 Session/Runtime PinTrace，不持久化、不导出，也不修改 Trace。

测试与构建：新增两个 Trace ViewModel 聚焦测试，覆盖搜索祖先投影、折叠穿透与恢复、Pin 返回和节点淘汰。Editor 与 Diagnostics Tests 生成项目源码编译均为 0 errors；`git diff --check` 通过。未运行 Unity Test Runner，不宣称 NUnit 已执行。

Unity 手工验收：未执行；栏宽拖拽、偏好恢复、Trace 行交互和稳定刷新行为已加入 TESTING 手工验收清单。

已知限制：Trace 导出、跨根或跨会话持久化 Pin、远端和离线 Trace 仍未实现；自动刷新仍以 0.25 秒轮询检查实体集合，周期重绘仍用于实时诊断面板。

当前能力文档更新：Trace 搜索、折叠和临时导航 Pin 已登记；窗口布局持久化和实体快照替换策略已登记，并继续区分 Editor 临时 Pin 与未实现的 Session PinTrace。

## 第二十九批：Battle Debug Workspace UX 第三批

目标：缩短大 Trace 树中的重复定位路径，并改善实体列表在持续调试期间的浏览和刷新控制。

关键结果：Trace 搜索支持在直接命中节点间首尾循环导航，不把仅为上下文保留的祖先当作命中；新增全部展开和保留当前选中父链的全部折叠。Event → Trace、搜索命中导航、返回 Pin 和批量折叠后的程序化选择都会将目标节点滚动回树视口。

实体导航：顶部显示过滤后的可见实体数与总数，并支持一键清除过滤；左栏支持在当前可见实体间循环前后选择和清除选择。窗口增加本地自动刷新开关，关闭后暂停 0.25 秒窗口轮询，但手工刷新仍可用。

未改变的边界：Runtime DTO、Store、Session Query 和 capability 未修改；窗口自动刷新开关不等同于 Diagnostics Freeze，不停止底层采集，不冻结 Store，也不修改战斗状态。Actor 选择和自动刷新开关均不跨窗口持久化。

测试与构建：新增两个 Trace ViewModel 聚焦测试，覆盖直接命中循环导航、可见行索引、保留选中父链的批量折叠和全部展开。首次使用 `BuildProjectReferences=false` 的隔离构建因 Unity `Temp/bin` 缺少既有依赖 DLL 而失败，不属于源码编译错误；随后完整 Editor 生成项目构建为 0 errors、106 个既有 warning，完整 Diagnostics Tests 生成项目构建为 0 errors、108 个既有 warning，范围化 `git diff --check` 通过。未运行 Unity Test Runner，不宣称 NUnit 已执行。

Unity 手工验收：未执行；命中导航、自动滚动、批量折叠、实体循环导航和自动刷新暂停语义已加入 TESTING 手工验收清单。

已知限制：当前没有快捷键绑定、虚拟化树或跨 Trace 根的搜索；自动刷新暂停仅作用于当前窗口轮询，不是可复用的采集控制工作流。

当前能力文档更新：登记 Trace 命中导航、批量展开/折叠、程序化滚动，以及实体计数、循环导航和窗口自动刷新边界。

## 第三十批：不可变诊断快照基础

目标：建立标准导出和离线重载之前的可靠内存边界，使诊断现场不再依赖活动 Store 分页或活动 Runtime 对象。

关键结果：Diagnostics Core 新增平台无关的 Event、State、Trace、Attribute、Buff、Tag、Effect 轨道快照和聚合 Session Snapshot；各 Store 新增独立窄快照源并执行整批防御性复制。Event 快照一次复制完整 Ring 保留区，不受 500 条查询页和 retained read view 淘汰限制。Runtime 新增 scoped 本地快照协调器，并验证所有快照源 Scope 与 Session 一致。

一致性边界：各轨道保留独立 revision，latest-only 轨道保留 frame 和对齐判断，不声称跨 Store 原子事务。Trace 全根导出使用导出前后 revision 校验和最多三次重试，并通过 `IsStable` 明示持续变化时的不稳定结果。

未改变的边界：`IBattleDiagnosticReadOnlySession` 和 Editor Query 表面没有获得整批导出或可变控制能力；当前只生成不可变内存快照，尚未接入 `abilitykit-analysis.v1`、JSON 序列化、磁盘 IO、Editor 导出按钮、导入器或离线 Session Adapter。

测试：新增默认 20,000 Event 完整保序复制、Metrics 冻结、Clear 后旧快照不变、Trace 多根树预序聚合，以及真实 World DI 下所有轨道聚合和统一 Clear 后旧快照不变的覆盖。Unity 批处理完成脚本导入与编译并以退出码 0 结束，但未生成预期测试结果 XML，因此不宣称目标 NUnit 用例已被 Runner 执行。

构建：Diagnostics Core、Moba Runtime 和 Diagnostics Core Tests 三个 Unity 生成项目均 `dotnet build --no-restore` 通过，0 errors；warning 为既有 Unity 程序集版本冲突、nullable 和弃用 API。范围化 `git diff --check` 通过。

Unity 手工验收：本批没有新增 Editor UI，不需要视觉验收。

已知限制：协调器为顺序轨道复制；采集期间跨 Store 更新会体现在独立 revision/frame 和 Trace `IsStable` 中。JSON 工作可在主线程取得快照后移到后台，但本批未实现序列化与文件 IO。

当前能力文档更新：登记 Runtime 不可变内存快照基础，并继续将标准文件导出和离线诊断标为未完成。

## 第三十一批：标准 Artifact 与离线只读 Session

目标：在不可变内存快照之上建立可版本化、可验证、可离线查询的标准文件边界，同时保持既有分析 Artifact 向后兼容。

关键结果：复用顶层 `abilitykit-analysis.v1`，新增可选 `battleDiagnostics` Section 和独立版本 `abilitykit-battle-diagnostics.v1`。Codec 显式映射 Event、State、Trace、Attribute、Buff、Tag、Effect 全部八条轨道、Store Metrics、Session 元数据和已知结构化 Event Payload；导入校验顶层/Section 版本、必需轨道、Event revision/count/sequence、World ActorCount、Scope、时间戳及领域构造约束。旧 Artifact 不含该 Section 时仍可作为普通分析 Artifact 导入。

离线查询：新增 `BattleDiagnosticOfflineSession`，通过 `IBattleDiagnosticReadOnlySession` 暴露导入快照，固定为 `Disconnected` / `Frozen`；保留各轨道 revision、Event 筛选和固定 revision 分页、latest-only Frame/Actor 不可用语义，以及不稳定或截断 Trace 的 `Partial/Truncated` 状态。

未改变的边界：本批没有增加磁盘 IO、Editor 导入/导出按钮、Timeline、Bookmark、SelfMetrics、Remote Session、SkillRuntime 或历史帧。标准 JSON Codec 和离线 Adapter 是程序化基础能力，不等同于最终用户文件工作流。

测试：新增 `MobaBattleDiagnosticArtifactCodecTests`，覆盖八轨 round-trip、CamelCase Section、结构化同步 Payload、旧 Artifact 兼容、稳定错误码、损坏 Metrics/Sequence、离线 Session 身份和 revision、Event 筛选分页、latest-only 查询以及 Trace Partial。现有 Unity Editor 已占用项目，本批未启动第二实例；截至记录时未获得 Test Runner XML，因此不宣称 NUnit 已执行通过。

构建：Unity 已生成新文件 `.meta` 并将 Codec、Offline Session、Snapshot、Analysis Section 和聚焦测试收录到对应生成项目。Diagnostics Core、Moba Runtime 和 Diagnostics Core Tests 三个 Unity 生成项目均使用单节点 `dotnet build --no-restore` 通过，0 errors；warning 为既有 Unity framework 程序集版本冲突和项目图代码告警。此前隔离构建因 `Temp/bin/Debug` 缺依赖 DLL 失败，不作为源码失败证据。范围化 `git diff --check` 通过，新增 `.meta` GUID 均全仓唯一。

Unity 手工验收：本批没有新增 Editor UI，不需要视觉验收。

已知限制：Artifact 仅定义内存对象与 JSON 字符串边界；调用方仍需负责文件 IO、来源信任、大小限制和工作流集成。离线状态轨道继续遵循 latest-only，不提供历史帧回放。

当前能力文档更新：标准 JSON Codec 和离线只读 Session Adapter 登记为基础能力，磁盘与 Editor 工作流继续标记为未实现。

## 第三十二批：Battle Debug Artifact 文件工作流

目标：把标准 Artifact 和离线只读 Session 从程序化基础能力接入用户可操作的 Battle Debug 工作流，使现场捕获、传递和 Edit Mode 复盘形成闭环。

关键结果：Battle Debug 工具栏新增“打开”“导出”和离线态“返回实时”；活动 World 通过窄快照捕获服务导出 UTF-8 无 BOM 的 `abilitykit-analysis.v1` JSON，Play Mode 与 Edit Mode 均可打开包含 Battle Diagnostics 的 Artifact。窗口明确显示实时、离线或未连接来源，离线列表来自 State Actor 快照，DTO 面板消费显式注入的离线 Session，帧同步面板在离线模式隐藏。

数据源边界：实时与离线来源显式互斥，不伪造 Runtime Unit，也不把后台 Facade 数据混入离线现场。离线文件采用先完整导入并构造新 Session、再替换旧 Session 的原子切换；损坏文件显示稳定 Artifact 错误码并保留当前现场，“返回实时”和窗口销毁都会释放离线 Session。

测试：扩展 `MobaBattleDiagnosticArtifactCodecTests`，覆盖快照窄导出 API 的标准根、有效文件切换为离线、Actor 与身份状态投影、返回实时清理，以及损坏替换保持原 Session。测试源码已编译；本批未获得 Unity Test Runner XML，因此不宣称 NUnit 已执行通过。

构建：Moba Runtime、Moba Editor 和 Diagnostics Core Tests 三个 Unity 生成项目均使用单节点 `dotnet build --no-restore` 通过，0 errors；warning 分别为 93、133 和 108 个，主要是既有 Unity framework 程序集版本冲突、`EcsEntityId` 弃用和项目图告警。新数据源已由 Unity 生成 `.meta` 并收录到 Editor 项目，GUID 全仓唯一；`git diff --check` 无空白错误，仅报告工作树中既有 LF/CRLF 转换警告。

Unity 手工验收：代码与手工验收清单已就绪，但本批没有在运行中的 Editor 内完成窗口视觉和文件对话框验收。

已知限制：离线状态仍为 latest-only，不支持历史帧 Timeline；文件入口尚无大小上限、来源信任或签名策略；Actor 导航沿用既有 `EcsEntityId` 范围，只显示正数且不超过 `int.MaxValue` 的 Actor ID；远端 Session、SelfMetrics 和跨文件持久化 Pin 仍未实现。

当前能力文档更新：磁盘导出、Editor 离线导入、来源状态和返回实时登记为可用，并保留实时 Runtime 面板与离线 DTO 面板的能力边界。

## 第三十三批：Battle Debug 真实逻辑世界录像播放器

目标：在 Battle Debug 内提供小型录像控制区，让标准录像输入驱动真实 `BattleLogicSession`，并复用现有 Diagnostics 面板形成接近实时调试的历史重演工作流，而不是增加静态录像 DTO 浏览器。

关键结果：新增公开 Replay 控制契约和当前控制器 Provider；Battle Debug 支持加载录像、播放/暂停、单帧前进/后退、进度 Slider Seek、`0.1x` 至 `8x` 速度和末帧自动暂停。暂停冻结逻辑 Tick；向前 Seek 逐帧推进，向后优先 Rollback，失败时重建 Session 并从头确定性重放，同时同步录像输入游标和状态哈希校验状态。开发快捷键复用同一确定性 Seek 入口，不再只移动输入游标。

数据源与生命周期：录像加载复用活动战斗完整 `BattleStartPlan` 中录像未携带的 Map、玩家 Loadout 和 LaunchSpec，将当前 Session 热切换为本地 Lockstep Replay。Replay Session 继续注册为当前 Facade 和 Diagnostics Session，因此 Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 面板查询真实重演世界。成功加载录像会退出 Artifact 离线模式；Replay、Live 和 Artifact 不混合为并行数据源。加载启动失败时尝试恢复原计划和原 Session。

测试：在既有 `AbilityKit.Game.UnitTests` 中新增 3 个 `FrameReplayDriverTests`，覆盖播放/暂停与速度 Clamp、Inputs/StateHashes/Snapshots 的末帧最大值，以及 Seek 重置一次性哈希不匹配抑制状态。Unity 2022.3.62f1 EditMode Test Runner 已实际执行，`Logs/frame-replay-driver-tests.xml` 记录 `testcasecount="3"`、`passed="3"`、`failed="0"`。首次命令附带 `-quit` 时只完成工程刷新且未生成 XML；移除 `-quit` 后得到结构化通过结果。

构建：Moba View Runtime、Moba Editor 和 `AbilityKit.Game.UnitTests` 三个 Unity 生成项目均 `dotnet build --no-restore` 通过，0 errors；warning 为既有 Unity framework 程序集版本冲突、弃用 API 和未使用字段。测试 asmdef 增加对 `AbilityKit.Record` 的显式引用，并由 Unity batchmode 刷新生成项目，没有手工维护 generated `.csproj`。

Unity 手工验收：本批未在 Play Mode 完成真实录像加载、完整世界前后 Seek、Rollback 成功路径、Session 重建 fallback、窗口控件和诊断面板随帧变化的视觉验收；这些项目已加入 `TESTING.md` 手工清单，不能由 Driver 纯状态测试替代。

已知限制：当前 MVP 必须在 Play Mode 已有活动 `BattleLogicSession`，因为录像不包含独立创建战斗所需的完整启动元数据；加载录像会替换当前 Session，不支持 Live/Replay 并行对照。Artifact 继续是 Edit Mode 可浏览的单快照，不是录像 Timeline。

当前能力文档更新：登记标准录像驱动真实逻辑世界、控制项、确定性 Seek、Diagnostics 路由、活动启动计划前置条件，以及 Replay 与 Artifact 的互斥边界；移除“Battle Debug 完全没有历史帧回放”的过时表述。

## 第三十四批：Replay Session 帧包自动化覆盖

目标：将 Replay Driver 的验证从纯内存状态扩展到真实本地 `BattleLogicSession`，确认录像输入会进入 MOBA 逻辑世界的帧流，而非仅移动 Driver 游标。

关键结果：`FrameReplayDriverTests` 新增真实 Session fixture。测试经 `TestBattleBootstrapper` 创建本地 MOBA World，使用标准移动 Codec 构造两条录像输入，逐帧驱动 `FrameReplayDriver` 和 Session Tick，并从 `FrameReceived` 收集帧包；断言只有匹配帧提交，且 Frame、Player、OpCode 与 Base64 还原后的 Payload 完整保留。暂停后继续 Tick 不会产生新的录像输入。

测试：Unity 2022.3.62f1 EditMode Test Runner 实际执行 `AbilityKit.Game.Test.UnitTest.FrameReplayDriverTests`。`Unity/Logs/frame-replay-driver-tests.xml` 记录 `testcasecount="4"`、`passed="4"`、`failed="0"`；新增真实 Session 用例通过，耗时约 5.43 秒。`AbilityKit.Game.UnitTests.csproj` 使用 `dotnet build --no-restore` 通过，0 errors、181 个既有 warning。batchmode 的相对 `Logs` 路径以 `-projectPath` 为基准，因此结构化结果位于 Unity 工程目录。

已知限制：本批覆盖 FrameReplayDriver 到真实本地 Session 的输入提交边界，不直接构造内部 `SessionReplayController` 状态或验证 `BattleSessionFeature` 热切换。因此 Rollback 成功、Session 重建 fallback、Replay 加载失败恢复、窗口控件和 Diagnostics 面板随帧变化仍保留为 Play Mode 手工验收项。

当前能力文档更新：测试指南登记正确的 fixture 全名、Unity 内相对结果路径，以及真实 Session 帧包覆盖范围与保留的人工验收边界。

## 第三十五批：Replay 表现重置与纯逻辑分析模式

目标：修复渲染 Replay 向后 Seek 只回滚逻辑世界时可能保留旧表现状态的问题，并允许 Battle Debug 使用录像纯跑逻辑、跳转到指定帧后继续查询 Diagnostics。

关键结果：Replay 控制契约新增 `RenderPresentation` 和带模式参数的加载入口，Battle Debug 工具栏新增持久化“渲染表现”开关与“表现渲染/纯逻辑”状态。纯逻辑模式 Detach `BattleHudFeature` 和 `BattleViewFeature`，不创建或驱动主 HUD、View、VFX 和相机，同时保留完整 `BattleLogicSession`、逻辑 World、Replay 输入和 Diagnostics。向前 Seek 继续逐帧推进；纯逻辑模式允许使用 Rollback 快路径；渲染模式向后 Seek 按 HUD、View 顺序 Suspend，重建 Session 并从第 0 帧重演，再按 View、HUD 顺序 Restore，Restore 位于 `finally` 中，重建失败也不会让表现永久停用。

未改变的边界：录像仍不携带完整 Map、玩家 Loadout 和 LaunchSpec，必须在 Play Mode 复用活动战斗的启动计划；Replay 继续替换当前 Session，不支持 Live/Replay 并行对照。“渲染表现”属于 Replay 控制器运行时状态，尚未写入 `BattleStartPlan` 或录像格式。

测试：新增 `SessionReplayControllerTests`，覆盖纯逻辑且存在 Rollback Module 时才允许 Rollback 的策略矩阵、渲染模式向后 Seek 的 `suspend -> stop -> start -> auto -> restore` 顺序，以及 Session 重建失败仍 Restore 表现。`AbilityKit.Game.UnitTests.csproj` 已编译包含这些用例；由于同一 Unity 工程正被运行中的 Editor 占用，本批未启动第二个 batchmode 实例，也没有新的 Test Runner XML，因此不宣称 NUnit 已实际执行通过。

构建：Moba View Runtime、Moba Editor 和 `AbilityKit.Game.UnitTests` 三个 Unity 生成项目均使用 `dotnet build --no-restore` 通过，0 errors；warning 分别为 141、163 和 136 个，主要是既有 Unity framework 程序集版本冲突、弃用 API 和项目图告警。目标文件范围化 `git diff --check` 通过，仅有既有 LF/CRLF 转换提示。

Unity 手工验收：尚未在 Play Mode 验证完整 HUD/View/VFX/Timeline/插值/对象池清理、纯逻辑 Diagnostics、连续模式切换、加载失败恢复和窄窗口布局；这些项目已加入 `TESTING.md`。

已知限制：渲染模式 Seek 重建发生异常时会通过 `finally` 恢复表现，但当前异常仍向调用方传播；是否统一转成 `false` 需结合错误展示契约另行决定。自动化 fixture 验证控制顺序，不替代真实场景中的资源与订阅清理验收。

当前能力文档更新：登记“渲染表现”开关、纯逻辑 Diagnostics、按模式分流的向后 Seek、表现 Suspend/Restore 顺序，以及当前启动上下文和运行时模式边界。

## 第三十六批：技能失败调查案例与结构化证据

目标：按 P0、P1、P2 顺序把事件列表从原始失败浏览提升为案例导向的技能调查工作流，使开发者能稳定聚合失败、判断证据置信度、定位根因并沿既有关联入口继续分析。

关键结果：P0 新增纯 Editor 调查案例投影，只合并共享可靠 Root Context 的失败，无 Root Trace 的失败保持独立，并显式区分 `Confirmed`、`Inferred` 和 `InsufficientEvidence`。P1 新增版本化 `SkillFailure` Payload，稳定保存 Slot、Source、Stage、Code 和 Message；技能输入、释放准备、战斗规则、Runner 与 Pipeline 失败路径统一采集，Store 搜索、Artifact 映射、事件详情和复制文本均消费结构化字段，不从 Summary 反解析。P2 新增置信度与根因组合筛选、前后案例导航、证据事件直选、关联聚焦、Actor/Trace 跳转和可复制证据摘要；证据按钮按窗口宽度自动换行，重复技能失败按 Code/Source/Stage 形成稳定问题簇。

未改变的边界：案例仍是当前事件查询结果上的只读投影，没有增加独立 Store、可变 Runtime 旁路或基于 Actor/Config 的弱因果合并；无可靠 Root Context 时不推断同一案例。顶层 `abilitykit-analysis.v1` 与 Battle Diagnostics Section 版本保持不变，新增 Artifact 字段为向后兼容的可选字段。

测试：已补调查模型、Event Store、Artifact Codec 和 Events ViewModel 聚焦契约，覆盖可靠 Root Trace 聚合、无 Trace 隔离、置信度/根因分类及组合筛选、结构化 Code/Message/Slot 搜索、Artifact round-trip、复制文本和 Summary 变化时的稳定问题簇。Unity 2022.3.62f1 batchmode 在脚本编译阶段被 `SessionReplayController.cs` 两处既有 `FramePacket` 类型缺失错误阻断，目标 NUnit fixture 未进入执行，且没有生成预期 XML，因此不宣称测试通过。

构建：本批执行的 `git diff --check` 通过，没有空白错误或冲突标记，仅报告工作树既有 LF/CRLF 转换提示。Unity 生成的 Editor 项目尚未收录新增调查模型脚本；未手工修改生成项目，也没有用过期生成项目的结果代替 Unity 编译结论。

Unity 手工验收：未在 Play Mode 完成案例筛选、前后导航、证据选择、Actor/Trace 跳转、稳定问题簇和窄窗口换行的实际交互验收；对应步骤已加入 `TESTING.md`。

已知限制：案例模型只分析当前最多 200 条事件结果，不是跨完整保留区的离线索引；稳定问题簇当前以筛选聚焦为主，没有趋势图、首次/末次发生对比或跨 Artifact 合并。除 SkillFailure 与既有强类型 Payload 外，其他失败仍可能只能给出推断或证据不足结论。

当前能力文档更新：README、当前能力和测试指南已登记案例聚合规则、结构化技能失败证据、筛选与导航入口、稳定问题簇、自动化契约及手工验收边界。

## 第三十七批：调查工作集与固定 Revision 分页

目标：消除失败调查只分析首批 200 条结果的主要盲区，使开发者能在一致事件快照上显式扩展更早证据，同时补充问题簇的发生范围信息。

关键结果：Events ViewModel 将首屏刷新与“加载更多”分离。首屏仍以 200 条为页大小，并固定当次 Event Store revision；后续页沿同一 revision 和筛选条件追加，按 Sequence 去重，重建案例与问题簇。筛选或 live revision 变化时重建首屏；固定快照淘汰时保留已加载结果、停止继续分页并显示明确状态。面板新增调查工作集数量、SnapshotRevision、剩余结果和加载操作；案例在追加后按稳定 Key 保持选择并更新证据；问题簇新增首次帧、最近帧和跨度。

未改变的边界：本批只扩展 Editor 只读查询工作流，没有新增独立 Runtime Store、Bookmark、Freeze/Clear UI、Runtime PinTrace 或基于 Actor/Config 的弱因果合并。调查工作集不是后台完整索引，不跨 Artifact 聚合。

测试：`BattleDebugDiagnosticViewModelTests` 新增固定 revision 下一页、追加顺序与问题簇范围、live revision 首屏重建和 revision 淘汰保留用例；既有稳定问题簇用例补充首次帧与跨度断言。Unity 2022.3.62f1 batchmode 仍在脚本编译阶段被 `SessionReplayController.cs` 两处既有 `FramePacket` 类型缺失错误阻断，目标 NUnit 未执行且没有新 XML，因此不宣称测试通过。

构建：本批目标源码和文档执行范围化 `git diff --check` 通过。未手工修改 Unity 生成项目；由于生成项目刷新与上述 Unity 编译错误边界，未用过期 `.csproj` 构建代替 Unity 结果。

Unity 手工验收：尚未在 Play Mode 验证超过 200 条事件的追加、live revision 并发增长、案例选择保持、问题簇范围和快照淘汰提示；对应步骤已加入 `TESTING.md`。

已知限制：单页固定为 200 条，用户需显式加载更早结果；快照淘汰后只能保留已加载部分并重新刷新到 live revision，不能继续读取已淘汰历史。当前提供范围统计而非趋势图，也没有 Bookmark 或跨 Artifact 对比。

当前能力文档更新：README、当前能力和测试指南已登记固定 revision 工作集、增量加载、案例选择保持、问题簇范围、淘汰语义及验证边界。

## 第三十八批：Health、Overview 与稳定调查上下文

目标：实施产品路线图推荐的第一批最小闭环，使用户先确认数据链路健康，再从 Overview 进入技能失败并在 Actor、Event 与 Trace 间稳定调查。

关键结果：Diagnostics Core 新增平台无关 Health Snapshot 与只读端口，Runtime 从 scoped Session、Event/State/Trace Store 指标和有界最近错误生成实时 Health，Artifact 数据源按实际轨道保守生成离线 Health。Editor Session Resolver 显式区分 Offline、FacadeMissing、LogicSessionMissing、WorldMissing、ServicesMissing、DiagnosticSessionMissing 和 Connected；Overview 在没有 Actor 选择时展示来源、Health、独立 revision、最后帧/事件和错误，并将“最近失败”与“全部事件”路由到不同 Event 预设。

统一上下文：Core Workspace State 维护 Session Scope、稳定 Selection、Frame、Filter 和有界 Navigation History；窗口已接入 Actor、Event、Trace Root 与 Trace Node 的前进/后退和稳定恢复。Events 与 State 接入纯 ViewModel 空态投影，区分 SelectionRequired、FilteredEmpty、Empty、NotProduced、NotCaptured、Evicted、Truncated、Unsupported、Disconnected 和 Failed；Partial/Truncated 且仍有结果时继续展示数据。

未改变的边界：共享 Workspace Filter 尚未统一驱动各面板的局部筛选；Trace 和 Actor 详情尚未全部迁移统一空态组件。Event 历史目标必须位于当前已加载工作集，窗口不会为恢复选择隐式扫描完整 Ring；清除 Actor 选择与 Artifact 自动选择的历史语义仍需收敛。没有新增 Timeline、持久 Inspector、Bookmark、远端 Session 或跨 Store 原子快照声明。

测试：`BattleDiagnosticCoreTests` 新增 Health 有效性、Produced 判定、错误、512 字符截断、相等性和哈希覆盖；`BattleDebugDiagnosticViewModelTests` 新增必需选择、过滤空、普通空、不可用原因、Failed 错误信息及 Partial/Truncated 可显示语义覆盖。Diagnostics Tests 生成项目完成源码编译，0 errors；这只证明测试程序集可编译，未执行 Unity Test Runner，不宣称 NUnit 通过。

构建：Diagnostics Core 与 Moba Runtime 隔离构建均为 0 errors、2 个既有 Unity framework 程序集版本冲突 warning；Diagnostics Tests 隔离构建为 0 errors、0 warnings。Moba Editor 生成项目当前未收录新增空态源码，直接构建以两个 `BattleDebugEmptyStateProjection` 缺失错误结束；此前临时补入该源码后编译为 0 errors、132 warnings，临时 Compile 项已删除，生成 `.csproj` 无保留差异。范围化 `git diff --check` 通过，仅报告工作树既有 LF/CRLF 转换提示。

Unity 手工验收：`TESTING.md` 已增加真实技能失败场景，覆盖 Overview Health、最近失败/全部事件、结构化证据、Actor/Event/Trace 导航历史和空态。Unity 2022.3.62f1 已安装，但同一项目正被活动 Editor 进程占用，因此未启动第二实例、未结束用户进程，也未代替用户操作现有 Editor；该 Play Mode 场景与 Unity Test Runner 均未执行，没有 XML、截图、录像或通过结论。

已知限制：Health 当前覆盖数据链路和 Store 生产状态，不是完整 SelfMetrics，不含 UI 分配、查询耗时、拒绝分类趋势或 Panel 构造异常汇总。一级工作区、持久 Inspector、统一可见 Filter、完整 Frame Cursor 和真实技能场景验收仍属于后续批次。

当前能力文档更新：已登记 Health、Resolver 阶段、Overview 异常入口、稳定导航历史、Events/State 空态语义及共享 Filter 和历史恢复限制。

## 第三十九批：Events 共享筛选与详情空态统一

目标：继续实施 Phase 1 工作区整合，让 Workspace Filter 真正参与 Events 调查，同时将结构化查询状态和统一空态扩展到 Trace 与 Actor DTO 详情。

关键结果：Events 查询、缓存键和固定 revision 分页均接入 Workspace Filter；共享与局部条件采用明确合并规则，Channel 取交集，共享 Actor、Config、Correlation、Search 和 Trigger 标量条件存在时优先，局部条件只补共享未设置字段，`FailuresOnly` 取 OR，共享 Frame 与 `UnfinishedOnly` 保留。面板持续显示条件来源，并提供“清除局部”“设为共享”和“清除共享”；`RecentFrameCount` 保持 Events 局部工作集窗口，提升可共享条件时不会写入 Workspace Filter。Trace、Attributes、Buffs、Tags 和 Effects 保存真实 QueryStatus，并与 Events/State 一起使用统一空态投影；Trace 缺少选择时使用 Root Context 文案，缓存失效会清除旧状态。

未改变的边界：Workspace Filter 当前只正式驱动 Events，尚未让 State、Trace 和 Actor 详情消费不适用或待设计字段；共享标量冲突采用共享优先，不是严格数学交集。没有新增 Timeline、Frame Cursor、持久 Inspector、Bookmark、后台索引或 Runtime 控制能力；Event 历史恢复仍受当前已加载工作集限制。

测试：`BattleDebugDiagnosticViewModelTests` 新增 Workspace/局部 Filter 合并优先级、Filter 缓存键与固定 revision 分页失效、清除局部不修改 Workspace、Trace 与 Actor DTO QueryStatus 生命周期，以及 Root Context 空态文案用例。测试源码已完成编译；本批未执行 Unity Test Runner，没有 NUnit XML，不宣称用例通过。

构建：`AbilityKit.Demo.Moba.Diagnostics.Core.Tests.csproj` 完整项目引用构建成功，134 warnings、0 errors；警告为既有 Unity framework 程序集版本冲突与过时 API。目标源码与文档执行范围化 `git diff --check`；全仓检查仍被任务外 `NetworkBattleConfig.cs` 文件末尾新增空行阻断，该文件未修改或回退。

Unity 手工验收：尚未在 Play Mode 验证共享/局部来源、三种清除操作、最近帧不共享、Trace/Actor 空态和真实技能调查路径。同一项目由活动 Unity Editor 占用，因此未启动第二实例、未结束用户进程，也未代替用户操作现有 Editor。

已知限制：Channel 交集可以得到 None；当前只通过 ViewModel 合并测试锁定语义，没有新增 Store 专项行为测试。共享 Filter 仍不是跨 Events、State、Trace 和 Actor 的完整统一筛选，真实技能场景和窄窗口交互仍待手工验收。

当前能力文档更新：已登记 Events 共享/局部 Filter 的来源、优先级、清除与分页语义，Trace/Actor DTO 统一空态覆盖，以及构建、NUnit 和 Play Mode 验证边界。

## 第四十批：统一 Frame Cursor 可见闭环

目标：继续实施 Phase 1 统一上下文，让已有 Core Frame Cursor 从内部状态变成用户可见、可操作且与 Selection、History 和 Replay 一致的窗口级能力。

关键结果：Battle Debug 顶部新增独立 Frame Cursor 状态条，显示当前帧、跟随最新状态、变更来源、最新完整帧和稳定 Selection；支持手工固定帧、恢复跟随最新，以及 Replay 可用时定位真实逻辑世界。Event、Trace Root、Trace Node 和导航历史沿用 Workspace Selection 的稳定帧语义；Trace 节点选择记录 StartFrame 与 RootContextId，详情起止帧操作先更新 Workspace Frame Cursor，再按能力端口尝试 Replay Seek。

未改变的边界：Core Frame Cursor、Workspace State、Session Query 和 Runtime Store 契约均未修改。Live 与 Artifact 只维护诊断帧上下文，不因状态条或 Trace 帧按钮获得历史状态查询或 Replay Seek；Replay Seek 失败不回滚用户选择的诊断游标。一级工作区、Timeline、持久 Inspector、Bookmark 和跨全部面板的共享 Filter 仍未实现。

测试：`BattleDiagnosticCoreTests` 新增固定帧恢复跟随最新、跟随游标推进与固定游标不推进，以及 Workspace 后退/前进恢复 Selection Frame 和 `SelectionNavigation` 原因的覆盖。测试源码已纳入生成测试项目；本批未执行 Unity Test Runner，没有 NUnit XML，不宣称用例通过。

构建：`AbilityKit.Demo.Moba.Diagnostics.Core.Tests.csproj` 完整项目引用构建成功，134 warnings、0 errors；警告为既有 Unity framework 程序集版本冲突与过时 API。目标源码和四份文档范围化 `git diff --check` 通过，仅报告 `BattleDebugWindow.cs` 的 LF/CRLF 转换提示；生成 `.csproj` 无改动，未发现冲突标记。

Unity 手工验收：已将 Live、Replay、Artifact、Selection/History 帧恢复、Trace 起止帧和 720/960/1180 宽度加入手工验收清单；同一项目由活动 Unity Editor 占用，因此未启动第二实例、未结束用户进程，也未代替用户操作现有 Editor。

已知限制：状态条仍属于现有 IMGUI 工作区骨架，不是 Timeline 游标；最新完整帧依赖 Health 或离线数据源，来源不可用时不能恢复跟随最新。Replay Seek 失败目前通过既有窗口状态反馈边界处理，Trace 面板不新增独立错误状态。

当前能力文档更新：已登记统一 Frame Cursor、Selection/History 帧联动、Replay 能力边界和剩余 Timeline/Inspector 缺口。

## 第四十一批：持久 Selection Inspector 首批闭环

目标：继续收口 Phase 1 工作区，让稳定 Workspace Selection 在切换一级工作区或二级面板后仍有持续可见的详情入口，并保持只读 Session、稳定 ID 与真实不可用语义。

关键结果：新增无 UnityEditor 依赖的 Selection Inspector ViewModel 和独立 IMGUI 渲染器，首批覆盖 Actor、Event、Trace Root 与 Trace Node。Actor 按 Selection Frame 查询并使用 State revision 缓存；Event 按精确帧、固定 Event revision 和稳定 Sequence 恢复，单页 500 条、最多扫描四页；Trace 使用 Related Root 查询并按 Context ID 恢复节点。Inspector 以 Workspace Selection 为唯一来源，不保存面板私有对象引用；Evicted、Unavailable、NotCaptured、Partial 和 Truncated 等状态原样展示。

窗口集成：宽度不小于 960 时 Inspector 作为右栏，窄窗口降级到主工作区下方并可收起；栏宽限制在 260 至 480 像素，宽度与显示状态通过 EditorPrefs 持久化。详情复用既有 Actor、Trace、Config、Frame 和复制导航入口，切换 Actor/Diagnostics 工作区或二级面板不会清除当前 Inspector 上下文。

未改变的边界：Diagnostics Core、Runtime Store、Session Query 和 Selection DTO 契约均未修改；Inspector 不直接访问活动 Unit、Store 或 Runtime 容器，也不增加历史状态查询、后台完整 Event 索引或可变控制能力。Skill Runtime、专用 Config Inspector、Timeline 和跨全部面板的统一 Filter 仍属于后续工作。

测试：`BattleDebugDiagnosticViewModelTests` 新增六个聚焦用例，覆盖 Actor 选择帧与 State revision 缓存、Event 精确帧与固定 revision、第二页稳定恢复、四页扫描上限及 Partial/Truncated、Evicted 状态保留，以及 Trace Related Root 与 Context 匹配。Unity 2022.3.62f1 EditMode Test Runner 已生成有效 XML `moba_selection_inspector_test_results.xml`，完整程序集 373/373 通过、0 failed、0 skipped。

构建：`AbilityKit.Demo.Moba.Diagnostics.Core.Tests.csproj` 完整项目引用构建成功，175 warnings、0 errors；警告为工作区既有 Unity framework 程序集版本冲突、过时 API 和并发项目图告警。测试前仅检查并清理当前 `C:\Workspace\gitProject\AbilityKit\Unity` 工程关联进程，实际残余进程数为 0，未终止其他 Unity 工程。

Unity 手工验收：尚未在实际窗口完成 960 阈值、栏宽拖动、偏好恢复、按钮行为、长内容布局和不可用状态的视觉验收；真实技能场景、文件对话框与 Play Mode 验收也未执行，没有截图、录像或手工通过结论。

当前能力文档更新：已登记持久 Selection Inspector 的数据源、查询、缓存、布局、导航与不可用语义，并继续将 Skill Runtime、Config Inspector、Timeline 和跨面板 Filter 标记为未完成。

## 第四十二批：Config 稳定引用与持久 Inspector 投影

目标：继续收口 Phase 1 Inspector 导航，让 Event/Trace 等现有配置入口从一次性 Asset 跳转升级为持续可见、可重新解析且不丢失 SkillFlow PhaseId 的配置投影。

关键结果：Selection Inspector ViewModel 新增 Editor-only Config 状态，保存完整 `BattleDebugConfigReference(Kind, Id, PhaseId)` 和来源 Workspace Selection。配置通过权威 JSON Source Index 结构化解析，成功时显示源路径与行号，失败时保留源缺失、JSON、ID 或 Phase 错误；支持打开配置、重新解析和复制完整引用。`OpenConfig` 在执行原有 SkillFlow Inspector、Asset Selection、Ping 和外部编辑器动作前先建立持久投影并确保 Inspector 可见，因此定位失败也不会丢失错误上下文。来源 Workspace Selection 改变时投影自动清除，避免陈旧配置继续冒充当前选择。

未改变的边界：Diagnostics Core、Runtime Store、Session Query、`BattleDiagnosticSelection` 和 Navigation History 契约均未修改。Core Selection 只有数值 Id/RelatedId，不能无损表达字符串 PhaseId，因此本批不做哈希、数字编码或不透明映射；Config 投影不进入 Core History，不依赖 Runtime Session 或 Store revision。Skill Runtime 独立选择、Timeline、一级工作区和跨全部面板的统一 Filter 仍未实现。

测试：`BattleDebugDiagnosticViewModelTests` 新增三个聚焦用例，覆盖 SkillFlow 精确 PhaseId 保留、权威 Skill JSON 路径与行号解析，以及 Workspace Selection 改变后清除 Config 投影。测试源码已补齐，但当前尚未获得本批 Unity EditMode Test Runner XML，因此不宣称新增用例通过。

构建：Diagnostics Tests 与 Moba Editor 生成项目均已尝试完整引用构建，但被任务外 `SessionMobaWorldBootstrapFactory.cs` 对当前 `BattleSyncMode` 不存在的 `StateSync` 和 `Hybrid` 成员引用阻断；两个项目日志只发现这两个错误，未发现本批文件编译错误。未修改或回退该并发文件。目标源码范围化 `git diff --check` 已通过，仅报告 `BattleDebugWindow.cs` 的 LF/CRLF 转换提示。

Unity 验证：运行 Unity 2022.3.62f1 EditMode Test Runner，但项目脚本编译被任务外 `MobaLogicWorldDriveGateTests.cs` 对缺失 `MobaGameStartSpec`/`AbilityKit.Protocol.Moba` 引用的错误阻断，未进入 NUnit、未生成 XML，因此没有 total/passed/failed/skipped 统计。已将 Config Kind/Id/PhaseId、权威 JSON 路径/行号、SkillFlow 精确 Phase、持续错误、重新解析、复制引用、来源选择变化和 720/960 布局加入 `TESTING.md`；窗口视觉、文件打开行为、真实技能场景和 Play Mode 尚未执行，没有截图、录像或手工通过结论。

已知限制：Config 的稳定性当前只覆盖 Inspector 会话内投影和来源 Selection 绑定，后退/前进恢复来源诊断选择时不会同时恢复 Config；配置源变化需要手工刷新或重新解析。若后续要求 Config 进入 Core History，应先设计可跨 Editor/CLI 表达字符串身份的正式契约，而不是复用数值字段。

当前能力文档更新：已登记 Editor-only Config 持久投影、完整 PhaseId、权威源定位、错误与缓存语义，并将剩余缺口收敛为 Skill Runtime 独立选择、Config Core History、一级工作区、跨面板 Filter、Timeline 和真实场景验收。

## 后续记录格式

新增批次使用以下模板：

```text
## 第 N 批：主题

目标：
关键结果：
未改变的边界：
测试：
构建：
Unity 手工验收：
已知限制：
当前能力文档更新：
```

每批结束时同步更新 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md)，不要在历史记录中建立新的“当前事实”段落。

# MOBA 战斗诊断实施历史

> 本文记录已经完成的实施批次及当时的验证边界。
>
> 当前能力以 [CURRENT-CAPABILITIES.md](CURRENT-CAPABILITIES.md) 为准。历史记录中的“当前”“尚未”等措辞只描述对应批次结束时的状态。
>
> 最后整理：2026-07-20

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

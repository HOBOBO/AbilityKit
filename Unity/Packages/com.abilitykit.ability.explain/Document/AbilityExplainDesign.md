# Ability Explain 可解释化框架设计文档

## 一、文档定位

本文是 `com.abilitykit.ability.explain` 的 package canonical 设计文档，用于定义 Unity Editor 可解释化框架的职责、扩展协议、生命周期、当前实现边界和证据成熟度。

快速接入见 [README](../README.md)，可运行演示见 [MockIntegration](../Samples~/MockIntegration/MockIntegration.md)。本文描述的是当前源码事实，不把规划能力、示例 UI 或类型存在外推为生产闭环。

## 二、模块定位与非目标

Ability Explain 将一个业务实体解析为可浏览、可导航的解释结构，面向以下问题：

- 当前技能、效果或配置为什么得到这一结果；
- 哪个来源、条件、修饰或引用参与了结果；
- 哪些节点存在问题，用户应跳转到哪里修复；
- 两组上下文之间哪些解释节点发生变化；
- 当前解释森林中可见实体之间存在哪些引用关系。

本包负责：

- 解释实体标识、请求、结果和森林模型；
- Editor 扩展注册、选择和优先级仲裁；
- 窗口交互编排、列表筛选、详情和上下文 UI；
- Discovery 懒展开、Diff 标记和 Relation 投影；
- 导航目标的协议与分发。

本包不负责：

- 业务数值、条件或依赖关系的真实计算；
- 技能、效果、Timeline 或 Trigger 的运行时执行；
- 业务资产的持久化和版本迁移；
- 自动采集生产数据或生成完整依赖图；
- 替业务包决定稳定 `NodeId`、实体键和导航语义。

## 三、核心模型

### 3.1 实体与解析请求

`PipelineItemKey` 是解释实体的稳定键。Provider 查询并展示实体，Resolver 根据 `ExplainResolveRequest` 生成 `ExplainResolveResult`。

请求包含：

- 被解析的实体键；
- `ExplainResolveContext`；
- `ExplainResolveOptions`。

`ExplainResolveContext.Values` 是显式业务上下文通道，可承载构筑、强化、词条、开关和 UI 临时状态。Resolver 应只读取请求内显式输入，避免依赖无法复现的 Editor 单例。

### 3.2 解释森林

解释结果以三层结构组织：

1. `ExplainForest`：同一次解析的全部解释维度；
2. `ExplainTreeRoot`：一个独立维度或入口；
3. `ExplainNode`：标题、摘要、严重级别、来源、问题、动作和子节点。

长期参与 Diff、选择恢复或 Relation 的节点必须提供稳定、唯一的 `NodeId`。标题不是身份；使用随机值、数组位置或本地化文本作为身份会导致刷新漂移。

### 3.3 问题、动作与来源

- `ExplainIssue` 表达可诊断的问题及其导航目标；
- `ExplainAction` 表达用户可执行动作，可在窗口内切换实体或委托 Navigator；
- `ExplainSourceRef` 表达节点来源，可转换为资产、文件、表行或业务实体目标。

这些对象只描述意图。实际打开资产、定位文件或切换业务窗口由 Navigator 实现。

## 四、扩展注册与仲裁

`AbilityExplainRegistry` 使用进程内静态列表保存八类扩展：

| 类型 | 选择输入 | 作用 |
|---|---|---|
| `IEntityProvider` | 可选搜索词 | 查询实体和显示名称 |
| `IExplainResolver` | 可选解析请求 | 构建结果和 Discovery 展开 |
| `INavigator` | 可选导航目标 | 执行外部导航 |
| `IExplainContextEditorProvider` | 实体键 | 构建上下文编辑器 |
| `IExplainDetailsSectionProvider` | 节点与详情上下文 | 追加详情 UI |
| `IDiscoveryPolicy` | 当前没有实体参数的 Registry 选择 | 判断引用是否可发现 |
| `IExplainEntityListModule` | Entity Provider | 扩展筛选和分组 |
| `IExplainNodeContextMenuProvider` | 节点与菜单上下文 | 追加右键菜单 |

### 4.1 优先级

实现 `IRegistryPriority` 后，数值更大的实例优先。相同优先级不替换已选实例，因而保持先注册者优先。Registry 的去重依据是对象引用；两个同类型、同配置的新实例仍会重复注册。

### 4.2 扩展接口过滤

- `IEntityProviderEx.CanProvide(searchText)` 只在调用带搜索词的选择方法时生效；
- `IExplainResolverEx.CanResolve(request)` 只在传入非空请求时生效；
- `INavigatorEx.CanNavigateExt(target)` 只在传入非空目标时生效。

调用方使用无参数方法时，Registry 只能按优先级选实例，不能按当前输入判断适配性。扩展实现不能假设所有调用路径都会触发扩展过滤。

### 4.3 生命周期与清理

Registry 不拥有扩展实例的资源生命周期，也没有单项注销。注册方负责：

- 在 Editor 初始化和 Domain Reload 配置下避免重复注册；
- 确保静态实例不引用已销毁的窗口或资产对象；
- 在测试隔离或重载流程中明确执行清理；
- 自行释放扩展内部资源。

`ClearAll()` 当前清理七类列表，但遗漏 Context Editor Provider 列表，因此它不是完整复位 API。这是实现缺陷，测试和重载流程不得依赖其完全隔离。

## 五、窗口编排与所有权

`AbilityExplainWindowPresenter` 是 View 与扩展之间的编排器。

### 5.1 初始化和释放

初始化时 Presenter 订阅搜索、刷新、选中、节点动作、问题、Discovery、Relation 和上下文编辑等 View 事件。释放时会对称退订，并关闭已打开的上下文编辑窗口。

Presenter 拥有当前窗口会话状态，包括：

- 当前选中实体；
- 最近一次解释森林；
- Diff 基线；
- Relation 懒展开根缓存；
- 解析上下文是否绑定到当前实体。

View 不应直接调用业务 Resolver，也不应持有 Registry 扩展的生命周期。

### 5.2 实体刷新

当前刷新流程为：

1. 从 Registry 取得 Entity Provider；
2. 调用 Provider 的 `Query(searchText)`；
3. 由 Entity List Module 可选地追加筛选和分组；
4. 恢复或更新选中实体；
5. 触发森林解析。

当前 Presenter 先调用无参数 `GetEntityProvider()`，再把搜索词传给 `Query()`。因此多个 `IEntityProviderEx` 并存时，`CanProvide(searchText)` 收不到真实搜索词，可能选择错误 Provider。

### 5.3 森林刷新

当前刷新流程构建完整 `ExplainResolveRequest`，但 Resolver 选择使用无参数 `GetResolver()`。这会绕过 `IExplainResolverEx.CanResolve(request)`，多个 Resolver 并存时可能先选中不适配当前实体的最高优先级实例。

解析成功后，Presenter 更新 Issues、Actions、详情和 Forest/Relation 视图。解析失败时，业务 Resolver 应返回可诊断结果或明确失败，不能依赖窗口猜测业务原因。

### 5.4 导航

窗口内导航优先处理实体切换和 Discovery 等内部目标；外部目标委托 Navigator。当前外部分发先调用无参数 `GetNavigator()`，再调用 `CanNavigate(target)`，没有使用 `GetNavigator(target)`，因而绕过 `INavigatorEx.CanNavigateExt(target)` 的预筛选和目标级仲裁。

Details Sample 中的按钮使用带目标选择方法，说明两条调用路径当前语义不一致。

## 六、Discovery、Diff 与 Relation

### 6.1 Discovery

Discovery 允许 Resolver 在主森林中只暴露引用实体，用户展开时再通过 `TryExpandDiscoveredRoot()` 获取子树。它用于控制解析成本和信息密度，不保证自动递归出完整业务依赖图。

Discovery Policy 决定实体是否允许发现，Resolver 决定能否构建展开根。两者任何一方拒绝或解析失败，都应保留主森林可用性。

### 6.2 Diff

Diff 以 `NodeId` 建立基线和当前节点索引：

- 当前存在、基线不存在：Added；
- 两边存在且快照变化：Changed；
- 基线存在、当前不存在：Removed。

当前快照只比较 `Title`、`Severity` 和 `SummaryLines`。Actions、Issues、Source、Children 顺序及其他字段变化不会产生 Changed；没有 `NodeId` 的节点不参与 Diff。

`BuildForestWithRemoved()` 会把被删除节点合成为 `diff_removed` 根，但当前 Presenter 最终仍把原始 `result.Forest` 传给 Tree 或 Relation 渲染，而不是合成后的森林。因此 Added/Changed 标记可能出现，Removed 根当前可能不可见。合成根还使用随机标识，不适合作为跨刷新稳定身份。

### 6.3 Relation

Relation Graph Builder 从当前森林节点的 Source 和 Navigate Action 中提取实体引用，并可把已展开的 Discovery 子树接入关系图。

它是“当前解释结果的引用投影”，不是资产数据库、静态代码或运行时调用链的完整依赖图。以下情况会造成缺边或不稳定：

- Resolver 没有提供 Source 或 Navigate Action；
- 引用位于未展开的 Discovery 中；
- 节点缺少 `NodeId`，构建器生成随机临时标识；
- 当前 Presenter 的展开缓存跨实体没有显式清理；
- 普通 Relation 重绘没有始终带入展开缓存。

## 七、详情与上下文编辑

Context Editor Provider 为当前实体构建独立编辑 UI，并可更新 `ExplainResolveContext` 后请求重新解析。它不自动持久化业务数据，保存责任由 Provider 和业务包定义。

Details Section Provider 面向当前节点追加业务视图。多个 Provider 可同时生效，并按优先级排序。Sample 中的 Timeline Details 只生成固定事件行和导航按钮，不读取 ActionEditor 或运行时 Timeline 数据，不能作为 Timeline 调试闭环证据。

## 八、失败边界

| 场景 | 当前行为或风险 | 调用方责任 |
|---|---|---|
| 没有 Provider 或 Resolver | 列表或解释结果不可用 | 安装并注册业务适配器，提供可诊断提示 |
| 多扩展并存 | 无参数选择可能绕过扩展过滤 | 避免重叠注册；修复调用路径前不要依赖输入级仲裁 |
| 重复初始化 | 不同实例可重复进入静态列表 | 注册方保证幂等 |
| Registry 清理 | Context Editor Provider 未被清除 | 测试隔离不能只依赖 `ClearAll()` |
| Resolver 抛异常 | Presenter 没有统一业务恢复契约 | Resolver 在边界内转换为明确失败或诊断节点 |
| 节点无稳定 `NodeId` | Diff 忽略、Relation 身份漂移 | Resolver 提供稳定唯一标识 |
| Diff 删除节点 | 合成森林未进入最终渲染 | 在实现修复前不要宣称 Removed 可见 |
| Relation 懒展开 | 缓存可能跨实体残留 | 避免把当前图作为权威依赖图 |
| Navigator 不匹配 | 可能先选中错误实例后直接返回 | 注册范围互斥或统一目标级选择路径 |
| Details UI | 扩展自行构建 VisualElement | Provider 管理事件退订和外部资源 |

## 九、采用与证据成熟度

当前确认的采用证据是 `Samples~/MockIntegration`：

- Provider、Resolver 和 Navigator；
- Discovery Policy；
- Entity List Module；
- Context Editor Provider；
- Timeline Details Section Provider；
- Forest、Issues、Actions、Diff 和 Relation 演示数据。

Mock Navigator 只输出日志，Timeline Details 使用固定数据。它们证明 Editor 扩展面可演示，不证明生产导航、业务解析、Timeline 联调或发布质量。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | 核心模型、Registry、Presenter、Diff、Relation 和详情源码存在 |
| E1 | 已具备 | MockIntegration 提供 Editor 演示 |
| E2 | 未确认 | 未找到生产业务主链消费者 |
| E3 | 未确认 | 未找到专项自动测试 |
| E4 | 未确认 | 未找到 Smoke、Acceptance 或可复现 artifact |
| E5 | 未具备 | 未接入 CI 阻断、预算、发布或回滚责任 |

## 十、源码阅读路径

建议按以下顺序核对实现：

1. [AbilityExplainRegistry.cs](../Editor/Core/AbilityExplainRegistry.cs)：扩展注册、优先级与清理；
2. [AbilityExplainWindowPresenter.cs](../Editor/Window/AbilityExplainWindowPresenter.cs)：窗口生命周期和主交互链；
3. [ExplainForestDiff.cs](../Editor/Diff/ExplainForestDiff.cs)：Diff 身份和字段比较范围；
4. [ExplainRelationGraphBuilder.cs](../Editor/Relation/ExplainRelationGraphBuilder.cs)：关系投影来源；
5. [MockAbilityExplainIntegration.cs](../Samples~/MockIntegration/MockAbilityExplainIntegration.cs)：完整 Mock 接入；
6. [MockTimelineDetailsSectionProvider.cs](../Samples~/MockIntegration/MockTimelineDetailsSectionProvider.cs)：详情扩展示例。

## 十一、后续治理顺序

1. 修复 Presenter，使 Provider、Resolver 和 Navigator 统一使用带输入的 Registry 选择方法；
2. 让 Diff 合成森林真正进入 Tree/Relation 渲染，并为 Removed 根提供稳定身份；
3. 在实体切换、刷新和窗口释放时定义 Relation 展开缓存的清理策略；
4. 修复 `ClearAll()`，并增加注册、相同优先级、过滤和重载隔离测试；
5. 为 Diff 字段范围、无 `NodeId`、Discovery 失败和导航不匹配增加自动测试；
6. 接入至少一个真实业务 Provider/Resolver/Navigator，再升级 E2 结论；
7. 建立可复现 Editor Smoke 和 CI 门禁后，再声明 E4-E5。

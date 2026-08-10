# Ability-Kit BaseEditor 基础编辑器工具模块开发设计文档

## 一、文档定位

本文是 `com.abilitykit.base.editor` 的 package canonical 文档。BaseEditor 是 Unity Editor-only 混合工具包，当前包含：

- 可插拔窗口框架与 Json/Csv 导出器；
- Pool Monitor 诊断窗口；
- ActionEditor 预览相关示例代码；
- 历史 GameplayTag 编辑器和导出器。

这些目录不是一个已经统一闭环的 Editor 平台。使用者应按具体子模块核对源码和成熟度，不应把包名或目录存在等同于所有工具都已生产化。

## 二、职责边界

### 2.1 适合承担的职责

- 为 EditorWindow 提供列表、搜索、筛选、详情和插件扩展的基础骨架；
- 在数据已经由窗口自身加载后，提供通用导出器接口；
- 展示 Pool 调试快照，辅助 Play Mode 诊断；
- 为 ActionEditor 示例提供项目侧初始化和预览入口。

### 2.2 明确不承担的职责

- 不负责运行时业务数据查询、资产数据库同步或通用持久化；
- 不提供自动完善的链式 Window Builder；
- 不拥有独立 GameplayTags 包的权威模型和编辑器职责；
- 不保证 ActionEditor 预览覆盖所有动画、音频、粒子或信号语义；
- 不代表所有 Editor 工具都有自动测试、跨平台支持或发布门禁。

## 三、可插拔窗口框架

### 3.1 PlugableWindow

`PlugableWindow<TData, TConfig>` 提供以下基础行为：

- `Initialize(dataSource, plugins)` 保存插件列表并触发刷新；
- `RefreshData()` 通过可重写的 `LoadData()` 获取 `_allData`；
- 工具栏搜索和配置筛选；
- 列表项选择与详情区域；
- `IWindowPlugin<TData>` 的插件回调；
- Json/Csv Exporter 扩展点。

窗口实际数据来源是 `LoadData()` 虚方法。当前 `Initialize()` 接收的 `dataSource` 没有保存或使用，调用方不能据此认为传入集合会成为窗口数据源。要使用外部数据，必须在派生窗口中重写 `LoadData()` 或修改框架实现。

### 3.2 插件生命周期

窗口销毁时会通知插件 `OnDestroy()`，但当前没有与之对称的通用 Initialize、Enable 或 Disable 回调契约。插件如果订阅 Editor 事件、持有资源或注册回调，必须自行管理完整生命周期，不能依赖 BaseEditor 自动清理。

### 3.3 导出器

`JsonExporter<T>` 和 `CsvExporter<T>` 接收窗口当前数据列表并写入指定路径。它们是格式转换工具，不负责：

- 创建稳定文件名；
- 版本标记和 schema 迁移；
- 原子写入、权限处理或失败回滚；
- 证明 `IList<T>` 中所有多态或接口类型可被 `JsonUtility` 正确序列化。

尤其是 Json 导出直接使用 Unity 序列化能力，复杂泛型、接口、派生类型和引用图需要调用方单独验证。

## 四、WindowBuilder 当前边界

`WindowBuilder<TData, TConfig>` 暴露 Data、DrawDetail、Filter、Config、Plugins 等链式配置，但当前数据加载回调定义为 `Action<IEnumerable<TData>>`，而不是返回数据的 `Func<IEnumerable<TData>>`。

`Build()` 中先创建空局部变量，再把它传给回调和 `PlugableWindow.Initialize()`。回调无法把数据写回该局部变量，且窗口初始化又不使用 data。因此 Builder 的 Data 配置当前不能形成有效的数据注入链；DrawDetail、Filter 和部分 Config 配置也不能据此宣称已接入窗口行为。

文档和消费者应将 WindowBuilder 视为实验性 API，避免在生产工具中依赖其未验证的链式语义。建议修复方向是：

1. 使用返回数据的函数或明确的可变数据容器；
2. 在 `Initialize()` 中保存或消费数据源；
3. 为 Builder 各字段增加最小执行验证；
4. 以 Editor 测试锁定搜索、筛选、选择、插件和导出行为。

## 五、Pool Monitor

`PoolMonitorWindow` 从 `Pools.GetDebugSnapshots()` 读取快照，仅在 Play Mode 展示 Pool 调试信息。

当前实现特点：

- `OnEnable()` 订阅 `EditorApplication.update`；
- `OnDisable()` 对称退订；
- 默认约 0.5 秒刷新，最小刷新间隔约 0.1 秒；
- 支持按搜索文本过滤快照；
- 展示池名称、数量和运行状态等诊断行。

这是包内相对完整的 Editor 诊断工具，但仍未确认专项自动测试、截图 artifact、性能预算或 CI 门禁。它读取调试快照，不改变 Pool 所有权，也不负责释放池内对象。

## 六、ActionEditor 项目侧示例

`Editor/ActionEditorImpl/Initializer.cs` 通过 `[InitializeOnLoadMethod]` 注册第三方 ActionEditor 回调和 Editor update，但 `OnEditorUpdate()` 当前为空。打开 ActionEditor 时还会尝试切换到 `Assets/Scenes/SampleScene.unity`。

该实现具有明显的 Sample/实验属性：

- 可能改变用户当前编辑场景；
- 未确认保存提示；
- 未确认场景存在性检查；
- 不是通用 BaseEditor 窗口初始化契约。

`Preview/Sampler/AnimationSampler.cs` 当前为空类。其他 Preview/Sampler 的实现应逐个核对，不能把目录名描述成完整采样框架，也不能把 Editor 预览能力外推为运行时播放能力。

## 七、GameplayTag 遗留所有权

`Editor/GamplayTag` 保留旧 GameplayTag 数据库、窗口、Exporter 和 TreeView，目录名本身还存在历史拼写错误。独立 `com.abilitykit.gameplaytags/Editor` 已提供新版同名编辑器，并且 BaseEditor 依赖该包。

因此当前建议：

- 独立 GameplayTags 包拥有 authoritative model 和正式编辑器职责；
- BaseEditor 中的 GameplayTag 工具标记为 legacy/兼容代码；
- 新功能不要同时修改两套编辑器；
- 迁移前明确菜单、资产格式、导出格式和用户数据兼容策略。

## 八、采用证据与成熟度

已确认的相邻消费者包括 Trace 包的 `TraceTreeWindow` 等 Editor 工具，它证明可插拔窗口模式在 Editor 侧存在采用；Pool Monitor 也有完整的 Editor update 订阅和刷新逻辑。

当前未确认 BaseEditor 的统一测试套件、WindowBuilder 数据注入测试、Action Preview 验收或旧 GameplayTag 迁移计划。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | 窗口框架、导出器、Pool Monitor 和示例代码存在 |
| E1 | 已具备 | Editor 窗口和插件扩展可被调用 |
| E2 | 局部具备 | Trace 等相邻 Editor 工具采用窗口模式 |
| E3 | 未确认 | 未找到 BaseEditor 专项自动测试 |
| E4 | 未确认 | 未找到 Editor Smoke 或可复现截图/导出 artifact |
| E5 | 未具备 | 未接入统一 CI、版本发布和回滚责任 |

## 九、源码阅读路径

1. [PlugableWindow.cs](../Editor/Framework/Core/PlugableWindow.cs)：窗口数据、插件和导出器；
2. [WindowBuilder.cs](../Editor/Framework/Layout/WindowBuilder.cs)：Builder 数据链及当前断点；
3. [PoolMonitorWindow.cs](../Editor/PoolExtension/PoolMonitorWindow.cs)：Pool 诊断窗口生命周期；
4. [Initializer.cs](../Editor/ActionEditorImpl/Initializer.cs)：ActionEditor 项目侧初始化；
5. [AnimationSampler.cs](../Editor/ActionEditorImpl/Preview/Sampler/AnimationSampler.cs)：当前空采样器；
6. [GamplayTag](../Editor/GamplayTag)：遗留 GameplayTag 编辑器；
7. `com.abilitykit.gameplaytags/Editor`：新版 GameplayTag 权威编辑器实现。

## 十、后续治理顺序

1. 修复 WindowBuilder 到 PlugableWindow 的数据注入和配置接线；
2. 为窗口和插件增加对称生命周期，并测试事件退订；
3. 为导出器补充复杂类型、失败路径和文件写入策略；
4. 将 ActionEditor 示例的场景切换改为显式、可配置且有保存检查的行为；
5. 明确 Preview/Sampler 的支持矩阵，删除或标记空实现；
6. 制定旧 GameplayTag 编辑器迁移和下线计划；
7. 建立 Editor Smoke、导出 artifact 和 CI 责任后，再提高 E3-E5。

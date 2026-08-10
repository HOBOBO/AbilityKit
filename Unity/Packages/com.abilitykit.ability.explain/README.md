# Ability Explain

Ability Explain 是 AbilityKit 的 Unity Editor 可解释化框架。它把“一个技能、效果、配置或流水线实体为什么得到当前结果”表达为可浏览的解释森林，并提供实体查询、上下文编辑、问题定位、关系投影、差异标记和导航扩展。

本包只负责 Editor 侧解释协议与展示编排，不负责业务规则计算、运行时技能执行、资产保存或生产数据采集。业务包必须提供实体、解析和导航适配器。

## 快速接入

最小接入由三类扩展组成：

1. 实现 `IEntityProvider` 或 `IEntityProviderEx`，返回可解释的 `PipelineItemKey`。
2. 实现 `IExplainResolver` 或 `IExplainResolverEx`，把 `ExplainResolveRequest` 解析为 `ExplainResolveResult`。
3. 实现 `INavigator` 或 `INavigatorEx`，处理节点来源、问题和动作产生的 `NavigationTarget`。
4. 在 Editor 初始化阶段调用 `AbilityExplainRegistry.Register(...)` 注册实例。
5. Resolver 为长期可比较的节点提供稳定且唯一的 `NodeId`；缺少稳定标识时，Diff 和 Relation 的结果都可能漂移。
6. 在 `ExplainResolveContext.Values` 中传递构筑、强化、词条或 UI 状态；不要让展示层反向读取隐式业务单例。

可运行示例见 [MockIntegration.md](Samples~/MockIntegration/MockIntegration.md)，完整契约见 [AbilityExplainDesign.md](Document/AbilityExplainDesign.md)。

## 扩展面

Registry 当前接收八类扩展：

| 扩展 | 职责 |
|---|---|
| `IEntityProvider` | 查询实体并生成显示名称 |
| `IExplainResolver` | 构建解释森林、问题、动作和 Discovery 展开结果 |
| `INavigator` | 跳转到资产、文件、表行、自定义窗口或其他业务目标 |
| `IExplainContextEditorProvider` | 为选中实体构建上下文编辑 UI |
| `IExplainDetailsSectionProvider` | 向节点详情区追加业务面板 |
| `IDiscoveryPolicy` | 判断引用实体是否允许懒展开 |
| `IExplainEntityListModule` | 为实体列表增加筛选和分组 |
| `IExplainNodeContextMenuProvider` | 为解释节点增加上下文菜单 |

实现 `IRegistryPriority` 可参与优先级仲裁，数值越大越优先；相同优先级保持先注册者优先。扩展接口 `IEntityProviderEx`、`IExplainResolverEx` 和 `INavigatorEx` 可以声明更细的适用条件，但调用方必须使用带查询、请求或目标的 Registry 选择方法，过滤才会生效。

## 展示能力

- **Forest**：以多个 `ExplainTreeRoot` 表达同一实体的不同解释维度。
- **Issues 与 Actions**：节点可以暴露诊断问题和可执行导航动作。
- **Discovery**：引用实体按需展开，避免一次解析整个依赖图。
- **Diff**：按 `NodeId` 标记 Added、Changed、Removed；当前 Changed 只比较标题、严重级别和摘要行。
- **Relation**：从当前森林中可见的 Source 与 Navigate Action 投影实体关系，不等同于完整业务依赖图。
- **Details**：业务扩展可向详情区追加 UI；示例 Timeline 只是固定预览，不读取真实时间轴数据。

## 生命周期与已知边界

- Registry 使用静态列表，注册项跨窗口实例共享；注册方负责避免 Domain Reload 配置下的重复注册和陈旧实例。
- Registry 只按对象引用去重，没有单项注销。`ClearAll()` 当前没有清理 Context Editor Provider，不能视为完全复位。
- Presenter 会在初始化时订阅 View 事件，并在释放时对称退订和关闭上下文编辑窗口。
- 当前 Presenter 的实体、Resolver 和 Navigator 主路径使用无参数选择方法，可能绕过扩展接口的按查询、请求或目标过滤。
- Diff 会构建包含 Removed 根的合成森林，但当前最终渲染仍使用原森林，Removed 节点可能不可见。
- Relation 的懒展开缓存没有在实体切换时显式清理，且缺少 `NodeId` 时会生成临时标识，跨刷新结果可能不稳定。
- 本包当前证据以源码和 Editor Mock Sample 为主，没有确认生产业务消费者、专项自动测试或发布门禁。

## 证据成熟度

| 等级 | 当前证据 |
|---|---|
| E0 | 模型、Registry、Presenter、Diff、Relation 和详情扩展源码存在 |
| E1 | MockIntegration 覆盖实体、解析、导航、上下文、Discovery、列表和详情演示 |
| E2 | 未确认生产业务主链采用 |
| E3 | 未确认专项自动测试 |
| E4 | 未确认 Smoke、Acceptance 或可复现 artifact |
| E5 | 未接入 CI 阻断、质量预算或发布回滚责任 |

文档基线完成不代表上述实现缺口已经修复，也不代表运行时、测试或发布成熟度提升。

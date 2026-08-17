# Ability Explain 模拟集成

## 一、用途

本 Sample 用固定数据演示 Ability Explain 的 Editor 扩展面，适合验证窗口交互和学习接入协议。它不是生产业务集成、自动测试、真实 Timeline 数据源或资产导航实现。

框架快速入口见 [README](../../README.md)，完整语义见 [AbilityExplainDesign](../../Document/AbilityExplainDesign.md)。

## 二、启用与打开

1. 在 Unity Package Manager 中导入本包的 `MockIntegration` Sample。
2. 等待 Editor 脚本重新编译；带 `InitializeOnLoad` 的集成类会自动注册扩展。
3. 通过菜单 `Window/AbilityKit/Ability Explain` 打开窗口。
4. 在实体列表中选择 Mock Ability，观察解释森林、问题、动作和详情。
5. 修改“强化/构筑”上下文，刷新并观察节点摘要或 Diff 标记。
6. 切换 Relation 模式并展开可发现实体，观察当前 Forest 中可见引用的投影。

如果关闭 Domain Reload 或重复导入 Sample，静态 Registry 可能保留重复的新实例。当前 Registry 没有单项注销，`ClearAll()` 也遗漏 Context Editor Provider；遇到重复扩展时应通过完整 Editor 重载恢复，而不是把 Sample 当作支持热卸载的插件。

## 三、注册项

`MockAbilityExplainIntegration` 自动注册：

| 实现 | 接口 | 演示内容 |
|---|---|---|
| `MockEntityProvider` | `IEntityProviderEx` | 固定 Ability 实体、搜索和显示名称 |
| `MockExplainResolver` | `IExplainResolverEx` | Forest、Issues、Actions、Discovery、Diff 和 Relation 数据 |
| `MockNavigator` | `INavigatorEx` | 接收导航目标并输出 `Debug.Log` |
| `MockDiscoveryPolicy` | `IDiscoveryPolicy` | 判断 Mock 引用实体是否可展开 |
| `MockEntityListModule` | `IExplainEntityListModule` | 列表筛选和分组 |
| `MockContextEditorProvider` | `IExplainContextEditorProvider` | 构筑、强化和词条上下文 UI |

`MockTimelineDetailsSectionProvider` 由另一个 `InitializeOnLoad` 类注册 `IExplainDetailsSectionProvider`。它只在标题包含“发射子弹”的节点上显示固定 Timeline 预览行。

## 四、可观察场景

### 4.1 Forest 与问题定位

Resolver 构造多棵解释树，并在节点上附加摘要、严重级别、问题和导航动作。问题或动作被触发后，Mock Navigator 只记录目标，不会打开真实资产、文件、表格或业务窗口。

### 4.2 上下文编辑与 Diff

Context Editor 修改 `ExplainResolveContext.Values` 后可重新解析。开启 Diff 时，框架按稳定 `NodeId` 对比当前结果与基线。

当前实现限制：

- Changed 只比较标题、严重级别和摘要行；
- Actions、Issues、Source 等变化不会标记 Changed；
- 合成的 Removed 根当前没有进入 Presenter 的最终渲染；
- Sample 只能用于观察现有标记，不能证明 Diff 契约完整。

### 4.3 Discovery 与 Relation

Discovery 演示引用实体的按需展开。Relation 模式从当前 Forest 的 Source 和 Navigate Action 投影关系，展开后的 Discovery 可追加子树。

该图不是完整业务依赖图。缺失来源、未展开引用或不稳定 `NodeId` 都会造成缺边或身份漂移；当前展开缓存还可能跨实体保留。

### 4.4 时间线详情

Timeline Details 固定构造 Cast、SpawnProjectile 和 Hit 三行预览，并提供导航按钮。它不读取 ActionEditor 资产、ActionSchema DTO 或运行时事件，因此只证明详情扩展可以构建 UI。

## 五、示例边界

- 所有实体和解释结果都来自硬编码 Mock 数据；
- Navigator 仅输出日志，没有真实副作用；
- Context Editor 不代表生产资产保存和撤销语义；
- Timeline Details 不连接真实时间轴；
- 没有专项自动测试、Smoke artifact 或 CI 阻断；
- Sample 的正常展示不能证明多 Provider、多 Resolver 和多 Navigator 仲裁正确。

## 六、证据成熟度

| 等级 | 当前证据 |
|---|---|
| E0 | Sample 扩展实现源码存在 |
| E1 | 可在 Unity Editor 中手工打开并交互 |
| E2 | 未确认生产业务采用 |
| E3 | 未确认自动测试 |
| E4 | 未确认可复现 Smoke 或 Acceptance artifact |
| E5 | 未接入 CI 和发布门禁 |

## 七、源码入口

- [MockAbilityExplainIntegration.cs](MockAbilityExplainIntegration.cs)：六类主扩展和演示数据；
- [MockTimelineDetailsSectionProvider.cs](MockTimelineDetailsSectionProvider.cs)：固定 Timeline 详情扩展；
- [AbilityExplainWindow.cs](../../Editor/Window/AbilityExplainWindow.cs)：真实菜单入口；
- [AbilityExplainRegistry.cs](../../Editor/Core/AbilityExplainRegistry.cs)：静态注册和仲裁；
- [AbilityExplainWindowPresenter.cs](../../Editor/Window/AbilityExplainWindowPresenter.cs)：窗口实际调用路径。

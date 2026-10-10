# MOBA 表现层副本阶段组合

`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/battle_flow_profiles.json` 是阶段 feature 组合的可编辑配置。`DefaultProfileId` 选择默认对局；运行时先解析并校验 profile，再用同一份只读组合生成阶段 spec、检查 feature 工厂并建立安装计划。

## 配置格式

- `Version: 1` 保留原有 `Phases[].FeatureIds` 扁平列表。
- `Version: 2` 可定义 `Groups[]`，每组是有序的 `FeatureIds`。阶段可继续使用扁平列表，或改用有序的 `FeatureRefs`；单个阶段不能同时使用两种写法。
- `FeatureRefs` 中每项只填写 `FeatureId` 或 `GroupId` 之一。组只包含 feature，不嵌套引用。解析时按引用顺序展开，重复 feature、未知组和未注册的工厂都会在对局启动前失败。
- 空列表表示该阶段不安装 feature。`Battle.Prepare` 仍必须先安装 `context`、再安装 `session`；`Battle.LoadAssets` 仍必须包含 `loading_screen`。

六个战斗阶段、状态转移、清理点和推进动作由 `MobaFlowConfiguration` 固定。配置只决定各阶段安装哪些 feature 及其顺序。新增 feature 时，先在 `UnityMobaFeatureFactoryProvider` 注册工厂，再把 ID 加入 profile；调整状态或生命周期动作需要修改流程代码并补流程测试。

`TopologyId` 当前只支持 `moba.root-battle.v1`；旧配置省略该字段时按此拓扑处理。Profile 不改变 Root/Battle 状态机的阶段和转移。

`SessionModuleIds` 按模块 ID 选择实现，启动时还会校验 `events`、`room`、`snapshots`、`dispatch`、`lifecycle`、`network`、`replay`、`tick`、`plan` 九项必需能力。替换标准模块时，模块工厂目录还需实现 `IBattleSessionModuleCapabilityCatalog`，声明新模块提供的能力。模块依赖与排序继续由 `ModuleHost` 校验。

阶段安装失败会回滚本阶段已经安装的 feature，并异步清理可能部分挂载的 session。退出清理失败时保留未释放 feature 和 battle scope，流程进入故障态；`ShutdownAsync` 可重试清理。成功清理前不会切换到 Lobby 或启动下一场。

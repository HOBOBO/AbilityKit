# Triggering 加载行为基线

本页记录现行加载行为和已确认的决策。生产作者化、Scope 和发布流程仍以 [MOBA Trigger Authoring](MobaTriggerAuthoring.md) 为准。

## 已确认：正式环境允许目录规则覆盖聚合

2026-11-21 用户确认「正式环境允许覆盖聚合」。保留既有行为：运行时先加载必需的聚合文件，再用目录规则覆盖聚合中的同 ID Trigger 记录，后续校验以生效记录为准。该决定不改变聚合发布编译拒绝重复 ID 的规则，也未确认是否要求显式声明覆盖。

两端使用现有 `TriggerPlanDirectoryLoader.LoadDirectory` 与 `TriggerPlanJsonDatabase.MergeFrom(..., replaceExisting: true)`，不再由 Console 自行解释计划格式。覆盖替换整条记录，不逐字段合并；规则未提供执行根时，旧执行根不会残留。资源路径、枚举和失败策略仍由宿主决定；本轮没有新增加载 API、配置字段或诊断格式。

## 当前行为

| 输入 | 当前结果 |
| --- | --- |
| 必需聚合文件缺失或为空 | `TriggerPlanJsonDatabase.Load` 抛错；两端都不以目录规则替代聚合。 |
| 目录不存在、宿主不支持枚举、目录存在但无匹配文件 | MOBA 默认工厂得到空结果，保留聚合。`ThrowOnFileParseError` 只检查已枚举文件，不能区分这三种情况；Console 缺目录也会返回空集合。 |
| 已枚举文件缺失或 JSON 无效 | 框架记录带来源的错误（调用方提供诊断列表时）。MOBA 默认工厂抛错；Console 使用宽松选项，跳过失败文件并继续合并有效规则。 |
| 同 ID 同时出现在聚合与规则中 | 两端宿主均以 `replaceExisting: true` 合并，规则覆盖聚合；有效记录才进入后续校验。测试用未注册事件验证覆盖后的记录可产生阻断诊断。 |
| 同 ID 出现在多个目录文件中 | 目录库 `Records` 仍包含重复行，按 ID 查询指向最后枚举的记录。合库只消费每个 ID 的生效记录；`replaceExisting` 不改变源库内部的取值。不能把平台目录枚举顺序视为跨宿主优先级契约。 |
| 目标库已有重复 ID | 实际执行跨库合并后，目标库每个 ID 只保留一条生效记录，遍历、按 ID 查询和执行根一致。保留 ID 首次出现的位置，不把旧行保留在合并结果中。 |
| 多个发布源定义相同 Trigger ID | `TriggerPlanAggregateCompiler` 拒绝重复 ID，错误同时指向两个来源；这不同于运行时覆盖。 |

## 宿主职责

- Unity/MOBA 由 `MobaTriggerPlanDatabaseFactory`（`TriggerPlansStage.cs`）按 `MobaTriggerPlanLoadProfile` 选择输入和加载策略；Console 由 `ConsoleConfigModule` 和文本加载适配器提供文件路径与枚举。两端都调用包内加载和合并入口。
- MOBA 的领域检查由启动校验链执行，例如 `MobaTriggerPlanIntegrityValidator` 检查生效记录。框架不在 JSON 加载时隐式运行语义校验；旧自动校验包装器已从包内移除，不改变现有启动校验。
- MOBA 直接规则执行从同一条生效记录读取计划和执行根，不从直接加载的同 ID 旧行继承执行根。
- 包内源码职责见 [计划加载与校验](../Unity/Packages/com.abilitykit.triggering/Documentation~/SourceLayout.md#计划加载与校验)。

## 如何验收

- `dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj -c Release --filter FullyQualifiedName~TriggerPlanLoadingBaselineTests`：校验单文件失败、目录严格/宽松失败、空枚举、重复和覆盖。
- `dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj -c Release --filter FullyQualifiedName~MobaTriggerPlanLoadingBaselineTests --logger "console;verbosity=detailed"`：实际 Console 模块对照默认 MOBA 工厂；后者使用文件系统测试适配器读取 Unity `Resources` 源文件。测试逐 ID 输出候选来源和生效记录摘要（Scope、事件、Phase、Priority、执行根类型、动作 ID），并比较聚合和目录规则的 JSON 内容。来源清单按路径呈现，不代表宿主枚举优先级。
- Unity EditMode：`AbilityKit.Demo.Moba.Diagnostics.Core.Tests` 中的 `MobaTriggerResourceLoadingBaselineTests` 使用真实 `ResourcesTextAssetLoader`，对照 Console 发布目录的聚合、规则内容及生效记录摘要；不等同于 Unity Player 或完整 Battle Session 验收。

两组宿主测试不改生产配置，不运行发布流程，也不覆盖完整的技能引用、运行时注册或战斗回归。

## 待确认的加载规则

后续收口前，仍需确认是否要求显式声明允许覆盖的记录、如何区分必需目录和可选目录、两个宿主何时统一拒绝坏规则，以及同层重复 ID 是显式排序还是直接拒绝。未确认前保留当前行为，不改既有 Source 导入、发布和启动链。

优先建议单独确认**同一规则层的重复 ID**：继续允许「规则覆盖聚合」，但让两个规则文件定义同一 ID 成为可定位的冲突，报告 ID 和两个来源，不再依赖宿主枚举顺序。当前 Unity `Resources/ability/rules` 和 Console `Configs/luban/ability/rules` 各有两份 JSON，均无同层重复；尚未审计其他宿主及自定义加载配置。实施时应回归聚合覆盖、非重复输入和 Unity/Console 一致性。此建议不改变目录缺失或坏文件策略，尚未实施。

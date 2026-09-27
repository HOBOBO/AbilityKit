# AbilityKit 框架实现阶段性复盘与边界准则

## 一、复盘目的

本文件用于阶段性记录 AbilityKit 框架层实现的设计边界，避免在持续迭代中把接入方业务语义逐步沉淀到通用框架包，导致框架变重、变窄、难复用。

本次复盘来自 Context / Trace 优化过程中发现的问题：`FlowContext` 曾短暂加入 `Tags` 和 `GetFlowsByTag`，虽然能满足 Moba 示例分类查询，但 `tag` 本身并不是所有上下文都必然具备的通用概念，应由具体业务通过自定义属性、扩展服务或查询适配层表达。

## 二、核心原则

### 2.1 框架只提供机制，不预设业务语义

框架层可以提供：

- 生命周期：创建、运行、完成、取消、失败。
- 关系结构：父子关系、归属关系、根节点、上下文链路。
- 通用存储：实体、属性、快照、索引。
- 通用查询：按属性存在性、类型、谓词过滤。
- 通用导出：不绑定业务字段含义的数据传输结构。
- 通用事件：创建、更新、销毁、阶段变化。

框架层不应直接提供：

- Moba、技能、Buff、Projectile、Summon 等业务名词。
- `tag`、`category`、`camp`、`element`、`skillType` 等并非所有接入方都需要的分类语义。
- 默认业务校验规则。
- 业务状态机、业务生命周期规则。
- 业务日志、业务诊断路径和业务错误码。

### 2.2 业务扩展应通过接入方完成

当业务需要分类、筛选、诊断或规则时，优先使用以下扩展点：

- 自定义 `IProperty`：例如 Moba 可添加 `MobaContextCategoryProperty`、`MobaSkillContextProperty`。
- 扩展查询服务：例如 `IMobaContinuousRuntimeQueryService`、Moba 专用 Context 查询适配器。
- 业务验证器：例如 `MobaContextIntegrityRuntimeValidator` 位于 Moba runtime 包，而不是 Context 框架包。
- 业务 Trace metadata：Trace 框架只保存 `object Metadata` 或泛型 metadata，业务字段由接入包定义。
- 业务导出适配器：框架导出通用 DTO，业务如需转换为回放/日志协议，应在业务包再包一层。

## 三、Context 模块当前边界

### 3.1 应保留在框架层的能力

`com.abilitykit.context` 当前适合保留以下职责：

- `ContextRegistry` 管理上下文实体和属性。
- `FlowContext` 管理流程生命周期、父子 flow、owner entity、flow 内 entity 列表。
- `IProperty` 作为接入方可扩展的属性载体。
- `TraceContextProperty` 作为可选桥接属性，仅表达通用 trace id / context id / kind 编号，不绑定具体业务含义。
- `Query` 支持 `With<T>`、`Without<T>` 和通用谓词过滤。
- `SnapshotStorage` 保存通用快照。

### 3.2 不应进入 Context 框架层的能力

以下能力不应放进 `com.abilitykit.context`：

- flow tags / category / label 等分类字段。
- Moba buff / skill / projectile 的上下文属性。
- 针对具体业务的 flow 查询方法，如 `GetBuffFlows`、`GetSkillFlows`、`GetFlowsByTag`。
- 固定的业务校验规则，如“持续行为必须有 source actor”。

### 3.3 正确扩展示例

业务如果需要分类上下文实体，应定义接入方属性：

```csharp
public sealed class MobaContextCategoryProperty : IProperty
{
    public int TypeId => PropertyTypeRegistry.Instance.Register<MobaContextCategoryProperty>().Id;
    public string Category { get; }
}
```

然后使用框架通用查询：

```csharp
registry.Query()
    .CreateQuery()
    .With<MobaContextCategoryProperty>()
    .Where<MobaContextCategoryProperty>((_, category) => category.Category == "buff")
    .Execute();
```

这样框架只知道“有一个属性”和“按谓词过滤”，不知道 `buff` 是什么。

## 四、Trace 模块当前边界

### 4.1 应保留在框架层的能力

`com.abilitykit.trace` 当前适合保留以下职责：

- trace root / child 节点关系。
- root active 引用和生命周期。
- 节点快照和树快照。
- registry directory 和 registry event。
- 通用导出 DTO：节点 id、root id、parent id、kind 编号、kind name、结束帧、结束原因、子节点数、metadata object。
- 导出裁剪选项：最大节点数、是否只导出 active root、是否包含 metadata。

### 4.2 不应进入 Trace 框架层的能力

以下能力不应放进 `com.abilitykit.trace`：

- `MobaExecutionKind` 枚举及其玩法语义。
- skill / buff / projectile 等业务 kind 的固定解释。
- 技能回放协议、战斗日志协议、运营埋点协议。
- 业务元数据字段，如 `SkillConfigId`、`SourceActorId`、`TargetActorId`。
- 针对具体业务的 trace 健康检查。

### 4.3 正确扩展示例

Trace 框架可以提供 `TraceNodeExportDto`，但 Moba 业务如果需要导出战斗回放，应在可选 Moba Trace Adapter 或更上层包中转换：

- `TraceNodeExportDto.Kind` → `MobaExecutionKind`（只作为可选投影的显示/分析字段）。
- `TraceNodeExportDto.Metadata` → `MobaTraceMetadata`。
- 业务协议字段由 Moba exporter 决定。

## 五、Validation / Diagnostics / Rollback 边界

这些能力可以作为框架扩展包存在，但不应强塞进 core triggering/context/trace：

- Validation 框架包：提供验证器接口、报告结构、执行器、分级。
- Diagnostics 框架包：提供事件聚合、采样、输出 sink。
- Rollback 框架包：提供快照、回滚栈、恢复协议。

具体业务规则应由接入包定义：

- Moba 可以定义 `MobaContextIntegrityRuntimeValidator`。
- Shooter 可以定义 `ShooterPredictionDriftValidator`。
- ET 接入可以定义 `ETSessionLifecycleValidator`。

## 六、本次修正记录

### 6.1 已纠正的问题

- 移除 `FlowContext.Tags`。
- 移除 `FlowContext` 构造函数中的 `tags` 参数。
- 移除 `ContextRegistry.BeginFlow(... tags)`。
- 移除 `ContextRegistry.CreateFlow(... tags)`。
- 移除 `ContextRegistry.GetFlowsByTag()`。
- 测试改为通过接入方自定义 `IProperty` 表达 Moba 分类。

### 6.2 保留的合理改动

- 保留 `Query.Where(Func<long, bool>)`。
- 保留 `Query.Where<T>(Func<long, T, bool>)`。
- 保留 Trace 通用导出 DTO 和裁剪选项。
- 保留 Moba runtime 包内的上下文完整性验证器。

原因：这些能力本身不携带业务语义，只提供扩展机制或位于业务接入包内。

## 七、后续迭代检查清单

每次改动框架包前，需要检查：

- 新字段是否所有接入方都天然需要。
- 新 API 名称是否出现业务词汇。
- 新枚举是否可能被不同项目重新定义。
- 新查询方法是否可以改为通用 predicate 或接入方扩展方法。
- 新校验规则是否属于具体业务而不是框架不变量。
- 新 DTO 是否绑定了业务字段。
- 新事件是否描述框架生命周期，而不是业务动作。
- 示例项目中的便利能力是否被误提到框架包。

如果答案不明确，默认不进入框架层，先放到接入方包验证。

## 八、推荐迭代节奏

### 8.1 每个阶段结束做一次边界复盘

建议每完成一组框架能力后补充阶段性总结，至少包括：

- 本阶段新增的框架 API。
- 这些 API 的通用性说明。
- 被拒绝进入框架层的业务能力。
- 业务侧推荐扩展方式。
- 当前已知设计风险。
- 后续拆分或回收计划。

### 8.2 框架 API 升级顺序

推荐顺序：

1. 先在业务包中用扩展属性/服务验证需求。
2. 多个接入方出现相同模式后，再抽象为框架机制。
3. 抽象时只保留机制，不保留业务命名。
4. 增加框架测试验证机制本身。
5. 增加业务测试验证接入方式。

## 九、当前结论

Context 和 Trace 的拆分方向是合理的：

- Context 负责“流程上下文、属性、查询、快照”。
- Trace 负责“因果链路、树形溯源、生命周期、导出”。
- Moba 等业务包负责“具体语义、分类、校验、业务 metadata、业务查询服务”。

后续优化应继续强化框架的可组合机制，而不是在框架层预置业务概念。

## 十、待优化点与扩展性优先级

### P0：框架边界与 API 稳定性治理

优先级原因：如果框架边界继续模糊，后续每次能力增强都会增加业务耦合，最终导致 Context / Trace / Validation 等包无法被 Shooter、ET、Moba、Server 等不同接入方复用。

待优化点：

- 建立框架 API 评审清单，将“是否包含业务语义”作为合入前检查项。
- 为 Context / Trace / Rollback / Diagnostics / Validation 分别维护最小职责边界。
- 对新增 public API 做命名审查，避免出现 `skill`、`buff`、`moba`、`projectile`、`tag` 等业务倾向词。
- 对示例工程中沉淀出的便利能力，先保留在示例或业务包，不直接上提到框架。
- 为框架包增加 public API 快照或文档索引，便于阶段性审查 API 是否膨胀。

建议落点：

- 文档层：继续维护本文件作为框架阶段性复盘入口。
- 测试层：增加框架包纯机制测试，不依赖 Moba 语义。
- 流程层：每轮中长期优化结束后补一次“新增 API 是否仍然通用”的复盘。

验收标准：

- 框架包 public API 中不出现业务域名词。
- 示例包可以依赖框架扩展，框架包不反向依赖示例或业务。
- 新增能力能用至少两个不同业务接入场景解释其通用性。

### P0：Context 查询模型继续泛化

优先级原因：Context 是业务运行期可管理性的核心。如果查询能力不足，业务会倾向于把分类、标签、业务索引推进框架层，造成类似 `FlowContext.Tags` 的问题。

待优化点：

- 将当前 `Query.Where<T>` 的 registry-bound 限制做得更稳健，避免 standalone query 在 `Execute(registry)` 时无法使用 typed predicate。
- 增加组合谓词能力，如 `Any`、`All`、分组条件，但保持纯机制表达。
- 增加只读查询结果 DTO，便于工具层展示实体、属性类型、flow 关系。
- 增加通用 property projection，避免业务直接遍历内部结构。
- 明确 query 的执行时机、快照语义和线程安全边界。

建议落点：

- `com.abilitykit.context`：只增强 Query 的机制能力。
- 业务包：通过自定义 `IProperty` 和扩展查询服务表达业务分类。

验收标准：

- 不新增任何业务字段也能完成复杂筛选。
- Moba、Shooter 可以各自定义属性并复用同一套 Query API。
- Query API 不要求调用方了解 registry 内部索引结构。

### P1：Trace 导出与工具链协议分层

优先级原因：Trace 是溯源、诊断、回放、可视化的基础，但框架导出如果直接承载业务协议，会快速失去通用性。

待优化点：

- 明确 `TraceNodeExportDto` 是框架通用 DTO，不是业务日志协议。
- 增加导出排序策略，如按创建顺序、上下文 ID、树遍历顺序。
- 增加导出裁剪策略，如最大深度、最大子节点、只导出异常链路。
- 增加 metadata 序列化适配接口，但不内置业务 metadata 字段。
- 增加 Trace 导出文档，说明业务 exporter 应在接入方实现。

建议落点：

- `com.abilitykit.trace`：提供通用导出选项、遍历策略、metadata adapter 接口。
- `com.abilitykit.demo.moba.trace.adapter`：实现 MobaTraceExporter，将 `MobaTraceMetadata` 转成战斗日志或回放协议；runtime 不依赖该 exporter。

验收标准：

- Trace 框架导出不依赖 Moba 类型。
- 业务 exporter 可以完全在业务包中实现。
- 工具层可选择带 metadata 或不带 metadata 的安全导出。

### P1：Validation / Diagnostics 扩展包产品化

优先级原因：复杂商业项目需要运行前验证、运行时诊断、异常归因，但这些能力应是可组合扩展包，而不是散落在业务或 core 包中。

待优化点：

- 抽象通用 Validation 包：validator、report、severity、contract、runner。
- 抽象通用 Diagnostics 包：event、sink、sampling、scope、category。
- 支持业务包注册自己的 validator 和 diagnostics sink。
- 支持按阶段运行验证，如 bootstrap、battle start、runtime tick、shutdown。
- 支持验证报告导出为文本、结构化 DTO、工具面板数据。

建议落点：

- 新增或强化 `com.abilitykit.validation` / `com.abilitykit.diagnostics`。
- Moba 当前验证器可逐步迁移到通用接口之上，但规则仍留在 Moba 包。

验收标准：

- Moba / Shooter 能共用同一个验证框架接口。
- 具体校验规则仍由业务包提供。
- 启动阻断、非阻断 warning、info 可统一展示。

### P1：Rollback 与 Snapshot 的接口统一

优先级原因：Rollback 是复杂战斗、预测同步、服务器校正的重要能力，但不应和 Triggering 或具体业务强绑定。

待优化点：

- 明确 SnapshotStorage、Rollback、Trace 的协作边界。
- 统一 snapshot id、frame、version、owner、scope 等通用元信息。
- 提供 rollback operation result 和失败原因模型。
- 支持业务自定义 snapshot payload，不内置战斗字段。
- 支持按 flow / trace / frame 定位可回滚范围。

建议落点：

- Rollback 扩展包负责机制。
- Context 负责上下文实体和快照存取。
- Trace 负责定位因果链路。
- 业务包负责如何恢复具体状态。

验收标准：

- Rollback 不依赖 Moba 或 Shooter 类型。
- 业务只实现 snapshot payload 和 restore adapter。
- 通过正式 Context 或稳定业务身份定位回滚目标；Trace 只用于解释和验收，不作为恢复输入。

### P2：Context / Trace 联动的可观测性增强

优先级原因：Context 是执行身份和生命周期权威，Trace 通过只读 observer 可选投影“为什么产生这些状态”。两者联动后对调试复杂项目价值很高，但业务正确性不能依赖 Trace 是否安装。

待优化点：

- 维护 `IContextLifecycleSource<TNode>` 的 observer bridge 文档和异常隔离测试。
- 增加从 context entity 找 trace node、从 trace node 找 context entity 的通用索引方式。
- 增加调试视图 DTO，但只包含 id、kind、phase、parent/root 等通用字段。
- 支持导出某个 flow 下关联的 trace roots。
- 支持按 trace root 查询关联 context entities。

建议落点：

- `TraceContextProperty` 由 Trace 包提供，作为可选属性桥；Context 包不反向依赖 Trace。
- Trace 包不依赖 Context 包的业务属性。
- 工具或业务包负责把 bridge 展示为具体战斗含义。

验收标准：

- 联动 DTO 不出现业务字段。
- 任意业务都可通过自定义 metadata/property 获得自己的展示效果。
- 能支持调试、日志、回放工具的基础数据需求。

### P2：生命周期与资源治理

优先级原因：Context 和 Trace 如果长期运行在战斗服或编辑器工具中，需要避免泄漏、无限增长和难以清理的问题。

待优化点：

- Context flow/entity 的生命周期清理策略。
- Trace root/node 的保留策略、引用计数策略、过期裁剪策略。
- Snapshot 的容量限制和淘汰策略。
- 事件订阅的释放检查和泄漏诊断。
- 长局战斗或压测下的内存占用基准。

建议落点：

- 框架包提供通用 retention policy 接口。
- 业务包决定具体保留多久、哪些 trace 必须保留。

验收标准：

- 长时间运行不会无限堆积 context/trace/snapshot。
- 业务能配置保留策略，不需要改框架源码。
- 工具层能看到当前保留数量和裁剪原因。

### P3：开发者体验与文档完善

优先级原因：框架机制越通用，越需要清晰示例告诉业务如何正确扩展，否则接入方仍可能误把业务能力推回框架。

待优化点：

- 增加 Context 最小接入示例。
- 增加 Trace 最小接入示例。
- 增加“业务属性扩展而不是框架字段扩展”的示例。
- 增加 Validation / Diagnostics / Rollback 组合示例。
- 增加 API 使用反例，如 `FlowContext.Tags` 为什么不合适。

建议落点：

- 包内 Document 记录框架 API。
- `Docs` 记录跨包架构和阶段性复盘。
- 示例工程记录业务接入方式。

验收标准：

- 新接入方能在不阅读 Moba 代码的情况下使用 Context/Trace。
- 文档能明确告诉用户业务分类应该用自定义属性或扩展服务。
- 示例不暗示框架应内置业务语义。

## 十一、当前推荐推进顺序

短期优先推进：

1. P0：Context Query typed predicate 的 registry 绑定问题修正。
2. P0：框架 API 边界审查清单常态化。
3. P1：Trace 导出排序、深度裁剪、metadata adapter。
4. P1：抽象通用 Validation / Diagnostics 接口，业务规则留在接入方。
5. P1：Rollback / Snapshot 接口统一。

中期再推进：

1. P2：Context-Trace bridge 的通用调试 DTO。
2. P2：retention policy 和资源治理。
3. P3：开发者文档、最小接入样例、反例说明。

暂不建议推进：

- 在 Context flow 上增加任何业务分类字段。
- 在 Trace 框架里加入业务 kind 枚举或业务 metadata 字段。
- 在 Triggering core 中直接加入 Rollback / Validation / Diagnostics 业务流程。
- 因单一示例项目便利性而扩大框架 public API。

# Ability-Kit Context 上下文注册与快照模块开发设计文档

> **阅读对象**：需要理解 Ability-Kit 上下文数据容器、流程上下文、实体属性注册、快照存取机制的框架开发者。
>
> **文档目标**：说明 Context 模块解决什么问题、边界在哪里、核心类型如何协作，以及后续接入 Triggering、Trace、Combat、FrameSync 等模块时应注意哪些约束。

---

## 阅读入口与术语

本文以 2026-09-17 的实现为准，描述基础包，不把 MOBA 业务适配能力视为基础包能力。

| 名称 | 回答的问题 | 不应混用为 |
|------|------------|------------|
| `AbilityKit.Context` | 如何按实体 ID 注册属性、解析值及保存快照 | 效果因果树、完整 ECS、权威战斗状态 |
| MOBA 执行 Context | 本次条件/行为执行需要什么事实与来源 | 跨帧活对象的永久容器 |
| Trace | 谁派生了谁、节点是否结束 | 实时属性仓库 |
| MOBA Runtime Context | 如何通过独立 ID 读取 Buff 实时值或最终快照 | Trace ID、技能 runtime handle、逐帧历史 |

关联阅读：[Trace 设计](../../com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md)、[MOBA 整体上下文指南](../../com.abilitykit.demo.moba.runtime/Runtime/Docs/MobaCombatContextDesignGuide.md)、[Runtime Context 设计](../../com.abilitykit.demo.moba.runtime/Document/RuntimeContext运行时值与快照设计文档.md)。

## 一、设计理念：为什么需要 Context 模块

Context 模块提供轻量的运行时上下文注册中心。它不试图替代完整 ECS，而是为技能、触发、战斗计算、快照回放等模块提供一套“按流程组织实体、按实体 ID 组织属性和快照”的基础能力。

典型痛点包括：

| 问题 | 具体表现 | Context 的处理方式 |
|------|----------|-------------------|
| 流程上下文散落 | 一次技能、一段命中、一轮结算缺少统一边界 | 用 `FlowContext` 表达 flow/session/scope |
| 临时上下文散落 | 技能释放者、目标、参数、运行状态散落在多个对象中 | 用 `ContextRegistry` 按实体 ID 管理属性集合 |
| 属性类型识别不统一 | 模块之间难以统一判断“是否具备某类数据” | 通过 `PropertyTypeRegistry` 为属性类型分配类型 ID |
| 快照追踪困难 | 临时实体销毁后仍需要回溯来源、Owner 或历史状态 | 用 `SnapshotStorage` 独立保存 `IContextSnapshot` |
| 溯源弱关联 | Context 与 Trace 直接互相依赖会导致包边界变重 | 用 `TraceContextProperty` 只保存 trace id |

核心思想是：Flow 表示一次流程边界，Entity 表示流程中的运行时对象，Property 表示对象能力或状态，Snapshot 表示可留存的历史视图。

---

## 二、模块边界

### 2.1 Context 负责什么

- 分配和维护上下文实体 ID。
- 创建和管理流程级 `FlowContext`，记录父子 flow、owner、阶段和包含的 entity。
- 为实体挂载、读取、覆盖、移除实现了 `IProperty` 的属性对象。
- 通过属性类型查询实体集合，并维护增量索引。
- 在属性、实体、flow 生命周期变化时派发事件。
- 保存实体快照，并按 `SourceEntityId`、`OwnerEntityId` 建立反向索引。
- 提供可选 `TraceContextProperty`，让业务把 trace root/context id 挂到 context entity 上。

### 2.2 Context 不负责什么

- 不负责系统调度，不会主动 Tick。
- 不负责组件序列化协议，只保存调用者传入的快照对象。
- 不负责 ECS 级别的 archetype、chunk、稀疏集合优化。
- 不负责属性对象的深拷贝和不可变性，需要调用者自行约束。
- 不负责 Unity 场景对象或 GameObject 生命周期。
- 不直接依赖 Trace 包；Trace 关联通过属性或适配器弱耦合完成。

---

## 三、目录结构

| 路径 | 职责 |
|------|------|
| `Runtime/Context/FlowContext.cs` | 流程上下文、流程阶段和作用域封装 |
| `Runtime/Context/TraceContextProperty.cs` | Trace id 弱关联属性和扩展方法 |
| `Runtime/Registry/ContextRegistry.cs` | 上下文实体、属性、flow 的注册中心 |
| `Runtime/Property/IProperty.cs` | 属性对象的基础标记接口 |
| `Runtime/Property/PropertyType.cs` | 属性类型描述和类型 ID 注册表 |
| `Runtime/Events/ContextEvent*.cs` | 上下文事件、事件类型、事件委托定义 |
| `Runtime/Query/Query.cs` | 基于属性类型的查询封装 |
| `Runtime/Snapshot/IContextSnapshot.cs` | 快照基础接口、版本快照接口和快照记录 |
| `Runtime/Snapshot/ISnapshotAccessor.cs` | 快照访问接口 |
| `Runtime/Snapshot/SnapshotStorage.cs` | 快照保存、查询、销毁标记和索引维护 |
| `Runtime/Internal/TimeUtil.cs` | 内部时间戳工具 |

---

## 四、核心类型与职责

### 4.1 FlowContext

`FlowContext` 表示一次流程上下文，例如一次技能释放、一次命中结算、一段状态机流程。它记录：

- `FlowId`：流程 ID。
- `ParentFlowId`：父流程 ID，可表达流程嵌套。
- `OwnerEntityId`：流程归属实体。
- `Phase`：`Created`、`Running`、`Completed`、`Cancelled`、`Failed`。
- `EntityIds`：挂在该流程下的上下文实体。
- `ChildFlowIds`：子流程。

`FlowContextScope` 适合用 `using` 包裹业务流程：进入时创建并置为 running，释放时按默认阶段完成，也可以显式 `Complete`、`Cancel` 或 `Fail`。

### 4.2 ContextRegistry

`ContextRegistry` 是本包的核心入口。内部维护：

- `_entities`：`entityId -> EntityData`，保存实体和属性字典。
- `_flows`：`flowId -> FlowContext`，保存流程上下文。
- `_entitiesByPropertyType`：属性类型到实体集合的增量索引。
- `_globalHandlers`：全局事件订阅者。
- `_idHandlers`：指定实体事件订阅者。
- `_lock`：保护实体、属性、flow 和订阅列表的同步锁。

对外能力：

| 方法 | 行为 |
|------|------|
| `Create()` | 创建无 flow 归属实体并返回 `EntityBuilder` |
| `CreateInFlow(flowId)` | 创建归属指定 flow 的实体 |
| `RestoreEntity(entityId)` | 显式恢复无 flow 身份；拒绝覆盖已有实体，只推进分配游标 |
| `BeginFlow(...)` | 创建 flow scope，适合流程级上下文管理 |
| `SetFlowPhase(flowId, phase)` | 更新 flow 阶段并派发事件 |
| `Destroy(entityId)` | 派发 Destroying，移除实体和属性索引，派发 Destroyed |
| `Add<T>` / `Set<T>` | 注册属性类型 ID，写入属性，更新索引并派发 Updated |
| `Get<T>` / `Has<T>` | 按属性类型读取实体属性 |
| `Remove<T>` | 移除属性，更新索引并派发 Updated |
| `GetEntitiesWith<T>` | 查询拥有某类属性的实体 ID |
| `Query()` | 创建绑定当前 registry 的查询构建器 |
| `Clear()` | 逐个销毁当前实体，并清理 flow 与索引 |

事件处理器现在在锁外派发。注册表会先在锁内完成状态变更并复制订阅列表，然后释放锁再调用 handler，避免业务回调导致锁内重入和长时间阻塞。

观察器异常逐个隔离，通过可选 `EventHandlerException` 报告；报告回调异常也被隔离，不改变创建、更新、销毁的结果。这不是事务回调，不能用于否决状态变更。

### 4.3 Query

`Query` 支持属性存在与排除条件：

```csharp
var ids = registry.Query()
    .CreateQuery()
    .With<HealthProperty>()
    .Without<DeadProperty>()
    .Execute();
```

旧式 `query.Execute(registry)` 仍可使用，便于兼容已有调用。

### 4.4 SnapshotStorage

`SnapshotStorage` 独立于 `ContextRegistry`。它保存 `IContextSnapshot`，并根据快照是否实现额外接口建立索引：

| 接口 | 含义 | 索引/记录 |
|------|------|----------|
| `IContextSnapshot` | 快照基础信息，至少包含 `EntityId` | `_snapshots` |
| `IVersionedContextSnapshot` | 提供业务版本和帧号 | `ContextSnapshotRecord` |
| `ISourceContext` | 快照来源实体 | `_bySource` |
| `IOwnerContext` | 快照归属实体 | `_byOwner` |
| `IDestroyableSnapshot` | 可标记销毁状态 | `MarkDestroyed` |

重复保存同一实体快照时会先移除旧 source/owner 索引，再重建新索引，避免重复 ID 或旧索引残留。若快照未实现 `IVersionedContextSnapshot`，存储层会按 entity 自动递增版本。

旧 `Save(IContextSnapshot)` 保留每个 EntityId 一份的兼容语义；它不保证数据独立或不可变，不适用于历史事实查询。实现 `IImmutableContextSnapshot` 的新类型必须走 `SaveManaged`，禁止静默退回旧存储路径。实体仍有受管历史时，旧 Save 拒绝覆盖，须由逻辑宿主先显式 Remove。

受管快照保存多份记录，默认总容量 4096、每实体 16，可在构造存储时配置。`Count` 是有最新快照的实体数量，`HistoryCount` 是受管记录数量；容量不约束旧兼容快照。所谓保存/持久化是内存留存，不是落盘或网络序列化。Source/Owner 索引仍只查询各实体最新保存记录，ID 语义由业务约定，基础包不会证明它属于哪个注册表。

#### 4.4.1 类型、身份与不可变契约

宿主通过 `RegisterType<T>(typeId, schemaVersion, purpose)` 显式注册具体快照类型。稳定 TypeId 不依赖 CLR 类名，重复相同注册幂等；同一 CLR 类型的不同注册或跨类型重复 TypeId 均拒绝。注册属于存储实例，不是跨世界静态表；Clear 清数据但保留类型注册。

| 信息 | 语义 |
|------|------|
| `TypeId` / `SchemaVersion` | 载荷种类与结构版本，不是数据修订号 |
| `Purpose` | Observation 或 Recovery；不同用途应使用不同载荷类型，基础存储不提供恢复协议 |
| `StorageId` / `SnapshotId` | 存储实例与单份记录身份；SnapshotId 单调分配，Clear/回滚不回退 |
| `EntityId` / `Generation` | 上下文与实例代次；Generation 由逻辑宿主提供，不从业务 Version 猜测 |
| `Frame` / `Kind` | 逻辑采集帧与业务时刻标签，如 applied、executed、removed；同帧允许多份 |
| `Version` | 兼容业务载荷提供的版本，不承担 Generation 或 SchemaVersion 职责 |
| `SavedAtMs` / `IsDestroyed` | 入库时间与采集时的销毁事实，不随后续生命周期改变 |

`IImmutableContextSnapshot` 是载荷实现契约：构造时复制必要字段，不暴露运行时可变引用；集合也须复制并只读。存储不做反射深拷贝，不能自动证明实现类遵守契约。受管路径拒绝 `IDestroyableSnapshot`，销毁事实在 capture metadata 中一次写入，旧 MarkDestroyed 只服务兼容对象，不修改受管载荷或历史记录。

#### 4.4.2 精确查询与保留管理

SaveManaged 返回 `ContextSnapshotReference`，历史消费者应保存这份引用，而不是以后仅按 EntityId 取最新值。`Query` 校验所属 StorageId、EntityId 和 Generation；记录已淘汰/删除返回 Unavailable，尚未分配的 ID 返回 NotCaptured，无效或不匹配引用分别返回 InvalidReference/IdentityMismatch。

`ReadSnapshot<TSnapshot, TValue>(reference, key)` 只读指定载荷，另外区分 TypeMismatch 和 FieldMissing；不会补默认值、读实时 provider 或切到其他快照。`GetHistory(entityId, generation)` 列出该代次的记录；`TryGetLatest<T>(entityId, generation, kind, out record)` 按保存顺序选最新匹配项，不按 Frame 排序，也不是任意历史帧的插值查询。

容量自动淘汰最早保存且未 retain 的受管记录。逻辑宿主可调用 `PruneBeforeFrame` 做帧窗口淘汰，也可用 `Retain` 的 IDisposable 句柄暂时保护必要记录；释放幂等。全部容量被保护时，TrySaveManaged 返回 false，不分配 ID、不改现有数据；SaveManaged 则抛异常。观察快照采集失败不得阻断战斗生命周期。

`RemoveSnapshot`、Remove(entityId)、RemoveFromEntityId 和 Clear 是宿主显式失效操作，即使存在 retain 也会删除，旧引用/句柄不会别名新记录。删除最新项会重建最新视图和来源/归属索引。历史查询与帧清理只作用于受管记录，不把旧兼容对象伪装成历史记录。

工具应依赖 `IContextSnapshotReader`，仅查询类型、记录、历史和字段；不注册、保存、retain 或淘汰。窗口关闭清理工具自己的缓存/订阅，不清除逻辑存储。采集、帧窗口和世界结束清理仍由逻辑宿主管理。

### 4.5 TraceContextProperty

`TraceContextProperty` 是 Context 与 Trace 的弱桥接点。Context 包不引用 Trace 包，只保存：

- `RootTraceId`
- `TraceContextId`
- `TraceKind`

业务可以在创建 context entity 时调用 `WithTrace(rootId, contextId, kind)`，工具层或诊断层再按需把这些 id 关联到 Trace 注册表。

---

## 五、执行流程

### 5.1 创建流程和实体

```csharp
using (var flow = registry.BeginFlow("SkillCast", ownerEntityId: casterId))
{
    var entityId = flow.Create()
        .With(new HealthProperty())
        .WithTrace(rootTraceId, traceContextId)
        .Build();

    flow.Complete();
}
```

### 5.2 销毁实体

`Destroy(entityId)` 会先派发 Destroying，再从 `_entities` 移除实体、清理属性索引、从 flow 中解除绑定，最后派发 Destroyed。快照不会自动删除；需要保留最终事实时应在销毁前复制数据，并将销毁事实写入受管 capture metadata。只有旧兼容快照可在销毁后调用 `SnapshotStorage.MarkDestroyed` 修改标记。

### 5.3 查询实体

`GetEntitiesWith<T>` 读取增量索引，不再每次扫描全部实体。`Query` 可以组合 With/Without 条件，适合常见的运行时筛选。

### 5.4 统一值读取

`ContextValueResolver` 将实体属性、外部实时 provider 和快照组合为只读解析入口，不主动创建实体或生成快照。

| 读取模式 | 优先级 |
|----------|--------|
| `RealtimeThenSnapshot` | 实时 provider / 注册属性，未命中再读快照 |
| `RealtimeOnly` | 仅实时 provider / 注册属性 |
| `SnapshotOnly` | 仅已保存快照 |
| `SnapshotThenRealtime` | 快照未命中后再读实时 |

实时 provider 是对业务真值的投影，不要求把每次数值变化写回 Registry。Registry.Destroy 不自动解绑 provider；宿主必须协调解绑，否则解析器仍可能读到 provider。

`GetValue` 未命中时可返回 `Found=true, Source=DefaultValue`，不要只检查 Found 来判断实际数据命中；`TryGetValue` 排除默认值。基础解析器不校验业务 Version，也不保证两次读取来自同一原子快照。

新快照使用 `IImmutableContextSnapshot`，通过 `TryGetValue` 明确表达键缺失；同时实现 provider 和旧 accessor 时，字段缺失不再回退 accessor 伪装命中。只有旧 `ISnapshotAccessor.GetValue` 的类型仍没有命中标志，兼容路径可能把默认值视为快照命中。属性级 `GetProperty` 回退也要求快照能提供该属性对象，不能假设任意字段快照都可还原属性。历史事实读取使用带完整快照引用的 ReadSnapshot，而不是这些按实体最新视图的回退入口。

### 5.5 实时实例引用与精确读取

`ContextEntityReference` 保存 RegistryId、EntityId 和 Generation。RegistryId 每个注册表实例唯一；Generation 在实体创建或 RestoreEntity 时单调分配，Clear 与游标回滚均不重置。恢复的业务 EntityId 可以相同，但本地实例身份不同。通过 `TryGetReference` 只读取当前身份，不创建实体或快照；跨注册表、旧代次、缺失实体分别由 Query 返回 IdentityMismatch 或 Unavailable，无效引用返回 InvalidReference。

`ReadRealtimeProperty<TProperty>(reference)` 只解析该实例的实时属性，`ReadRealtime<TValue,TProperty>(reference,key)` 从同一属性对象读字段。它们不回退快照，不返回默认值伪装命中，也不在查询时注册新属性类型。PropertyMissing 与 FieldMissing 独立于身份失败；真实零值正常命中，默认结果不视为成功。

外部 provider 返回属性后重新校验实体代次，字段读取后也重新校验，拒绝读取过程中销毁并复用 ID 的结果。这不是对可变业务对象的跨线程事务或自动深拷贝：provider 应返回一致的属性视图，宿主仍需在模拟线程协调。原 long ID 的 Get/GetValue 等入口保留兼容，不具备实例代次校验；历史快照必须使用独立 ContextSnapshotReference，不能把实时引用当作某份历史记录。

### 5.6 恢复身份与回滚不是同一能力

`RestoreEntity` 只建立指定 ID 的空实体并发布 Created，不恢复属性、flow、实时 provider 或快照；ID 必须为正且小于 long.MaxValue。宿主应先校验冲突，再重建领域状态与绑定。

`ValidateRollbackEntityCursor` 在删除前检查确认实体存在且其 ID 小于恢复游标。`RestoreRollbackEntityCursor` 先完成相同预检，再移除非确认实体并回退分配游标；非法参数、缺失确认实体或确认 ID 冲突不会先删除预测实体。MOBA 适配层也在清理 provider/快照前调用此预检。该保证针对预检失败，不是外部回调或并发写入的完整事务。

回滚保留确认实体的 Generation，但重放创建的预测实体获得新代次；旧精确实时引用不能因 ID 重用重新有效。此能力不能复活已销毁实体，也不恢复属性变化、flow 阶段和外部 provider。MOBA 适配层另清理预测 provider/快照，仍不具备完整 Context 生命周期回滚。

---

## 六、扩展点

- 新增上下文属性：实现 `IProperty`，直接挂载到实体。
- 新增流程语义：在业务层封装 `BeginFlow`，约定 flow name、owner 和 phase。
- 新增快照类型：实现 `IContextSnapshot`，需要版本/帧号时实现 `IVersionedContextSnapshot`。
- 订阅变更：通过 `Subscribe(handler)` 监听全局变化，或 `Subscribe(entityId, handler)` 监听指定实体。
- Trace 桥接：通过 `TraceContextProperty` 保存 trace id，不让 Context 直接依赖 Trace。

---

## 七、注意事项与当前限制

- `ContextRegistry` 的属性对象按引用保存，修改属性内部字段不会自动派发事件；需要通过 `Set` 重新写入才能通知。
- 观察器及异常报告回调均隔离；业务应通过显式命令返回值或领域校验处理失败，不能依赖观察器异常改变结果。
- 快照和实体注册中心没有自动同步销毁关系，调用方需要显式保存、移除或标记快照。
- `FlowContext` 是流程组织模型，不负责驱动业务状态机，也不会自动 Tick。
- Query 支持 With/Without 与谓词过滤；OR、分组和值比较 DSL 可在后续按实际接入复杂度扩展。
- `FlowContext` 不内置 tag/category/faction 等业务分类；这些语义应通过业务侧 `IProperty`、业务查询服务或诊断适配层扩展。
- Registry/Storage 的内部锁不等于业务对象线程安全；属性引用、flow 引用、外部 provider 与多步读取没有统一事务锁，战斗宿主应在同一模拟线程协调。
- 精确 ContextEntityReference 只保护实体实例；FlowContextScope 仍使用 flow ID，Clear 后旧 flow 作用域尚无同等代次保护，不能把实体引用安全推导为全部流程引用安全。
- 受管快照有容量淘汰、显式帧窗口清理与 retain；旧兼容快照仍无容量限制，存储不自动调度 TTL。只关闭工具不应删除逻辑服务拥有的最终快照。

---

## 八、最小接入示例

实体引用安全、纯实时读取和非法回滚预检的回归用例见 [ContextEntityReferenceTests](../../../../src/AbilityKit.Context.Tests/ContextEntityReferenceTests.cs)。

```csharp
public sealed class BusinessCategoryProperty : IProperty
{
    public BusinessCategoryProperty(string category) => Category = category;
    public int TypeId => PropertyTypeRegistry.Instance.Register<BusinessCategoryProperty>().Id;
    public string Category { get; }
}

var registry = new ContextRegistry();
using var flow = registry.BeginFlow("skill-effect", ownerEntityId: casterId);
var entityId = flow.Create()
    .WithTrace(rootTraceId, traceContextId)
    .With(new BusinessCategoryProperty("buff"))
    .Build();

var buffEntities = registry.Query()
    .CreateQuery()
    .With<BusinessCategoryProperty>()
    .Where<BusinessCategoryProperty>((_, property) => property.Category == "buff")
    .Execute();
```

接入方只需要把稳定身份、流程归属和通用 trace id 写入 Context；技能、Buff、阵营、标签、诊断分组等业务语义不要进入 Context 包，而是通过业务自定义属性或业务查询服务表达。

---

## 九、后续演进

- 为 Query 增加 OR、分组条件和值比较 DSL。
- 为业务诊断面板提供只读查询适配示例，而不是在 `FlowContext` 内置业务分类字段。
- 为快照提供序列化适配层和历史多版本存储。
- 增加只读属性访问或不可变属性约束，降低引用共享导致的状态不一致。

---

*文档版本：1.4*
*最后更新：2026-09-17*

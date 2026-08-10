# Ability-Kit GameplayTags 标签系统模块开发设计文档

## 一、定位与边界

`com.abilitykit.gameplaytags` 提供层级标签值、标签目录、容器、查询、需求判断、序列化和模板运行时；Ability 包中的 `GameplayTagService` 再以 owner/source 引用计数管理运行时标签。该模块适合表达状态、能力条件、效果路由和表现提示，不负责替代 ECS 状态组件、数值属性系统或完整事件总线。

当前文档区分三层能力：

```text
GameplayTags Core       标签值、层级目录、容器、Query、Requirements
GameplayTags Template   运行时模板与 grant/remove/require/block 配置
Ability GameplayTagService  owner/source 引用计数、Delta 事件和模板应用
```

Core 包可以独立使用；AbilityTagService 位于 `com.abilitykit.ability`，由宿主通过依赖注入注册。包内源码为 E0，MOBA 装配和效果/表现消费为 E1/E2，现有独立测试证据仍然有限，不能把当前实现描述为已完成的跨进程协议或发布门禁。

## 二、标签身份与层级语义

标签名称采用点分层级，例如 `State.Control.Stunned`。`GameplayTagManager` 维护进程级可变目录，按注册顺序分配内部 Id 和 NetIndex。两者依赖当前进程的目录构建顺序，不是可以脱离目录独立解释的稳定跨进程身份；网络或持久化边界必须先同步/加载一致目录。

匹配是“查询祖先覆盖候选后代”的方向：

```text
State.Control.Stunned.Matches(State.Control) == true
State.Control.Matches(State.Control.Stunned) == false
```

`GameplayTag.FromNetIndex()` 只创建带 NetIndex、Id 为 0 的值，因而 `IsValid` 仍为 false。完整反查必须使用 `GameplayTagManager.Instance.GetTagFromNetIndex(netIndex)`，不能把 FromNetIndex 的返回值直接当作已注册标签。

Manager 是进程级单例，注册、反序列化和网络反序列化都会改变目录；内部集合没有并发保护。推荐在单线程启动阶段完成注册、排序和目录交换，运行阶段只读。`RequestTag()` 对未知名称可能创建标签，因此生产代码应优先使用 `TryGetTag()` 或预注册目录，避免拼写错误静默污染全局状态。

## 三、核心类型与真实语义

### 3.1 `GameplayTagContainer`

Container 保存显式标签 Id，不展开保存祖先，也不记录标签来源或引用次数。`HasTag`、`HasAny` 和 `HasAll` 支持层级匹配及 `exact` 参数；集合内部基于 HashSet，枚举顺序不应作为协议、日志或确定性回放依据。`GameplayTagContainer.Empty` 是公开可变静态实例，调用方不得对其执行 Add、Remove 或 Append。

容器是值语义 API，但返回/暴露可变内容时仍需注意别名。AbilityTagService 的 `GetTags(ownerId)` 会创建 owner 状态，并直接暴露内部可变 Container；调用方不能绕过服务修改它，否则会绕过 source 引用计数和 Delta 事件。

### 3.2 `GameplayTagQuery`

Query 节点支持单标签、复合节点以及 And/Or/Not 等组合。Include、Exclude 和 Exact 语义必须通过构建器与源码实现共同确认，不能仅根据方法名推断。

当前实现存在两个接入注意点：

- Query 的 Include 节点通过 `HasAny(new GameplayTagContainer(node.Tags))` 判断，因此 `RequireTags(a, b)` 当前表现为“任一标签满足”，不是“所有标签都满足”；需要 All 语义时应显式构建 And 组合并配套测试。
- 对 null/空 Container 会提前返回 false，纯 Exclude Query 因此对空集也可能得到 false；业务若要求“空集只要没有排除项就通过”，必须在上层定义并测试该策略。

### 3.3 `GameplayTagRequirements`

`Require()` 与 `Block()` 构造必需和禁止标签集合。Container 重载会根据 Exact 标志进行判断；单标签重载没有完整复现 Container 重载的 Exact 语义，且匹配方向不能与 Container 重载简单等同。对能力施放、效果应用等关键门禁，应优先使用 Container 重载并覆盖 exact/non-exact、空集合和层级父子标签案例。

## 四、运行时所有权与引用计数

Ability `GameplayTagService` 的状态结构为：

```text
OwnerState
├── Tags: GameplayTagContainer
└── Refs: TagId -> SourceId -> Count
```

同一 owner 下，同一 tag 可以由多个 source 持有。第一个 source 引用使标签进入 Container 并触发 Added Delta；最后一个 source 引用移除后才从 Container 删除并触发 Removed Delta。`AddTag`、`RemoveTag`、`ApplyTemplate` 和 `RemoveTemplate` 都必须通过 Service 操作，禁止直接改 `GetTags()` 返回的内部容器。

模板应用的边界如下：

1. `checkRequirements` 为 true 时先检查 Requirements；失败则不应写入 grant/remove 状态。
2. GrantTags 按 source 增加引用；RemoveTags 按 source 减少引用。
3. `RemoveTemplate()` 对模板中的 RemoveTags 使用 `TryRemoveAllRefs`，会清除该标签的所有来源引用。
4. `RemoveTemplate()` 不会自动撤销 ApplyTemplate 产生的 GrantTags；Apply/Remove 不是对称租约，若需要租约语义必须由调用方保存并逐项释放 source 引用。
5. `ClearOwner()` 和 `Dispose()` 清除状态但不逐项发布 Removed Delta；依赖 Removed 事件做表现或副作用清理的宿主不能把它们视作普通逐标签移除。

`GetTags(ownerId)` 会懒创建 OwnerState，即读取操作也可能改变服务内部状态。owner 生命周期结束时应显式调用 `ClearOwner()`，并由宿主处理清理期间不发布 Removed Delta 的后果。

## 五、模板、持久化与 Editor 生产链

`GameplayTagTemplate` 是普通 C# class，不是 ScriptableObject。它保存 Requirements、GrantTags、RemoveTags，并可通过 `CreateRuntime()` 创建运行时模板。若需要 Unity 资产，应由 Editor 数据库或其他资产层保存配置，再转换为运行时模板。

`GameplayTagDatabase` 才是 Editor 侧的 ScriptableObject 数据库，负责条目去重、排序、前缀删除/重命名和迁移。`GameplayTagJsonExporter` 负责 JSON 导入导出。编辑器数据库、运行时 Manager 和网络目录是不同生命周期的对象，发布流程必须明确从哪个资产生成哪个运行时目录。

`DefaultTagSerializer` 按名称序列化标签和容器。反序列化未知名称会调用 `RequestTag()`，从而修改全局目录；读取不可信或版本不一致的数据前，应先完成目录校验，或使用不会隐式注册的严格适配器。网络序列化使用 NetIndex，接收端必须先具备相同目录映射。

## 六、启动与消费者

MOBA 启动阶段先调用 `MobaGameplayTagCatalog.RegisterAll()` 注册标签，再通过：

```csharp
builder.TryRegisterType<IGameplayTagService, AbilityTagService>(WorldLifetime.Scoped);
```

将 AbilityTagService 注册为 scoped 服务。不存在可直接替代该装配流程的 `GameplayTagService.Instance` 单例 API。

当前可见消费者包括：

- MOBA 的标签目录与启动阶段；
- Ability 效果路由和持续标签查询；
- 表现提示等依赖 Added/Removed Delta 的系统；
- 标签模板运行时应用路径。

这些消费者证明标签系统已进入示例/业务运行时路径，但不证明目录版本协商、跨进程稳定 Id、并发注册和重载流程已经具备生产级门禁。

## 七、验证证据与采用门槛

证据分级：

| 等级 | 当前证据 |
|---|---|
| E0 | Core、Template、Serializer、Editor Database、AbilityTagService 源码 |
| E1 | MOBA `MobaGameplayTagCatalog` 与 `TagsStage` 装配 |
| E2 | Ability 效果路由、持续标签查询和表现提示消费 |
| E3 | 当前独立 GameplayTags 测试仅覆盖 `None` 默认值和零值；关键层级、Query、引用计数和序列化场景仍缺测试 |
| E4 | 未发现本包专项 Smoke/Acceptance artifact |
| E5 | 未发现目录变更 CI 阻断、协议预算、发布回滚和运行时一致性门禁 |

新消费者接入前至少应验证：

1. 目录注册顺序、导出版本与网络/持久化边界一致；
2. 父子匹配、Exact、Query Include/Exclude/All 语义符合业务预期；
3. 多 source 引用的 Added/Removed 边沿和重复移除行为；
4. ClearOwner/Dispose 期间是否需要补发清理事件；
5. `GetTags()` 的可变别名不会绕过 Service；
6. 未知序列化名称不会在不应注册的环境中污染目录。

推荐源码阅读顺序：`GameplayTag.cs` -> `GameplayTagManager.cs` -> `GameplayTagContainer.cs` -> `GameplayTagQuery.cs`/`GameplayTagRequirements.cs` -> `DefaultTagSerializer.cs` -> `GameplayTagTemplate.cs` -> `Ability/Tags/GameplayTagService.cs` -> Editor 与 MOBA 消费者。

更高层的层级治理和跨模块边界见 `Docs/design/08-GameplayModules/12-GameplayTagsHierarchyAndEngineeringBoundaries.md`；快速接入示例见本包 `README.md`。

# com.abilitykit.gameplaytags

GameplayTags 提供可注册的层级标签、标签容器、组合查询、需求判断、模板运行时和名称/NetIndex 序列化能力。它适合描述状态、控制效果、能力条件、效果路由和表现提示。

包内核心命名空间为 `AbilityKit.GameplayTags`。Ability 包另提供基于 owner/source 引用计数的 `GameplayTagService`，该服务不是本包的全局单例。

## 一、能力定位与边界

本包负责：

- 维护点分层级标签，例如 `State.Control.Stunned`；
- 提供父子匹配、Exact 匹配、Container 集合运算和 Query；
- 提供 `GameplayTagRequirements` 与 `GameplayTagTemplate`；
- 提供编辑器数据库和 JSON 导入导出辅助；
- 提供按名称和 NetIndex 的序列化入口。

本包不负责：

- 数值属性、效果持续时间或完整事件总线；
- 自动管理 owner/source 生命周期；
- 稳定的跨进程 Id 分配或目录版本协商；
- Unity 协程、ECS 状态组件或网络同步策略。

## 二、最小运行时接入

```csharp
using AbilityKit.GameplayTags;

var manager = GameplayTagManager.Instance;
manager.RegisterTags(new[]
{
    "State.Control.Stunned",
    "State.Control.Silenced",
    "State.Buff.Haste"
});

if (!manager.TryGetTag("State.Control.Stunned", out var stunned))
{
    throw new InvalidOperationException("Tag catalog is incomplete.");
}

var tags = new GameplayTagContainer();
tags.Add(stunned);

bool hasControlState = tags.HasTag(manager.RequestTag("State.Control"));
```

推荐在启动阶段一次性注册并校验目录，运行阶段优先使用 `TryGetTag()`。`RequestTag()` 对未知名称可能创建全局标签，不应作为不可信输入的严格读取 API。

父子匹配方向是查询祖先覆盖候选后代：

```csharp
bool matches = stunned.Matches(manager.RequestTag("State.Control")); // true
```

`GameplayTag.FromNetIndex()` 仅构造带 NetIndex 的未解析值，`IsValid` 可能为 false。接收 NetIndex 后应通过：

```csharp
var resolved = manager.GetTagFromNetIndex(netIndex);
```

完成目录反查。Id 和 NetIndex 依赖一致的目录注册顺序，不能单独当作稳定协议身份。

## 三、Container、Query 与 Requirements

```csharp
var query = new GameplayTagQueryBuilder()
    .RequireTags(stunned)
    .ExcludeTags(manager.RequestTag("State.Immunity.Control"))
    .Build();

bool allowed = query.Matches(tags);
```

接入时必须确认 Query 的组合语义：当前 Include 节点通过 `HasAny` 评估，`RequireTags(a, b)` 不应未经测试地解释为 All；需要全部满足时显式构造 And 组合。对空 Container 的纯 Exclude Query 也要由业务定义预期，因为当前实现对 null/空容器可能提前返回 false。

`GameplayTagRequirements` 的 Container 重载比单标签重载更完整地表达 Exact 语义。能力或效果门禁应覆盖父子标签、Exact、空集合和禁止标签场景的测试。

## 四、AbilityTagService：owner/source 运行时服务

使用 Ability 层的运行时标签服务时，通过世界容器注册接口：

```csharp
builder.TryRegisterType<IGameplayTagService, GameplayTagService>(WorldLifetime.Scoped);
```

服务内部按以下结构保存状态：

```text
OwnerState
├── Tags: GameplayTagContainer
└── Refs: TagId -> SourceId -> Count
```

第一个来源引用触发 Added，最后一个来源引用移除触发 Removed。必须通过 `AddTag`、`RemoveTag`、`ApplyTemplate` 和 `RemoveTemplate` 修改标签，不能修改 `GetTags(ownerId)` 返回的内部容器，否则会绕过引用计数和 Delta 事件。

模板移除不是 Apply 的对称租约：`RemoveTemplate()` 会对模板的 RemoveTags 清除所有来源引用，但不会自动撤销 ApplyTemplate 产生的 GrantTags。`ClearOwner()` 和 `Dispose()` 清状态时也不会逐项发布 Removed Delta，表现层或副作用系统需要对此单独处理。

## 五、编辑器数据库、模板与序列化

`GameplayTagDatabase` 是 Editor 侧的 `ScriptableObject`，负责标签条目、排序、去重、前缀重命名和迁移；`GameplayTagJsonExporter` 负责 JSON 导入导出。

`GameplayTagTemplate` 是普通 C# class，不是 `ScriptableObject`。它可以保存 Requirements、GrantTags 和 RemoveTags，并通过 `CreateRuntime()` 创建运行时模板。若需要 Unity 资产，应由编辑器数据库或其他资产层保存配置，再转换成运行时模板。

`DefaultTagSerializer` 按标签名称序列化。反序列化未知名称会调用 `RequestTag()` 并修改全局目录，因此加载持久化数据前应校验目录版本和名称；网络 NetIndex 序列化同样要求发送端和接收端使用一致目录。

## 六、目录结构与源码阅读路径

```text
Runtime/GameplayTags/
├── Core/          GameplayTag、Manager、Container、Query、Requirements
├── Persistence/   DefaultTagSerializer
├── Service/       IGameplayTagService 等接口
└── Template/      GameplayTagTemplate、TagTemplateRuntime

Editor/GameplayTags/
├── GameplayTagDatabase.cs
└── GameplayTagJsonExporter.cs
```

推荐阅读顺序：

1. `Runtime/GameplayTags/Core/GameplayTag.cs`
2. `Runtime/GameplayTags/Core/GameplayTagManager.cs`
3. `Runtime/GameplayTags/Core/GameplayTagContainer.cs`
4. `Runtime/GameplayTags/Core/GameplayTagQuery.cs` 与 `GameplayTagRequirements.cs`
5. `Runtime/GameplayTags/Persistence/DefaultTagSerializer.cs`
6. `Runtime/GameplayTags/Template/GameplayTagTemplate.cs`
7. `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Tags/GameplayTagService.cs`
8. `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Tags/MobaGameplayTagCatalog.cs`

## 七、证据等级与采用门槛

- E0：本包 Core、Template、Persistence、Editor 源码及 AbilityTagService 实现。
- E1：MOBA 标签目录和启动装配调用。
- E2：效果路由、持续标签查询与表现提示等消费者。
- E3：当前独立测试覆盖有限，尚不足以证明所有层级、Query、引用计数和序列化边界。
- E4/E5：当前未发现本包专项 Smoke/Acceptance artifact、CI 阻断、协议预算或发布回滚门禁。

新消费者接入前，至少补充目录一致性、父子/Exact 匹配、Query All/Any、source 引用边沿、ClearOwner 清理语义和未知名称反序列化测试。

完整运行语义见 [`GameplayTags标签系统模块开发设计文档.md`](Document/GameplayTags标签系统模块开发设计文档.md)；跨模块层级治理见 [`12-GameplayTagsHierarchyAndEngineeringBoundaries.md`](../../../Docs/design/08-GameplayModules/12-GameplayTagsHierarchyAndEngineeringBoundaries.md)。Behavior、Threading 等模块的总导航见 [`00-index.md`](../../../Docs/design/00-index.md)。

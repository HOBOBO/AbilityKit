# com.abilitykit.gameplaytags

GameplayTags 提供可注册的层级标签、标签容器、组合查询、需求判断、模板运行时、按名称序列化和 NetIndex 目录反查。它适合描述状态、控制效果、能力条件、效果路由和表现提示。

运行时程序集与核心命名空间均为 `AbilityKit.GameplayTags`，不依赖 UnityEngine，也不需要引用 Editor 程序集。`IGameplayTagService` 接口在本包；基于 owner/source 引用计数的 `GameplayTagService` 实现在 `com.abilitykit.ability`，不是本包的全局单例。

## 一、能力定位与边界

本包负责：

- 维护点分层级标签，例如 `State.Control.Stunned`；
- 提供父子匹配、Exact 匹配、Container 集合运算和 Query；
- 提供 `GameplayTagRequirements` 与 `GameplayTagTemplate`；
- 提供编辑器数据库和 JSON 导入导出辅助；
- 提供按名称的序列化入口和 NetIndex 目录反查；不定义线协议。

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

`RegisterTags()` 按输入顺序注册有效名称并跳过无效项。`RegisterTagsWithNotification()` 只通知本次显式输入中首次注册的规范化名称；仅首尾空白不同不算新标签，隐式补齐的父标签也不通知。监听器在注册锁外执行，单个监听器失败不影响其他监听器；并发批次之间不保证通知顺序。

宿主需要从资产或配置表加载时，可在装配阶段通过 `GameplayTagConfigLoader.SetLoader()` 安装 `ITagConfigLoader`。`LoadFromData()` 按原顺序传递条目和元数据，不替宿主校验或注册；未设置加载器时不枚举输入。`LoadFromNames()` 不经过宿主加载器，直接注册名称：跳过空白名称，但遇到其他非法名称会中断，已注册的项不回退。

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

`RequireTags(a, b)` 生成 `IncludeTags` 节点，匹配任一标签即可；需要全部满足时使用 `IncludeAllTags` 节点或显式组合 `And`。`GameplayTagQuery.Matches()` 将 null 容器视为空容器，因此纯排除条件在两者上均成立。

`GameplayTagRequirements` 的容器重载同时检查必需标签全部存在、禁止标签全部不存在、配置的 Query 成立；需使用 `Exact` 或表达多个必需标签时，使用容器重载而不是单标签重载。需求判断不接受 null 容器，不等同于 `GameplayTagQuery.Matches()` 的 null 处理。

`ContinuousTagRequirements.ShouldRemove()` 在未配置条件或传入 null 容器时返回 false。空容器是有效输入；只配置禁止标签时，所有禁止标签均不存在即触发移除。标签授予、撤销和对象结束由持续行为消费者执行，本类型只判断条件。

## 四、GameplayTagService：owner/source 运行时服务

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

模板移除不是 Apply 的对称租约：`RemoveTemplate()` 会对模板的 RemoveTags 清除所有来源引用，但不会自动撤销 ApplyTemplate 产生的 GrantTags。`ClearOwner()` 清除该 owner 的全部引用，并在有活动标签时以 `GameplayTagSource.System` 发出一个聚合的 Removed Delta。`Dispose()` 清除所有 owner 状态，不派发 Delta。

## 五、编辑器数据库、模板与序列化

`GameplayTagDatabase` 是 Editor 侧的 `ScriptableObject`，负责标签条目、排序、去重、前缀重命名和迁移；`GameplayTagJsonExporter` 负责 JSON 导入导出。

`GameplayTagTemplate` 是普通 C# class，不是 `ScriptableObject`。它可以保存 Requirements、GrantTags 和 RemoveTags，并通过 `CreateRuntime()` 创建运行时模板。若需要 Unity 资产，应由编辑器数据库或其他资产层保存配置，再转换成运行时模板。

`DefaultTagSerializer` 按标签名称序列化。反序列化未知名称会调用 `RequestTag()` 并修改全局目录，因此加载持久化数据前应校验目录版本和名称。若业务在网络上承载 NetIndex，发送端和接收端须自行保证目录一致，本包不负责协商。

## 六、目录结构与源码阅读路径

```text
Runtime/GameplayTags/
├── Core/          GameplayTag、Manager、Container、Query、Requirements
├── Persistence/   DefaultTagSerializer
├── Service/       IGameplayTagService 等接口
├── Template/      GameplayTagTemplate、TagTemplateRuntime
└── Utilities/     GameplayTagConfigLoader 等接入入口

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

## 七、证据等级与采用门槛

- E0：本包 Core、Template、Persistence、Editor 源码及 Ability 层 `GameplayTagService` 实现。
- E1：MOBA 标签目录和启动装配调用。
- E2：效果路由、持续标签查询与表现提示等消费者。
- E3：`.NET` 契约测试覆盖层级、Query、持续需求和名称往返；Ability 消费测试覆盖多来源引用边沿与 owner 清理。
- E4/E5：当前未发现本包专项 Smoke/Acceptance artifact、CI 阻断、协议预算或发布回滚门禁。

新消费者接入前，至少补充目录一致性、父子/Exact 匹配、Query All/Any、source 引用边沿、ClearOwner 清理语义和未知名称反序列化测试。

包内设计见 [`GameplayTags标签系统模块开发设计文档.md`](Document/GameplayTags标签系统模块开发设计文档.md)。

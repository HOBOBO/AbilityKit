# EntityManager 与 SkillLibrary 索引基础设施

> 文档类型：Canonical 设计（战斗索引基础设施）
> 事实基线：2026-08-16
> 文档版本：v3.2
>
> 本文说明 `com.abilitykit.combat.entitymanager` 与 `com.abilitykit.combat.skilllibrary` 的公共索引模型、写入顺序、一致性边界和验证现状。两者都用“主存储 + 派生查询结构”降低按分类查找成本，但键的来源、更新责任和成熟度不同。

## 1. 能力定位

这两个包解决的是战斗逻辑中的轻量内存索引问题：

- EntityManager 保存实体 ID 集，并按阵营、类型、标签等维度建立 ID bucket。
- SkillLibrary 保存技能数据，并从数据中派生学派、标签等查询 bucket。
- 查询结果是索引当前持有的 ID 集合，不负责多条件查询规划、排序、分页或持久化。
- 两者都不是数据库、ECS 存储、事务管理器或线程安全容器。

关键区别如下：

| 维度 | EntityManager | SkillLibrary |
|------|---------------|--------------|
| 主存储 | 实体 ID 集 | `TKey -> TData` 字典 |
| 索引键来源 | 调用方显式设置 | selector 从 `TData` 派生 |
| 内建更新行为 | `OnEntityUpdated` 为空，不自动改键 | `Update` 用 old/new data 重算派生键 |
| 删除清理 | Registry 删除时通知索引清理 ID | Library 删除时通知索引清理技能键 |
| 生产调用证据 | MOBA `MobaEntityManager` | 当前仅包内示例 |
| 直接测试证据 | `KeyedIndexUpdateTests.cs` 仅覆盖 3 个 update DTO 的构造和字段 | 未发现 SkillLibrary 专门测试 |

框架只拥有注册表、bucket 和增量索引维护契约；项目拥有权威实体/技能主数据、索引键目录、聚合写入口和异常后的重建策略。MOBA 的 Team/MainType 等索引是项目选择，不应成为通用 EntityManager 的内建维度。

## 2. 源码与构建入口

| 内容 | 路径 |
|------|------|
| EntityManager Unity 源码 | `Unity/Packages/com.abilitykit.combat.entitymanager/Runtime/EntityManager` |
| EntityManager .NET 镜像 | `src/AbilityKit.Combat.EntityManager/AbilityKit.Combat.EntityManager.csproj` |
| SkillLibrary Unity 源码 | `Unity/Packages/com.abilitykit.combat.skilllibrary/Runtime/SkillLibrary` |
| SkillLibrary .NET 镜像 | `src/AbilityKit.Combat.SkillLibrary/AbilityKit.Combat.SkillLibrary.csproj` |
| MOBA 实体索引接入 | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/EntityManager/MobaEntityManager.cs` |
| MOBA 生成与索引深潜 | `../09-ImplementationExamples/MOBA/06-ConfigEntitySpawnDeepDive.md` |

两个 `.NET` 工程通过 `Compile Include` 链接 Unity package 的 Runtime 源码。Unity asmdef 和 `.NET` 镜像是同一份实现的两个构建入口，不应维护源码副本。

## 3. EntityManager：注册表与显式索引

### 3.1 对象关系

```mermaid
flowchart LR
    Facade[BattleEntityManager] --> Registry[EntityRegistry]
    Registry --> Ids[Entity ID Set]
    Registry --> Indices[IEntityIndex List]
    Indices --> Keyed[KeyedEntityIndex]
    Indices --> Multi[MultiKeyEntityIndex]
    Caller[Domain Service] -->|SetKey / AddKey / RemoveKey| Keyed
    Caller -->|SetKey / AddKey / RemoveKey| Multi
```

`BattleEntityManager<TId>` 是 `EntityRegistry<TId>` 的薄门面，额外提供 keyed 和 multi-key 索引工厂。Registry 只知道实体 ID 和 `IEntityIndex<TId>` 观察者，不拥有实体组件或领域对象。

`KeyedEntityIndex<TKey,TId>` 保存每个实体最多一个键的双向关系：

- `TKey -> HashSet<TId>` 用于按键查询。
- `TId -> TKey` 用于换键和删除清理。

`MultiKeyEntityIndex<TKey,TId>` 保存每个实体零到多个键：

- `TKey -> HashSet<TId>` 用于按键查询。
- `TId -> HashSet<TKey>` 用于反向清理。

### 3.2 正确写入顺序

创建实体时应先注册 ID，再写入全部领域索引：

```csharp
manager.Add(entityId);
byTeam.SetKey(entityId, team);
byType.SetKey(entityId, type);
byTag.AddKey(entityId, tag);
```

修改分类属性时，调用方必须显式更新相应索引：

```csharp
byTeam.SetKey(entityId, newTeam);
byTag.RemoveKey(entityId, oldTag);
byTag.AddKey(entityId, newTag);
```

删除实体只需从 Registry 删除。Registry 会调用所有已注册索引的 `OnEntityRemoved`，内建索引据此清理反向关系和 bucket。

```mermaid
sequenceDiagram
    participant Domain as Domain Service
    participant Registry as EntityRegistry
    participant Index as Entity Index

    Domain->>Registry: Add(id)
    Registry->>Registry: _entities.Add(id)
    Registry->>Index: OnEntityAdded(id)
    Note over Index: 内建索引不派生键
    Domain->>Index: SetKey/AddKey

    Domain->>Registry: Remove(id)
    Registry->>Registry: _entities.Remove(id)
    Registry->>Index: OnEntityRemoved(id)
    Index->>Index: 清理反向关系与 bucket
```

### 3.3 `NotifyUpdated` 的真实边界

`EntityUpdate` 只是一个 `int Type + object Payload` 信封。Registry 的 `NotifyUpdated` 会：

1. 忽略未注册 ID。
2. 对实现 `IUpdateTypeAwareIndex<TId>` 的自定义索引调用 `Accepts(update.Type)`。
3. 将通过过滤的更新转给 `OnEntityUpdated`。

内建 `KeyedEntityIndex` 和 `MultiKeyEntityIndex` 的 `OnEntityUpdated` 当前都是空实现，也没有实现 `IUpdateTypeAwareIndex<TId>`。因此：

- `NotifyUpdated` 不会自动调用 `SetKey`、`AddKey` 或 `RemoveKey`。
- `SetKeyUpdate`、`AddKeyUpdate`、`RemoveKeyUpdate` 只是数据结构，当前没有被内建索引消费。
- 如需事件驱动派生键，应实现自定义 `IEntityIndex<TId>`，并自行读取领域数据或 payload。

不能把“实体属性已修改”和“所有索引已同步”视为同一操作。

### 3.4 注册与索引的独立性

内建索引的直接写 API 不检查 ID 是否存在于 Registry。下面的调用可以建立悬空索引记录：

```csharp
byTeam.SetKey(unregisteredId, team);
```

后续对该 ID 调用 `Registry.Remove` 会因主集合中不存在该 ID 而直接返回，无法触发索引清理。因此领域服务应把“注册 ID + 设置索引”封装在同一个受控入口，并禁止业务代码绕过该入口。

新增索引时，Registry 会向它回放当前全部 ID；但内建索引的 `OnEntityAdded` 为空，所以后加的内建索引仍是空索引。调用方必须遍历领域数据完成 backfill。

`AddIndex` 的顺序是先把索引加入 `_indices`，再遍历当前实体执行 `OnEntityAdded`。自定义索引在回填中抛异常时，Registry 不会移除该索引，也不会撤销已经回填的实体；下一次实体写入仍会继续通知这个部分初始化的索引。`RemoveIndex` 只解除后续通知，不会调用清理或清空索引内容，因此外部若仍持有该索引，看到的是移除时刻留下的陈旧视图。动态安装索引应先在独立实例完成全量构建与校验，再切换查询引用；当前 API 不能提供原子挂载。

## 4. SkillLibrary：主数据与派生索引

### 4.1 对象关系

```mermaid
flowchart LR
    Library[SkillLibrary] --> Store[TKey to TData]
    Library --> Indices[ISkillIndex List]
    Indices --> Single[DerivedKeyedSkillIndex]
    Indices --> Multiple[DerivedMultiKeySkillIndex]
    Data[TData] -->|selector| Single
    Data -->|selector| Multiple
```

`SkillLibrary<TKey,TData>` 同时拥有主数据和索引列表。内建派生索引通过 selector 读取 `TData`：

- `CreateDerivedKeyedIndex` 为每条技能派生一个索引键。
- `CreateDerivedMultiKeyIndex` 为每条技能派生零到多个索引键。

索引只保存技能主键，不复制 `TData`。查询到技能键后仍应通过 `TryGet` 或 `Get` 读取主数据。

### 4.2 增删改流程

```mermaid
sequenceDiagram
    participant Caller
    participant Library as SkillLibrary
    participant Store as Main Dictionary
    participant Index as Derived Index

    Caller->>Library: Update(key, newData, update)
    Library->>Store: TryGet oldData
    Library->>Store: store[key] = newData
    Library->>Index: OnUpdated(key, oldData, newData, update)
    Index->>Index: selector(old/new) and reconcile
    Library-->>Caller: true
```

行为要点：

- `Add` 在主字典插入成功后通知全部索引。
- `Remove` 先从主字典删除，再用旧数据通知全部索引。
- `Update` 先替换主数据，再把 old/new data 交给全部索引。
- `NotifyUpdated` 不替换主数据，只把当前数据同时作为 old/new data 发送。
- 新增索引时会回放当前全部技能，因此内建派生索引可以完成 backfill。
- 但 `AddIndex` 同样先挂入列表再回填；selector 或索引回调中途失败会留下已挂载、部分回填的索引。
- `RemoveIndex` 只停止后续通知，不清空派生 bucket；继续持有被移除索引会读到冻结旧数据。
- `Get` 在键缺失时抛 `KeyNotFoundException`；普通探测应使用 `TryGet`。

`DerivedKeyedSkillIndex` 比较 old/new selector 结果并在键变化时移动技能键。`DerivedMultiKeySkillIndex` 构造 old/new key set，并按集合差添加或删除关系。两个内建索引当前都不根据 `SkillUpdate.Type` 过滤；更新信封只对自定义 `ISkillIndex` 有潜在价值。

## 5. 一致性与失败边界

### 5.1 通知不是事务

EntityRegistry 和 SkillLibrary 都采用“先修改主存储，再逐个通知索引”的顺序。索引回调没有 rollback、两阶段提交或统一异常隔离：

```mermaid
flowchart LR
    Write[修改主存储] --> I1[通知索引 1]
    I1 --> I2[通知索引 2]
    I2 --> I3[通知索引 3]
    I2 -. exception .-> Partial[主存储和部分索引已更新]
```

任一自定义 selector 或索引回调抛异常时，调用者可能观察到：

- 主存储已经变更。
- 排在异常前的索引已经变更。
- 排在异常后的索引尚未变更。

内建索引自身也可能在单个回调内部分提交。例如单键索引换键时先从旧 bucket 删除，再写 `_currentKey` 和新 bucket；新键为不被 Dictionary 接受的值或后续添加失败时，旧关系不会自动恢复。多键 selector 是延迟枚举：添加/删除遍历中途失败会留下部分键；更新虽然先构造 old/new set，但随后删除旧差集再添加新差集，添加阶段失败时也不会恢复已经删除的关系。

| 操作 | 先提交内容 | 回调/selector 失败后的典型状态 |
|------|------------|-------------------------------|
| Entity `Add/Remove` | 主 ID set | 部分索引已通知，主表不回滚 |
| Entity `AddIndex` | 索引列表 | 自定义索引仅回填部分实体但仍保持挂载 |
| Skill `Add/Remove/Update` | 主技能字典 | 主数据与部分派生索引不一致 |
| Skill `AddIndex` | 索引列表 | 派生索引只包含部分既有技能 |
| 单键换键 | 旧 bucket 先删除 | 新键写入失败时实体可能失去原索引关系 |
| 多键添加/删除 | 按 selector 枚举逐项修改 | selector 延迟异常时只提交前缀键 |

生产接入应保证 selector 和回调无副作用、可重复执行且不抛异常。若故障后无法证明一致性，应从领域权威数据重建索引，而不是继续增量写入。

### 5.2 比较器并非全链路传播

构造函数允许传入自定义 comparer，但当前实现并未在全部内部比较中统一使用它：

- `KeyedEntityIndex.SetKey` 用 `EqualityComparer<TKey>.Default` 判断旧键和新键是否相等。
- `DerivedKeyedSkillIndex.OnUpdated` 也用默认比较器判断派生键是否变化。
- `DerivedMultiKeySkillIndex` 的派生键差分会使用构造时传入的 key comparer，但 bucket 内技能主键 `HashSet<TKey>` 没有可注入的技能键 comparer。
- `DerivedKeyedSkillIndex` 的 bucket 内技能主键 `HashSet<TKey>` 同样使用默认比较器；Library 主字典的技能键 comparer 不会传入任一派生索引。
- bucket 字典本身使用调用方传入的 key comparer。

例如忽略大小写的字符串 comparer 可能让字典认为两个键相同，但更新差分逻辑仍认为它们不同。当前更稳妥的约束是使用默认等价语义，或在写入前规范化键。自定义 comparer 的端到端一致性需要源码修复和专门测试后才能作为正式能力承诺。

### 5.3 返回集合与线程模型

`Get` / `TryGet` 返回的是内部 `HashSet` 的只读接口视图，不是不可变快照。`EntityRegistry.Entities` 与 `SkillLibrary.Keys` 也直接暴露主集合的延迟枚举视图。调用方不能通过声明接口修改 bucket，但后续主表或索引写入会改变同一个集合实例；遍历期间发生重入或并发修改可能使枚举失效。需要跨回调、跨帧或跨线程保存结果时必须复制快照。

两个包都没有锁或线程安全声明。推荐在单一逻辑线程中写入和查询；跨线程消费应由上层复制快照并明确同步边界。

### 5.4 查询复杂度的准确表述

按单个键定位 bucket 通常是字典查找，平均复杂度接近 O(1)，但完整成本还包括：

- bucket 内结果数量。
- 多条件查询时由上层执行的集合求交或过滤。
- 哈希质量、扩容和自定义 comparer 成本。
- 更新时对多键集合执行的差分和反向关系维护。

因此不能笼统承诺“所有查询 O(1)”。这两个包也没有查询规划器或跨索引原生求交 API。

## 6. 推荐接入模式

### 6.1 EntityManager 聚合写入口

领域服务应集中管理注册与索引：

```csharp
public void RegisterActor(int id, Team team, EntityMainType type)
{
    _entities.Add(id);
    _byTeam.SetKey(id, team);
    _byType.SetKey(id, type);
}

public void ChangeTeam(int id, Team team)
{
    _byTeam.SetKey(id, team);
}

public void UnregisterActor(int id)
{
    _entities.Remove(id);
}
```

如果中途写入可能失败，上层应先验证全部输入，再按固定顺序提交；异常后从权威实体数据重建相关索引。

### 6.2 SkillLibrary 使用不可变数据

派生索引依赖 old/new data。若 `TData` 是可变引用对象，并在调用 `Update` 前原地修改，old/new 可能指向同一对象，selector 无法看到旧值。推荐：

- 使用不可变记录或替换整个数据对象。
- selector 只读取稳定字段，不访问外部可变状态。
- 通过 `Update(key, newData, update)` 统一提交替换。
- 配置热重载场景优先构建新 Library 或新索引集合，验证后整体切换。

## 7. 验证现状与门禁

截至 2026-08-16：

- `AbilityKit.Combat.EntityManager.csproj` Release 构建成功，0 error。
- `AbilityKit.Combat.SkillLibrary.csproj` Release 构建成功，0 error。
- 两个工程存在仓库既有 nullable 和公共 API XML 注释警告。
- EntityManager 在 MOBA 中建立 Team、MainType、UnitSubType、OwnerPlayer 索引，属于真实运行接入。
- SkillLibrary 全仓仅发现 `SkillLibraryExample`，尚无生产 runtime 调用证据。
- 当次 `AbilityKit.Combat.EntityManager.Tests` 为 `3/3`，但三项只验证 `SetKeyUpdate`、`AddKeyUpdate`、`RemoveKeyUpdate` 的构造与字段，不执行 Registry、KeyedIndex、MultiKeyIndex、backfill 或失败一致性；SkillLibrary 仍未发现专门测试。

建议补充的最低测试集合：

1. Add、Remove、后加索引 backfill 和重复写入。
2. 单键换键、多键集合差和空 selector 结果。
3. `NotifyUpdated` 的自定义 update-aware 索引过滤。
4. selector 或回调抛异常后的部分提交状态。
5. 自定义 comparer 在字典、换键判断和 multi-key 差分中的一致性。
6. 可变 `TData` 原地修改造成的旧值丢失回归。
7. AddIndex 回填失败后是否挂载、RemoveIndex 后旧视图是否继续变化。
8. 主键 comparer 与派生 bucket comparer 不一致时的重复技能键。

## 8. 采用结论

- EntityManager 可作为单线程战斗域内的轻量 ID 索引，但业务必须集中维护键，并接受当前非事务边界。
- SkillLibrary 的派生索引模型可用于原型和受控配置集；在生产采用前应补直接测试、真实 runtime 接入和热重载一致性验证。
- 两者都不应被描述为自动同步数据库。权威数据始终在领域实体或技能主数据中，索引是可重建的派生状态。

证据分层需要按包分别声明：EntityManager 为 E0 实现、E2 MOBA 消费，现有 E3 入口只固定 update 信封结构，不能称为索引行为 E3；SkillLibrary 为 E0 实现、E1 示例，尚无真实 runtime 消费和直接契约测试。两者均无配置热切换、异常重建、comparer 全链一致、并发访问或规模预算的 E4/E5 证据。

---

*文档类型：Canonical 设计（战斗索引基础设施） | 事实基线：2026-08-16 | 证据等级：EntityManager E0-E2 + 局部结构 E3，SkillLibrary E0-E1 | 文档版本：v3.2*

# SearchTarget（战斗查找目标模块）

本模块提供一套通用、可扩展、可替换实体系统后端、并面向高频调用优化（低/零分配）的“目标查找”框架。

## 目标

- 统一入口管理各种“找目标”需求。
- 可扩展：候选来源、过滤条件、几何形状、排序/选择策略均可替换。
- 可替换：不依赖具体实体系统（例如 Entitas、ActorEntity 或其他实现）。
- 高性能：
  - 候选提供者通过 `ForEachCandidate` 与结构体 Consumer 推送候选；引擎对最终候选流只消费一次。
  - 实现 `IStreamingTopKByScoreSelector` 的 Selector 配合正数 `MaxCount` 时，在候选回调内融合规则、评分与 Top-K，只保留有限命中。
  - 普通自定义 Selector 保留完整命中后处理能力；候选集合可直接引用索引集合，避免无必要的候选复制。
- 确定性：除“显式随机”策略外，相同输入应得到稳定一致的结果；稳定排序使用 `IEntityKeyProvider`。

## 数据流（管线）

1. **候选生成（候选提供者）**：`ICandidateProvider`
2. **过滤（规则）**：`ITargetRule[]`
3. **评分与排序（有序排序项）**：`SearchOrder[]`，每项包含 `ITargetScorer` 和独立方向
4. **选择（选择器）**：`ITargetSelector`
5. **映射输出（映射器）**：`ITargetMapper<T>`（例如输出 `IUnitFacade`）

核心执行由 `TargetSearchEngine` 完成：
- `SearchIds(in query, context, results)`：写入调用方提供的 `List<EntityId>`。
- `SearchIds(in query, context)`：返回池化 `SearchResult`，使用完成后 `Dispose()` 或 `TargetingPool.Release(result)`。
- `Search<T>(..., ITargetMapper<T> mapper)`：输出任意类型列表（如 `IUnitFacade`）。

## 关键接口

### SearchQuery
- `Provider`：候选来源
- `Rules`：过滤链；列表中的规则按顺序执行，任一规则失败即短路，因此顶层语义是 AND
- `Orders`：有序排序项快照；每个 `SearchOrder` 包含一个非空 `ITargetScorer` 和该项自己的合法升降序方向
- `Selector`：选择策略；普通实现接收完整命中视图，实现 `IStreamingTopKByScoreSelector` 的实现可在有效 K 下由引擎融合执行
- `MaxCount`：结果硬上限；`0` 表示不限数量，负值在查询构造和 Builder 边界被拒绝。普通 Selector 可读取完整命中视图，但 `SearchResultWriter` 不允许写出超过该预算的结果
- `DuplicatePolicy`：默认保留 Provider 产生的重复候选，也可显式按实体稳定键去重；稳定键只在规则通过且所有评分有效后提交

多个排序项按声明顺序执行严格字典序比较，等价于 `ORDER BY Distance ASC, Threat DESC`：只有前一项同分时才比较后一项，所有排序项同分后按实体稳定键升序决胜。`SearchPipelineBuilder.ScoreBy()` 设置或替换主排序项，`ThenScoreBy()` 追加后续排序项；两者都可为当前项指定独立方向。加权复合 Scorer 仍可表达业务效用函数，但不等价于严格多字段排序。

### AND、OR、NOT 与候选源组合

组合能力分为规则判定和候选集合两层：

| 表达式 | 内置表达方式 | 契约 |
|---|---|---|
| `A AND B AND C` | 顶层 `Rules`，或 `new AndRule(A, B, C)` | 声明顺序求值，首个失败即短路；空 AND 为 `true` |
| `A OR B` | `new OrRule(A, B)` | 声明顺序求值，首个成功即短路；空 OR 为 `false` |
| `NOT A` | `new NotRule(A)` | 反转单个非空 Rule；空操作数在构造时被拒绝 |
| `A AND (B OR C) AND NOT D` | 嵌套 `AndRule`、`OrRule`、`NotRule` | 复合 Rule 可继续作为子节点或顶层 Rule |
| 顺序拼接 | `ConcatCandidateProvider` | 按来源声明顺序推送，保留重复；空组合不产生候选 |
| 并集 | `UnionDistinctCandidateProvider` | 按稳定键去重，保留所有来源中的首次代表项和出现顺序 |
| 交集 | `IntersectCandidateProvider` | 结果唯一，保留第一来源中的首次代表项和顺序；空组合或空来源得到空集 |
| 差集 | `ExceptCandidateProvider` | 从主来源排除所有排除源稳定键，保留主来源中的首次代表项和顺序 |

这相当于数据库中的 `WHERE A AND (B OR C) AND NOT D` 与 `UNION`、`INTERSECT`、`EXCEPT`。顶层 `Rules` 仍只负责隐式 AND，不解析字符串表达式或运算符优先级；括号通过复合 Rule 的对象嵌套显式表达。组合器在构造时复制输入数组，调用方后续修改原数组不会改变组合语义。

集合型 Provider 优先读取 Context 中的 `IEntityKeyProvider`；缺失时使用 `EntityId.Value`，因此稳定键相同的不同实体会被视为同一集合成员。`BlacklistRule` 和 `ExcludeEntityRule` 仍是逐候选排除条件，查询级 `DistinctByEntityKey` 也只处理最终 Provider 已推送的重复候选，三者与 Provider 差集的执行层级不同。

### SearchContext
- 框架已知能力通过 `PositionProvider`、`EntityKeyProvider` 和 `SearchStats` 强类型属性直接注入，不使用通用服务定位器。
- 包外单次查询数据使用 `SetData<T>(SearchContextKey<T>, T)`；键应由业务包静态持有，并通过业务 facade 暴露读写操作。键按实例身份隔离，名称只用于诊断。
- 上下文不提供整数键数据入口，也不应作为任意业务服务黑板；包外长期能力优先通过构造注入或业务 facade 传递。
- `ClearData()` 只清理包外扩展数据，适合串行复用且需要保留框架能力属性的上下文。
- 高频调用建议使用 `TargetingPool.RentContext()` 获取，结束后 `Dispose()` 或 `TargetingPool.Release(context)`；完整释放会同时清理框架能力引用和扩展数据，避免跨租约残留。
- 只有通过 `TargetingPool.RentContext()` 获取的 Context 才会归还全局池；普通 `new SearchContext()` 的 `Dispose()` 只清理自身并允许再次配置后复用。
- 池化 Context 和 Result 的重复或并发释放是幂等的。归还后访问其公开状态会抛出 `ObjectDisposedException`；对象被重新租出后，旧引用无法代表独立租约，因此不得保存或再次使用旧引用。
- `SearchResult.Ids` 是当前租约内对内部列表的只读视图，不是结果快照。该视图及索引器只能在释放前同步消费，不能跨 `Dispose()`、下一次租借或异步边界保存。

### TargetingPool
- 统一接入 `AbilityKit.Core.Pooling`，池化查询上下文、查询结果、命中列表、标识列表、规则列表、排序项列表、共享分数缓冲、组合 Provider 临时集合、查询去重集合和 Top-K 缓冲。
- `TargetSearchEngine` 内部不再常驻私有临时列表，而是单次查询租借临时集合，减少引擎实例并发/复用时的状态污染。
- 融合 Top-K 路径租借 K 个正式命中槽、K × M 个正式评分槽和 M 个临时评分槽；缓冲区由引擎持有并在查询结束时归还。
- 池化数组的物理长度可能大于本次逻辑 K；实现必须始终以 `MaxCount` 作为容量边界。
- 常规峰值容量会保留以减少稳态扩容；命中、结果、规则、排序列表、稳定键集合以及 Top-K/评分数组超过各自保留阈值时，在归还阶段缩回初始容量、收缩集合容量或释放底层数组，避免偶发超大查询造成长期内存滞留。

### 高频低 GC 接入清单

1. Provider 直接遍历空间、阵营、类型或可见集合索引，以结构体 Consumer 推送 ID，不创建临时候选列表，也不使用 LINQ 或接口枚举器。
2. 长期复用无状态 Provider、Rule、Scorer、Selector、Mapper 和已构建 Query；热路径不使用反射、捕获 lambda 或临时委托创建组件。
3. 复用调用方提供的 `List<EntityId>`。`SearchIds(in query, context, results)` 会先清空列表，无需每次创建新列表。
4. 高频路径从 `TargetingPool` 租借 Context，并在 `finally` 中释放；池化 `SearchResult` 必须在 `using` 作用域内消费，不能保存其列表引用。
5. K 远小于有效命中量时使用实现 `IStreamingTopKByScoreSelector` 的 Selector 并设置正数 `MaxCount`；这两个条件同时成立才进入不构建完整命中列表的融合路径。Provider 已保证唯一时保持默认 `Preserve`，避免无必要的查询级去重集合成本。
6. 高频路径优先让单个项目 Provider 直接遍历复合索引；确需多来源集合运算时再使用并、交、差组合器，因为它们会填充池化 `HashSet<ulong>`，交集还会暂存第一来源的唯一候选。
7. 优先消费实体 ID。Mapper 应返回已有业务对象，避免按命中创建包装对象；多个规则共用的昂贵数据应在查询前计算一次，通过包外强类型键保存可变查询数据，或通过组件构造注入复用长期能力。
8. `SearchContextKey<T>` 不会消除值类型装箱。高频数值可集中放入可复用引用类型状态对象，再以一个静态强类型键保存。

推荐调用形态：

```csharp
private readonly TargetSearchEngine _engine = new TargetSearchEngine();
private readonly List<EntityId> _targetIds = new List<EntityId>(8);
private readonly SearchQuery _query;

public void CollectTargets(IPositionProvider positions)
{
    var context = TargetingPool.RentContext();
    try
    {
        context.PositionProvider = positions;
        _engine.SearchIds(in _query, context, _targetIds);
        ConsumeImmediately(_targetIds);
    }
    finally
    {
        TargetingPool.Release(context);
    }
}
```

`_targetIds` 不能被并发查询共享，消费方也不能跨下一次查询持有其内容视图。并发执行应使用独立 Context 和结果容器。

### 执行成本与 GC 验收

令 C 为 Provider 最终推送的候选数，H 为规则通过且评分有效的命中数，K 为结果上限，M 为排序项数：

- 完整命中路径保存约 H × M 个评分，完整排序约 `O(H log H × M)`；自定义 Selector 可以读取完整 H 个命中并执行任意后处理。
- 融合 Streaming Top-K 路径保存约 `(K + 1) × M` 个评分。当前实现使用有序小数组插入，最坏约 `O(H × K × M)`，适合小 K，不是堆式 `O(H log K)`。
- 两条路径都只消费一次最终候选流；复合 Provider 为实现交集或差集仍可能遍历多个来源并租借集合。
- 先预热 JIT、静态初始化和对象池，再测稳态查询，首次扩容单独记录。
- 固定候选规模、命中率、K 和重复策略，分别测无 Selector、普通 Top-K、融合 Top-K、自定义 Selector 和去重路径。
- 同时记录 `ISearchStats` 的 candidate/hit/result、耗时分位数、单次分配字节和 GC 次数。
- Unity 使用 Profiler `GC Alloc` 与 Allocation Call Stacks；纯 .NET 可用 `GC.GetAllocatedBytesForCurrentThread()` 比较批次前后差值。
- 门禁使用量化预算，例如“预热后 1000 次固定查询无托管分配”，不能仅以“使用对象池”作为完成标准。

### 组件注册表
- Rule、Scorer 和 Selector 可通过 Attribute 注册无参类型，也可通过 `RegisterFactory(id, factory)` 注册参数化创建委托。
- 类型与工厂共享同一个整数 ID 命名空间；同一实现类型也只能绑定一个 Attribute ID。首次注册生效，冲突注册不会隐式覆盖。
- 注册、创建和扫描状态由注册表实例锁保护；创建时只在锁内读取 Factory 或 Type 快照，用户 Factory 和反射构造在锁外执行。
- 扫描只在完整成功后标记完成；扫描异常可以重试，但一次失败扫描已经完成的部分注册不会事务性回滚。
- Attribute 类型没有公开无参构造且没有工厂时，`Create(id)` 返回 `null`；项目层应在装配或配置校验阶段报告缺失组件。
- 工厂只返回通用组件接口，不接收技能配置、阵营或具体实体世界等项目类型。
- Builder 的 `FilterById`、`ScoreById`、`ThenScoreById` 和 `SelectById` 在查找失败时保持已有配置，不把缺失 ID 隐式解释为清空操作。

### TargetQueryDatabase
- `Register(queryId, ITargetQueryFactory)`：包外模块可按标识注册上下文驱动的查询工厂。
- `Register(queryId, in SearchQuery)`：注册静态查询。
- `TrySearchIds(queryId, context, out SearchResult)`：以数据库式查询标识执行查询并返回池化结果。
- 未注册或工厂构建失败返回 `false`；空 Context 或空结果列表属于调用参数错误并抛出 `ArgumentNullException`。列表重载在查找目录前先清空调用方结果。
- `ITargetQueryFactory.TryBuild(context, out query)`：业务包可根据技能等级、阵营、锁定目标、落点等上下文动态构造查询。
- 单条注册、替换、移除、清空和读取操作受锁保护；读取方获得 Factory 快照后在锁外构建和执行查询，因此正在执行的搜索不受后续替换影响，也不会阻塞目录写入。
- 多条配置的整体热更不是事务；需要原子切换一组 query ID 时，项目层仍应构建新目录或增加版本化发布边界。

### 候选提供者（推送式候选消费）
`ICandidateProvider.ForEachCandidate<TConsumer>(..., ref TConsumer consumer)`
- 消费者为结构体，实现 `ICandidateConsumer.Consume(EntityId)`
- 设计为推送模式，避免为最终候选流生成中间列表
- “最终候选流只消费一次”不表示复合 Provider 只读取一个来源，也不表示完整命中路径没有排序或 Selector 后处理

### 位置能力（可选上下文属性）
`IPositionProvider.TryGetPosition(EntityId entity, out Vec2 position)`
- 位置不是查询定义的必填字段，而是通过 `SearchContext.PositionProvider` 显式提供的框架能力。
- 缺少位置能力或位置数据时，由具体组件决定失败语义；通用引擎不会预检或把位置要求传播到 Provider、Rule、Scorer、Selector。

### 稳定键（确定性与去重）
`IEntityKeyProvider.GetKey(EntityId id) -> ulong`
- 用于同分/同权重时稳定排序、显式 `DistinctByEntityKey` 去重，以及 Provider 并、交、差集合成员判断
- `SearchContext.EntityKeyProvider` 为空时使用 `EntityId.Value`；存在 ID 复用或多世界标识时，应由项目提供包含版本或世界信息的稳定键
- 默认重复策略是 `Preserve`，框架不会假设两个相同稳定键一定代表应合并的业务对象

## 形状系统（解析器）

### 基础形状规则
- `CircleShapeRule`、`OrientedRectShapeRule`、`SectorShapeRule`

### 解析器化（应对复杂需求）
把“坐标系”和“参数”解耦：

- 参考系：`IShapeFrameResolver2D -> ShapeFrame2D(Origin, Forward, Right)`
- 参数：
  - 矩形：`IRectParamResolver2D`
  - 圆形：`ICircleParamResolver2D`
  - 扇形：`ISectorParamResolver2D`

对应组合规则：
- `ResolvedOrientedRectRule2D`
- `ResolvedCircleRule2D`
- `ResolvedSectorRule2D`

典型复杂需求覆盖：
- **偏移**：`OffsetFrameResolver2D(inner, offsetLocal)`
- **朝向来自两个实体**：`EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin)`
- **动态长度/半径来自两实体距离**：`RectLengthFromEntityDistanceResolver2D` / `CircleRadiusFromEntityDistanceResolver2D`

### 数据驱动解析器（来自上下文数据）

当“来源不是实体”（例如技能落点、鼠标点、外部系统给定点或动态参数）时，可使用数据驱动解析器从 `SearchContext` 的数据中读取：

- `DataFrameResolver2D(originKey, forwardKey)`：从上下文数据读取 `Vector2 origin/forward`
- `DataRectParamsResolver2D(widthKey, lengthKey)`：从上下文数据读取矩形宽/长
- `DataCircleParamsResolver2D(radiusKey)`：从上下文数据读取圆半径
- `DataSectorParamsResolver2D(radiusKey, halfAngleDegKey)`：从上下文数据读取扇形半径/半角

## 与 EntityManager 联动（索引候选）

当候选来自框架层 `BattleEntityManager` 的索引（内部通常是 `HashSet<int>`）时：
- 使用 `IEntityIdCollectionIndex.ForEach(key, ref consumer)` 以避免接口枚举带来的分配
- `EntityManager/KeyedEntityIndexAdapter<TKey>` 提供了将 `IKeyedEntityIndex<TKey, int>` 适配为 `IEntityIdCollectionIndex` 的示例

## Entitas 集成点

### 位置能力
- `Entitas/EntitasActorTransformPositionProvider`：
  - 通过 `EntitasActorIdLookup` 找到 `ActorEntity`
  - 读取 `ActorEntity.transform.Value.Position`，输出 XZ

### 输出类型
- `Entitas/EntitasUnitFacadeMapper`：把 `EntityId -> IUnitFacade`（依赖 `IUnitResolver`）

## 包外扩展建议

- 新候选源：实现 `ICandidateProvider`，从业务索引、实体系统分组、空间划分、服务器快照等来源推送候选。
- 新过滤条件：实现 `ITargetRule`；包外查询数据通过静态 `SearchContextKey<T>` 和业务 facade 读取，长期依赖通过构造注入传入。
- 新评分策略：实现 `ITargetScorer`，支持最近、血量最低、威胁最高、仇恨最高、稳定随机等排序。
- 新选择策略：实现 `ITargetSelector`，支持前若干个结果、加权随机、分组取样、优先级桶等。
- 查询目录：实现 `ITargetQueryFactory` 并注册到 `TargetQueryDatabase`，让技能、智能体或触发器只通过查询标识和上下文发起查询。

## 当前值得继续优化的点

- 引入空间索引候选提供者（网格、四叉树、层次包围盒、EntityManager 索引适配），让候选生成从全量扫描升级为范围查询。
- 增加 `SearchQuery` 描述化/序列化能力，支持配置表、技能模板和热更新层生成查询。
- 扩展核心回归测试，继续覆盖更多调用方异常路径、并发边界和池化容量滞留策略。
- 统一文档中尚未落地的形状解析器与矩形规则命名，避免设计文档超前于运行时实现。

## 选择器（排序/选择策略）

`ITargetSelector.Select` 通过 `SearchHitView` 只读访问引擎拥有的完整命中序列，并通过 `SearchResultWriter` 追加结果。普通自定义选择器不能清空、重排或替换引擎内部列表；需要临时状态时应在当前调用内自行租还。`SearchResultWriter` 始终强制查询的 `MaxCount` 硬上限。

- `TopKByScoreSelector`：在完整命中路径中排序后取前若干个结果，并遵守所有排序项的独立方向。
- `IStreamingTopKByScoreSelector`：公开的无成员能力接口。实现该接口且 `MaxCount > 0` 时，引擎在 Provider 回调内维护固定 Top-K，不调用完整命中后处理。该能力表示实现接受引擎定义的严格 Top-K 语义，不适用于需要完整命中集合的任意后处理。
- `StreamingTopKByScoreSelector`：内置能力实现；无有效 K 或直接调用其 `Select()` 时可对完整命中视图执行回退行为。实例不保存跨查询状态。

## 通用规则与评分器

规则：
- `ExcludeEntityRule`
- `RequireValidIdRule`
- `RequireHasPositionRule`
- `BlacklistRule`（依赖 `IEntityIdSet`）
- `WhitelistRule`（依赖 `IEntityIdSet`）

评分器：
- `DistanceToEntityScorer2D`（最近优先：返回负距离平方）
- `DistanceToFrameOriginScorer2D`
- `SeededHashRandomScorer`（可控随机：可在构造时捕获只读种子，也可显式使用 `SearchContextKey<int>` 读取单次查询种子）

分数边界：
- 任一排序项返回 `float.NaN` 都表示候选不可评分，候选在进入 Selector 前被排除。
- 正负无穷仍是可排序分数，按对应排序项的方向参与比较。
- 业务资格仍应由 Rule 表达，不应普遍用特殊分数替代过滤。
- 多项分数存放在单次查询租借的共享扁平缓冲区中，`SearchHit` 只在本次查询的排序和选择阶段引用该缓冲区，不应跨查询保存。

## 可选统计（默认不启用）

为方便性能/逻辑排查，框架层提供轻量统计钩子：

- `ISearchStats`：`OnCandidate/OnHit/OnResult`
- `SearchStats`：一个简单实现（记录候选数/命中数/结果数）

用法：设置 `SearchContext.SearchStats`，引擎会自动在一次查询中更新统计数据。

## 多来源候选的当前处理方式

当前包提供顺序拼接以及稳定键并、交、差组合器。多阵营、多类型或多来源查询按以下优先级接入：

1. 高频路径优先建立能够直接表达组合键的业务索引，例如 `(camp, type)` 或空间分区索引，由一个 Provider 直接推送尽可能小的候选集。
2. 资格条件优先写成顶层 Rule，或使用 `AndRule`、`OrRule`、`NotRule` 逐候选短路求值；布尔组合不需要中间候选集合。
3. 只需保持来源顺序且允许重复时使用 `ConcatCandidateProvider`；确实需要集合成员语义时，再使用 `UnionDistinctCandidateProvider`、`IntersectCandidateProvider`、`ExceptCandidateProvider`。

集合型组合器按 `IEntityKeyProvider` 的稳定键判断成员，缺失时使用 `EntityId.Value`，并通过 `TargetingPool` 租还临时键集合和交集候选列表。组合器支持嵌套且异常路径会释放租约；列表、数组与稳定键集合池会回收超过阈值的异常容量，但接入层仍应限制异常候选上界，并避免在可直接查询复合索引时进行集合运算。


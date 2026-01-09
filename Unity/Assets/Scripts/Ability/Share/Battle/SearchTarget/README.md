# SearchTarget（战斗查找目标模块）

本模块提供一套通用、可扩展、可替换 ECS 后端、并面向高频调用优化（低/零 GC）的“目标查找”框架。

## 目标

- 统一入口管理各种“找目标”需求。
- 可扩展：候选来源、过滤条件、几何形状、排序/选择策略均可替换。
- 可替换：不依赖具体 ECS（Entitas / ActorEntity / 其他）。
- 高性能：
  - 单次遍历候选（Provider `ForEachCandidate` + struct consumer）。
  - 可选 streaming TopK（不构建 hits 列表）。
  - 候选集合可直接引用索引集合（避免创建/复制临时列表）。
- 确定性：除“显式随机”策略外，相同输入应得到稳定一致的结果；稳定排序使用 `IEntityKeyProvider`。

## 数据流（Pipeline）

1. **候选生成（Provider）**：`ICandidateProvider`
2. **过滤（Rules）**：`ITargetRule[]`
3. **评分（Scorer）**：`ITargetScorer`
4. **选择（Selector）**：`ITargetSelector`（可选 `IStreamingHitSelector`）
5. **映射输出（Mapper）**：`ITargetMapper<T>`（例如输出 `IUnitFacade`）

核心执行由 `TargetSearchEngine` 完成：
- `SearchIds(...)`：输出 `List<EcsEntityId>`
- `Search<T>(..., ITargetMapper<T> mapper)`：输出任意类型列表（如 `IUnitFacade`）

## 关键接口

### SearchQuery
- `Provider`：候选来源
- `Rules`：过滤链
- `Scorer`：评分（用于“最近/最优”等排序）
- `Selector`：选择策略（全量排序 / 在线 TopK / streaming TopK）
- `MaxCount`：TopK 数量（>0 时启用 TopK 逻辑）

### SearchContext
- `SetService<T>/TryGetService<T>`：注入能力（Position/Key/Index/Resolver 等）
- `SetData/TryGetData<T>`：存放上下文数据（技能落点、锁定目标、动态参数等）

### Candidate Provider（零 GC 单次遍历）
`ICandidateProvider.ForEachCandidate<TConsumer>(..., ref TConsumer consumer)`
- consumer 为 struct，实现 `ICandidateConsumer.Consume(EcsEntityId)`
- 设计为 push 模式，避免生成中间候选列表

### Position 能力（严格依赖）
`IPositionProvider.TryGetPositionXZ(EcsEntityId id, out Vector2 positionXZ)`
- 形状过滤、距离评分等规则都依赖此能力
- **严格策略**：如果 Query 中包含 `RequiresPosition=true` 的 provider/scorer/selector/rule，但 `SearchContext` 未提供 `IPositionProvider`，则直接返回空结果。

### 稳定 key（确定性）
`IEntityKeyProvider.GetKey(EcsEntityId id) -> ulong`
- 用于同分/同权重时稳定排序
- 未来如存在 id 复用，可扩展为 `(id, version)` 编码成 key

## 形状系统（Resolver）

### 基础形状 Rule
- `CircleShapeRule`、`OrientedRectShapeRule`、`SectorShapeRule`

### Resolver 化（应对复杂需求）
把“坐标系(frame)”与“参数(params)”解耦：

- Frame：`IShapeFrameResolver2D -> ShapeFrame2D(Origin, Forward, Right)`
- Params：
  - Rect：`IRectParamResolver2D`
  - Circle：`ICircleParamResolver2D`
  - Sector：`ISectorParamResolver2D`

对应组合 rule：
- `ResolvedOrientedRectRule2D`
- `ResolvedCircleRule2D`
- `ResolvedSectorRule2D`

典型复杂需求覆盖：
- **偏移**：`OffsetFrameResolver2D(inner, offsetLocal)`
- **朝向来自两个实体**：`EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin)`
- **动态长度/半径来自两实体距离**：`RectLengthFromEntityDistanceResolver2D` / `CircleRadiusFromEntityDistanceResolver2D`

### Data-based Resolver（来自上下文数据）

当“来源不是实体”（例如技能落点/鼠标点/外部系统给定点/动态参数）时，可使用 Data-based Resolver 从 `SearchContext` 的 data 中读取：

- `DataFrameResolver2D(originKey, forwardKey)`：从 data 读取 `Vector2 origin/forward`
- `DataRectParamsResolver2D(widthKey, lengthKey)`：从 data 读取矩形宽/长
- `DataCircleParamsResolver2D(radiusKey)`：从 data 读取圆半径
- `DataSectorParamsResolver2D(radiusKey, halfAngleDegKey)`：从 data 读取扇形半径/半角

## 与 EntityManager 联动（索引候选）

当候选来自框架层 `BattleEntityManager` 的索引（内部通常是 `HashSet<int>`）时：
- 使用 `IEntityIdCollectionIndex.ForEach(key, ref consumer)` 以避免接口枚举带来的 GC
- `EntityManager/KeyedEntityIndexAdapter<TKey>` 提供了将 `IKeyedEntityIndex<TKey, int>` 适配为 `IEntityIdCollectionIndex` 的示例

## Entitas 集成点

### 位置能力
- `Entitas/EntitasActorTransformPositionProvider`：
  - 通过 `EntitasActorIdLookup` 找到 `ActorEntity`
  - 读取 `ActorEntity.transform.Value.Position`，输出 XZ

### 输出类型
- `Entitas/EntitasUnitFacadeMapper`：把 `EcsEntityId -> IUnitFacade`（依赖 `IUnitResolver`）

## Selector（排序/选择策略）

- `TopKByScoreSelector`：全量排序后取前 K
- `OnlineTopKByScoreSelector`：不排序全量 hits，仅维护 TopK（O(N*K)）
- `Selectors/StreamingTopKByScoreSelector`：**streaming TopK**，引擎在枚举候选时直接 Offer hit，不构建 hits 列表（高频最优）

## 通用规则与 Scorer

规则（Rules）：
- `ExcludeEntityRule`
- `RequireValidIdRule`
- `RequireHasPositionRule`
- `BlacklistRule`（依赖 `IActorIdSet`）
- `WhitelistRule`（依赖 `IActorIdSet`）

Scorer：
- `DistanceToEntityScorer2D`（最近优先：返回负距离平方）
- `DistanceToFrameOriginScorer2D`
- `SeededHashRandomScorer`（可控随机：seed 由 `SearchContext` data 决定）

## 可选统计（默认不启用）

为方便性能/逻辑排查，框架层提供轻量统计钩子：

- `ISearchStats`：`OnCandidate/OnHit/OnResult`
- `SearchStats`：一个简单实现（记录候选数/命中数/结果数）

用法：在 `SearchContext` 注入 `ISearchStats`，引擎会自动在一次查询中更新统计数据。

## Provider 组合器与去重（支持多重条件候选）

当需要表达“多阵营/多类型/多来源”的候选组合时，推荐使用框架层提供的 Provider 组合器，避免业务侧重复实现集合运算。

组合器（Providers）：
- `ConcatCandidateProvider`：按顺序串联多个 Provider（不去重）
- `UnionDistinctCandidateProvider`：并集 + 去重（依赖 `IVisitedSet`）
- `IntersectCandidateProvider`：交集（依赖 `IVisitedSet`）
- `ExceptCandidateProvider`：差集（依赖 `IVisitedSet`）

去重/集合运算依赖：
- `IVisitedSet`：零 GC 的“访问标记”服务
- 默认实现：`Visited/VersionedVisitedSet`（tick/version 递增，避免 Clear O(n)）

使用方式：
- 在 `SearchContext` 注入 `IVisitedSet`（例如 `ctx.SetService<IVisitedSet>(new VersionedVisitedSet())`）
- 使用 `UnionDistinctCandidateProvider` / `IntersectCandidateProvider` / `ExceptCandidateProvider` 时会自动调用 `visited.Next()` 开启一次新的标记周期


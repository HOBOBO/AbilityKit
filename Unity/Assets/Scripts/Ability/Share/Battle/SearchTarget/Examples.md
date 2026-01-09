# SearchTarget 使用示例（组合思路）

本文仅给出“怎么组合”的示例思路，便于业务侧快速拼装 Query。

> 说明：本模块强调复用 `SearchContext` 的 services/data 来注入能力与动态参数。

## 示例 1：给定 id 列表（上下文/外部传入） -> 输出 UnitFacade

- Provider：`ExplicitListCandidateProvider(ids)`
- Rules：可选（如 `RequireValidIdRule`、`ExcludeEntityRule`）
- Scorer：`ZeroScorer` 或距离 scorer
- Selector：`StreamingTopKByScoreSelector`（若只要 TopK）
- Mapper：`EntitasUnitFacadeMapper`

组合要点：
- `SearchContext` 注入 `IUnitResolver`（用于 mapper）
- 若包含形状/距离规则，还要注入 `IPositionProvider`

## 示例 2：索引候选（Camp/Type） + Circle 形状 + 最近优先 + TopK

候选来源（两种常见方式）：

### 2.1 索引是 IReadOnlyList（如自维护 list 索引）
- `IEntityIdIndex` + `IndexedListCandidateProvider(key)`

### 2.2 索引是 HashSet（来自 EntityManager keyed index）
- `IEntityIdCollectionIndex` + `IndexedCollectionCandidateProvider(key)`

过滤与排序：
- Rules：
  - `ResolvedCircleRule2D(frameResolver, circleParams)` 或 `CircleShapeRule`
  - 可选 `ExcludeEntityRule(self)`
- Scorer：`DistanceToEntityScorer2D(self)`（返回负距离平方，最近分数最高）
- Selector：`StreamingTopKByScoreSelector`，设置 `MaxCount = K`

FrameResolver（圆心/朝向来源）：
- 圆心来自施法者：可用自定义 frameResolver（或把 origin 写入 SearchContext data 再实现一个 DataFrameResolver）
- 圆心来自两实体中点：`EntityToEntityFrameResolver2D(a, b, useMidPointAsOrigin:true)`

## 示例 3：矩形宽固定，长度 = 两实体距离（动态） + 起点锚定 + 偏移

需求：
- 矩形沿 source->target 方向
- 宽固定
- 长度动态：distance(source, target) * scale + add
- 锚点：从 source 开始向前延伸
- 再叠加局部偏移（例如向前推 1m、向右偏 0.5m）

组合：
- FrameResolver：
  - `EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin:false)`
  - `OffsetFrameResolver2D(inner, offsetLocal: (rightOffset, forwardOffset))`
- ParamResolver：`RectLengthFromEntityDistanceResolver2D(source, target, width, scale, add, minLength, maxLength)`
- Rule：`ResolvedOrientedRectRule2D(frame, rectParams, pivot: Start)`

注意：
- 这类组合对 `IPositionProvider` 是强依赖（严格模式下缺失直接无结果）。

## 示例 4：扇形（方向来自两实体） + 半径固定 + TopK

- FrameResolver：`EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin:false)`
- SectorParams：`SectorParamsConstantResolver2D(radius, halfAngleDeg)`
- Rule：`ResolvedSectorRule2D(frame, sectorParams)`
- Selector：`StreamingTopKByScoreSelector`（或 online topK）

## 示例 5：技能落点（非实体）作为圆心 / 参数来自上下文（Data-based Resolver）

需求：
- 圆心是技能落点（Vector2）
- 半径来自技能计算结果（float）

做法：
- 把 `originXZ`、`radius` 写入 `SearchContext` data
- FrameResolver 使用 `DataFrameResolver2D(originKey)`
- CircleParams 使用 `DataCircleParamsResolver2D(radiusKey)`
- Rule 使用 `ResolvedCircleRule2D(frame, circleParams)`

## 示例 6：黑名单/白名单（例如“已命中过的目标不再命中”）

- 使用 `BlacklistRule(IActorIdSet)` 或 `WhitelistRule(IActorIdSet)`
- 黑白名单集合实现放业务层，框架层只要求 `IActorIdSet.Contains(actorId)`

## 示例 7：可控随机（seed 控制确定性）

需求：
- 同一帧/同一次技能释放在相同 seed 下随机结果稳定

做法：
- 将 seed（int）写入 `SearchContext` data
- 使用 `SeededHashRandomScorer(seedKey)` 作为 scorer
- 配合 `StreamingTopKByScoreSelector`，并设置 `MaxCount=1` 或 K

## 示例 8：统计钩子（调试候选量/命中量/结果量）

做法：
- 创建一个 `SearchStats` 并注入 `SearchContext`：`ctx.SetService<ISearchStats>(stats)`
- 一次 Search 完成后读取：`stats.Candidates / stats.Hits / stats.Results`

## 常见扩展点建议

- 多阵营/多类型：
  - 高频：建立复合索引 `(camp,type)->set`，Provider 枚举多个 key
  - 通用：使用 Provider 组合器表达集合运算：
    - `UnionDistinctCandidateProvider`：多集合并集（需要 `IVisitedSet` 去重）
    - `IntersectCandidateProvider`：交集（需要 `IVisitedSet`）
    - `ExceptCandidateProvider`：差集（需要 `IVisitedSet`）
  - 去重服务：在 `SearchContext` 注入 `IVisitedSet`（例如 `VersionedVisitedSet`），组合器会在一次搜索开始时调用 `Next()`

- 动态参数来源：
  - 来自实体：用 frame/param resolver 通过 `IPositionProvider` 获取 position
  - 来自上下文数据：使用 `SearchContext.SetData` 写入参数，再实现对应的 DataParamResolver

- 确定性：
  - 使用 `IEntityKeyProvider` 保证同分时稳定排序


# SearchTarget 使用示例（组合思路）

本文仅给出“怎么组合”的示例思路，便于业务侧快速拼装查询。

> 说明：本模块强调复用 `SearchContext` 的服务和数据来注入能力与动态参数。

## 示例 1：给定标识列表（上下文/外部传入） -> 输出单位门面

- 候选提供者：`ExplicitListCandidateProvider(ids)`
- 规则：可选（如 `RequireValidIdRule`、`ExcludeEntityRule`）
- 评分器：`ZeroScorer` 或距离评分器
- 选择器：`StreamingTopKByScoreSelector`（若只要前若干个结果）
- 映射器：`EntitasUnitFacadeMapper`

组合要点：
- `SearchContext` 注入 `IUnitResolver`（用于映射器）
- 若包含形状/距离规则，还要注入 `IPositionProvider`

## 示例 2：索引候选（阵营/类型） + 圆形形状 + 最近优先 + 前若干个结果

候选来源（两种常见方式）：

### 2.1 索引是 `IReadOnlyList`（如自维护列表索引）
- `IEntityIdIndex` + `IndexedListCandidateProvider(key)`

### 2.2 索引是 `HashSet`（来自实体管理器键索引）
- `IEntityIdCollectionIndex` + `IndexedCollectionCandidateProvider(key)`

过滤与排序：
- 规则：
  - `ResolvedCircleRule2D(frameResolver, circleParams)` 或 `CircleShapeRule`
  - 可选 `ExcludeEntityRule(self)`
- 评分器：`DistanceToEntityScorer2D(self)`（返回负距离平方，最近分数最高）
- 选择器：`StreamingTopKByScoreSelector`，设置 `MaxCount` 为需要的数量

参考系解析器（圆心/朝向来源）：
- 圆心来自施法者：可用自定义参考系解析器（或把 `origin` 写入 `SearchContext` 上下文数据，再实现一个数据参考系解析器）
- 圆心来自两实体中点：`EntityToEntityFrameResolver2D(a, b, useMidPointAsOrigin:true)`

## 示例 3：矩形宽固定，长度 = 两实体距离（动态） + 起点锚定 + 偏移

需求：
- 矩形沿来源实体到目标实体的方向
- 宽固定
- 长度动态：来源实体到目标实体的距离乘以缩放值再加偏移值
- 锚点：从来源实体开始向前延伸
- 再叠加局部偏移（例如向前推 1m、向右偏 0.5m）

组合：
- 参考系解析器：
  - `EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin:false)`
  - `OffsetFrameResolver2D(inner, offsetLocal: (rightOffset, forwardOffset))`
- 参数解析器：`RectLengthFromEntityDistanceResolver2D(source, target, width, scale, add, minLength, maxLength)`
- 规则：`ResolvedOrientedRectRule2D(frame, rectParams, pivot: Start)`

注意：
- 这类组合对 `IPositionProvider` 是强依赖（严格模式下缺失直接无结果）。

## 示例 4：扇形（方向来自两实体） + 半径固定 + 前若干个结果

- 参考系解析器：`EntityToEntityFrameResolver2D(source, target, useMidPointAsOrigin:false)`
- 扇形参数：`SectorParamsConstantResolver2D(radius, halfAngleDeg)`
- 规则：`ResolvedSectorRule2D(frame, sectorParams)`
- 选择器：`StreamingTopKByScoreSelector`（或在线前若干个结果选择器）

## 示例 5：技能落点（非实体）作为圆心 / 参数来自上下文（数据驱动解析器）

需求：
- 圆心是技能落点（Vector2）
- 半径来自技能计算结果（float）

做法：
- 把 `originXZ`、`radius` 写入 `SearchContext` 上下文数据
- 参考系解析器使用 `DataFrameResolver2D(originKey)`
- 圆形参数使用 `DataCircleParamsResolver2D(radiusKey)`
- 规则使用 `ResolvedCircleRule2D(frame, circleParams)`

## 示例 6：AND、OR、NOT 条件组合

顶层 Rule 列表和 `AndRule` 都按声明顺序执行，并在首个 `false` 处短路；`OrRule` 在首个 `true` 处短路；`NotRule` 反转一个非空 Rule。空 `AndRule` 为 `true`，空 `OrRule` 为 `false`，因此动态组装时不需要额外伪造恒真或恒假规则。

例如 `(存活 AND 可选中) AND (低血量 OR 距离近) AND NOT 已命中`：

```csharp
var eligibility = new AndRule(
    aliveRule,
    selectableRule,
    new OrRule(lowHealthRule, nearSourceRule),
    new NotRule(alreadyHitRule));
```

把 `eligibility` 作为一个顶层 Rule 加入查询即可。组合器会在构造时复制输入数组，仍建议长期复用组合结果，并把便宜、高淘汰率的子规则放在前面。黑白名单集合实现放业务层，框架层只要求 `IEntityIdSet.Contains(EntityId id)`；业务层可在实现内部把中性标识映射到自己的实体模型。逐候选黑名单判定不等同于 Provider 集合差集。

## 示例 7：可控随机（种子控制确定性）

需求：
- 同一帧/同一次技能释放在相同种子下随机结果稳定

做法：
- 将种子（整数）写入 `SearchContext` 上下文数据
- 使用 `SeededHashRandomScorer(seedKey)` 作为评分器
- 配合 `StreamingTopKByScoreSelector`，并设置 `MaxCount=1` 或需要的数量

## 示例 8：多条件严格排序

需求：先选择距离最近的候选；距离相同时，再选择威胁最高的候选。

```csharp
var builder = SearchPipelineBuilder.Create()
    .From(provider)
    .ScoreBy(
        distanceScorer,
        SearchSortDirection.ScoreAscending)
    .ThenScoreBy(
        threatScorer,
        SearchSortDirection.ScoreDescending)
    .Select(new StreamingTopKByScoreSelector())
    .Take(3);

var query = builder.Build();
builder.Dispose();
```

`ScoreBy()` 设置或替换主排序项，`ThenScoreBy()` 按调用顺序追加次级排序项。比较采用严格字典序：只有距离同分才读取威胁排序结果，全部排序项同分后由稳定键升序决胜。每个排序项都可独立指定升序或降序；这与把多个分数乘权重后相加不是同一种语义。

## 示例 9：统计钩子（调试候选量/命中量/结果量）

做法：
- 创建一个 `SearchStats` 并注入 `SearchContext`：`ctx.SetService<ISearchStats>(stats)`
- 一次查询完成后读取：`stats.Candidates / stats.Hits / stats.Results`

## 常见扩展点建议

- 多阵营/多类型：
  - 高频：建立复合索引 `(camp,type)->set`，由一个项目 Provider 直接遍历目标索引
  - 资格组合：优先用多个顶层 Rule 或 `AndRule`、`OrRule`、`NotRule` 逐候选求值，避免中间候选集合
  - 顺序拼接：使用 `ConcatCandidateProvider(sourceA, sourceB)`，按来源顺序推送并保留重复
  - 集合组合：使用 `UnionDistinctCandidateProvider`、`IntersectCandidateProvider`、`ExceptCandidateProvider`；三者按 `IEntityKeyProvider` 的稳定键判断成员，保留首次代表项和契约规定的顺序
  - 成本边界：并、交、差会使用池化键集合，交集还会暂存第一来源中的唯一候选；高频路径能直接查询复合索引时仍优先单一项目 Provider
  - 查询级 `DistinctByEntityKey` 只能消除最终 Provider 已推送的重复 ID，不替代来源级交集或差集

- 动态参数来源：
  - 来自实体：用参考系解析器和参数解析器通过 `IPositionProvider` 获取位置
  - 来自上下文数据：使用 `SearchContext.SetData` 写入参数，再实现对应的数据参数解析器

- 确定性：
  - 使用 `IEntityKeyProvider` 保证同分时稳定排序


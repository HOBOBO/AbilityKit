# SearchTarget 使用示例（组合思路）

> 文档类型：组合示例
> 核校状态：2026-11-25 部分核校；已纠正旧类型与服务定位器写法。本文是组合配方，片段省略宿主与业务实现，完整可运行程序见[使用指南](Usage.md)。

## 示例 1：给定标识列表（上下文/外部传入） -> 输出单位门面

项目实现 `ICandidateProvider` 推送已有 ID 集合；`ExplicitListCandidateProvider` 不是本包内置类型。可加 RequireValidIdRule、ExcludeEntityRule，用 ZeroScorer 和选择器取需要的数量。输出业务对象时由项目实现 `ITargetMapper<T>`；`EntitasUnitFacadeMapper` 是包外适配器，不是本包依赖。

## 示例 2：索引候选（阵营/类型） + 圆形形状 + 最近优先 + 前若干个结果

### 2.1 索引是 `IReadOnlyList`（如自维护列表索引）

由项目 Provider 用索引循环调用 Consumer，不先复制临时集合。索引在查询期间不能被并发修改；本包不内置 IndexedListCandidateProvider。

### 2.2 索引是 `HashSet`（来自实体管理器键索引）

同样由项目 Provider 推送中性 ID；调用者保证稳定身份。配合 CircleShapeRule(origin, radius)、DistanceToEntityScorer(self)、StreamingTopKByScoreSelector 和正数 Take(K)，并注入 PositionProvider。距离评分是负距离平方，默认降序最近优先。

## 示例 3：矩形宽固定，长度 = 两实体距离（动态） + 起点锚定 + 偏移

这是项目扩展需求。当前本包没有 OrientedRectShapeRule、RectLengthFromEntityDistanceResolver2D 或参考系解析器。由项目实现 ITargetRule，明确读取坐标、方向归一化、锚点和边界策略，再通过 Filter 接入；不能把早期设计类型当成运行时 API。

## 示例 4：扇形（方向来自两实体） + 半径固定 + 前若干个结果

从 IPositionProvider 取得两实体位置，由宿主求方向，在本次查询构建 `SectorShapeRule(origin, forward, radius, halfAngleDegrees)`。配合 Take(K) 与 StreamingTopKByScoreSelector；位置缺失由宿主拒绝构建或让规则不匹配。角参数是半角，不是完整开角。

## 示例 5：技能落点（非实体）作为圆心 / 参数来自上下文（数据驱动解析器）

项目静态持有 `SearchContextKey<Vec2>` 和 `SearchContextKey<float>`，调用前 SetData；ITargetQueryFactory.TryBuild 读取参数并构建 CircleShapeRule。没有内置 DataFrameResolver2D / DataCircleParamsResolver2D；动态建 Query 有分配成本，高频路径可改用读键的项目 Rule 并实测。

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

需求：先选择距离最近的候选；距离相同时，再选择威胁最高的候选。以下为片段：`provider`、`threatScorer` 由项目提供，`distanceScorer` 特指返回正距离平方的项目评分器，故用升序；若使用内置 `DistanceToEntityScorer`，应改为降序。

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
- 项目实现 `ISearchStats`（需要明细时实现 `ISearchDetailedStats`）并注入 `SearchContext`：`ctx.SearchStats = stats`
- 一次查询完成后读取：自己在 Reset / OnCandidate / OnHit / OnResult 中累计的计数（不是包内置 SearchStats 类型）

## 常见扩展点建议

- 多阵营/多类型：
  - 高频：建立复合索引 `(camp,type)->set`，由一个项目 Provider 直接遍历目标索引
  - 资格组合：优先用多个顶层 Rule 或 `AndRule`、`OrRule`、`NotRule` 逐候选求值，避免中间候选集合
  - 顺序拼接：使用 `ConcatCandidateProvider(sourceA, sourceB)`，按来源顺序推送并保留重复
  - 集合组合：使用 `UnionDistinctCandidateProvider`、`IntersectCandidateProvider`、`ExceptCandidateProvider`；三者按 `IEntityKeyProvider` 的稳定键判断成员，保留首次代表项和契约规定的顺序
  - 成本边界：并、交、差会使用池化键集合，交集还会暂存第一来源中的唯一候选；高频路径能直接查询复合索引时仍优先单一项目 Provider
  - 查询级 `DistinctByEntityKey` 只能消除最终 Provider 已推送的重复 ID，不替代来源级交集或差集

- 动态参数来源：
  - 来自实体：由项目 Rule 或 QueryFactory 通过 `IPositionProvider` 获取位置
  - 来自上下文数据：使用静态强类型键与 `SearchContext.SetData`，由项目 Rule 或 QueryFactory 读取

- 确定性：
  - 使用 `IEntityKeyProvider` 明确同分稳定键，并保证候选与各组件输入一致；这不等于跨平台战斗确定性


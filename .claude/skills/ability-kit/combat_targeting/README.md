# combat.targeting — 目标搜索与选择

包 `com.abilitykit.combat.targeting`。可组合搜索管道，用于 BT 节点和技能目标选择。22 个 .cs 文件。

## 核心架构

```
SearchQuery(条件/范围/层)
    → TargetSearchEngine (执行)
    → ICandidateProvider (ActorId/位置 提供者)
    → ITargetRule (过滤条件)
    → ITargetScorer (评分)
    → ITargetSelector (TopK / Single)
    → SearchHit[SearchResult]
```

## 关键类型

- `TargetSearchEngine` — 核心引擎，执行 SearchQuery
- `SearchQuery` / `SearchContext` / `SearchResult` / `SearchHit` / `SearchHitBuffer`
- `ITargetRule` — 过滤条件接口（如"同队伍""存活""在范围内"）
- `ITargetScorer` — 评分接口（如"最近""最弱"）
- `ITargetSelector` — 选择接口（TopK、Single）
- `ICandidateProvider` / `IPositionProvider` — 候选源
- `SearchPipelineBuilder` — 流式构建器（+扩展方法）
- `TargetQueryDatabase` / `TargetRegistries` — 查询索引
- `TopKSelectors` / `CommonRules` / `CommonScorers` — 内置实现
- `TargetingPool` — 池化支持
- `IEntityId` / `IVec2` / `IAdditionalInterfaces` — 抽象（包不依赖 Entitas/Unity）

## BT 集成

moba demo 的 `MobaSelectNearestEnemyAction`（BT 节点）内部调用 `TargetSearchEngine` 搜索最近敌人。

## 关键文件
- `Runtime/SearchTarget/TargetSearchEngine.cs`
- `Runtime/SearchTarget/SearchPipelineBuilder.cs`
- `Runtime/SearchTarget/CommonRules.cs`
- `Runtime/SearchTarget/CommonScorers.cs`

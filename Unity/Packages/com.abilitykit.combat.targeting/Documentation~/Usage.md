# Combat Targeting 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.combat.targeting` 0.1.0，2026-11-25 Runtime 与 .NET 10 镜像
> 核校状态：部分核校；基础组合、排序、租约和失败契约已对照源码，局部 E3 见第 5 节
> 事实基线：2026-11-25

## 1. 目标与前置条件

目标是从宿主提供的中性 ID 流中筛选目标。宿主负责 ID 与世界映射、候选索引、位置坐标约定、阵营/资格、时序和跨帧有效性。本包不依赖具体实体系统；EntityId 只携带 ulong，不含 registry 或 generation。

版本、Unity 与依赖见 [package.json](../package.json)，安装及命名空间见[包 README](../README.md)。Rule/Scorer 中 Vec2 是包内中性二维值，不是 UnityEngine.Vector2；宿主自行决定二维平面，例如 Unity 世界的 XZ。

## 2. 完整示例：圆内最近两个目标

下面是完整 Console 程序。示例的 FixedCandidates / Positions 是本地适配器，不是包内置类型。结果为 2、3，距离评分返回负距离平方，因此默认降序是最近优先。预期输出 `targeting query contracts passed`。

```csharp
using System;
using System.Collections.Generic;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;

internal static class Program
{
    private static void Main()
    {
        var builder = SearchPipelineBuilder.Create()
            .From(new FixedCandidates())
            .Filter(new ExcludeEntityRule(new EntityId(1)))
            .Filter(new CircleShapeRule(Vec2.Zero, 5))
            .ScoreBy(new DistanceToEntityScorer(new EntityId(1)))
            .Select(new StreamingTopKByScoreSelector())
            .Take(2);
        SearchQuery query;
        try { query = builder.Build(); }
        finally { builder.Dispose(); }

        var engine = new TargetSearchEngine();
        var context = TargetingPool.RentContext();
        try
        {
            context.PositionProvider = new Positions();
            var ids = new List<EntityId> { new EntityId(99) };
            engine.SearchIds(in query, context, ids);
            Require(ids.Count == 2 && ids[0].Value == 2 && ids[1].Value == 3);
            // 另一个重载返回池化结果，必须在本租约内消费。
            using (var pooled = engine.SearchIds(in query, context))
            {
                Require(pooled.Ids.Count == 2 && pooled.Ids[0].Value == 2);
            }
        }
        finally { TargetingPool.Release(context); }
        Console.WriteLine("targeting query contracts passed");
    }
    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("Targeting example failed.");
    }
    private sealed class FixedCandidates : ICandidateProvider
    {
        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context,
            ref TConsumer consumer) where TConsumer : struct, ICandidateConsumer
        {
            for (int id = 1; id <= 4; id++) consumer.Consume(new EntityId(id));
        }
    }
    private sealed class Positions : IPositionProvider
    {
        public bool TryGetPosition(EntityId entity, out Vec2 position)
        {
            switch (entity.Value)
            {
                case 1: position = Vec2.Zero; return true;
                case 2: position = new Vec2(1, 0); return true;
                case 3: position = new Vec2(3, 0); return true;
                case 4: position = new Vec2(9, 0); return true;
                default: position = default(Vec2); return false;
            }
        }
    }
}
```

## 3. 查询、租约与失败

| 边界 | 使用契约 |
| --- | --- |
| Builder | Build 复制规则和排序项列表，随后 Dispose 归还 Builder 的临时集合；组件实例浅共享。Builder 是可变 ref struct，不复制多个持有者来重复释放同一列表 |
| Query | 值类型配置快照，不深冻结 Provider/Rule/Scorer/Selector 或业务集合；复用时保持其依赖有效 |
| Context | 每笔并发或重入查询独占；框架能力用 PositionProvider / EntityKeyProvider / SearchStats 属性注入，没有通用 SetService |
| 扩展数据 | 静态持有 SearchContextKey<T>，用 SetData / TryGetData；按键实例隔离，名称只诊断，值类型仍可能装箱 |
| 调用方 List | SearchIds 先 Clear，再写本次结果；调用者拥有列表，不能并发共享或跨下一次查询保存视图 |
| 池化 Result | 用 using / Dispose；Ids 是本次租约的只读视图，归还后不再跟随底层列表。归还后的 Result 引用及已取得的枚举器不可继续使用；需要长期保存就用 CopyTo 复制 |
| 池化 Context | 用 try/finally 归还；Dispose 同样归还。释放后不能继续使用旧引用，即使池后来重新出租了同一实例 |

引擎不封装业务异常为 Result 状态，Provider、Rule、Scorer、Selector、Mapper 或基础统计回调异常会传播。只有详细诊断 `OnDecision` 的异常被隔离，见[运行时手册的统计契约](Manual.md#可选统计默认不启用)。内部租约在 `finally` 归还，调用方仍要清理自己的 Context 并丢弃可能部分写入的列表。

无 Provider 返回空；无位置服务或候选位置缺失时 Circle/Sector 不匹配。距离评分缺位置返回负无穷，而负无穷仍可参与排序；要排除缺位置候选，显式加 RequireHasPositionRule。任一排序项为 NaN 会剔除候选。位置单位、阵营与有效性不由引擎猜测。

## 4. 排序、去重与扩展路径

ScoreBy 设置主项，ThenScoreBy 追加次项；每项各自指定升降序，严格字典序后按稳定键升序决胜。DistanceToEntityScorer 返回负距离平方，用默认降序最近优先；若自定义评分返回正距离平方，才应选择升序。默认保留候选重复，需要时显式 DistinctCandidatesByEntityKey；键缺省为 EntityId.Value，业务提供 IEntityKeyProvider 时也决定集合组合与去重身份。

Take(0) 表示不限，负值被拒绝。正 K 加支持 `IStreamingTopKByScoreSelector` 的选择器才走融合 Top-K；普通选择器可看全部命中，但 Writer 仍限制输出数。当前融合路径是有序小数组插入，适合小 K；不能据“Streaming”推定堆复杂度或零分配。完整成本、目录和注册表契约见[运行时手册](Manual.md)。

CircleShapeRule、SectorShapeRule 和 DistanceToEntityScorer 已实现；旧文档中的 OrientedRectShapeRule、Resolved*Rule2D 和参考系解析器不是本包当前 API。矩形或动态几何由项目实现 ITargetRule，动态查询可由 ITargetQueryFactory 基于 Context 构建。按[组合示例](Examples.md)组织，不把包外 Entitas/EntityManager 适配器当运行时依赖。

## 5. 验证范围与维护

2026-11-25 核对 [Builder](../Runtime/SearchTarget/Pipeline/SearchPipelineBuilder.cs)、[Query](../Runtime/SearchTarget/Queries/SearchQuery.cs)、[Engine](../Runtime/SearchTarget/Execution/TargetSearchEngine.cs)、[Context](../Runtime/SearchTarget/Execution/SearchContext.cs)、[Result](../Runtime/SearchTarget/Execution/SearchResult.cs)、[规则](../Runtime/SearchTarget/Rules/CommonRules.cs)及[评分器](../Runtime/SearchTarget/Scorers/CommonScorers.cs)。同源 .NET suite 与本页完整示例本次结果由仓库 D4 记录持有，证据限 E0 与局部 E3。

未验证独立安装、Unity Player、真实世界/空间索引、跨端确定性、性能预算或完整 gate/CI。手册保留性能测量方法，不把它写成本轮已测结果。返回[文档导航](README.md)。

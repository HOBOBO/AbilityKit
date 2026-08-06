using System;
using System.Collections.Generic;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Targeting
{
    /// <summary>
    /// Targeting 模块基础示例
    /// </summary>
    [Sample]
    public sealed class TargetingBasics : SampleBase
    {
        public override string Title => "Targeting Basics";
        public override string Description => "目标搜索系统的基本概念和使用方式";
        public override SampleCategory Category => SampleCategory.Targeting;

        protected override void OnRun()
        {
            Log("Targeting 目标搜索模块");
            Output.Divider();

            ExplainArchitecture();
            Output.Divider();
            DemonstrateBasicUsage();
        }

        private void ExplainArchitecture()
        {
            Log("架构概述:");
            Log("");
            Log("TargetSearchEngine 是核心搜索引擎，负责执行目标搜索流程");
            Log("");
            Log("搜索流程: Provider -> Rules -> Scorer -> Selector -> Results");
            Log("              |          |          |         |");
            Log("           候选来源     过滤器     评分      选择");
            Log("");
            Log("核心接口:");
            Output.Bullet("ICandidateProvider - 提供候选实体");
            Output.Bullet("ITargetRule - 过滤候选实体");
            Output.Bullet("ITargetScorer - 为候选实体打分");
            Output.Bullet("ITargetSelector - 从候选中选择最终结果");
            Output.Bullet("IPositionProvider - 提供实体位置（可选）");
            Output.Bullet("SearchContext - 承载框架能力属性和包外强类型扩展数据");
        }

        private void DemonstrateBasicUsage()
        {
            Log("基本使用示例:");
            Log("");

            // 创建搜索引擎
            var engine = new TargetSearchEngine();
            var context = new SearchContext();

            // 设置位置提供者（演示用）
            var positionProvider = new SimplePositionProvider();
            positionProvider.SetPosition(1, 0f, 0f);
            positionProvider.SetPosition(2, 5f, 0f);
            positionProvider.SetPosition(3, 10f, 0f);
            positionProvider.SetPosition(4, 15f, 0f);
            positionProvider.SetPosition(5, 20f, 0f);
            context.PositionProvider = positionProvider;

            Log("假设有以下实体:");
            Log("  Entity 1: 位置 (0, 0)");
            Log("  Entity 2: 位置 (5, 0)");
            Log("  Entity 3: 位置 (10, 0)");
            Log("  Entity 4: 位置 (15, 0)");
            Log("  Entity 5: 位置 (20, 0)");
            Log("");

            Log("示例 1: 查找距离 Entity 1 最近的 3 个目标");
            var results1 = new List<EntityId>();
            var query1 = SearchPipelineBuilder.Create()
                .From(new AllEntitiesProvider(new[] { 1, 2, 3, 4, 5 }))
                .Filter(new CircleShapeRule(new Vec2(0f, 0f), 100f))
                .ScoreBy(new DistanceToEntityScorer(new EntityId(1)))
                .Take(3)
                .Build();

            engine.SearchIds(in query1, context, results1);
            Log($"  结果: {FormatIds(results1)}");
            Log("");

            Log("示例 2: 查找 Entity 1 周围 10 范围内的所有目标");
            var results2 = new List<EntityId>();
            var query2 = SearchPipelineBuilder.Create()
                .From(new AllEntitiesProvider(new[] { 1, 2, 3, 4, 5 }))
                .Filter(new CircleShapeRule(new Vec2(0f, 0f), 10f))
                .Take(10)
                .Build();

            engine.SearchIds(in query2, context, results2);
            Log($"  结果: {FormatIds(results2)}");
            Log("");

            Log("示例 3: 使用扇形选择器");
            var results3 = new List<EntityId>();
            var query3 = SearchPipelineBuilder.Create()
                .From(new AllEntitiesProvider(new[] { 1, 2, 3, 4, 5 }))
                .Filter(new SectorShapeRule(new Vec2(0f, 0f), Vec2.Up, 15f, 60f))
                .Select(new TopKByScoreSelector())
                .Take(10)
                .Build();

            engine.SearchIds(in query3, context, results3);
            Log($"  结果: {FormatIds(results3)} (前向 60 度扇形)");
        }

        private static string FormatIds(List<EntityId> ids)
        {
            if (ids.Count == 0) return "[]";
            var parts = new List<string>();
            foreach (var id in ids)
                parts.Add($"Entity {id.Value}");
            return $"[{string.Join(", ", parts)}]";
        }
    }

    /// <summary>
    /// 简单的位置提供者实现（演示用）
    /// </summary>
    public sealed class SimplePositionProvider : IPositionProvider
    {
        private readonly Dictionary<int, Vec2> _positions = new Dictionary<int, Vec2>();

        public void SetPosition(int actorId, float x, float y)
        {
            _positions[actorId] = new Vec2(x, y);
        }

        public bool TryGetPosition(EntityId entity, out Vec2 position)
        {
            if (entity.Value <= int.MaxValue &&
                _positions.TryGetValue((int)entity.Value, out var pos))
            {
                position = pos;
                return true;
            }
            position = Vec2.Zero;
            return false;
        }
    }

    /// <summary>
    /// 提供所有实体的 Provider（演示用）
    /// </summary>
    public sealed class AllEntitiesProvider : ICandidateProvider
    {
        private readonly int[] _entityIds;

        public AllEntitiesProvider(int[] entityIds)
        {
            _entityIds = entityIds;
        }

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            for (int i = 0; i < _entityIds.Length; i++)
            {
                consumer.Consume(new EntityId(_entityIds[i]));
            }
        }
    }
}

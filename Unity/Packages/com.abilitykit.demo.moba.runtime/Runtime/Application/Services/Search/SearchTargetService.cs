using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Mathematics;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Demo.Moba.Services.Search
{
    /// <summary>
    /// 目标搜索服务
    /// 提供基于配置模板的单位目标搜索功能
    /// </summary>
    [WorldService(typeof(SearchTargetService))]
    public sealed class SearchTargetService : IService
    {
        private readonly MobaConfigDatabase _configs;
        private readonly TargetSearchEngine _engine = new TargetSearchEngine();
        private readonly IPositionProvider _positionProvider;
        private readonly AllActorsCandidateProvider _allActorsProvider;
        private readonly MobaSearchQueryBuilder _queryBuilder;

        public SearchTargetService(MobaActorRegistry actors, MobaConfigDatabase configs = null, MobaCombatRulesService combatRules = null)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            _configs = configs;
            _positionProvider = new RegistryPositionProvider(actors);
            _allActorsProvider = new AllActorsCandidateProvider(actors);
            _queryBuilder = new MobaSearchQueryBuilder(actors, _allActorsProvider, combatRules);
        }

        /// <summary>
        /// 搜索最近的单个目标
        /// </summary>
        public bool TrySearchFirstActorId(int queryTemplateId, int casterActorId, in Vec3 aimPos, out int targetActorId)
        {
            targetActorId = 0;
            if (queryTemplateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTemplateId), queryTemplateId, "Search query template id must be positive.");
            }

            var context = RentContext();
            try
            {
                var query = BuildQuery(
                    context,
                    queryTemplateId,
                    casterActorId,
                    in aimPos,
                    explicitTargetActorId: 0,
                    maxCountOverride: 1);
                using (var searchResult = _engine.SearchIds(in query, context))
                {
                    return searchResult.Count > 0 &&
                        TryGetActorId(searchResult[0], out targetActorId);
                }
            }
            finally
            {
                TargetingPool.Release(context);
            }
        }

        /// <summary>
        /// 搜索多个目标
        /// </summary>
        public bool TrySearchActorIds(int queryTemplateId, int casterActorId, in Vec3 aimPos, int explicitTargetActorId, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            if (queryTemplateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTemplateId), queryTemplateId, "Search query template id must be positive.");
            }

            var context = RentContext();
            try
            {
                var query = BuildQuery(
                    context,
                    queryTemplateId,
                    casterActorId,
                    in aimPos,
                    explicitTargetActorId,
                    maxCountOverride: 0);
                return ExecuteSearch(in query, context, results);
            }
            finally
            {
                TargetingPool.Release(context);
            }
        }

        public bool TrySearchActorIds(SearchQueryTemplateMO template, int casterActorId, in Vec3 aimPos, int explicitTargetActorId, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var context = RentContext();
            try
            {
                if (!_queryBuilder.TryBuild(template, context, casterActorId, in aimPos, explicitTargetActorId, maxCountOverride: 0, out var query))
                {
                    throw new InvalidOperationException($"Search query builder failed without diagnostics. templateId={template.Id}");
                }

                return ExecuteSearch(in query, context, results);
            }
            finally
            {
                TargetingPool.Release(context);
            }
        }

        private bool ExecuteSearch(in SearchQuery query, SearchContext context, List<int> results)
        {
            using (var searchResult = _engine.SearchIds(in query, context))
            {
                for (int i = 0; i < searchResult.Count; i++)
                {
                    if (TryGetActorId(searchResult[i], out var actorId))
                    {
                        results.Add(actorId);
                    }
                }
            }

            return results.Count > 0;
        }

        private SearchQuery BuildQuery(
            SearchContext context,
            int queryTemplateId,
            int casterActorId,
            in Vec3 aimPos,
            int explicitTargetActorId,
            int maxCountOverride)
        {
            var template = GetTemplate(queryTemplateId);
            if (!_queryBuilder.TryBuild(template, context, casterActorId, in aimPos, explicitTargetActorId, maxCountOverride, out var query))
            {
                throw new InvalidOperationException($"Search query builder failed without diagnostics. templateId={queryTemplateId}");
            }

            return query;
        }

        private SearchContext RentContext()
        {
            var context = TargetingPool.RentContext();
            context.PositionProvider = _positionProvider;
            return context;
        }

        private static bool TryGetActorId(ST.EntityId entity, out int actorId)
        {
            if (!entity.IsValid || entity.Value > int.MaxValue)
            {
                actorId = 0;
                return false;
            }

            actorId = (int)entity.Value;
            return true;
        }

        private SearchQueryTemplateMO GetTemplate(int queryTemplateId)
        {
            if (queryTemplateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTemplateId), queryTemplateId, "Search query template id must be positive.");
            }

            if (_configs == null)
            {
                throw new InvalidOperationException("SearchTargetService requires MobaConfigDatabase for template queries.");
            }

            if (!_configs.TryGetSearchQueryTemplate(queryTemplateId, out var template) || template == null)
            {
                throw new InvalidOperationException($"Search query template not found. templateId={queryTemplateId}");
            }

            return template;
        }

        private sealed class RegistryPositionProvider : IPositionProvider
        {
            private readonly MobaActorRegistry _actors;

            public RegistryPositionProvider(MobaActorRegistry actors)
            {
                _actors = actors;
            }

            public bool TryGetPosition(ST.EntityId entity, out ST.Vec2 position)
            {
                position = default;
                if (_actors == null || !TryGetActorId(entity, out var actorId)) return false;

                if (!_actors.TryGet(actorId, out var e) || e == null) return false;
                if (!e.hasTransform) return false;

                var p = e.transform.Value.Position;
                position = new ST.Vec2(p.X, p.Z);
                return true;
            }
        }

        private sealed class AllActorsCandidateProvider : ICandidateProvider
        {
            private readonly MobaActorRegistry _actors;

            public AllActorsCandidateProvider(MobaActorRegistry actors)
            {
                _actors = actors;
            }

            public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
                where TConsumer : struct, ICandidateConsumer
            {
                if (_actors == null) return;

                foreach (var kv in _actors.Entries)
                {
                    var id = kv.Key;
                    if (id <= 0) continue;
                    consumer.Consume(new ST.EntityId(id));
                }
            }
        }

        public void Dispose()
        {
        }
    }
}

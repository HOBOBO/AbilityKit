using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Diagnostics;
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
        private readonly IMobaBattleDiagnosticEventSink _eventCollector;

        public SearchTargetService(MobaActorRegistry actors, MobaConfigDatabase configs = null,
            MobaCombatRulesService combatRules = null, IMobaBattleDiagnosticEventSink eventCollector = null)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            _configs = configs;
            _positionProvider = new RegistryPositionProvider(actors);
            _allActorsProvider = new AllActorsCandidateProvider(actors);
            _queryBuilder = new MobaSearchQueryBuilder(actors, _allActorsProvider, combatRules);
            _eventCollector = eventCollector;
        }

        /// <summary>
        /// 搜索最近的单个目标
        /// </summary>
        public bool TrySearchFirstActorId(int queryTemplateId, int casterActorId, in Vec3 aimPos,
            out int targetActorId, long diagnosticCommandId = 0L)
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
                var trace = PrepareTrace(context);
                using (var searchResult = _engine.SearchIds(in query, context))
                {
                    var found = searchResult.Count > 0 && TryGetActorId(searchResult[0], out targetActorId);
                    CollectSearchTrace(trace, queryTemplateId, casterActorId, 0,
                        diagnosticCommandId, searchResult.Ids);
                    return found;
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
        public bool TrySearchActorIds(int queryTemplateId, int casterActorId, in Vec3 aimPos,
            int explicitTargetActorId, List<int> results, long diagnosticCommandId = 0L)
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
                return ExecuteSearch(in query, context, results, queryTemplateId, casterActorId,
                    explicitTargetActorId, diagnosticCommandId);
            }
            finally
            {
                TargetingPool.Release(context);
            }
        }

        public bool TrySearchActorIds(SearchQueryTemplateMO template, int casterActorId, in Vec3 aimPos,
            int explicitTargetActorId, List<int> results, long diagnosticCommandId = 0L)
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

                return ExecuteSearch(in query, context, results, template.Id, casterActorId,
                    explicitTargetActorId, diagnosticCommandId);
            }
            finally
            {
                TargetingPool.Release(context);
            }
        }

        private bool ExecuteSearch(in SearchQuery query, SearchContext context, List<int> results,
            int templateId, int casterActorId, int explicitTargetActorId,
            long diagnosticCommandId)
        {
            var trace = PrepareTrace(context);
            using (var searchResult = _engine.SearchIds(in query, context))
            {
                for (int i = 0; i < searchResult.Count; i++)
                {
                    if (TryGetActorId(searchResult[i], out var actorId))
                    {
                        results.Add(actorId);
                    }
                }
                CollectSearchTrace(trace, templateId, casterActorId, explicitTargetActorId,
                    diagnosticCommandId, searchResult.Ids);
            }

            return results.Count > 0;
        }

        private SearchTraceStats PrepareTrace(SearchContext context)
        {
            if (_eventCollector == null || !_eventCollector.IsEnabled(BattleDiagnosticEventChannel.Targeting))
                return null;
            var trace = new SearchTraceStats();
            context.SearchStats = trace;
            return trace;
        }

        private void CollectSearchTrace(SearchTraceStats trace, int templateId, int casterActorId,
            int explicitTargetActorId, long diagnosticCommandId,
            IReadOnlyList<ST.EntityId> selected)
        {
            if (trace == null) return;
            try
            {
                var selectedIds = trace.DescribeSelected(selected);
                var decisions = trace.DescribeDecisions(selected);
                var payloadData = new BattleDiagnosticTargetSearchPayload(
                    diagnosticCommandId, explicitTargetActorId, trace.CandidateCount,
                    trace.EligibleCount, selected?.Count ?? 0, selectedIds, decisions);
                var payload = BattleDiagnosticEventPayload.FromTargetSearch(in payloadData);
                var draft = new MobaBattleDiagnosticEventDraft(
                    BattleDiagnosticEventKind.TargetSearch, BattleDiagnosticEventChannel.Targeting,
                    selected != null && selected.Count > 0
                        ? BattleDiagnosticEventOutcome.Succeeded : BattleDiagnosticEventOutcome.Failed,
                    sourceActorId: casterActorId, configId: templateId,
                    payloadVersion: BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion,
                    summary: $"command={diagnosticCommandId} candidates={trace.CandidateCount} " +
                             $"eligible={trace.EligibleCount} selected={selected?.Count ?? 0}",
                    payload: payload);
                _eventCollector.TryCollect(in draft);
            }
            catch { }
        }

        private sealed class SearchTraceStats : ST.ISearchDetailedStats
        {
            private readonly List<SearchDecision> _decisions = new List<SearchDecision>(32);
            private int _candidates;
            private int _eligible;
            public int CandidateCount => _candidates;
            public int EligibleCount => _eligible;

            public void Reset() { _decisions.Clear(); _candidates = 0; _eligible = 0; }
            public void OnCandidate() { _candidates++; }
            public void OnHit() { _eligible++; }
            public void OnResult(int count) { }
            public void OnDecision(ST.EntityId id, ST.SearchCandidateDecision decision, int ruleIndex,
                string ruleName, float? primaryScore)
            {
                if (_decisions.Count < 32)
                    _decisions.Add(new SearchDecision(id, decision, ruleIndex, ruleName, primaryScore));
            }

            public string DescribeSelected(IReadOnlyList<ST.EntityId> selected)
            {
                var builder = new StringBuilder();
                if (selected != null)
                {
                    for (var rank = 0; rank < selected.Count && rank < 32; rank++)
                    {
                        if (builder.Length > 0) builder.Append(',');
                        builder.Append(selected[rank].Value);
                    }
                }
                return builder.ToString();
            }

            public string DescribeDecisions(IReadOnlyList<ST.EntityId> selected)
            {
                var builder = new StringBuilder();
                for (var i = 0; i < _decisions.Count; i++)
                {
                    var item = _decisions[i];
                    var rank = -1;
                    if (item.Decision == ST.SearchCandidateDecision.Eligible && selected != null)
                        for (var j = 0; j < selected.Count; j++)
                            if (selected[j].Value == item.Id.Value) { rank = j + 1; break; }
                    builder.Append('\n').Append(item.Id.Value).Append(": ")
                        .Append(rank > 0 ? "selected #" + rank :
                            item.Decision == ST.SearchCandidateDecision.Eligible ? "eligible, not selected" : item.Decision.ToString());
                    if (item.Decision == ST.SearchCandidateDecision.RuleRejected)
                        builder.Append(" rule[").Append(item.RuleIndex).Append("] ").Append(item.RuleName);
                    if (item.PrimaryScore.HasValue)
                        builder.Append(" score=").Append(item.PrimaryScore.Value.ToString("0.###",
                            System.Globalization.CultureInfo.InvariantCulture));
                }
                if (_candidates > _decisions.Count) builder.Append("\n...");
                return builder.ToString();
            }

            private readonly struct SearchDecision
            {
                public SearchDecision(ST.EntityId id, ST.SearchCandidateDecision decision, int ruleIndex,
                    string ruleName, float? primaryScore)
                { Id = id; Decision = decision; RuleIndex = ruleIndex; RuleName = ruleName; PrimaryScore = primaryScore; }
                public ST.EntityId Id { get; }
                public ST.SearchCandidateDecision Decision { get; }
                public int RuleIndex { get; }
                public string RuleName { get; }
                public float? PrimaryScore { get; }
            }
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

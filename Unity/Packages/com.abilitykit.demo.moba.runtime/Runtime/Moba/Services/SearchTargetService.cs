using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.Services;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Core.Math;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SearchTargetService : IService
    {
        private readonly MobaActorRegistry _actors;
        private readonly TargetSearchEngine _engine = new TargetSearchEngine();
        private readonly SearchContext _context = new SearchContext();
        private readonly List<ITargetRule> _rules = new List<ITargetRule>(8);
        private readonly List<ST.IEntityId> _searchResults = new List<ST.IEntityId>(32);
        private readonly List<ST.EntityId> _results = new List<ST.EntityId>(32);
        private readonly AllActorsCandidateProvider _allActorsProvider;
        private readonly TopKByScoreSelector _selector = new TopKByScoreSelector();

        public SearchTargetService(MobaActorRegistry actors)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _allActorsProvider = new AllActorsCandidateProvider(_actors);
            _context.SetService<IPositionProvider>(new RegistryPositionProvider(_actors));
        }

        public bool TrySearchFirstActorId(int queryTemplateId, int casterActorId, in Vec3 aimPos, out int targetActorId)
        {
            targetActorId = 0;
            if (queryTemplateId <= 0) return false;

            var origin = ResolveOrigin(0, casterActorId, in aimPos);
            var radius = 5f; // TODO: read from config

            _rules.Clear();
            _rules.Add(RequireValidIdRule.Instance);
            _rules.Add(new CircleShapeRule(new ST.Vec2(origin.x, origin.y), radius));
            if (casterActorId > 0)
            {
                _rules.Add(new ExcludeEntityRule(new ST.EntityId(casterActorId)));
            }

            var query = new SearchQuery(
                provider: _allActorsProvider,
                rules: _rules,
                scorer: new DistanceToEntityScorer(new ST.EntityId(casterActorId)),
                selector: _selector,
                maxCount: 1);

            _searchResults.Clear();
            _engine.SearchIds(in query, _context, _searchResults);
            if (_searchResults.Count == 0) return false;

            targetActorId = _searchResults[0].ActorId;
            return targetActorId > 0;
        }

        public bool TrySearchActorIds(int queryTemplateId, int casterActorId, in Vec3 aimPos, int explicitTargetActorId, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            if (queryTemplateId <= 0) return false;

            if (explicitTargetActorId > 0)
            {
                results.Add(explicitTargetActorId);
                return true;
            }

            var origin = ResolveOrigin(0, casterActorId, in aimPos);
            var radius = 5f; // TODO: read from config

            _rules.Clear();
            _rules.Add(RequireValidIdRule.Instance);
            _rules.Add(new CircleShapeRule(new ST.Vec2(origin.x, origin.y), radius));
            if (casterActorId > 0)
            {
                _rules.Add(new ExcludeEntityRule(new ST.EntityId(casterActorId)));
            }

            var query = new SearchQuery(
                provider: _allActorsProvider,
                rules: _rules,
                scorer: new DistanceToEntityScorer(new ST.EntityId(casterActorId)),
                selector: _selector,
                maxCount: 10);

            _searchResults.Clear();
            _engine.SearchIds(in query, _context, _searchResults);
            if (_searchResults.Count == 0) return false;

            for (int i = 0; i < _searchResults.Count; i++)
            {
                var id = _searchResults[i].ActorId;
                if (id > 0) results.Add(id);
            }

            return results.Count > 0;
        }

        private (float x, float y) ResolveOrigin(int centerMode, int casterActorId, in Vec3 aimPos)
        {
            if (casterActorId > 0 && _actors.TryGet(casterActorId, out var caster) && caster != null && caster.hasTransform)
            {
                var p = caster.transform.Value.Position;
                return (p.X, p.Z);
            }

            return (0, 0);
        }

        private sealed class RegistryPositionProvider : IPositionProvider
        {
            private readonly MobaActorRegistry _actors;

            public RegistryPositionProvider(MobaActorRegistry actors)
            {
                _actors = actors;
            }

            public bool TryGetPosition(ST.IEntityId entity, out IVec2 position)
            {
                position = default;
                if (!entity.IsValid) return false;
                if (_actors == null) return false;

                if (!_actors.TryGet(entity.ActorId, out var e) || e == null) return false;
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

            public bool RequiresPosition => false;

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

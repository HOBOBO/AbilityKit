using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Battle.SearchTarget;
using AbilityKit.Ability.Share.Battle.SearchTarget.Rules;
using AbilityKit.Ability.Share.Battle.SearchTarget.Scorers;
using AbilityKit.Ability.Share.Battle.SearchTarget.Selectors;
using AbilityKit.Ability.Share.Battle.SearchTarget.Shapes;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Services;
using UnityEngine;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SearchTargetService : IService
    {
        private const int OriginKey = 1;
        private const int ForwardKey = 2;
        private const int RadiusKey = 3;

        private readonly MobaConfigDatabase _configs;
        private readonly MobaActorRegistry _actors;

        private readonly TargetSearchEngine _engine = new TargetSearchEngine();
        private readonly SearchContext _context = new SearchContext();
        private readonly List<ITargetRule> _rules = new List<ITargetRule>(8);
        private readonly List<EcsEntityId> _results = new List<EcsEntityId>(32);

        private readonly AllActorsCandidateProvider _allActorsProvider;
        private readonly TopKByScoreSelector _selector = new TopKByScoreSelector();
        private readonly DistanceToFrameOriginScorer2D _scorer;
        private readonly ResolvedCircleRule2D _circleRule;
        private readonly DataFrameResolver2D _frameResolver;
        private readonly DataCircleParamsResolver2D _circleParams;

        public SearchTargetService(MobaConfigDatabase configs, MobaActorRegistry actors)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));

            _allActorsProvider = new AllActorsCandidateProvider(_actors);

            _frameResolver = new DataFrameResolver2D(originKey: OriginKey, forwardKey: ForwardKey);
            _circleParams = new DataCircleParamsResolver2D(radiusKey: RadiusKey);
            _circleRule = new ResolvedCircleRule2D(_frameResolver, _circleParams);
            _scorer = new DistanceToFrameOriginScorer2D(_frameResolver);

            _context.SetService<IPositionProvider>(new RegistryPositionProvider(_actors));
        }

        public bool TrySearchFirstActorId(int queryTemplateId, int casterActorId, in Vec3 aimPos, out int targetActorId)
        {
            targetActorId = 0;
            if (queryTemplateId <= 0) return false;

            if (!_configs.TryGetDto<SearchQueryTemplateDTO>(queryTemplateId, out var dto) || dto == null)
            {
                return false;
            }

            var origin = ResolveOrigin(dto.CenterMode, casterActorId, in aimPos);
            _context.SetData(OriginKey, origin);
            _context.SetData(RadiusKey, dto.Radius);

            _rules.Clear();
            _rules.Add(new RequireValidIdRule());
            _rules.Add(_circleRule);
            if (dto.ExcludeCaster)
            {
                _rules.Add(new ExcludeEntityRule(new EcsEntityId(casterActorId)));
            }

            var query = new SearchQuery(
                provider: _allActorsProvider,
                rules: _rules,
                scorer: _scorer,
                selector: _selector,
                maxCount: dto.MaxCount <= 0 ? 1 : dto.MaxCount);

            _engine.SearchIds(in query, _context, _results);
            if (_results.Count == 0) return false;

            targetActorId = _results[0].ActorId;
            return targetActorId > 0;
        }

        // CenterMode conventions (SearchQueryTemplateDTO.CenterMode):
        // 0: Caster
        // 1: AimPos
        // 2: ExplicitTarget (e.g. collision target / context.Target)
        //
        // For performance, caller provides a reusable list as output container.
        public bool TrySearchActorIds(int queryTemplateId, int casterActorId, in Vec3 aimPos, int explicitTargetActorId, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            if (queryTemplateId <= 0) return false;

            if (!_configs.TryGetDto<SearchQueryTemplateDTO>(queryTemplateId, out var dto) || dto == null)
            {
                return false;
            }

            // Fast path: explicit target with no area search.
            if (dto.CenterMode == 2 && dto.Radius <= 0f)
            {
                if (explicitTargetActorId > 0)
                {
                    results.Add(explicitTargetActorId);
                    return true;
                }
                return false;
            }

            var origin = ResolveOrigin(dto.CenterMode, casterActorId, in aimPos, explicitTargetActorId);
            _context.SetData(OriginKey, origin);
            _context.SetData(RadiusKey, dto.Radius);

            _rules.Clear();
            _rules.Add(new RequireValidIdRule());
            _rules.Add(_circleRule);
            if (dto.ExcludeCaster)
            {
                _rules.Add(new ExcludeEntityRule(new EcsEntityId(casterActorId)));
            }

            var query = new SearchQuery(
                provider: _allActorsProvider,
                rules: _rules,
                scorer: _scorer,
                selector: _selector,
                maxCount: dto.MaxCount <= 0 ? 1 : dto.MaxCount);

            _results.Clear();
            _engine.SearchIds(in query, _context, _results);
            if (_results.Count == 0) return false;

            for (int i = 0; i < _results.Count; i++)
            {
                var id = _results[i].ActorId;
                if (id > 0) results.Add(id);
            }

            return results.Count > 0;
        }

        private Vector2 ResolveOrigin(int centerMode, int casterActorId, in Vec3 aimPos)
        {
            if (centerMode == 1)
            {
                return new Vector2(aimPos.X, aimPos.Z);
            }

            if (casterActorId > 0 && _actors.TryGet(casterActorId, out var caster) && caster != null && caster.hasTransform)
            {
                var p = caster.transform.Value.Position;
                return new Vector2(p.X, p.Z);
            }

            return Vector2.zero;
        }

        private Vector2 ResolveOrigin(int centerMode, int casterActorId, in Vec3 aimPos, int explicitTargetActorId)
        {
            if (centerMode == 2)
            {
                if (explicitTargetActorId > 0 && _actors.TryGet(explicitTargetActorId, out var target) && target != null && target.hasTransform)
                {
                    var p = target.transform.Value.Position;
                    return new Vector2(p.X, p.Z);
                }
                return Vector2.zero;
            }

            return ResolveOrigin(centerMode, casterActorId, in aimPos);
        }

        private sealed class RegistryPositionProvider : IPositionProvider
        {
            private readonly MobaActorRegistry _actors;

            public RegistryPositionProvider(MobaActorRegistry actors)
            {
                _actors = actors;
            }

            public bool TryGetPositionXZ(EcsEntityId id, out Vector2 positionXZ)
            {
                positionXZ = default;
                if (!id.IsValid) return false;
                if (_actors == null) return false;

                if (!_actors.TryGet(id.ActorId, out var e) || e == null) return false;
                if (!e.hasTransform) return false;

                var p = e.transform.Value.Position;
                positionXZ = new Vector2(p.X, p.Z);
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
                    consumer.Consume(new EcsEntityId(id));
                }
            }
        }

        public void Dispose()
        {
        }
    }
}

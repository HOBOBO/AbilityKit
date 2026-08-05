using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Share.Config;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Demo.Moba.Services.Search
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class MobaTargetSourceProviderAttribute : Attribute
    {
        public int Code { get; }

        public MobaTargetSourceProviderAttribute(int code)
        {
            Code = code;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class MobaTargetFilterAttribute : Attribute
    {
        public int Code { get; }

        public MobaTargetFilterAttribute(int code)
        {
            Code = code;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class MobaTargetOrderAttribute : Attribute
    {
        public int Code { get; }

        public MobaTargetOrderAttribute(int code)
        {
            Code = code;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class MobaTargetSelectAttribute : Attribute
    {
        public int Code { get; }

        public MobaTargetSelectAttribute(int code)
        {
            Code = code;
        }
    }

    internal interface IMobaTargetSourceFactory
    {
        ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config);
    }

    internal interface IMobaTargetFilterFactory
    {
        ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config);
    }

    internal interface IMobaTargetOrderFactory
    {
        ITargetScorer Create(in MobaTargetQueryBuildContext context, SearchTargetScorerConfig config);
    }

    internal interface IMobaTargetSelectFactory
    {
        ITargetSelector Create(in MobaTargetQueryBuildContext context, SearchTargetSelectorConfig config);
    }

    internal readonly struct MobaTargetQueryBuildContext
    {
        public MobaTargetQueryBuildContext(
            MobaActorRegistry actors,
            ICandidateProvider allActorsProvider,
            SearchContext searchContext,
            int casterActorId,
            Vec3 aimPosition,
            int explicitTargetActorId,
            ITargetScorer zeroScorer,
            ITargetSelector topKSelector,
            ITargetSelector streamingTopKSelector)
        {
            Actors = actors;
            AllActorsProvider = allActorsProvider;
            SearchContext = searchContext;
            CasterActorId = casterActorId;
            AimPosition = aimPosition;
            ExplicitTargetActorId = explicitTargetActorId;
            ZeroScorer = zeroScorer;
            TopKSelector = topKSelector;
            StreamingTopKSelector = streamingTopKSelector;
        }

        public MobaActorRegistry Actors { get; }
        public ICandidateProvider AllActorsProvider { get; }
        public SearchContext SearchContext { get; }
        public int CasterActorId { get; }
        public Vec3 AimPosition { get; }
        public int ExplicitTargetActorId { get; }
        public ITargetScorer ZeroScorer { get; }
        public ITargetSelector TopKSelector { get; }
        public ITargetSelector StreamingTopKSelector { get; }

        public ST.Vec2 ResolvePoint(int pointKind)
        {
            var kind = (SearchTargetPointKind)pointKind;
            switch (kind)
            {
                case SearchTargetPointKind.AimPosition:
                    return new ST.Vec2(AimPosition.X, AimPosition.Z);
                case SearchTargetPointKind.ExplicitTarget:
                    if (TryGetActorPosition(ExplicitTargetActorId, out var targetPos)) return targetPos;
                    break;
            }

            if (TryGetActorPosition(CasterActorId, out var casterPos)) return casterPos;
            return new ST.Vec2(AimPosition.X, AimPosition.Z);
        }

        public ST.Vec2 ResolveForward(int pointKind, ST.Vec2 origin)
        {
            var toPoint = ResolvePoint(pointKind);
            var fx = toPoint.X - origin.X;
            var fy = toPoint.Y - origin.Y;
            if (fx * fx + fy * fy > 0.000001f) return new ST.Vec2(fx, fy);

            if (TryGetActorPosition(CasterActorId, out var casterPos))
            {
                fx = origin.X - casterPos.X;
                fy = origin.Y - casterPos.Y;
                if (fx * fx + fy * fy > 0.000001f) return new ST.Vec2(fx, fy);
            }

            return ST.Vec2.Up;
        }

        public bool TryGetActorPosition(int actorId, out ST.Vec2 position)
        {
            position = default;
            if (actorId <= 0) return false;
            if (Actors != null && Actors.TryGet(actorId, out var actor) && actor != null && actor.hasTransform)
            {
                var p = actor.transform.Value.Position;
                position = new ST.Vec2(p.X, p.Z);
                return true;
            }

            return false;
        }

        public bool TryGetActorTeam(int actorId, out Team team)
        {
            team = Team.None;
            if (actorId <= 0) return false;
            if (Actors == null || !Actors.TryGet(actorId, out var actor) || actor == null || !actor.hasTeam) return false;
            team = actor.team.Value;
            return team != Team.None;
        }

        public static float ResolveRadius(SearchTargetRuleConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Radius <= 0f)
            {
                throw new InvalidOperationException($"Search target shape rule requires positive radius. ruleKind={config.Kind}");
            }

            return config.Radius;
        }

        public static float ResolveHalfAngleDeg(SearchTargetRuleConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.HalfAngleDeg <= 0f)
            {
                throw new InvalidOperationException($"Search target sector rule requires positive half angle. ruleKind={config.Kind}");
            }

            return config.HalfAngleDeg;
        }
    }

    internal sealed class MobaTargetQueryFactoryRegistry
    {
        private static readonly Lazy<MobaTargetQueryFactoryRegistry> DefaultRegistry = new Lazy<MobaTargetQueryFactoryRegistry>(BuildDefaultRegistry);

        private readonly Dictionary<int, IMobaTargetSourceFactory> _sources = new Dictionary<int, IMobaTargetSourceFactory>();
        private readonly Dictionary<int, IMobaTargetFilterFactory> _filters = new Dictionary<int, IMobaTargetFilterFactory>();
        private readonly Dictionary<int, IMobaTargetOrderFactory> _orders = new Dictionary<int, IMobaTargetOrderFactory>();
        private readonly Dictionary<int, IMobaTargetSelectFactory> _selects = new Dictionary<int, IMobaTargetSelectFactory>();

        public static MobaTargetQueryFactoryRegistry CreateDefault()
        {
            return DefaultRegistry.Value;
        }

        internal static MobaTargetQueryFactoryRegistry BuildDefaultRegistry()
        {
            var registry = new MobaTargetQueryFactoryRegistry();
            if (MobaGeneratedTargetQueryFactoryManifest.Register(registry) > 0)
            {
                return registry;
            }

            if (AppContext.TryGetSwitch(
                    "AbilityKit.Moba.DisableTargetQueryFactoryReflectionFallback",
                    out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
            {
                throw new InvalidOperationException(
                    "The generated MOBA target query factory manifest is empty and reflection fallback is disabled.");
            }

            registry.ScanAssembly(typeof(MobaTargetQueryFactoryRegistry).Assembly);
            return registry;
        }

        internal int SourceCount => _sources.Count;
        internal int FilterCount => _filters.Count;
        internal int OrderCount => _orders.Count;
        internal int SelectCount => _selects.Count;

        internal void RegisterSource(int code, IMobaTargetSourceFactory factory)
        {
            RegisterUnique(_sources, code, factory, "source");
        }

        internal void RegisterFilter(int code, IMobaTargetFilterFactory factory)
        {
            RegisterUnique(_filters, code, factory, "filter");
        }

        internal void RegisterOrder(int code, IMobaTargetOrderFactory factory)
        {
            RegisterUnique(_orders, code, factory, "order");
        }

        internal void RegisterSelect(int code, IMobaTargetSelectFactory factory)
        {
            RegisterUnique(_selects, code, factory, "select");
        }

        private static void RegisterUnique<TFactory>(
            Dictionary<int, TFactory> factories,
            int code,
            TFactory factory,
            string kind)
            where TFactory : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (factories.TryGetValue(code, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate MOBA target query {kind} code '{code}' uses both '{existing.GetType().FullName}' and '{factory.GetType().FullName}'.");
            }

            factories.Add(code, factory);
        }

        public ICandidateProvider CreateSource(SearchTargetProviderConfig config, in MobaTargetQueryBuildContext context)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!_sources.TryGetValue(config.Kind, out var factory))
            {
                throw new InvalidOperationException($"Search target provider factory not registered. kind={config.Kind}");
            }

            var provider = factory.Create(in context, config);
            if (provider == null)
            {
                throw new InvalidOperationException($"Search target provider factory returned null. kind={config.Kind}, factory={factory.GetType().Name}");
            }

            return provider;
        }

        public ITargetRule CreateFilter(SearchTargetRuleConfig config, in MobaTargetQueryBuildContext context)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!_filters.TryGetValue(config.Kind, out var factory))
            {
                throw new InvalidOperationException($"Search target filter factory not registered. kind={config.Kind}");
            }

            var rule = factory.Create(in context, config);
            if (rule == null)
            {
                throw new InvalidOperationException($"Search target filter factory returned null. kind={config.Kind}, factory={factory.GetType().Name}");
            }

            return rule;
        }

        public ITargetScorer CreateOrder(SearchTargetScorerConfig config, in MobaTargetQueryBuildContext context)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!_orders.TryGetValue(config.Kind, out var factory))
            {
                throw new InvalidOperationException($"Search target scorer factory not registered. kind={config.Kind}");
            }

            var scorer = factory.Create(in context, config);
            if (scorer == null)
            {
                throw new InvalidOperationException($"Search target scorer factory returned null. kind={config.Kind}, factory={factory.GetType().Name}");
            }

            return scorer;
        }

        public ITargetSelector CreateSelect(SearchTargetSelectorConfig config, in MobaTargetQueryBuildContext context)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!_selects.TryGetValue(config.Kind, out var factory))
            {
                throw new InvalidOperationException($"Search target selector factory not registered. kind={config.Kind}");
            }

            var selector = factory.Create(in context, config);
            if (selector == null)
            {
                throw new InvalidOperationException($"Search target selector factory returned null. kind={config.Kind}, factory={factory.GetType().Name}");
            }

            return selector;
        }

        private void ScanAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type == null || type.IsAbstract || type.IsInterface) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                RegisterSourceFactory(type);
                RegisterFilterFactory(type);
                RegisterOrderFactory(type);
                RegisterSelectFactory(type);
            }
        }

        private void RegisterSourceFactory(Type type)
        {
            if (!typeof(IMobaTargetSourceFactory).IsAssignableFrom(type)) return;
            var attrs = type.GetCustomAttributes<MobaTargetSourceProviderAttribute>(false);
            using (var enumerator = attrs.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return;
            }

            var instance = (IMobaTargetSourceFactory)Activator.CreateInstance(type);
            foreach (var attr in attrs)
            {
                RegisterSource(attr.Code, instance);
            }
        }

        private void RegisterFilterFactory(Type type)
        {
            if (!typeof(IMobaTargetFilterFactory).IsAssignableFrom(type)) return;
            var attrs = type.GetCustomAttributes<MobaTargetFilterAttribute>(false);
            using (var enumerator = attrs.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return;
            }

            var instance = (IMobaTargetFilterFactory)Activator.CreateInstance(type);
            foreach (var attr in attrs)
            {
                RegisterFilter(attr.Code, instance);
            }
        }

        private void RegisterOrderFactory(Type type)
        {
            if (!typeof(IMobaTargetOrderFactory).IsAssignableFrom(type)) return;
            var attrs = type.GetCustomAttributes<MobaTargetOrderAttribute>(false);
            using (var enumerator = attrs.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return;
            }

            var instance = (IMobaTargetOrderFactory)Activator.CreateInstance(type);
            foreach (var attr in attrs)
            {
                RegisterOrder(attr.Code, instance);
            }
        }

        private void RegisterSelectFactory(Type type)
        {
            if (!typeof(IMobaTargetSelectFactory).IsAssignableFrom(type)) return;
            var attrs = type.GetCustomAttributes<MobaTargetSelectAttribute>(false);
            using (var enumerator = attrs.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return;
            }

            var instance = (IMobaTargetSelectFactory)Activator.CreateInstance(type);
            foreach (var attr in attrs)
            {
                RegisterSelect(attr.Code, instance);
            }
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.AllActors)]
    internal sealed class AllActorsTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            return context.AllActorsProvider;
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.ContextTarget)]
    [MobaTargetSourceProvider((int)SearchTargetProviderKind.ExplicitTarget)]
    internal sealed class ExplicitTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            return context.ExplicitTargetActorId > 0 ? new SingleActorCandidateProvider(context.ExplicitTargetActorId) : null;
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.Caster)]
    internal sealed class CasterTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            return context.CasterActorId > 0 ? new SingleActorCandidateProvider(context.CasterActorId) : null;
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.SameTeam)]
    internal sealed class SameTeamTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            return context.TryGetActorTeam(context.CasterActorId, out var team)
                ? new FilteredActorsCandidateProvider(context.Actors, actor => HasTeam(actor, team))
                : null;
        }

        private static bool HasTeam(global::ActorEntity actor, Team team)
        {
            return actor != null && actor.hasTeam && actor.team.Value == team;
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.EnemyTeam)]
    internal sealed class EnemyTeamTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            return context.TryGetActorTeam(context.CasterActorId, out var casterTeam)
                ? new FilteredActorsCandidateProvider(context.Actors, actor => actor.hasTeam && actor.team.Value != Team.None && actor.team.Value != casterTeam)
                : null;
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.MainType)]
    internal sealed class MainTypeTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            var param = config != null ? config.Param : 0;
            return new FilteredActorsCandidateProvider(context.Actors, actor => actor.hasEntityMainType && (int)actor.entityMainType.Value == param);
        }
    }

    [MobaTargetSourceProvider((int)SearchTargetProviderKind.UnitSubType)]
    internal sealed class UnitSubTypeTargetSourceFactory : IMobaTargetSourceFactory
    {
        public ICandidateProvider Create(in MobaTargetQueryBuildContext context, SearchTargetProviderConfig config)
        {
            var param = config != null ? config.Param : 0;
            return new FilteredActorsCandidateProvider(context.Actors, actor => actor.hasUnitSubType && (int)actor.unitSubType.Value == param);
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.RequireValidId)]
    internal sealed class RequireValidIdTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return RequireValidIdRule.Instance;
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.RequireHasPosition)]
    internal sealed class RequireHasPositionTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return RequireHasPositionRule.Instance;
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.CircleShape)]
    internal sealed class CircleShapeTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            var origin = context.ResolvePoint(config.Center);
            return new CircleShapeRule(origin, MobaTargetQueryBuildContext.ResolveRadius(config));
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.SectorShape)]
    internal sealed class SectorShapeTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            var origin = context.ResolvePoint(config.Center);
            var forward = context.ResolveForward(config.Forward, origin);
            return new SectorShapeRule(
                origin,
                forward,
                MobaTargetQueryBuildContext.ResolveRadius(config),
                MobaTargetQueryBuildContext.ResolveHalfAngleDeg(config));
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.ExcludeCaster)]
    internal sealed class ExcludeCasterTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return context.CasterActorId > 0 ? new ExcludeEntityRule(new ST.EntityId(context.CasterActorId)) : null;
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.ExcludeExplicitTarget)]
    internal sealed class ExcludeExplicitTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return context.ExplicitTargetActorId > 0 ? new ExcludeEntityRule(new ST.EntityId(context.ExplicitTargetActorId)) : null;
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.Whitelist)]
    internal sealed class WhitelistTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return new WhitelistRule(new ArrayActorIdSet(config.ActorIds));
        }
    }

    [MobaTargetFilter((int)SearchTargetRuleKind.Blacklist)]
    internal sealed class BlacklistTargetFilterFactory : IMobaTargetFilterFactory
    {
        public ITargetRule Create(in MobaTargetQueryBuildContext context, SearchTargetRuleConfig config)
        {
            return new BlacklistRule(new ArrayActorIdSet(config.ActorIds));
        }
    }

    [MobaTargetOrder((int)SearchTargetScorerKind.Zero)]
    internal sealed class ZeroTargetOrderFactory : IMobaTargetOrderFactory
    {
        public ITargetScorer Create(in MobaTargetQueryBuildContext context, SearchTargetScorerConfig config)
        {
            return context.ZeroScorer;
        }
    }

    [MobaTargetOrder((int)SearchTargetScorerKind.SeededHashRandom)]
    internal sealed class SeededHashRandomTargetOrderFactory : IMobaTargetOrderFactory
    {
        private readonly SeededHashRandomScorer _scorer = new SeededHashRandomScorer(MobaSearchQueryBuilder.RandomSeedContextKey);

        public ITargetScorer Create(in MobaTargetQueryBuildContext context, SearchTargetScorerConfig config)
        {
            context.SearchContext.SetData(MobaSearchQueryBuilder.RandomSeedContextKey, config != null ? config.RandomSeed : 0);
            return _scorer;
        }
    }

    [MobaTargetOrder((int)SearchTargetScorerKind.DistanceToExplicitTarget)]
    internal sealed class DistanceToExplicitTargetOrderFactory : IMobaTargetOrderFactory
    {
        public ITargetScorer Create(in MobaTargetQueryBuildContext context, SearchTargetScorerConfig config)
        {
            return context.ExplicitTargetActorId > 0 ? new DistanceToEntityScorer(new ST.EntityId(context.ExplicitTargetActorId)) : context.ZeroScorer;
        }
    }

    [MobaTargetOrder((int)SearchTargetScorerKind.DistanceToCaster)]
    internal sealed class DistanceToCasterTargetOrderFactory : IMobaTargetOrderFactory
    {
        public ITargetScorer Create(in MobaTargetQueryBuildContext context, SearchTargetScorerConfig config)
        {
            return context.CasterActorId > 0 ? new DistanceToEntityScorer(new ST.EntityId(context.CasterActorId)) : context.ZeroScorer;
        }
    }

    [MobaTargetSelect((int)SearchTargetSelectorKind.TopKByScore)]
    internal sealed class TopKTargetSelectFactory : IMobaTargetSelectFactory
    {
        public ITargetSelector Create(in MobaTargetQueryBuildContext context, SearchTargetSelectorConfig config)
        {
            return context.TopKSelector;
        }
    }

    [MobaTargetSelect((int)SearchTargetSelectorKind.StreamingTopKByScore)]
    internal sealed class StreamingTopKTargetSelectFactory : IMobaTargetSelectFactory
    {
        public ITargetSelector Create(in MobaTargetQueryBuildContext context, SearchTargetSelectorConfig config)
        {
            return context.StreamingTopKSelector;
        }
    }

    internal sealed class SingleActorCandidateProvider : ICandidateProvider
    {
        private readonly int _actorId;

        public SingleActorCandidateProvider(int actorId)
        {
            _actorId = actorId;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_actorId > 0) consumer.Consume(new ST.EntityId(_actorId));
        }
    }

    internal sealed class FilteredActorsCandidateProvider : ICandidateProvider
    {
        private readonly MobaActorRegistry _actors;
        private readonly Predicate<global::ActorEntity> _predicate;

        public FilteredActorsCandidateProvider(MobaActorRegistry actors, Predicate<global::ActorEntity> predicate)
        {
            _actors = actors;
            _predicate = predicate;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_actors == null || _predicate == null) return;

            foreach (var kv in _actors.Entries)
            {
                var id = kv.Key;
                if (id <= 0) continue;
                var actor = kv.Value;
                if (actor == null || !_predicate(actor)) continue;
                consumer.Consume(new ST.EntityId(id));
            }
        }
    }

    internal sealed class ArrayActorIdSet : IActorIdSet
    {
        private readonly int[] _actorIds;

        public ArrayActorIdSet(int[] actorIds)
        {
            _actorIds = actorIds ?? Array.Empty<int>();
        }

        public int Count => _actorIds.Length;

        public bool Contains(int actorId)
        {
            for (int i = 0; i < _actorIds.Length; i++)
            {
                if (_actorIds[i] == actorId) return true;
            }

            return false;
        }
    }
}

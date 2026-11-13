using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Predicates;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using Entitas;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaOwnerTeamPredicateTests
{
    [Fact]
    public void Owner_team_predicates_require_valid_owner_participant_and_team_relationship()
    {
        var contexts = new global::Contexts();
        AddActor(contexts, 1, Team.Team1);
        AddActor(contexts, 2, Team.Team1);
        AddActor(contexts, 3, Team.Team2);
        AddActor(contexts, 4, Team.None);

        var index = new ActorIdIndex(contexts);
        var registry = new MobaActorRegistry();
        foreach (var actor in contexts.actor.GetEntities()) registry.Register(actor.actorId.Value, actor);
        var lookup = new MobaActorLookupService(index, registry, new MobaEntityManager(null), contexts);
        var rules = new MobaCombatRulesService(lookup);
        var gates = new MobaOwnerBoundTriggerGateService();
        var resolver = new TestResolver(gates, rules);
        var functions = new FunctionRegistry();
        MobaPlanPredicateFunctions.Register(functions);

        Assert.True(functions.TryGet<Predicate2<object, IWorldResolver>>(
            MobaPlanPredicateFunctions.OwnerSameTeamAsPayloadSourceFunctionId, out var sameTeam, out _));
        Assert.True(functions.TryGet<Predicate2<object, IWorldResolver>>(
            MobaPlanPredicateFunctions.OwnerEnemyToPayloadTargetFunctionId, out var enemyTarget, out _));
        Assert.True(functions.TryGet<Predicate2<object, IWorldResolver>>(
            MobaPlanPredicateFunctions.OwnerMatchesPayloadSourceFunctionId, out var ownerSource, out _));

        var exec = new ExecCtx<IWorldResolver>(resolver, null, functions, null, null, null, null, null, null,
            default, default);
        var sameTeamPayload = new ActorPayload(2, 2);
        var enemyPayload = new ActorPayload(2, 3);
        var killByOwner = new UnitDieEventPayload(2, 1, 2, 2, 30040001, 40f);
        var killByOther = new UnitDieEventPayload(2, 3, 2, 2, 30040001, 40f);

        using (gates.BeginEvaluationScope(new MobaOwnerBoundTriggerExecutionSource(1, 0, 10, 10, 10, 0)))
        {
            Assert.True(sameTeam(sameTeamPayload, NamedArgsDict.Empty, exec));
            Assert.False(enemyTarget(sameTeamPayload, NamedArgsDict.Empty, exec));
            Assert.True(enemyTarget(enemyPayload, NamedArgsDict.Empty, exec));
            Assert.True(ownerSource(killByOwner, NamedArgsDict.Empty, exec));
            Assert.False(ownerSource(killByOther, NamedArgsDict.Empty, exec));
            Assert.False(sameTeam(new ActorPayload(4, 3), NamedArgsDict.Empty, exec));
            Assert.False(enemyTarget(new ActorPayload(3, 4), NamedArgsDict.Empty, exec));
            Assert.False(sameTeam(new object(), NamedArgsDict.Empty, exec));
        }

        using (gates.BeginEvaluationScope(new MobaOwnerBoundTriggerExecutionSource(4, 0, 11, 11, 11, 0)))
        {
            Assert.False(sameTeam(sameTeamPayload, NamedArgsDict.Empty, exec));
            Assert.False(enemyTarget(enemyPayload, NamedArgsDict.Empty, exec));
            Assert.False(ownerSource(killByOwner, NamedArgsDict.Empty, exec));
        }

        Assert.False(sameTeam(sameTeamPayload, NamedArgsDict.Empty, exec));
        Assert.False(enemyTarget(enemyPayload, NamedArgsDict.Empty, exec));
    }

    private static void AddActor(global::Contexts contexts, int actorId, Team team)
    {
        var actor = contexts.actor.CreateEntity();
        actor.AddActorId(actorId);
        actor.AddTeam(team);
    }

    private sealed class ActorPayload : IMobaActorContextProvider
    {
        private readonly int _source;
        private readonly int _target;

        public ActorPayload(int source, int target) { _source = source; _target = target; }
        public bool TryGetSourceActorId(out int actorId) { actorId = _source; return actorId > 0; }
        public bool TryGetTargetActorId(out int actorId) { actorId = _target; return actorId > 0; }
    }

    private sealed class TestResolver : IWorldResolver
    {
        private readonly object[] _services;
        public TestResolver(params object[] services) => _services = services;
        public object Resolve(Type serviceType) => Array.Find(_services, serviceType.IsInstanceOfType);
        public T Resolve<T>() => TryResolve<T>(out var value) ? value : default!;
        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }
        public bool TryResolve<T>(out T instance)
        {
            var value = Resolve(typeof(T));
            instance = value is T typed ? typed : default!;
            return value is T;
        }
    }
}

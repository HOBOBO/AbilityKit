using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using BTCore.Runtime;
using BTCore.Runtime.Externals;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// Thin behavior-tree adapter over the shared target-search pipeline. Query composition remains
    /// in SearchTargetService; this node only selects a query and commits its first result.
    /// </summary>
    public sealed class MobaSelectNearestEnemyAction : ExternalAction, IMobaBTreeContextNode
    {
        private const string QueryTemplateIdProperty = "queryTemplateId";
        private const string SearchRadiusProperty = "searchRadius";
        private const float DefaultSearchRadius = 1000f;

        private readonly List<int> _results = new List<int>(1);
        private MobaBTreeRuntimeContext _context;
        private SearchTargetService _fallbackSearch;

        public void Bind(MobaBTreeRuntimeContext context)
        {
            _context = context;
        }

        protected override NodeState OnUpdate()
        {
            if (Blackboard == null) return NodeState.Failure;
            MobaBTreeBlackboard.ClearTarget(Blackboard);

            var behavior = _context?.Behavior;
            var world = _context?.World;
            var registry = _context?.Registry;
            if (behavior == null || world == null || registry == null
                || behavior.OwnerId.Value <= 0 || behavior.OwnerId.Value > int.MaxValue)
                return NodeState.Success;

            var search = _context.SearchTargets
                         ?? (_fallbackSearch ??= new SearchTargetService(registry, _context.Config));
            var ownerId = (int)behavior.OwnerId.Value;
            var ownerPosition = world.GetPosition(behavior.OwnerId);
            _results.Clear();

            var queryTemplateId = ReadIntProperty(QueryTemplateIdProperty, 0);
            var found = queryTemplateId > 0
                ? search.TrySearchActorIds(queryTemplateId, ownerId, in ownerPosition, 0, _results)
                : search.TrySearchActorIds(CreateDefaultQuery(ReadFloatProperty(
                    SearchRadiusProperty, DefaultSearchRadius)), ownerId, in ownerPosition, 0, _results);

            if (!found || _results.Count == 0) return NodeState.Success;
            var targetId = _results[0];
            if (!registry.TryGet(targetId, out var target) || target == null || !target.hasTransform)
                return NodeState.Success;

            var targetPosition = target.transform.Value.Position;
            Blackboard.SetValue(MobaBTreeKeys.TargetId, targetId);
            Blackboard.SetValue(MobaBTreeKeys.TargetX, targetPosition.X);
            Blackboard.SetValue(MobaBTreeKeys.TargetY, targetPosition.Y);
            Blackboard.SetValue(MobaBTreeKeys.TargetZ, targetPosition.Z);
            Blackboard.SetValue(MobaBTreeKeys.TargetDistance,
                world.GetDistanceToPosition(behavior.OwnerId, targetPosition));
            Blackboard.SetValue(MobaBTreeKeys.TargetSelectedFrame,
                Blackboard.GetValue<long>(MobaBTreeKeys.EvaluationFrame));
            Blackboard.SetValue(MobaBTreeKeys.TargetValid, true);
            return NodeState.Success;
        }

        private static SearchQueryTemplateMO CreateDefaultQuery(float radius)
        {
            var rules = radius > 0f
                ? new[]
                {
                    new SearchTargetRuleConfig(
                        0,
                        (int)SearchTargetRuleKind.CircleShape,
                        center: (int)SearchTargetPointKind.Caster,
                        radius: radius)
                }
                : Array.Empty<SearchTargetRuleConfig>();

            return new SearchQueryTemplateMO(
                id: 0,
                name: "moba.ai.nearest-enemy",
                maxCount: 1,
                explicitTargetPolicy: (int)SearchQueryExplicitTargetPolicy.IgnoreExplicitTarget,
                provider: new SearchTargetProviderConfig(0, (int)SearchTargetProviderKind.EnemyTeam),
                rules: rules,
                scorer: new SearchTargetScorerConfig(
                    0,
                    (int)SearchTargetScorerKind.DistanceToCaster,
                    (int)SearchTargetPointKind.Caster),
                selector: new SearchTargetSelectorConfig(0, (int)SearchTargetSelectorKind.TopKByScore));
        }

        private int ReadIntProperty(string name, int defaultValue)
        {
            return Properties != null
                   && Properties.TryGetValue(name, out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }

        private float ReadFloatProperty(string name, float defaultValue)
        {
            return Properties != null
                   && Properties.TryGetValue(name, out var value)
                   && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }
}

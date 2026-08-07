using System;
using System.Collections.Generic;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Search
{
    internal sealed class MobaSearchQueryBuilder
    {
        private static readonly ITargetRule[] ExplicitTargetRules = { RequireValidIdRule.Instance };

        private readonly MobaActorRegistry _actors;
        private readonly ICandidateProvider _allActorsProvider;
        private readonly MobaTargetQueryFactoryRegistry _factories;
        private readonly ZeroScorer _zeroScorer = new ZeroScorer();
        private readonly TopKByScoreSelector _topKSelector = new TopKByScoreSelector();
        private readonly StreamingTopKByScoreSelector _streamingTopKSelector = new StreamingTopKByScoreSelector();

        private readonly MobaCombatRulesService _combatRules;

        public MobaSearchQueryBuilder(MobaActorRegistry actors, ICandidateProvider allActorsProvider, MobaCombatRulesService combatRules = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _allActorsProvider = allActorsProvider ?? throw new ArgumentNullException(nameof(allActorsProvider));
            _combatRules = combatRules;
            _factories = MobaTargetQueryFactoryRegistry.CreateDefault();
        }

        public bool TryBuild(
            SearchQueryTemplateMO template,
            SearchContext context,
            int casterActorId,
            in Vec3 aimPos,
            int explicitTargetActorId,
            int maxCountOverride,
            out SearchQuery query)
        {
            query = default;
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ClearData();
            var maxCount = maxCountOverride > 0 ? maxCountOverride : template.MaxCount;
            if (maxCount <= 0)
            {
                throw new InvalidOperationException($"Search query requires positive max count. templateId={template.Id}, maxCount={maxCount}");
            }

            var explicitPolicy = (SearchQueryExplicitTargetPolicy)template.ExplicitTargetPolicy;
            var preferExplicitTarget = explicitTargetActorId > 0
                && explicitPolicy == SearchQueryExplicitTargetPolicy.PreferExplicitTarget;
            var buildContext = new MobaTargetQueryBuildContext(
                _actors,
                _allActorsProvider,
                context,
                casterActorId,
                aimPos,
                explicitTargetActorId,
                _zeroScorer,
                _topKSelector,
                _streamingTopKSelector);

            var provider = preferExplicitTarget
                ? new SingleActorCandidateProvider(explicitTargetActorId)
                : _factories.CreateSource(template.Provider, in buildContext);

            var configuredRules = template.Rules ?? Array.Empty<SearchTargetRuleConfig>();
            var rules = new List<ITargetRule>(configuredRules.Length + 2);
            AddDefaultRules(rules, casterActorId);
            for (int i = 0; i < configuredRules.Length; i++)
            {
                var ruleConfig = configuredRules[i];
                if (ruleConfig == null)
                {
                    throw new InvalidOperationException($"Search query rule config is null. templateId={template.Id}, ruleIndex={i}");
                }

                rules.Add(_factories.CreateFilter(ruleConfig, in buildContext));
            }

            var configuredScorers = template.Scorers ?? Array.Empty<SearchTargetScorerConfig>();
            var orders = new List<SearchOrder>(preferExplicitTarget ? 1 : configuredScorers.Length);
            if (preferExplicitTarget)
            {
                orders.Add(new SearchOrder(_zeroScorer));
            }
            else
            {
                for (int i = 0; i < configuredScorers.Length; i++)
                {
                    var scorerConfig = configuredScorers[i];
                    if (scorerConfig == null)
                    {
                        throw new InvalidOperationException($"Search query scorer config is null. templateId={template.Id}, scorerIndex={i}");
                    }
                    var direction = scorerConfig.Direction == (int)SearchSortDirection.ScoreAscending
                        ? SearchSortDirection.ScoreAscending
                        : SearchSortDirection.ScoreDescending;
                    orders.Add(new SearchOrder(
                        _factories.CreateOrder(scorerConfig, in buildContext),
                        direction));
                }
            }

            var selector = preferExplicitTarget ? _topKSelector : _factories.CreateSelect(template.Selector, in buildContext);
            query = new SearchQuery(
                provider: provider,
                rules: rules,
                orders: orders,
                selector: selector,
                maxCount: preferExplicitTarget ? 1 : maxCount);
            return true;
        }

        private void AddDefaultRules(List<ITargetRule> rules, int casterActorId)
        {
            rules.Add(RequireValidIdRule.Instance);
            if (_combatRules != null)
            {
                rules.Add(new MobaCombatTargetRule(_combatRules, casterActorId));
            }
        }
    }

    internal sealed class MobaCombatTargetRule : ITargetRule
    {
        private readonly MobaCombatRulesService _rules;
        private readonly int _casterActorId;

        public MobaCombatTargetRule(MobaCombatRulesService rules, int casterActorId)
        {
            _rules = rules;
            _casterActorId = casterActorId;
        }

        public bool IsMatch(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            if (_rules == null)
            {
                return candidate.IsValid;
            }
            if (!candidate.IsValid || candidate.Value > int.MaxValue) return false;

            var result = _rules.CanBeSearchedTarget(_casterActorId, (int)candidate.Value);
            return result.Passed;
        }
    }
}

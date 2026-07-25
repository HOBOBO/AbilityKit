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
        public const int RandomSeedContextKey = 0x5EED;
        private static readonly ITargetRule[] ExplicitTargetRules = { RequireValidIdRule.Instance };

        private readonly MobaActorRegistry _actors;
        private readonly ICandidateProvider _allActorsProvider;
        private readonly MobaTargetQueryFactoryRegistry _factories;
        private readonly List<ITargetRule> _rules = new List<ITargetRule>(8);
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

            _rules.Clear();
            AddDefaultRules(casterActorId);
            var configuredRules = template.Rules ?? Array.Empty<SearchTargetRuleConfig>();
            for (int i = 0; i < configuredRules.Length; i++)
            {
                var ruleConfig = configuredRules[i];
                if (ruleConfig == null)
                {
                    throw new InvalidOperationException($"Search query rule config is null. templateId={template.Id}, ruleIndex={i}");
                }

                _rules.Add(_factories.CreateFilter(ruleConfig, in buildContext));
            }

            var scorer = preferExplicitTarget ? _zeroScorer : _factories.CreateOrder(template.Scorer, in buildContext);
            var selector = preferExplicitTarget ? _topKSelector : _factories.CreateSelect(template.Selector, in buildContext);

            query = new SearchQuery(
                provider: provider,
                rules: _rules,
                scorer: scorer,
                selector: selector,
                maxCount: preferExplicitTarget ? 1 : maxCount);
            return true;
        }

        private void AddDefaultRules(int casterActorId)
        {
            _rules.Add(RequireValidIdRule.Instance);
            if (_combatRules != null)
            {
                _rules.Add(new MobaCombatTargetRule(_combatRules, casterActorId));
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

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (_rules == null)
            {
                return candidate.IsValid;
            }
            var result = _rules.CanBeSearchedTarget(_casterActorId, candidate.ActorId);
            return result.Passed;
        }
    }
}

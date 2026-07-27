using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum MobaActionTargetSourceCode
    {
        ContextTarget = SearchTargetProviderKind.ContextTarget,
        Self = SearchTargetProviderKind.Caster,
        ExplicitActor = SearchTargetProviderKind.ExplicitTarget,
        AllActors = SearchTargetProviderKind.AllActors,
        SameTeam = SearchTargetProviderKind.SameTeam,
        EnemyTeam = SearchTargetProviderKind.EnemyTeam,
        MainType = SearchTargetProviderKind.MainType,
        UnitSubType = SearchTargetProviderKind.UnitSubType,
        SearchQueryTemplate = 1000,
    }

    public enum MobaActionTargetFilterCode
    {
        None = 0,
        RequireValidId = SearchTargetRuleKind.RequireValidId,
        RequireHasPosition = SearchTargetRuleKind.RequireHasPosition,
        CircleShape = SearchTargetRuleKind.CircleShape,
        SectorShape = SearchTargetRuleKind.SectorShape,
        ExcludeCaster = SearchTargetRuleKind.ExcludeCaster,
        ExcludeContextTarget = SearchTargetRuleKind.ExcludeExplicitTarget,
        Whitelist = SearchTargetRuleKind.Whitelist,
        Blacklist = SearchTargetRuleKind.Blacklist,
    }

    public enum MobaActionTargetOrderCode
    {
        None = 0,
        Zero = SearchTargetScorerKind.Zero,
        Random = SearchTargetScorerKind.SeededHashRandom,
        DistanceToCaster = SearchTargetScorerKind.DistanceToCaster,
        DistanceToContextTarget = SearchTargetScorerKind.DistanceToExplicitTarget,
    }

    public enum MobaActionTargetSelectCode
    {
        TopK = SearchTargetSelectorKind.TopKByScore,
        StreamingTopK = SearchTargetSelectorKind.StreamingTopKByScore,
    }

    public readonly struct MobaActionTargetRequest
    {
        public MobaActionTargetRequest(
            MobaActionTargetSourceCode sourceCode,
            int sourceParam = 0,
            MobaActionTargetFilterCode filterCode = MobaActionTargetFilterCode.None,
            int filterParam = 0,
            float radius = 0f,
            float halfAngleDeg = 0f,
            MobaActionTargetOrderCode orderCode = MobaActionTargetOrderCode.DistanceToCaster,
            int orderParam = 0,
            MobaActionTargetSelectCode selectCode = MobaActionTargetSelectCode.TopK,
            int maxCount = 1,
            int queryTemplateId = 0,
            int targetActorId = 0,
            int targetPayloadActorId = 0)
        {
            SourceCode = sourceCode;
            SourceParam = sourceParam;
            FilterCode = filterCode;
            FilterParam = filterParam;
            Radius = radius;
            HalfAngleDeg = halfAngleDeg;
            OrderCode = orderCode;
            OrderParam = orderParam;
            SelectCode = selectCode;
            MaxCount = maxCount;
            QueryTemplateId = queryTemplateId;
            TargetActorId = targetActorId;
            TargetPayloadActorId = targetPayloadActorId;
        }

        public MobaActionTargetSourceCode SourceCode { get; }
        public int SourceParam { get; }
        public MobaActionTargetFilterCode FilterCode { get; }
        public int FilterParam { get; }
        public float Radius { get; }
        public float HalfAngleDeg { get; }
        public MobaActionTargetOrderCode OrderCode { get; }
        public int OrderParam { get; }
        public MobaActionTargetSelectCode SelectCode { get; }
        public int MaxCount { get; }
        public int QueryTemplateId { get; }
        public int TargetActorId { get; }
        public int TargetPayloadActorId { get; }
        public bool UsesTemplate => QueryTemplateId > 0 || SourceCode == MobaActionTargetSourceCode.SearchQueryTemplate;

        public static MobaActionTargetRequest ContextTarget(int queryTemplateId = 0)
        {
            return queryTemplateId > 0
                ? new MobaActionTargetRequest(MobaActionTargetSourceCode.SearchQueryTemplate, queryTemplateId: queryTemplateId)
                : new MobaActionTargetRequest(MobaActionTargetSourceCode.ContextTarget);
        }

        public static MobaActionTargetRequest Self(int queryTemplateId = 0)
        {
            return queryTemplateId > 0
                ? new MobaActionTargetRequest(MobaActionTargetSourceCode.SearchQueryTemplate, queryTemplateId: queryTemplateId)
                : new MobaActionTargetRequest(MobaActionTargetSourceCode.Self);
        }

        public static MobaActionTargetRequest ExplicitActor(int targetActorId, int queryTemplateId = 0)
        {
            return queryTemplateId > 0
                ? new MobaActionTargetRequest(MobaActionTargetSourceCode.SearchQueryTemplate, queryTemplateId: queryTemplateId, targetActorId: targetActorId)
                : new MobaActionTargetRequest(MobaActionTargetSourceCode.ExplicitActor, targetActorId: targetActorId);
        }
    }

    internal static class MobaActionTargetResolver
    {
        public static bool TryResolveTargets(
            in MobaActionTargetRequest request,
            in MobaPlanActionInput coreInput,
            in MobaEffectActionInput effectInput,
            ExecCtx<IWorldResolver> ctx,
            string actionName,
            List<int> results)
        {
            if (results == null) return false;
            results.Clear();

            if (!ctx.Context.TryResolve<SearchTargetService>(out var search) || search == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, actionName, $"cannot resolve SearchTargetService. targetSource={request.SourceCode}");
                return false;
            }

            if (!effectInput.HasCasterActor && RequiresCaster(request.SourceCode))
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, actionName, $"target query missing caster. targetSource={request.SourceCode}");
                return false;
            }

            var explicitTargetActorId = ResolveContextTargetActorId(in request, in effectInput);

            if (RequiresExplicitTarget(request.SourceCode) && explicitTargetActorId <= 0)
            {
                MobaPlanActionDiagnostics.Rejected(
                    ctx.Context,
                    actionName,
                    $"target query missing explicit target. targetSource={request.SourceCode}");
                return false;
            }

            var aimPosition = coreInput.AimPosition;
            if (request.UsesTemplate)
            {
                if (request.QueryTemplateId <= 0)
                {
                    MobaPlanActionDiagnostics.Rejected(ctx.Context, actionName, $"target template query requires queryTemplateId. targetSource={request.SourceCode}");
                    return false;
                }

                var found = search.TrySearchActorIds(request.QueryTemplateId, effectInput.CasterActorId, in aimPosition, explicitTargetActorId, results);
                Log.Warning($"[MobaActionTargetResolver] template query action={actionName} queryId={request.QueryTemplateId} caster={effectInput.CasterActorId} explicitTarget={explicitTargetActorId} found={found} count={results.Count} targets={FormatTargets(results)} aim=({aimPosition.X:0.###},{aimPosition.Y:0.###},{aimPosition.Z:0.###})");
                return found;
            }

            var template = BuildInlineTemplate(in request);
            var inlineFound = search.TrySearchActorIds(template, effectInput.CasterActorId, in aimPosition, explicitTargetActorId, results);
            Log.Warning($"[MobaActionTargetResolver] inline query action={actionName} source={request.SourceCode} caster={effectInput.CasterActorId} explicitTarget={explicitTargetActorId} found={inlineFound} count={results.Count} targets={FormatTargets(results)} aim=({aimPosition.X:0.###},{aimPosition.Y:0.###},{aimPosition.Z:0.###})");
            return inlineFound;
        }

        private static string FormatTargets(List<int> targets)
        {
            if (targets == null) return "<null>";
            if (targets.Count == 0) return "<empty>";
            return string.Join(",", targets);
        }

        private static bool RequiresExplicitTarget(MobaActionTargetSourceCode sourceCode)
        {
            return sourceCode == MobaActionTargetSourceCode.ContextTarget
                || sourceCode == MobaActionTargetSourceCode.ExplicitActor;
        }

        private static bool RequiresCaster(MobaActionTargetSourceCode sourceCode)
        {
            switch (sourceCode)
            {
                case MobaActionTargetSourceCode.Self:
                case MobaActionTargetSourceCode.SameTeam:
                case MobaActionTargetSourceCode.EnemyTeam:
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveContextTargetActorId(in MobaActionTargetRequest request, in MobaEffectActionInput input)
        {
            if (request.TargetPayloadActorId > 0) return request.TargetPayloadActorId;
            if (request.TargetActorId > 0) return request.TargetActorId;
            if (request.SourceCode == MobaActionTargetSourceCode.Self) return input.CasterActorId;
            return input.TargetActorId;
        }

        private static SearchQueryTemplateMO BuildInlineTemplate(in MobaActionTargetRequest request)
        {
            var provider = new SearchTargetProviderConfig(0, (int)request.SourceCode, request.SourceParam);
            var rules = BuildRules(in request);
            var scorer = BuildScorer(in request);
            var selector = new SearchTargetSelectorConfig(0, (int)request.SelectCode);
            var explicitTargetPolicy = request.TargetPayloadActorId > 0 || request.TargetActorId > 0 || request.SourceCode == MobaActionTargetSourceCode.ExplicitActor
                ? SearchQueryExplicitTargetPolicy.PreferExplicitTarget
                : SearchQueryExplicitTargetPolicy.IgnoreExplicitTarget;

            return new SearchQueryTemplateMO(
                id: 0,
                name: "inline_action_target_query",
                maxCount: request.MaxCount > 0 ? request.MaxCount : 1,
                explicitTargetPolicy: (int)explicitTargetPolicy,
                provider: provider,
                rules: rules,
                scorer: scorer,
                selector: selector);
        }

        private static SearchTargetRuleConfig[] BuildRules(in MobaActionTargetRequest request)
        {
            if (request.FilterCode == MobaActionTargetFilterCode.None) return Array.Empty<SearchTargetRuleConfig>();

            var actorIds = request.FilterParam > 0 ? new[] { request.FilterParam } : null;
            return new[]
            {
                new SearchTargetRuleConfig(
                    id: 0,
                    kind: (int)request.FilterCode,
                    center: (int)SearchTargetPointKind.Caster,
                    forward: (int)SearchTargetPointKind.AimPosition,
                    radius: request.Radius,
                    halfAngleDeg: request.HalfAngleDeg,
                    actorIds: actorIds)
            };
        }

        private static SearchTargetScorerConfig BuildScorer(in MobaActionTargetRequest request)
        {
            var orderCode = request.OrderCode == MobaActionTargetOrderCode.None
                ? MobaActionTargetOrderCode.Zero
                : request.OrderCode;
            return new SearchTargetScorerConfig(0, (int)orderCode, (int)SearchTargetPointKind.Caster, request.OrderParam);
        }
    }
}

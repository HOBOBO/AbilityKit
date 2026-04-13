using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Triggering.SummonSpawning;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Pool;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class SpawnSummonAction : ITriggerRunningAction
    {
        private readonly ActionDef _def;

        private static readonly ObjectPool<List<int>> _intListPool = Pools.GetPool(
            createFunc: () => new List<int>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        public SpawnSummonAction(ActionDef def)
        {
            _def = def;
        }

        public static SpawnSummonAction FromDef(ActionDef def)
        {
            return new SpawnSummonAction(def);
        }

        public void Execute(TriggerContext context)
        {
            Start(context);
        }

        public IRunningAction Start(TriggerContext context)
        {
            if (context == null) return null;

            var spec = SpawnSummonSpecResolver.Resolve(_def, context);
            if (spec == null)
            {
                Log.Warning("[Trigger] spawn_summon failed to resolve spec");
                return null;
            }

            if (spec.SummonId <= 0)
            {
                Log.Warning("[Trigger] spawn_summon requires summonId > 0");
                return null;
            }

            // If no ongoing parameters are set, do a one-shot spawn and return null.
            var hasOngoing = spec.IntervalMs > 0 && (spec.DurationMs > 0 || spec.TotalCount > 0);
            if (!hasOngoing)
            {
                DoSpawnOnce(context, spec);
                return null;
            }

            if (!SpawnSummonContextResolver.TryResolve(in spec, context, out var data)) return null;

            var ownerKey = SpawnSummonOwnerKeyUtil.ResolveOwnerKey(spec.OwnerKey, context, data.CasterActorId);
            var running = new SummonSpawnerRunningAction(
                tickSpawnOnce: () => DoSpawnOnce(context, spec),
                intervalMs: spec.IntervalMs,
                durationMs: spec.DurationMs,
                totalCount: spec.TotalCount);

            // TriggerRunner will bind running actions by reading args["ownerKey"] first.
            var dict = context.Event.Args as IDictionary<string, object>;
            if (dict != null)
            {
                if (ownerKey != 0) dict["ownerKey"] = ownerKey;
                else dict.Remove("ownerKey");
            }

            return running;
        }

        private static void DoSpawnOnce(TriggerContext context, SpawnSummonSpec spec)
        {
            if (context == null) return;
            if (spec == null) return;

            var summons = context.Services?.GetService(typeof(MobaSummonService)) as MobaSummonService;
            if (summons == null)
            {
                Log.Warning("[Trigger] spawn_summon cannot resolve MobaSummonService from DI");
                return;
            }

            var actors = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;

            if (!SpawnSummonContextResolver.TryResolve(in spec, context, out var data)) return;

            if (spec.Target == SpawnSummonSpec.TargetMode.QueryTargets)
            {
                if (spec.QueryTemplateId <= 0)
                {
                    Log.Warning("[Trigger] spawn_summon targetMode=QueryTargets requires queryTemplateId > 0");
                    return;
                }

                var search = context.Services?.GetService(typeof(SearchTargetService)) as SearchTargetService;
                if (search == null)
                {
                    Log.Warning("[Trigger] spawn_summon cannot resolve SearchTargetService from DI");
                    return;
                }

                TriggerActionArgUtil.TryResolveActorId(data.TargetObj, out var explicitTargetActorId);

                var list = _intListPool.Get();
                try
                {
                    if (!search.TrySearchActorIds(spec.QueryTemplateId, data.CasterActorId, in data.AimPos, explicitTargetActorId, list))
                    {
                        return;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var targetActorId2 = list[i];
                        if (targetActorId2 <= 0) continue;

                        var anchor2 = SpawnSummonPositionResolver.ResolveAnchorPos(spec.Position, actors, data.CasterActorId, targetActorId2, in data.AimPos, in data.FixedPos);
                        var targetPos2 = default(Vec3);
                        ActorEntity te = null;
                        var hasTargetPos2 = actors != null && actors.TryGet(targetActorId2, out te) && te != null && te.hasTransform;
                        if (hasTargetPos2) targetPos2 = te.transform.Value.Position;
                        var forward2 = SpawnSummonRotationResolver.ResolveForward(spec.Rotation, actors, data.CasterActorId, targetActorId2, context);
                        SpawnSummonPatternGenerator.Generate(in spec, in anchor2, hasTargetPos2, in targetPos2, in forward2, (in Vec3 p, in Vec3 f) => summons.TrySummon(data.CasterActorId, spec.SummonId, in p, in f));
                    }
                }
                finally
                {
                    _intListPool.Release(list);
                }

                return;
            }

            var targetActorId = 0;
            TriggerActionArgUtil.TryResolveActorId(data.TargetObj, out targetActorId);

            var anchor = SpawnSummonPositionResolver.ResolveAnchorPos(spec.Position, actors, data.CasterActorId, targetActorId, in data.AimPos, in data.FixedPos);
            var targetPos = default(Vec3);
            ActorEntity te2 = null;
            var hasTargetPos = actors != null && targetActorId > 0 && actors.TryGet(targetActorId, out te2) && te2 != null && te2.hasTransform;
            if (hasTargetPos) targetPos = te2.transform.Value.Position;
            var forward = SpawnSummonRotationResolver.ResolveForward(spec.Rotation, actors, data.CasterActorId, targetActorId, context);
            SpawnSummonPatternGenerator.Generate(in spec, in anchor, hasTargetPos, in targetPos, in forward, (in Vec3 p, in Vec3 f) => summons.TrySummon(data.CasterActorId, spec.SummonId, in p, in f));
        }
    }
}

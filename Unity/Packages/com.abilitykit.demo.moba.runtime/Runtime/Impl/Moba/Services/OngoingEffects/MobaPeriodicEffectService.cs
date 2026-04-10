using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public class MobaPeriodicEffectService : IService
    {
        private readonly MobaConfigDatabase _config;
        private readonly MobaEffectInvokerService _invoker;
        private readonly MobaActorLookupService _actors;

        private static long _nextInstanceId;

        public MobaPeriodicEffectService(MobaConfigDatabase config, MobaEffectInvokerService invoker, MobaActorLookupService actors)
        {
            _config = config;
            _invoker = invoker;
            _actors = actors;
        }

        public IRunningAction Start(int ongoingEffectId, int sourceActorId, int targetActorId)
        {
            return Start(ongoingEffectId, sourceActorId, targetActorId, ownerKey: 0);
        }

        public IRunningAction Start(int ongoingEffectId, int sourceActorId, int targetActorId, long ownerKey)
        {
            if (ongoingEffectId <= 0) return null;
            if (targetActorId <= 0) return null;
            if (_config == null) return null;
            if (_invoker == null) return null;

            if (!_config.TryGetOngoingEffect(ongoingEffectId, out var cfg) || cfg == null)
            {
                return null;
            }

            if (_actors == null) return null;
            if (!_actors.TryGetActorEntity(targetActorId, out var e) || e == null) return null;

            if (ownerKey != 0)
            {
                StopByOwnerKey(targetActorId, ownerKey);
            }

            if (!e.hasOngoingEffects)
            {
                e.AddOngoingEffects(new List<OngoingEffectRuntime>());
            }

            var list = e.ongoingEffects.Active;
            if (list == null)
            {
                list = new List<OngoingEffectRuntime>();
                e.ReplaceOngoingEffects(list);
            }

            var instanceId = ++_nextInstanceId;
            var rt = new OngoingEffectRuntime
            {
                InstanceId = instanceId,
                OngoingEffectId = ongoingEffectId,
                SourceActorId = sourceActorId,
                RemainingMs = cfg.DurationMs,
                NextTickMs = cfg.PeriodMs,
                OwnerKey = ownerKey,
                Applied = false,
            };
            list.Add(rt);

            if (cfg.OnApplyEffectId > 0)
            {
                try
                {
                    _invoker.Execute(
                        effectId: cfg.OnApplyEffectId,
                        sourceActorId: sourceActorId,
                        targetActorId: targetActorId,
                        contextKind: 0,
                        sourceContextId: 0);
                    rt.Applied = true;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[OngoingEffect] Execute OnApply failed. ongoingEffectId={ongoingEffectId} effectId={cfg.OnApplyEffectId} ex={ex.Message}");
                }
            }

            return null;
        }

        public int StopByOwnerKey(int targetActorId, long ownerKey)
        {
            if (targetActorId <= 0) return 0;
            if (ownerKey == 0) return 0;
            if (_config == null) return 0;
            if (_invoker == null) return 0;
            if (_actors == null) return 0;
            if (!_actors.TryGetActorEntity(targetActorId, out var e) || e == null) return 0;
            if (!e.hasOngoingEffects) return 0;

            var list = e.ongoingEffects.Active;
            if (list == null || list.Count == 0) return 0;

            var removed = 0;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var rt = list[i];
                if (rt == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (rt.OwnerKey != ownerKey) continue;

                try
                {
                    if (_config.TryGetOngoingEffect(rt.OngoingEffectId, out var cfg) && cfg != null)
                    {
                        if (cfg.OnRemoveEffectId > 0)
                        {
                            _invoker.Execute(
                                effectId: cfg.OnRemoveEffectId,
                                sourceActorId: rt.SourceActorId,
                                targetActorId: targetActorId,
                                contextKind: 0,
                                sourceContextId: 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[OngoingEffect] Execute OnRemove failed. ongoingEffectId={rt.OngoingEffectId} ownerKey={ownerKey} ex={ex.Message}");
                }

                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public void Dispose()
        {
        }
    }
}

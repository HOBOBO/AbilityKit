using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffApplySystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IUnitResolver _units;
        private IEventBus _eventBus;

        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffApplySystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _units);
            Services.TryGet(out _eventBus);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.ApplyBuffRequest));
        }

        protected override void OnExecute()
        {
            if (_configs == null || _units == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasApplyBuffRequest) continue;

                var req = e.applyBuffRequest;
                e.RemoveApplyBuffRequest();

                if (req.BuffId <= 0) continue;

                if (!_units.TryResolve(new EcsEntityId(e.actorId.Value), out var unit) || unit == null) continue;

                if (!_configs.TryGetBuff(req.BuffId, out var buff) || buff == null) continue;

                if (!e.hasBuffs)
                {
                    e.AddBuffs(new List<BuffRuntime>());
                }

                var list = e.buffs.Active;
                if (list == null)
                {
                    list = new List<BuffRuntime>();
                    e.ReplaceBuffs(list);
                }

                var duration = req.DurationOverrideMs > 0 ? req.DurationOverrideMs : buff.DurationMs;

                var durationSeconds = duration > 0 ? duration / 1000f : 0f;

                var targetActorId = e.actorId.Value;
                var existingIndex = FindExistingBuffIndex(list, buff.Id);
                if (existingIndex >= 0)
                {
                    ApplyToExisting(list[existingIndex], buff, req, durationSeconds);
                    PublishBuffEvent(_eventBus, BuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, list[existingIndex]);
                }
                else
                {
                    var rt = CreateNewRuntime(buff, req, durationSeconds);
                    list.Add(rt);
                    PublishBuffEvent(_eventBus, BuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, rt);
                }
            }
        }

        private static int FindExistingBuffIndex(List<BuffRuntime> list, int buffId)
        {
            if (list == null) return -1;

            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId == buffId) return i;
            }

            return -1;
        }

        private static void ApplyToExisting(BuffRuntime existing, BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds)
        {
            if (existing == null) return;

            switch (buff.StackingPolicy)
            {
                case BuffStackingPolicy.IgnoreIfExists:
                    return;
                case BuffStackingPolicy.Replace:
                    existing.SourceId = req.SourceId;
                    existing.StackCount = 0;
                    existing.Remaining = durationSeconds;
                    AddStack(existing, buff.MaxStacks);
                    return;
                case BuffStackingPolicy.AddStack:
                    AddStack(existing, buff.MaxStacks);
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return;
                case BuffStackingPolicy.RefreshDuration:
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return;
                case BuffStackingPolicy.None:
                default:
                    return;
            }
        }

        private static BuffRuntime CreateNewRuntime(BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds)
        {
            var rt = new BuffRuntime
            {
                BuffId = buff.Id,
                Remaining = durationSeconds,
                SourceId = req.SourceId,
                StackCount = 0,
            };

            AddStack(rt, buff.MaxStacks);
            return rt;
        }

        private static void RefreshRemaining(BuffRuntime rt, BuffRefreshPolicy policy, float durationSeconds)
        {
            if (rt == null) return;

            switch (policy)
            {
                case BuffRefreshPolicy.ResetRemaining:
                    rt.Remaining = durationSeconds;
                    return;
                case BuffRefreshPolicy.AddRemaining:
                    rt.Remaining += durationSeconds;
                    return;
                case BuffRefreshPolicy.KeepRemaining:
                case BuffRefreshPolicy.None:
                default:
                    return;
            }
        }

        private static void AddStack(BuffRuntime rt, int maxStacks)
        {
            if (rt == null) return;

            if (maxStacks <= 0) maxStacks = int.MaxValue;
            if (rt.StackCount >= maxStacks) return;

            rt.StackCount++;
        }

        private static void PublishBuffEvent(IEventBus bus, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            PublishOnce(bus, eventId, buff, sourceActorId, targetActorId, durationSeconds, runtime);

            if (buff.EffectId > 0)
            {
                PublishOnce(bus, BuffTriggering.Events.WithEffect(eventId, buff.EffectId), buff, sourceActorId, targetActorId, durationSeconds, runtime);
            }
        }

        private static void PublishOnce(IEventBus bus, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args[BuffTriggering.Args.BuffId] = buff.Id;
            args[BuffTriggering.Args.EffectId] = buff.EffectId;
            args[BuffTriggering.Args.DurationSeconds] = durationSeconds;
            args[BuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;

            bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args));
        }

        private static class BuffTriggering
        {
            public static class Events
            {
                public const string ApplyOrRefresh = "buff.apply";
                public const string Remove = "buff.remove";

                public static string WithEffect(string baseEventId, int effectId)
                {
                    return string.IsNullOrEmpty(baseEventId) ? null : $"{baseEventId}.{effectId}";
                }
            }

            public static class Args
            {
                public const string BuffId = "buff.id";
                public const string EffectId = "buff.effectId";
                public const string StackCount = "buff.stackCount";
                public const string DurationSeconds = "buff.durationSeconds";
            }
        }
    }
}

using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaBuffService : IService
    {
        private readonly MobaConfigDatabase _configs;
        private readonly IEventBus _eventBus;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly EffectSourceRegistry _effectSource;
        private readonly IFrameTime _frameTime;

        public MobaBuffService(MobaConfigDatabase configs, IEventBus eventBus, ITriggerActionRunner actionRunner, EffectSourceRegistry effectSource, IFrameTime frameTime)
        {
            _configs = configs;
            _eventBus = eventBus;
            _actionRunner = actionRunner;
            _effectSource = effectSource;
            _frameTime = frameTime;
        }

        public bool RemoveBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, EffectSourceEndReason reason)
        {
            if (target == null) return false;
            if (buffId <= 0) return false;

            if (target.hasApplyBuffRequest && target.applyBuffRequest != null && target.applyBuffRequest.BuffId == buffId)
            {
                target.RemoveApplyBuffRequest();
            }

            if (!target.hasBuffs) return false;

            var list = target.buffs.Active;
            if (list == null || list.Count == 0) return false;

            var removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId != buffId) continue;

                removed = true;

                try
                {
                    if (b.SourceContextId != 0)
                    {
                        _actionRunner?.CancelByOwnerKey(b.SourceContextId);
                    }
                }
                catch
                {
                }

                var normalizedReason = reason;
                if (normalizedReason == EffectSourceEndReason.None) normalizedReason = EffectSourceEndReason.Dispelled;

                try
                {
                    if (b.SourceContextId != 0)
                    {
                        _effectSource?.End(b.SourceContextId, GetFrame(), normalizedReason);
                    }
                }
                catch
                {
                }

                if (_configs != null)
                {
                    if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                    {
                        PublishBuffRemove(_eventBus, buff.EffectId, sourceActorId, target.actorId.Value, b.BuffId, b.StackCount, b.SourceContextId, normalizedReason);
                    }
                }

                list.RemoveAt(i);
            }

            return removed;
        }

        private static void PublishBuffRemove(IEventBus bus, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount, long sourceContextId, EffectSourceEndReason reason)
        {
            if (bus == null) return;

            PublishOnce(bus, "buff.remove", effectId, sourceActorId, targetActorId, buffId, stackCount, sourceContextId, reason);
            if (effectId > 0)
            {
                PublishOnce(bus, $"buff.remove.{effectId}", effectId, sourceActorId, targetActorId, buffId, stackCount, sourceContextId, reason);
            }
        }

        private static void PublishOnce(IEventBus bus, string eventId, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount, long sourceContextId, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args["buff.id"] = buffId;
            args["buff.effectId"] = effectId;
            args["buff.stackCount"] = stackCount;
            args["buff.removeReason"] = (int)reason;
            if (sourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = sourceContextId;
            }
            bus.Publish(new TriggerEvent(eventId, payload: null, args: args));
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
        }
    }
}

using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectApplyDemoSubscriber : IService, IEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly ILogSink _log;
        private readonly global::AbilityKit.Ability.Share.Impl.Moba.Services.MobaDamageService _damage;
        private IEventSubscription _sub;

        public MobaEffectApplyDemoSubscriber(IEventBus eventBus, ILogSink log, global::AbilityKit.Ability.Share.Impl.Moba.Services.MobaDamageService damage)
        {
            _eventBus = eventBus;
            _log = log;
            _damage = damage;
            _sub = _eventBus?.Subscribe(MobaTriggerEventIds.EffectApplyById(10001), this);
        }

        public void Handle(in TriggerEvent evt)
        {
            var effectId = 0;
            if (evt.Args != null && evt.Args.TryGetValue("effect.id", out var obj) && obj is int i)
            {
                effectId = i;
            }

            if (evt.Args != null)
            {
                var casterActorId = 0;
                if (evt.Args.TryGetValue(MobaSkillTriggering.Args.CasterActorId, out var casterObj) && casterObj is int casterI)
                {
                    casterActorId = casterI;
                }

                var targetActorId = 0;
                if (evt.Args.TryGetValue(MobaSkillTriggering.Args.TargetActorId, out var targetObj) && targetObj is int targetI)
                {
                    targetActorId = targetI;
                }

                if (targetActorId > 0)
                {
                    _damage?.ApplyDamage(attackerActorId: casterActorId, targetActorId: targetActorId, damageType: 0, value: 50f, reasonKind: 1, reasonParam: effectId);
                }
            }

            var payloadType = evt.Payload?.GetType().Name ?? "null";
            _log?.Info($"[EffectApplyDemo] received {evt.Id}, effectId={effectId}, payloadType={payloadType}");
        }

        public void Dispose()
        {
            var s = _sub;
            if (s != null)
            {
                _sub = null;
                s.Unsubscribe();
            }
        }
    }
}

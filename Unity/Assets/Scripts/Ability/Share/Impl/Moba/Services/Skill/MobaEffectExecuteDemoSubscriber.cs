using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectExecuteDemoSubscriber : IService, IEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly ILogSink _log;
        private IEventSubscription _sub;

        public MobaEffectExecuteDemoSubscriber(IEventBus eventBus, ILogSink log)
        {
            _eventBus = eventBus;
            _log = log;
            _sub = _eventBus?.Subscribe("effect.execute", this);
        }

        public void Handle(in TriggerEvent evt)
        {
            var effectId = 0;
            if (evt.Args != null && evt.Args.TryGetValue("effect.id", out var obj) && obj is int i)
            {
                effectId = i;
            }

            var payloadType = evt.Payload?.GetType().Name ?? "null";
            _log?.Info($"[EffectExecuteDemo] received {evt.Id}, effectId={effectId}, payloadType={payloadType}");
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

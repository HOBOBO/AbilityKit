using System;
using System.Collections.Generic;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Core.Common.Log;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Triggering
{
    public sealed class BattleTriggerEventViewBridge : IEventHandler, IDisposable
    {
        private readonly IEventBus _bus;
        private readonly IBattleViewEventSink _sink;
        private readonly List<IEventSubscription> _subs = new List<IEventSubscription>(16);

        public BattleTriggerEventViewBridge(IEventBus bus, IBattleViewEventSink sink)
        {
            _bus = bus;
            _sink = sink;

            if (_bus == null) return;

            _subs.Add(_bus.Subscribe(DamagePipelineEvents.AfterApply, this));

            _subs.Add(_bus.Subscribe(MobaBuffTriggering.Events.ApplyOrRefresh, this));
            _subs.Add(_bus.Subscribe(MobaBuffTriggering.Events.Remove, this));

            _subs.Add(_bus.Subscribe(AreaTriggering.Events.Spawn, this));
            _subs.Add(_bus.Subscribe(AreaTriggering.Events.Enter, this));
            _subs.Add(_bus.Subscribe(AreaTriggering.Events.Exit, this));
            _subs.Add(_bus.Subscribe(AreaTriggering.Events.Expire, this));

            _subs.Add(_bus.Subscribe(ProjectileTriggering.Events.Hit, this));
        }

        public void Handle(in TriggerEvent evt)
        {
            _sink?.OnTriggerEvent(in evt);
        }

        public void Dispose()
        {
            for (int i = 0; i < _subs.Count; i++)
            {
                try
                {
                    _subs[i]?.Unsubscribe();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }
            _subs.Clear();
        }
    }
}

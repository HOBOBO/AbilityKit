using System;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal interface ISessionEventsHost
        {
            void OnStartSessionRequested();

            void RaiseSessionStarted(BattleStartPlan plan);
            void RaiseSessionFailed(Exception exception);
            void RaiseFirstFrameReceived();

            BattleContext Context { get; }

            Exception PendingModuleValidationFailure { get; set; }

            BattleEventBus Events { get; set; }
            BattleSessionHooks Hooks { get; set; }
        }

        internal interface IBattleSessionEventsProvider
        {
            bool TryCreate(out BattleEventBus events, out BattleSessionHooks hooks, out bool owned);
        }

        private sealed class SessionEventsController
        {
            private bool _owned;

            public void OnAttach(IBattleSessionEventsProvider provider, ISessionEventsHost host)
            {
                if (host == null) return;

                if (provider != null && provider.TryCreate(out var events, out var hooks, out var owned) && events != null)
                {
                    host.Events = events;
                    host.Hooks = hooks;
                    _owned = owned;
                }
                else
                {
                    host.Hooks = new BattleSessionHooks();
                    host.Events = new BattleEventBus();
                    _owned = true;
                }

                host.Events.Subscribe<StartSessionRequested>(_ => host.OnStartSessionRequested());

                host.Events.Subscribe<SessionStartedEvent>(e =>
                {
                    host.RaiseSessionStarted(e.Plan);
                });

                host.Events.Subscribe<SessionFailedEvent>(e =>
                {
                    host.RaiseSessionFailed(e.Exception);
                });

                host.Events.Subscribe<FirstFrameReceivedEvent>(_ =>
                {
                    host.RaiseFirstFrameReceived();
                });

                if (host.Context != null)
                {
                    host.Context.Events = host.Events;
                }

                if (host.PendingModuleValidationFailure != null)
                {
                    host.Events?.Publish(new SessionFailedEvent(host.PendingModuleValidationFailure));
                    host.Events?.Flush();
                    host.PendingModuleValidationFailure = null;
                }
            }

            public void OnDetach(ISessionEventsHost host)
            {
                if (host == null) return;

                if (host.Context != null)
                {
                    host.Context.Events = null;
                }

                if (_owned)
                {
                    host.Events?.Dispose();
                }

                host.Events = null;
                host.Hooks = null;

                _owned = false;
            }
        }
    }
}

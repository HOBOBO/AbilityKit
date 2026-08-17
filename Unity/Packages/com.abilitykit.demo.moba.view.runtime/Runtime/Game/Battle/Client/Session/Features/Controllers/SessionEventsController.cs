using System;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionEventsHost
    {
        void OnStartSessionRequested();

        void RaiseSessionStarted(BattleStartPlan plan);
        void RaiseWorldReady();
        void RaiseSessionFailed(Exception exception);
        void RaiseFirstFrameReceived();

        Exception PendingSubFeatureValidationFailure { get; set; }

        BattleSessionHooks Hooks { get; set; }
    }

    internal sealed class SessionEventsController
    {
        private bool _hasAttached;

        public void OnAttach(ISessionEventsHost host)
        {
            if (host == null) return;
            if (_hasAttached) return;

            host.Hooks ??= new BattleSessionHooks();

            if (host.PendingSubFeatureValidationFailure != null)
            {
                host.RaiseSessionFailed(host.PendingSubFeatureValidationFailure);
                host.PendingSubFeatureValidationFailure = null;
            }

            _hasAttached = true;
        }

        public void OnDetach(ISessionEventsHost host)
        {
            if (host == null) return;

            host.Hooks = null;
            _hasAttached = false;
        }

        public void RequestStartSession(ISessionEventsHost host)
        {
            if (host == null) return;
            host.OnStartSessionRequested();
        }

        public void NotifySessionStarted(ISessionEventsHost host, BattleStartPlan plan)
        {
            if (host == null) return;
            host.RaiseSessionStarted(plan);
        }

        public void NotifyWorldReady(ISessionEventsHost host)
        {
            if (host == null) return;
            host.RaiseWorldReady();
        }

        public void NotifySessionFailed(ISessionEventsHost host, Exception exception)
        {
            if (host == null) return;
            host.RaiseSessionFailed(exception);
        }

        public void NotifyFirstFrameReceived(ISessionEventsHost host)
        {
            if (host == null) return;
            host.RaiseFirstFrameReceived();
        }
    }
}

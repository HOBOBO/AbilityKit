using System;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionEventsSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_events";

            public System.Collections.Generic.IEnumerable<string> Dependencies => null;

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f.Hooks = new BattleSessionHooks();

                f.Events = new BattleEventBus();
                f.Events.Subscribe<StartSessionRequested>(_ => f.OnStartSessionRequested());

                f.Events.Subscribe<SessionStartedEvent>(e =>
                {
                    f.SessionStarted?.Invoke();
                    f.Hooks?.SessionStarted.Invoke(e.Plan);
                });

                f.Events.Subscribe<SessionFailedEvent>(e =>
                {
                    f.SessionFailed?.Invoke(e.Exception);
                    f.Hooks?.SessionFailed.Invoke(e.Exception);
                });

                f.Events.Subscribe<FirstFrameReceivedEvent>(_ =>
                {
                    f.FirstFrameReceived?.Invoke();
                    f.Hooks?.FirstFrameReceived.Invoke();
                });

                if (f._ctx != null)
                {
                    f._ctx.Events = f.Events;
                }

                if (f._pendingModuleValidationFailure != null)
                {
                    f.Events?.Publish(new SessionFailedEvent(f._pendingModuleValidationFailure));
                    f.Events?.Flush();
                    f._pendingModuleValidationFailure = null;
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}

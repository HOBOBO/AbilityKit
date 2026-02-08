using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionEditorHooksSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_editor_hooks";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
#if UNITY_EDITOR
                var f = ctx.Feature;
                if (f == null) return;
                f.TryInstallEditorPlayModeStopHook();
#endif
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
#if UNITY_EDITOR
                var f = ctx.Feature;
                if (f == null) return;
                f.TryUninstallEditorPlayModeStopHook();
#endif
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}

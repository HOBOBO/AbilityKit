using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionEditorHooksSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_editor_hooks";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
#if UNITY_EDITOR
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionEditorHooksRuntime>(ctx, Id);
            runtime.TryInstallEditorPlayModeStopHook();
#endif
        }

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
#if UNITY_EDITOR
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionEditorHooksRuntime>(ctx, out var runtime)) return;
            runtime.TryUninstallEditorPlayModeStopHook();
#endif
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}

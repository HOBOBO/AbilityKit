using System.Collections.Generic;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private GamePhaseContext _phaseCtx
        {
            get => _handles.Phase.PhaseCtx;
            set => _handles.Phase.PhaseCtx = value;
        }

        private BattleContext _ctx
        {
            get => _handles.Phase.Ctx;
            set => _handles.Phase.Ctx = value;
        }

        private List<ISessionSubFeature<ISessionRuntimeFeature>> _subFeatures
        {
            get => _handles.Phase.SubFeatures;
            set => _handles.Phase.SubFeatures = value;
        }

        private ModuleHost<FeatureModuleContext<ISessionRuntimeFeature>, ISessionSubFeature<ISessionRuntimeFeature>> _subFeatureHost
        {
            get => _handles.Phase.SubFeatureHost;
            set => _handles.Phase.SubFeatureHost = value;
        }

        private IBattleViewFeatureHost _flow
        {
            get => _handles.Phase.Flow;
            set => _handles.Phase.Flow = value;
        }

#if UNITY_EDITOR
        private bool _editorPlayModeHookActive
        {
            get => _state.EditorHooks.PlayModeHookActive;
            set => _state.EditorHooks.PlayModeHookActive = value;
        }
#endif
    }
}

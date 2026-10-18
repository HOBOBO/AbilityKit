using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BootMenuOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private bool _show = true;

        public void OnAttach(in GamePhaseContext ctx)
        {
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
#if UNITY_EDITOR
            if (!_show) return;
            if (!ctx.Entry.DebugEnabled) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 120), GUI.skin.window);
            GUILayout.Label("Game Flow");

            if (GUILayout.Button("Enter Battle (Test)", GUILayout.Height(28)))
            {
                // TestBattleBootstrapper 需要通过 GameFlowDomain 具体方法传入，
                // IFlowCommandSink.RequestEnterBattle() 不带 bootstrapper。
                var flow = ctx.Entry.Get<GameFlowDomain>();
                flow.EnterBattle(new TestBattleBootstrapper());
            }

            GUILayout.EndArea();
#endif
        }
    }
}

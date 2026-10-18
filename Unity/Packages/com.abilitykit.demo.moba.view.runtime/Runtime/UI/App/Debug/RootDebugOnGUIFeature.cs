using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Root 级调试 OnGUI 面板。
    /// 从 <see cref="GameFlowDomain.OnGUI"/> 中提取，通过 <see cref="IFlowCommandSink"/> 接口提交命令。
    /// 在 Battle.InMatch 期间自动隐藏，避免遮挡战斗 HUD。
    /// </summary>
    public sealed class RootDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ctx.Entry.DebugEnabled) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink == null) return;
            var state = sink.CurrentState;

            // Battle.InMatch 期间隐藏，避免遮挡战斗 HUD
            if (state.RootPhase == MobaRootState.Battle
                && state.BattlePhase == MobaBattleState.InMatch)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(350, 10, 420, 140), GUI.skin.window);
            GUILayout.Label($"HFSM Root={state.RootPhase}, Battle={state.BattlePhase?.ToString() ?? "-"}");

            if (GUILayout.Button("Enter Battle", GUILayout.Height(28)))
            {
                sink.RequestEnterBattle();
            }

            if (GUILayout.Button("Battle End", GUILayout.Height(28)))
            {
                sink.RequestBattleEnd();
            }

            if (GUILayout.Button("Return Lobby", GUILayout.Height(28)))
            {
                sink.RequestReturnLobby();
            }

            GUILayout.EndArea();
#endif
        }
    }
}

using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleContext _ctx;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!ctx.Entry.DebugEnabled) return;

            GUILayout.BeginArea(new Rect(10, 140, 420, 160), GUI.skin.window);
            GUILayout.Label("Battle Debug");

            if (_ctx == null || _ctx.Session == null)
            {
                GUILayout.Label("Session: null");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"WorldId: {_ctx.Plan.WorldId}");
            GUILayout.Label($"LastFrame: {_ctx.LastFrame}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect")) _ctx.Session.Connect();
            if (GUILayout.Button("Disconnect")) _ctx.Session.Disconnect();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Join"))
            {
                _ctx.Session.Join(new JoinWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_ctx.Plan.WorldId), new PlayerId(_ctx.Plan.PlayerId)));
            }
            if (GUILayout.Button("Leave"))
            {
                _ctx.Session.Leave(new LeaveWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_ctx.Plan.WorldId), new PlayerId(_ctx.Plan.PlayerId)));
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Exit Battle", GUILayout.Height(26)))
            {
                var flow = ctx.Entry.Get<GameFlowDomain>();
                flow.ReturnToBoot();
            }

            GUILayout.EndArea();
        }
    }
}

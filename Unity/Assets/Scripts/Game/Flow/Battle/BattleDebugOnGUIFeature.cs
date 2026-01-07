using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleSessionFeature _session;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _session);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _session = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!ctx.Entry.DebugEnabled) return;

            GUILayout.BeginArea(new Rect(10, 140, 420, 160), GUI.skin.window);
            GUILayout.Label("Battle Debug");

            if (_session == null || _session.Session == null)
            {
                GUILayout.Label("Session: null");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"WorldId: {_session.Plan.WorldId}");
            GUILayout.Label($"LastFrame: {_session.LastFrame}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect")) _session.Session.Connect();
            if (GUILayout.Button("Disconnect")) _session.Session.Disconnect();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Join"))
            {
                _session.Session.Join(new JoinWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_session.Plan.WorldId), new PlayerId(_session.Plan.PlayerId)));
            }
            if (GUILayout.Button("Leave"))
            {
                _session.Session.Leave(new LeaveWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_session.Plan.WorldId), new PlayerId(_session.Plan.PlayerId)));
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

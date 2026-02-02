using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleContext _ctx;

        private Vector2 _scroll;

        private bool _showFrameSyncStats;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            BattleFlowDebugProvider.Current = _ctx;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ReferenceEquals(BattleFlowDebugProvider.Current, _ctx))
            {
                BattleFlowDebugProvider.Current = null;
            }
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!ctx.Entry.DebugEnabled) return;

            var flowDomain = ctx.Entry.Get<GameFlowDomain>();
            if (flowDomain == null || flowDomain.CurrentPhase != GameFlowDomain.RootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 170, 70), GUI.skin.window);
            if (GUILayout.Button("Exit Battle", GUILayout.Height(34)))
            {
                var flow = ctx.Entry.Get<GameFlowDomain>();
                flow.ReturnToBoot();
            }
            GUILayout.EndArea();
        }
    }
}

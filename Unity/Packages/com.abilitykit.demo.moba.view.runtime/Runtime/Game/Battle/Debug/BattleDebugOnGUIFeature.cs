using AbilityKit.Demo.Moba;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private readonly BattleDebugPublicationOwner _publication = new BattleDebugPublicationOwner();

        public void OnAttach(in GamePhaseContext ctx)
        {
            _publication.Refresh(in ctx);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _publication.Dispose();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _publication.Refresh(in ctx);
            if (!ctx.Entry.DebugEnabled) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink == null || sink.CurrentRootPhase != MobaRootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 170, 110), GUI.skin.window);
            if (GUILayout.Button("Exit Battle", GUILayout.Height(34)))
            {
                sink.RequestReturnLobby();
            }

            if (GUILayout.Button("Rebind Views", GUILayout.Height(34)))
            {
                if (ctx.Features.TryGet(out BattleViewFeature view) && view != null)
                {
                    view.RebindAll();
                }
                if (ctx.Features.TryGet(out ConfirmedBattleViewFeature confirmed) && confirmed != null)
                {
                    confirmed.RebindAll();
                }
            }
            GUILayout.EndArea();
#endif
        }

    }
}

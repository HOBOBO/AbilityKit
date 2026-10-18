using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class FormalLobbyOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private Vector2 _roomScroll;

        public void OnAttach(in GamePhaseContext ctx) => _roomScroll = default;
        public void OnDetach(in GamePhaseContext ctx) { }
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null || !ctx.Entry.TryGet(out FormalLobbyFeature lobby) ||
                !lobby.TryCreateScreen(in ctx, out var snapshot, out var commands))
                return;

            FormalLobbyRenderer.Draw(snapshot, commands, ref _roomScroll);
        }
    }
}

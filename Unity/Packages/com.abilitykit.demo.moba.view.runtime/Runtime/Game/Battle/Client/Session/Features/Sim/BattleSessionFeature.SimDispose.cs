namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TryDestroyBattleWorlds()
        {
            _runtime.Simulation.DestroyBattleWorlds(_plan);
        }

        private void DisposeConfirmedView()
        {
            _runtime.Simulation.DisposeConfirmedView(_flow, DestroyEntityTree);
        }

        private void DisposeRemoteDrivenWorld()
        {
            _runtime.Simulation.DisposeRemoteDrivenWorld();
        }

        private void DisposeConfirmedWorld()
        {
            _runtime.Simulation.DisposeConfirmedWorld(_ctx);
        }
    }
}

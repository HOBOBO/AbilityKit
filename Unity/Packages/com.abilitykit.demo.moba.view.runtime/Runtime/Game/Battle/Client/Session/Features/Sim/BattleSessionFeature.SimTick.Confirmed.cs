namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TickConfirmedAuthorityWorldSim(float deltaTime)
        {
            _runtime.Simulation.TickConfirmedAuthority(
                _plan,
                _ctx,
                _worldCatchUp,
                GetFixedDeltaSeconds());
        }
    }
}

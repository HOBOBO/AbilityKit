namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TickRemoteDrivenLocalSim(float deltaTime)
        {
            _runtime.Simulation.TickRemoteDriven(
                _plan,
                _ctx,
                _worldCatchUp,
                _snapshots,
                GetFixedDeltaSeconds(),
                _lastServerAckFrame);
        }
    }
}

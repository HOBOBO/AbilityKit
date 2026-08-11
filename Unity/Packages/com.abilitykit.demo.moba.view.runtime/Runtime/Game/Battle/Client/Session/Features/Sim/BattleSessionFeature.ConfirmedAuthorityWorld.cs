namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void StartConfirmedAuthorityWorld()
        {
            _runtime.Simulation.StartConfirmedAuthority(
                _plan,
                _ctx,
                _flow,
                _session != null,
                GetFixedDeltaSeconds(),
                ResolveIdealFrameLimit,
                DestroyEntityTree);
        }
    }
}

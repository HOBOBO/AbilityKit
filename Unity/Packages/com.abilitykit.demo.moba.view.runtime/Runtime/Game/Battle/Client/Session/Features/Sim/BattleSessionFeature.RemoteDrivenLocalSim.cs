namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void StartRemoteDrivenLocalWorld()
        {
            _runtime.Simulation.StartRemoteDriven(
                _plan,
                _ctx,
                GetFixedDeltaSeconds(),
                ResolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => _runtime.Diagnostics.ShouldForceClientHashMismatch);
#else
                () => false);
#endif
        }
    }
}

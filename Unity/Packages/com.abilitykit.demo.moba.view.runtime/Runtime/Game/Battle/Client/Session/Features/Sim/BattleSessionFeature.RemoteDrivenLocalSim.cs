namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void StartRemoteDrivenLocalWorld()
        {
            _worldInstaller.EnsureRemoteDrivenStarted(new RemoteDrivenWorldInstallOptions(
                _plan,
                _ctx,
                _handles.RemoteDriven,
                GetFixedDeltaSeconds(),
                ResolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => DebugForceClientHashMismatch,
#else
                () => false,
#endif
                () => _remoteDrivenLastTickedFrame = 0));
        }
    }
}

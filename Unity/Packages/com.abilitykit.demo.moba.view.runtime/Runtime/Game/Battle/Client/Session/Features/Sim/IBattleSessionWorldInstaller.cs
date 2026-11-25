namespace AbilityKit.Game.Flow
{
    internal interface IBattleSessionWorldInstaller
    {
        void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options);
        void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options);
    }

    internal sealed class DefaultBattleSessionWorldInstaller : IBattleSessionWorldInstaller
    {
        public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
        {
            if (options.Plan.UsesStateSyncAuthority)
            {
                if (options.Handles.World != null) return;
                StateSyncWorldRuntime.EnsureStarted(options.Plan, options.Handles, options.FixedDeltaSeconds);
                options.ResetTickState?.Invoke();
            }
            else
            {
                RemoteDrivenWorldInstaller.EnsureStarted(options);
            }
        }

        public void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options)
        {
            ConfirmedAuthorityWorldInstaller.EnsureStarted(options);
        }
    }
}

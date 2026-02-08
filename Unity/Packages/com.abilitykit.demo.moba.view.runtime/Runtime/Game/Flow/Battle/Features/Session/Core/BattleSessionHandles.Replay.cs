using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class BattleSessionHandles
    {
        internal sealed class ReplayHandles_Deprecated
        {
            internal LockstepReplayDriver Driver;

            public void Reset()
            {
                Driver = null;
            }
        }
    }
}

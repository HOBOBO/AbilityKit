using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Log;
using AbilityKit.Network.Abstractions;
using AbilityKit.Game.Flow.Battle.Replay;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class BattleSessionHandles
    {
        internal sealed class DispatcherHandles
        {
            internal IDispatcher UnityDispatcher;
            internal DedicatedThreadDispatcher NetworkIoDispatcher;

            public void Reset()
            {
                UnityDispatcher = null;
                if (NetworkIoDispatcher != null)
                {
                    try
                    {
                        NetworkIoDispatcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    NetworkIoDispatcher = null;
                }

                NetworkIoDispatcher = null;
            }
        }

        internal sealed class ReplayHandles
        {
            internal LockstepReplayDriver Driver;

            public void Reset()
            {
                Driver = null;
            }
        }
    }
}

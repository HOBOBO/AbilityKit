using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.SnapshotRouting;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class BattleSessionHandles
    {
        internal sealed class SnapshotHandles
        {
            internal FrameSnapshotDispatcher Snapshots;
            internal SnapshotPipeline Pipeline;
            internal SnapshotCmdHandler CmdHandler;
            internal SnapshotRoutingInstance Routing;

            public void Reset()
            {
                if (CmdHandler != null)
                {
                    DisposeUtils.TryDispose(ref CmdHandler, ex => Log.Exception(ex));
                }

                if (Pipeline != null)
                {
                    DisposeUtils.TryDispose(ref Pipeline, ex => Log.Exception(ex));
                }

                if (Snapshots != null)
                {
                    DisposeUtils.TryDispose(ref Snapshots, ex => Log.Exception(ex));
                }

                if (Routing != null)
                {
                    try
                    {
                        Routing.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Routing = null;
                }

                Snapshots = null;
                Pipeline = null;
                CmdHandler = null;
                Routing = null;
            }
        }
    }
}

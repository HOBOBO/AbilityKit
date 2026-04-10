using System;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public sealed class SnapshotRoutingInstance : IDisposable
    {
        public SnapshotRoutingInstance(FrameSnapshotDispatcher snapshots, SnapshotPipeline pipeline, SnapshotCmdHandler cmdHandler)
        {
            Snapshots = snapshots;
            Pipeline = pipeline;
            CmdHandler = cmdHandler;
        }

        public FrameSnapshotDispatcher Snapshots { get; }
        public SnapshotPipeline Pipeline { get; }
        public SnapshotCmdHandler CmdHandler { get; }

        public void Dispose()
        {
            Pipeline?.Dispose();
            CmdHandler?.Dispose();
            Snapshots?.Dispose();
        }
    }
}

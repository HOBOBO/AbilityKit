using System;

namespace AbilityKit.Core.Snapshots.Routing
{
    /// <summary>
    /// 持有一次快照路由装配的处理管线、命令订阅和可选的自有派发器。
    /// </summary>
    public sealed class SnapshotRoutingInstance : IDisposable
    {
        public SnapshotRoutingInstance(FrameSnapshotDispatcher snapshots, SnapshotPipeline pipeline, SnapshotCmdHandler cmdHandler)
        {
            Snapshots = snapshots;
            ExternalDispatcher = null;
            Pipeline = pipeline;
            CmdHandler = cmdHandler;
        }

        public SnapshotRoutingInstance(ISnapshotDispatcher externalDispatcher, SnapshotPipeline pipeline, SnapshotCmdHandler cmdHandler)
        {
            Snapshots = null;
            ExternalDispatcher = externalDispatcher ?? throw new ArgumentNullException(nameof(externalDispatcher));
            Pipeline = pipeline;
            CmdHandler = cmdHandler;
        }

        public FrameSnapshotDispatcher? Snapshots { get; }
        public ISnapshotDispatcher? ExternalDispatcher { get; }
        public SnapshotPipeline Pipeline { get; }
        public SnapshotCmdHandler CmdHandler { get; }

        /// <summary>
        /// 撤销本实例持有的订阅并释放自有派发器，不释放借用的派发器。
        /// </summary>
        public void Dispose()
        {
            Pipeline?.Dispose();
            CmdHandler?.Dispose();
            Snapshots?.Dispose();
        }
    }
}

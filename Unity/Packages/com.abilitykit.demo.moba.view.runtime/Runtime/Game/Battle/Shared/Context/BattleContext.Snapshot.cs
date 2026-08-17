using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private readonly ReferenceBindingOwner<FrameSnapshotDispatcher> _snapshotRoutingBinding =
            new ReferenceBindingOwner<FrameSnapshotDispatcher>();
        private SnapshotPipeline _snapshotPipeline;
        private SnapshotCmdHandler _cmdHandler;

        public FrameSnapshotDispatcher FrameSnapshots
        {
            get => _snapshotRoutingBinding.Value;
        }

        public SnapshotPipeline SnapshotPipeline
        {
            get => _snapshotPipeline;
        }

        public SnapshotCmdHandler CmdHandler
        {
            get => _cmdHandler;
        }

        internal long BindSnapshotRouting(
            FrameSnapshotDispatcher snapshots,
            SnapshotPipeline pipeline,
            SnapshotCmdHandler cmdHandler)
        {
            var generation = _snapshotRoutingBinding.Bind(snapshots);
            _snapshotPipeline = pipeline;
            _cmdHandler = cmdHandler;
            return generation;
        }

        internal bool ClearSnapshotRouting(
            long bindingGeneration,
            FrameSnapshotDispatcher snapshots)
        {
            if (!_snapshotRoutingBinding.TryClear(
                    bindingGeneration,
                    snapshots,
                    out _,
                    out _))
            {
                return false;
            }

            _snapshotPipeline = null;
            _cmdHandler = null;
            return true;
        }

        internal void ClearSnapshotRouting()
        {
            _snapshotRoutingBinding.Reset(out _, out _);
            _snapshotPipeline = null;
            _cmdHandler = null;
        }

        internal bool TryGetFrameSnapshots(out FrameSnapshotDispatcher snapshots)
        {
            snapshots = _snapshotRoutingBinding.Value;
            return snapshots != null;
        }

        internal bool TryGetSnapshotPipeline(out SnapshotPipeline pipeline)
        {
            pipeline = _snapshotPipeline;
            return pipeline != null;
        }

        internal bool TryGetSnapshotCmdHandler(out SnapshotCmdHandler cmdHandler)
        {
            cmdHandler = _cmdHandler;
            return cmdHandler != null;
        }

        internal bool IsSnapshotRoutingBoundTo(
            FrameSnapshotDispatcher snapshots,
            SnapshotPipeline pipeline,
            SnapshotCmdHandler cmdHandler)
        {
            return ReferenceEquals(_snapshotRoutingBinding.Value, snapshots)
                && ReferenceEquals(_snapshotPipeline, pipeline)
                && ReferenceEquals(_cmdHandler, cmdHandler);
        }
    }
}

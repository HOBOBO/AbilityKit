using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionNetAdapterContextHost : INetAdapterContextHost
    {
        private readonly Func<BattleStartPlan> _getPlan;
        private readonly BattleSessionHandles _handles;
        private readonly Func<FrameSnapshotDispatcher> _getSnapshots;

        public SessionNetAdapterContextHost(
            Func<BattleStartPlan> getPlan,
            BattleSessionHandles handles,
            Func<FrameSnapshotDispatcher> getSnapshots)
        {
            _getPlan = getPlan;
            _handles = handles;
            _getSnapshots = getSnapshots;
        }

        public BattleStartPlan Plan => _getPlan();
        public IWorld RemoteDrivenWorld => _handles.RemoteDriven.World;
        public IWorld ConfirmedWorld => _handles.Confirmed.World;

        public IRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenInputSource
        {
            get => _handles.RemoteDriven.InputSource;
            set => _handles.RemoteDriven.InputSource = value;
        }

        public IConsumableRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenConsumable
        {
            get => _handles.RemoteDriven.Consumable;
            set => _handles.RemoteDriven.Consumable = value;
        }

        public IRemoteFrameSink<PlayerInputCommand[]> RemoteDrivenSink
        {
            get => _handles.RemoteDriven.Sink;
            set => _handles.RemoteDriven.Sink = value;
        }

        public IRemoteFrameSource<PlayerInputCommand[]> ConfirmedInputSource
        {
            get => _handles.Confirmed.InputSource;
            set => _handles.Confirmed.InputSource = value;
        }

        public IConsumableRemoteFrameSource<PlayerInputCommand[]> ConfirmedConsumable
        {
            get => _handles.Confirmed.Consumable;
            set => _handles.Confirmed.Consumable = value;
        }

        public IRemoteFrameSink<PlayerInputCommand[]> ConfirmedSink
        {
            get => _handles.Confirmed.Sink;
            set => _handles.Confirmed.Sink = value;
        }

        public FrameSnapshotDispatcher Snapshots => _getSnapshots();
    }
}

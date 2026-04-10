using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Record.Lockstep;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        BattleStartPlan INetAdapterContextHost.Plan => _plan;
        IWorld INetAdapterContextHost.RemoteDrivenWorld => _remoteDrivenWorld;
        IWorld INetAdapterContextHost.ConfirmedWorld => _confirmedWorld;

        IRemoteFrameSource<PlayerInputCommand[]> INetAdapterContextHost.RemoteDrivenInputSource
        {
            get => _remoteDrivenInputSource;
            set => _remoteDrivenInputSource = value;
        }

        IConsumableRemoteFrameSource<PlayerInputCommand[]> INetAdapterContextHost.RemoteDrivenConsumable
        {
            get => _remoteDrivenConsumable;
            set => _remoteDrivenConsumable = value;
        }

        IRemoteFrameSink<PlayerInputCommand[]> INetAdapterContextHost.RemoteDrivenSink
        {
            get => _remoteDrivenSink;
            set => _remoteDrivenSink = value;
        }

        IRemoteFrameSource<PlayerInputCommand[]> INetAdapterContextHost.ConfirmedInputSource
        {
            get => _confirmedInputSource;
            set => _confirmedInputSource = value;
        }

        IConsumableRemoteFrameSource<PlayerInputCommand[]> INetAdapterContextHost.ConfirmedConsumable
        {
            get => _confirmedConsumable;
            set => _confirmedConsumable = value;
        }

        IRemoteFrameSink<PlayerInputCommand[]> INetAdapterContextHost.ConfirmedSink
        {
            get => _confirmedSink;
            set => _confirmedSink = value;
        }

        FrameSnapshotDispatcher INetAdapterContextHost.Snapshots => _snapshots;
    }
}

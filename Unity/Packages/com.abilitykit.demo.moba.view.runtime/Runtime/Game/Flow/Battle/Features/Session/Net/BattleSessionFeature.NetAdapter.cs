using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private interface INetAdapterContextHost
        {
            BattleStartPlan Plan { get; }

            AbilityKit.Ability.World.Abstractions.IWorld RemoteDrivenWorld { get; }
            AbilityKit.Ability.World.Abstractions.IWorld ConfirmedWorld { get; }

            IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenInputSource { get; set; }
            IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenConsumable { get; set; }
            IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenSink { get; set; }

            IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedInputSource { get; set; }
            IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedConsumable { get; set; }
            IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedSink { get; set; }

            FrameSnapshotDispatcher Snapshots { get; }
        }

        private sealed class BattleSessionNetAdapterContext : IBattleSessionNetAdapterContext
        {
            private readonly INetAdapterContextHost _host;

            public BattleSessionNetAdapterContext(INetAdapterContextHost host)
            {
                _host = host;
            }

            public int InputDelayFrames => _host.Plan.InputDelayFrames;

            public AbilityKit.Ability.World.Abstractions.IWorld RemoteDrivenWorld => _host.RemoteDrivenWorld;
            public AbilityKit.Ability.World.Abstractions.IWorld ConfirmedWorld => _host.ConfirmedWorld;

            public IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenInputSource
            {
                get => _host.RemoteDrivenInputSource;
                set => _host.RemoteDrivenInputSource = value;
            }

            public IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenConsumable
            {
                get => _host.RemoteDrivenConsumable;
                set => _host.RemoteDrivenConsumable = value;
            }

            public IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenSink
            {
                get => _host.RemoteDrivenSink;
                set => _host.RemoteDrivenSink = value;
            }

            public IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedInputSource
            {
                get => _host.ConfirmedInputSource;
                set => _host.ConfirmedInputSource = value;
            }

            public IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedConsumable
            {
                get => _host.ConfirmedConsumable;
                set => _host.ConfirmedConsumable = value;
            }

            public IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedSink
            {
                get => _host.ConfirmedSink;
                set => _host.ConfirmedSink = value;
            }

            public FrameSnapshotDispatcher Snapshots => _host.Snapshots;
        }
    }
}

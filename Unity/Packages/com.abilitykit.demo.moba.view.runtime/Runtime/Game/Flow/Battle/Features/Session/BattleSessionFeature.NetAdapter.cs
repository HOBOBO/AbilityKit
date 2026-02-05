using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class BattleSessionNetAdapterContext : IBattleSessionNetAdapterContext
        {
            private readonly BattleSessionFeature _owner;

            public BattleSessionNetAdapterContext(BattleSessionFeature owner)
            {
                _owner = owner;
            }

            public int InputDelayFrames => _owner._plan.InputDelayFrames;

            public AbilityKit.Ability.World.Abstractions.IWorld RemoteDrivenWorld => _owner._remoteDrivenWorld;
            public AbilityKit.Ability.World.Abstractions.IWorld ConfirmedWorld => _owner._confirmedWorld;

            public IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenInputSource
            {
                get => _owner._remoteDrivenInputSource;
                set => _owner._remoteDrivenInputSource = value;
            }

            public IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenConsumable
            {
                get => _owner._remoteDrivenConsumable;
                set => _owner._remoteDrivenConsumable = value;
            }

            public IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenSink
            {
                get => _owner._remoteDrivenSink;
                set => _owner._remoteDrivenSink = value;
            }

            public IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedInputSource
            {
                get => _owner._confirmedInputSource;
                set => _owner._confirmedInputSource = value;
            }

            public IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedConsumable
            {
                get => _owner._confirmedConsumable;
                set => _owner._confirmedConsumable = value;
            }

            public IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedSink
            {
                get => _owner._confirmedSink;
                set => _owner._confirmedSink = value;
            }

            public FrameSnapshotDispatcher Snapshots => _owner._snapshots;
        }
    }
}

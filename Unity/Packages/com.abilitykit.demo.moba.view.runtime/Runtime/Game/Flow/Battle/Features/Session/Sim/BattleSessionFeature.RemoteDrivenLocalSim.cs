using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private const int MaxRemoteDrivenCatchUpStepsPerUpdate = 5;

        private AbilityKit.Ability.World.Management.IWorldManager _remoteDrivenWorlds
        {
            get => _handles.RemoteDriven.Worlds;
            set => _handles.RemoteDriven.Worlds = value;
        }

        private AbilityKit.Ability.Host.Framework.HostRuntime _remoteDrivenRuntime
        {
            get => _handles.RemoteDriven.Runtime;
            set => _handles.RemoteDriven.Runtime = value;
        }

        private AbilityKit.Ability.World.Abstractions.IWorld _remoteDrivenWorld
        {
            get => _handles.RemoteDriven.World;
            set => _handles.RemoteDriven.World = value;
        }

        private IRemoteFrameSource<PlayerInputCommand[]> _remoteDrivenInputSource
        {
            get => _handles.RemoteDriven.InputSource;
            set => _handles.RemoteDriven.InputSource = value;
        }

        private IConsumableRemoteFrameSource<PlayerInputCommand[]> _remoteDrivenConsumable
        {
            get => _handles.RemoteDriven.Consumable;
            set => _handles.RemoteDriven.Consumable = value;
        }

        private IRemoteFrameSink<PlayerInputCommand[]> _remoteDrivenSink
        {
            get => _handles.RemoteDriven.Sink;
            set => _handles.RemoteDriven.Sink = value;
        }

        private void StartRemoteDrivenLocalWorld()
        {
            if (_remoteDrivenWorld != null) return;

            CreateRemoteDrivenRuntimeAndWorld();
            SetupRemoteDrivenInputAndDebugStats();
        }
    }
}

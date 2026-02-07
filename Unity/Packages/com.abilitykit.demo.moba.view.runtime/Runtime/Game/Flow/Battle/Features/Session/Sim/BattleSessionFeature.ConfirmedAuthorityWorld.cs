using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private AbilityKit.Ability.World.Management.IWorldManager _confirmedWorlds
        {
            get => _handles.Confirmed.Worlds;
            set => _handles.Confirmed.Worlds = value;
        }

        private AbilityKit.Ability.Host.Framework.HostRuntime _confirmedRuntime
        {
            get => _handles.Confirmed.Runtime;
            set => _handles.Confirmed.Runtime = value;
        }

        private AbilityKit.Ability.World.Abstractions.IWorld _confirmedWorld
        {
            get => _handles.Confirmed.World;
            set => _handles.Confirmed.World = value;
        }

        private IRemoteFrameSource<PlayerInputCommand[]> _confirmedInputSource
        {
            get => _handles.Confirmed.InputSource;
            set => _handles.Confirmed.InputSource = value;
        }

        private IConsumableRemoteFrameSource<PlayerInputCommand[]> _confirmedConsumable
        {
            get => _handles.Confirmed.Consumable;
            set => _handles.Confirmed.Consumable = value;
        }

        private IRemoteFrameSink<PlayerInputCommand[]> _confirmedSink
        {
            get => _handles.Confirmed.Sink;
            set => _handles.Confirmed.Sink = value;
        }

        private FrameSnapshotDispatcher _confirmedSnapshots
        {
            get => _handles.Confirmed.Snapshots;
            set => _handles.Confirmed.Snapshots = value;
        }

        private DebugBattleViewEventSink _confirmedViewEventSink
        {
            get => _handles.Confirmed.ViewEventSink;
            set => _handles.Confirmed.ViewEventSink = value;
        }

        private BattleSnapshotViewAdapter _confirmedSnapshotViewAdapter
        {
            get => _handles.Confirmed.SnapshotViewAdapter;
            set => _handles.Confirmed.SnapshotViewAdapter = value;
        }

        private BattleTriggerEventViewBridge _confirmedTriggerBridge
        {
            get => _handles.Confirmed.TriggerBridge;
            set => _handles.Confirmed.TriggerBridge = value;
        }

        private BattleContext _confirmedViewCtx
        {
            get => _handles.Confirmed.ViewCtx;
            set => _handles.Confirmed.ViewCtx = value;
        }

        private FrameSnapshotDispatcher _confirmedViewSnapshots
        {
            get => _handles.Confirmed.ViewSnapshots;
            set => _handles.Confirmed.ViewSnapshots = value;
        }

        private SnapshotPipeline _confirmedViewPipeline
        {
            get => _handles.Confirmed.ViewPipeline;
            set => _handles.Confirmed.ViewPipeline = value;
        }

        private SnapshotCmdHandler _confirmedViewCmdHandler
        {
            get => _handles.Confirmed.ViewCmdHandler;
            set => _handles.Confirmed.ViewCmdHandler = value;
        }

        private ConfirmedBattleViewFeature _confirmedViewFeature
        {
            get => _handles.Confirmed.ViewFeature;
            set => _handles.Confirmed.ViewFeature = value;
        }

        private IDisposable _confirmedViewSubLobby
        {
            get => _handles.Confirmed.ViewSubLobby;
            set => _handles.Confirmed.ViewSubLobby = value;
        }

        private IDisposable _confirmedViewSubActorTransform
        {
            get => _handles.Confirmed.ViewSubActorTransform;
            set => _handles.Confirmed.ViewSubActorTransform = value;
        }

        private IDisposable _confirmedViewSubStateHash
        {
            get => _handles.Confirmed.ViewSubStateHash;
            set => _handles.Confirmed.ViewSubStateHash = value;
        }

        private void StartConfirmedAuthorityWorld()
        {
            if (_confirmedWorld != null) return;

            CreateConfirmedAuthorityRuntimeAndWorld(out var authWorldId);
            SetupConfirmedAuthorityInputAndBootstrap(authWorldId);

            EnsureConfirmedAuthorityViewEventPipeline();

            // Build a dedicated view context for confirmed authority world and attach an extra view feature.
            // This context owns its own EC.EntityWorld and view binder mappings, isolated from the main battle context.
            EnsureConfirmedAuthorityViewSide(authWorldId);
        }
    }
}

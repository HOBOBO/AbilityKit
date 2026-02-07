using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureConfirmedAuthorityViewEventPipeline()
        {
            // Build an independent snapshot/view-event pipeline for confirmed authority world.
            if (_session == null) return;

            _confirmedSnapshots = new FrameSnapshotDispatcher();
            // Debug-only: register decoders so BattleSnapshotViewAdapter can decode payloads.
            // Do NOT register cmd handlers here to avoid applying snapshots to the main battle entity world.
            AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(
                dispatcherDecoders: _confirmedSnapshots,
                pipelineDecoders: _confirmedSnapshots,
                pipeline: new NullSnapshotPipelineStageRegistry(),
                cmd: new NullSnapshotCmdHandlerRegistry());

            AbilityKit.Game.Flow.Snapshot.LobbySnapshotRegistry.RegisterAll(
                dispatcherDecoders: _confirmedSnapshots,
                pipelineDecoders: _confirmedSnapshots,
                pipeline: new NullSnapshotPipelineStageRegistry(),
                cmd: new NullSnapshotCmdHandlerRegistry());

            AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(
                dispatcherDecoders: _confirmedSnapshots,
                pipelineDecoders: _confirmedSnapshots,
                pipeline: new NullSnapshotPipelineStageRegistry(),
                cmd: new NullSnapshotCmdHandlerRegistry());

            _confirmedViewEventSink = new DebugBattleViewEventSink(maxLines: 32);

            var mode = _plan.ViewEventSourceMode;
            if (mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid)
            {
                _confirmedSnapshotViewAdapter = new BattleSnapshotViewAdapter(_confirmedSnapshots, _confirmedViewEventSink);
            }

            if (mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid)
            {
                if (_confirmedWorld?.Services != null && _confirmedWorld.Services.TryResolve(out AbilityKit.Ability.Triggering.IEventBus bus) && bus != null)
                {
                    _confirmedTriggerBridge = new BattleTriggerEventViewBridge(bus, _confirmedViewEventSink);
                }
            }
        }
    }
}

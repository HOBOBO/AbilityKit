using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class EventAdaptersSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._snapshotAdapter?.Dispose();
                f._snapshotAdapter = null;

                f._triggerAdapter?.Dispose();
                f._triggerAdapter = null;

                var mode = f._ctx != null ? f._ctx.Plan.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly;

                if ((mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid) && f._ctx?.Session != null)
                {
                    f._triggerAdapter = new BattleTriggerEventViewAdapter(f._ctx.Session, f._eventSink);
                }

                if ((mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid) && f._ctx?.FrameSnapshots != null)
                {
                    f._snapshotAdapter = new BattleSnapshotViewAdapter(f._ctx.FrameSnapshots, f._eventSink);
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._snapshotAdapter?.Dispose();
                f._snapshotAdapter = null;

                f._triggerAdapter?.Dispose();
                f._triggerAdapter = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class EventAdaptersSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._snapshotAdapter?.Dispose();
                f._snapshotAdapter = null;

                f._triggerAdapter?.Dispose();
                f._triggerAdapter = null;

                var mode = f._confirmedCtx != null ? f._confirmedCtx.Plan.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly;

                if ((mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid) && f._confirmedCtx?.Session != null)
                {
                    f._triggerAdapter = new BattleTriggerEventViewAdapter(f._confirmedCtx.Session, f._eventSink);
                }

                if ((mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid) && f._confirmedCtx?.FrameSnapshots != null)
                {
                    f._snapshotAdapter = new BattleSnapshotViewAdapter(f._confirmedCtx.FrameSnapshots, f._eventSink);
                }
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._snapshotAdapter?.Dispose();
                f._snapshotAdapter = null;

                f._triggerAdapter?.Dispose();
                f._triggerAdapter = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}

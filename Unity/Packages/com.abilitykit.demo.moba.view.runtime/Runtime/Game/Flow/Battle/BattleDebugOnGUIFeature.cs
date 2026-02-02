using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleContext _ctx;

        private Vector2 _scroll;

        private bool _showFrameSyncStats;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            BattleFlowDebugProvider.Current = _ctx;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ReferenceEquals(BattleFlowDebugProvider.Current, _ctx))
            {
                BattleFlowDebugProvider.Current = null;
            }
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!ctx.Entry.DebugEnabled) return;

            var w = 520f;
            var h = Mathf.Clamp(Screen.height - 160f, 180f, 520f);
            GUILayout.BeginArea(new Rect(10, 140, w, h), GUI.skin.window);
            GUILayout.Label("Battle Debug");

            _scroll = GUILayout.BeginScrollView(_scroll, alwaysShowHorizontal: false, alwaysShowVertical: true);

            if (_ctx == null || _ctx.Session == null)
            {
                GUILayout.Label("Session: null");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"WorldId: {_ctx.Plan.WorldId}");
            GUILayout.Label($"LastFrame: {_ctx.LastFrame}");

            GUILayout.Label($"RuntimeWorldId: {(_ctx.HasRuntimeWorldId ? _ctx.RuntimeWorldId.ToString() : "(none)")}");

            if (GUILayout.Button(_showFrameSyncStats ? "Hide FrameSync Stats" : "Show FrameSync Stats"))
            {
                _showFrameSyncStats = !_showFrameSyncStats;
            }

            if (_ctx.PredictionReconcileControl != null)
            {
                var wid = _ctx.HasRuntimeWorldId ? _ctx.RuntimeWorldId : new WorldId(_ctx.Plan.WorldId);
                if (_ctx.PredictionReconcileControl.TryGetReconcileEnabled(wid, out var recEnabled))
                {
                    GUILayout.Label($"ReconcileSwitch: {recEnabled}");
                }

                if (GUILayout.Button("Disable Reconcile"))
                {
                    _ctx.PredictionReconcileControl.SetReconcileEnabled(wid, false);

                    if (_ctx.HasRuntimeWorldId)
                    {
                        _ctx.PredictionReconcileControl.ResetReconcile(_ctx.RuntimeWorldId);
                    }

                    _ctx.PredictionReconcileControl.ResetReconcile(new WorldId(_ctx.Plan.WorldId));
                }

                if (GUILayout.Button("Enable Reconcile"))
                {
                    _ctx.PredictionReconcileControl.SetReconcileEnabled(wid, true);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label($"DebugForceHashMismatch: {BattleSessionFeature.DebugForceClientHashMismatch}");
            if (GUILayout.Button("Toggle ForceHashMismatch"))
            {
                BattleSessionFeature.DebugForceClientHashMismatch = !BattleSessionFeature.DebugForceClientHashMismatch;

                if (_ctx != null && _ctx.PredictionReconcileControl != null)
                {
                    if (_ctx.HasRuntimeWorldId)
                    {
                        _ctx.PredictionReconcileControl.ResetReconcile(_ctx.RuntimeWorldId);
                    }

                    _ctx.PredictionReconcileControl.ResetReconcile(new WorldId(_ctx.Plan.WorldId));
                }
            }
#endif

            GUILayout.Label($"ReconcileTarget: {(_ctx.PredictionReconcileTarget != null ? "set" : "null")}");

            if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hashComp) && hashComp != null)
            {
                GUILayout.Label($"StateHashSnap: frame={hashComp.Frame} hash={hashComp.Hash} ver={hashComp.Version}");
            }
            else
            {
                GUILayout.Label("StateHashSnap: none");
            }

            if (_showFrameSyncStats && _ctx.PredictionStats != null)
            {
                var wid = new WorldId(_ctx.Plan.WorldId);

                if (_ctx.PredictionStats.TryGetReconcileEnabled(wid, out var reconcileEnabled))
                {
                    GUILayout.Label($"ReconcileEnabled: {reconcileEnabled}");
                }

                if (_ctx.PredictionStats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    GUILayout.Label($"Pred: confirmed={confirmed.Value} predicted={predicted.Value}");
                }
                else
                {
                    GUILayout.Label("Pred: no world context");
                }

                GUILayout.Label($"Pred: inputDelay={_ctx.PredictionStats.InputDelayFrames}");
                GUILayout.Label($"Pred: lastPred={_ctx.PredictionStats.LastConsumedPredictedFrames} lastConf={_ctx.PredictionStats.LastConsumedConfirmedFrames}");
                GUILayout.Label($"Pred: drops={_ctx.PredictionStats.TotalLocalDelayQueueDroppedBatches} totalPred={_ctx.PredictionStats.TotalPredictedFrames} totalConf={_ctx.PredictionStats.TotalConsumedConfirmedFrames}");
                GUILayout.Label($"Rollback: replaying={_ctx.PredictionStats.IsReplaying} replayTo={_ctx.PredictionStats.ReplayToFrame.Value} lastRb={_ctx.PredictionStats.LastRollbackFrame.Value}");
                GUILayout.Label($"Rollback: total={_ctx.PredictionStats.TotalRollbackCount} restoreFail={_ctx.PredictionStats.TotalRollbackRestoreFailed}");
                GUILayout.Label($"Reconcile: mismatch={_ctx.PredictionStats.TotalReconcileMismatch} lastFrame={_ctx.PredictionStats.LastReconcileMismatchFrame.Value}");
                GUILayout.Label($"Reconcile: predHash={_ctx.PredictionStats.LastReconcilePredictedHash.Value} authHash={_ctx.PredictionStats.LastReconcileAuthoritativeHash.Value}");
                GUILayout.Label($"Reconcile: comparedFrame={_ctx.PredictionStats.LastReconcileComparedFrame.Value} predRecorded={_ctx.PredictionStats.TotalPredictedHashRecorded} skipNoPred={_ctx.PredictionStats.TotalAuthoritativeHashSkippedNoPredictedHash}");
                GUILayout.Label($"HashRx: total={_ctx.PredictionStats.TotalAuthoritativeHashReceived} lastFrame={_ctx.PredictionStats.LastAuthoritativeHashFrame.Value} lastHash={_ctx.PredictionStats.LastAuthoritativeHash.Value}");
                GUILayout.Label($"HashRx: ignoredNoReconciler={_ctx.PredictionStats.TotalAuthoritativeHashIgnoredNoReconciler}");

                if (_ctx.PredictionStats.TryGetLocalDelayQueueDepth(wid, out var depth))
                {
                    GUILayout.Label($"Pred: delayQueueDepth={depth}");
                }
            }

            if (_showFrameSyncStats && BattleFlowDebugProvider.ConfirmedAuthorityWorldStats != null)
            {
                var s = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;
                GUILayout.Label($"权威对照世界: worldId={s.WorldId}");
                GUILayout.Label($"权威对照世界: confirmed={s.ConfirmedFrame} predicted={s.PredictedFrame}");
                GUILayout.Label($"权威对照世界: inputTarget={s.AuthorityInputTargetFrame} driveTarget={s.AuthorityDriveTargetFrame} ticked={s.AuthorityLastTickedFrame}");

                GUILayout.Label($"权威对照世界: ViewEventTotal={s.ViewEventTotal}");
                if (s.RecentViewEvents != null)
                {
                    for (int i = 0; i < s.RecentViewEvents.Length; i++)
                    {
                        GUILayout.Label($"  {s.RecentViewEvents[i]}");
                    }
                }
            }

            var isGatewayRemote = _ctx.Plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _ctx.Plan.UseGatewayTransport;
            if (isGatewayRemote)
            {
                GUILayout.Label("GatewayRemote: Join/Leave/CreateWorld are not wired.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect")) _ctx.Session.Connect();
            if (GUILayout.Button("Disconnect")) _ctx.Session.Disconnect();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !isGatewayRemote;
            if (GUILayout.Button("Join"))
            {
                _ctx.Session.Join(new JoinWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_ctx.Plan.WorldId), new PlayerId(_ctx.Plan.PlayerId)));
            }
            if (GUILayout.Button("Leave"))
            {
                _ctx.Session.Leave(new LeaveWorldRequest(new AbilityKit.Ability.World.Abstractions.WorldId(_ctx.Plan.WorldId), new PlayerId(_ctx.Plan.PlayerId)));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Exit Battle", GUILayout.Height(26)))
            {
                var flow = ctx.Entry.Get<GameFlowDomain>();
                flow.ReturnToBoot();
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}

using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugOverviewPanel : IBattleDebugPanel
    {
        public string Name => "总览";
        public int Order => 0;

        private readonly BattleDebugDiagnosticOverviewViewModel _viewModel =
            new BattleDebugDiagnosticOverviewViewModel();

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含 Battle Diagnostics 的 Artifact。",
                    MessageType.Info);
                return;
            }

            DrawHealth(in ctx, session);
            DrawAnomalyEntry(in ctx);
            EditorGUILayout.Space(8);

            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox(
                    "选择 Actor 后可在此继续查看状态、标签、Effect 和最近活动。",
                    MessageType.Info);
                return;
            }

            const BattleDiagnosticCapabilities requiredCapabilities =
                BattleDiagnosticCapabilities.ActorState |
                BattleDiagnosticCapabilities.ActorTags |
                BattleDiagnosticCapabilities.ActorEffects;
            if ((session.SessionInfo.Capabilities & requiredCapabilities) != requiredCapabilities)
            {
                EditorGUILayout.HelpBox(
                    "当前诊断会话不支持 Actor、标签或 Effect 总览查询。",
                    MessageType.Info);
                return;
            }

            var actorId = ctx.SelectedId.ActorId;
            _viewModel.RefreshIfNeeded(session, actorId);

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            EditorGUILayout.LabelField("实体", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", actorId.ToString());
            if (_viewModel.Actor.HasValue)
            {
                var actor = _viewModel.Actor.Value;
                EditorGUILayout.LabelField("类型", actor.Kind.ToString());
                EditorGUILayout.LabelField("名称", actor.DisplayName);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("汇总", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("标签数", _viewModel.TagCount.ToString());
            EditorGUILayout.LabelField("效果数", _viewModel.EffectCount.ToString());
            EditorGUILayout.LabelField(
                $"State={_viewModel.StateStoreRevision} Tag={_viewModel.TagStoreRevision} " +
                $"Effect={_viewModel.EffectStoreRevision}",
                EditorStyles.miniLabel);

            DrawRecentActivity(in ctx);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制 ID", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = actorId.ToString();
            }

            if (GUILayout.Button("复制标签", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = _viewModel.BuildTagList();
            }

            if (GUILayout.Button("刷新", GUILayout.Width(100)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHealth(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.LabelField("诊断健康", EditorStyles.boldLabel);
            var resolution = ctx.DiagnosticResolution;
            if (!resolution.HasHealthSnapshot)
            {
                EditorGUILayout.HelpBox(
                    "会话已连接，但当前数据源未提供 Health 快照。查询功能仍可使用。",
                    MessageType.Info);
                EditorGUILayout.LabelField(
                    $"Source={(ctx.IsOffline ? "Artifact" : "Live")}  " +
                    $"E{session.EventStoreRevision} S{session.StateStoreRevision} T{session.TraceStoreRevision}",
                    EditorStyles.miniLabel);
                return;
            }

            var health = resolution.HealthSnapshot.Value;
            var metrics = health.EventStoreMetrics;
            EditorGUILayout.LabelField(
                "来源 / 会话",
                $"{(ctx.IsOffline ? "Artifact" : "Live")} / {health.SessionInfo.Scope}");
            EditorGUILayout.LabelField(
                "轨道 Revision",
                $"Event {health.EventStoreRevision} / State {health.StateStoreRevision} / Trace {health.TraceStoreRevision}");
            EditorGUILayout.LabelField(
                "进度",
                $"完整帧 {health.LastSuccessfulStateFrame} / Event #{health.LastEventSequence}");
            EditorGUILayout.LabelField(
                "捕获",
                $"Channels={health.EnabledChannels} / Frozen={health.IsFrozen}");
            EditorGUILayout.LabelField(
                "Event Store",
                $"{metrics.Count}/{metrics.Capacity} / Accepted={metrics.AcceptedCount} / " +
                $"Evicted={metrics.EvictedCount} / Rejected={metrics.RejectedCount}");

            if (!health.HasProducedState)
            {
                EditorGUILayout.HelpBox("State 轨道尚未产生完整快照。", MessageType.Info);
            }
            if (!health.HasProducedEvents)
            {
                EditorGUILayout.HelpBox("Event 轨道尚未产生事件。", MessageType.Info);
            }
            if (health.HasErrors)
            {
                var message = string.Empty;
                if (!string.IsNullOrEmpty(health.LastStateSampleError))
                {
                    message += $"State [{health.StateSampleFailureCount}] {health.LastStateSampleError}";
                }
                if (!string.IsNullOrEmpty(health.LastEventCollectError))
                {
                    if (!string.IsNullOrEmpty(message)) message += "\n";
                    message += $"Event [{health.EventCollectFailureCount}] {health.LastEventCollectError}";
                }
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }
        }

        private static void DrawAnomalyEntry(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("问题入口", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(ctx.OpenRecentFailures == null);
            if (GUILayout.Button("最近失败", GUILayout.Width(100)))
            {
                ctx.OpenRecentFailures?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(ctx.OpenEvents == null);
            if (GUILayout.Button("全部事件", GUILayout.Width(100)))
            {
                ctx.OpenEvents?.Invoke(0L);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecentActivity(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("最近活动", EditorStyles.boldLabel);
            if (!_viewModel.RecentEvent.HasValue)
            {
                EditorGUILayout.LabelField("当前 Actor 暂无可查询的诊断事件。", EditorStyles.miniLabel);
            }
            else
            {
                var evt = _viewModel.RecentEvent.Value;
                EditorGUILayout.LabelField(
                    $"#{evt.Sequence}  F{evt.Frame}  {evt.Kind}  {evt.Outcome}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(evt.Summary, EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(evt.RootContextId <= 0 || ctx.OpenTrace == null);
                if (GUILayout.Button("打开最近 Trace", GUILayout.Width(110)))
                {
                    ctx.OpenTrace?.Invoke(evt.RootContextId, evt.ContextId);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.BeginDisabledGroup(ctx.OpenEvents == null);
            if (GUILayout.Button("查看该 Actor 的全部事件", GUILayout.Width(180)))
            {
                ctx.OpenEvents?.Invoke(ctx.SelectedId.ActorId);
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}

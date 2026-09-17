using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(BattleDebugModuleIds.DiagnosticHealth, "调查",
        Selections = BattleDebugModuleSelectionSupport.None)]
    internal sealed class BattleDebugDiagnosticsHealthPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "诊断健康与耗时";
        public int Order => 395;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => false;

        public bool IsVisible(in BattleDebugContext ctx) =>
            BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out _);

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session)) return;
            EditorGUILayout.LabelField("诊断采集健康", EditorStyles.boldLabel);
            var health = ctx.DiagnosticResolution.HealthSnapshot;
            if (health.HasValue)
            {
                var value = health.Value;
                EditorGUILayout.LabelField("事件", $"{value.EventStoreMetrics.Count}/{value.EventStoreMetrics.Capacity}  rejected={value.EventStoreMetrics.RejectedCount}  evicted={value.EventStoreMetrics.EvictedCount}");
                EditorGUILayout.LabelField("完整状态帧", value.LastSuccessfulStateFrame.ToString());
                EditorGUILayout.LabelField("采集失败", $"state={value.StateSampleFailureCount}  events={value.EventCollectFailureCount}");
                EditorGUILayout.LabelField("采集通道", BattleDebugDisplayText.EventChannel(value.EnabledChannels));
            }
            else
            {
                EditorGUILayout.LabelField("事件版本", session.EventStoreRevision.ToString());
                EditorGUILayout.HelpBox("当前数据源没有采集健康快照。", MessageType.Info);
            }

            if (ctx.IsOffline)
            {
                EditorGUILayout.HelpBox("离线 Artifact 没有实时耗时与输入聚合数据。", MessageType.Info);
                return;
            }

            if (ctx.Facade == null || !ctx.Facade.TryGetSession(out var logicSession) ||
                !logicSession.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve(out IMobaBattleDiagnosticsService diagnostics) || diagnostics == null)
            {
                EditorGUILayout.HelpBox("实时诊断统计未就绪。", MessageType.Info);
                return;
            }

            var snapshot = diagnostics.GetSnapshot();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("输入聚合", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("命令", $"accepted={snapshot.Input.AcceptedCommands}  handled={snapshot.Input.HandledCommands}  rejected={snapshot.Input.RejectedCommands}  exceptions={snapshot.Input.CommandExceptions}");
            EditorGUILayout.LabelField("警告 / 异常", $"{snapshot.Warnings?.Count ?? 0} / {snapshot.Exceptions?.Count ?? 0}");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("耗时 (ms)", EditorStyles.boldLabel);
            var durations = snapshot.Profiler.Durations;
            DrawDuration(durations, MobaBattleDiagnosticMetric.ContinuousTick);
            DrawDuration(durations, MobaBattleDiagnosticMetric.DamagePipeline);
            DrawDuration(durations, MobaBattleDiagnosticMetric.DamageStage);
            DrawDuration(durations, MobaBattleDiagnosticMetric.SkillPipelineStep);
            DrawDuration(durations, MobaBattleDiagnosticMetric.SkillRunnerStep);
            DrawDuration(durations, MobaBattleDiagnosticMetric.EffectsStep);
            if (durations == null || durations.Count == 0)
                EditorGUILayout.HelpBox("当前采集模式尚未产生耗时样本。", MessageType.Info);
        }

        private static void DrawDuration(
            System.Collections.Generic.IReadOnlyDictionary<string, AbilityKit.Diagnostics.DurationSummaryRecord> durations,
            string key)
        {
            if (durations == null || !durations.TryGetValue(key, out var record)) return;
            EditorGUILayout.LabelField(key,
                $"n={record.Count}  mean={record.MeanMilliseconds:0.###}  max={record.MaxMilliseconds:0.###}");
        }
    }

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
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
                    MessageType.Info);
                return;
            }

            DrawHealth(in ctx, session);
            DrawAnomalyEntry(in ctx);
            EditorGUILayout.Space(8);

            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox(
                    "选择 Actor 后可在此继续查看状态、标签、效果和最近活动。",
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
                    "当前诊断会话不支持 Actor、标签或效果总览查询。",
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
                EditorGUILayout.LabelField("类型", BattleDebugDisplayText.ActorKind(actor.Kind));
                EditorGUILayout.LabelField("名称", actor.DisplayName);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("汇总", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("标签数", _viewModel.TagCount.ToString());
            EditorGUILayout.LabelField("效果数", _viewModel.EffectCount.ToString());
            EditorGUILayout.LabelField(
                $"状态版本={_viewModel.StateStoreRevision} 标签版本={_viewModel.TagStoreRevision} " +
                $"效果版本={_viewModel.EffectStoreRevision}",
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
                    "会话已连接，但当前数据源未提供健康快照。查询功能仍可使用。",
                    MessageType.Info);
                EditorGUILayout.LabelField(
                    $"来源={(ctx.IsOffline ? "离线 Artifact" : "实时会话")}  " +
                    $"事件版本={session.EventStoreRevision} 状态版本={session.StateStoreRevision} Trace 版本={session.TraceStoreRevision}",
                    EditorStyles.miniLabel);
                return;
            }

            var health = resolution.HealthSnapshot.Value;
            var metrics = health.EventStoreMetrics;
            EditorGUILayout.LabelField(
                "来源 / 会话",
                $"{(ctx.IsOffline ? "离线 Artifact" : "实时会话")} / {health.SessionInfo.Scope}");
            EditorGUILayout.LabelField(
                "轨道版本",
                $"事件 {health.EventStoreRevision} / 状态 {health.StateStoreRevision} / Trace {health.TraceStoreRevision}");
            EditorGUILayout.LabelField(
                "进度",
                $"完整帧 {health.LastSuccessfulStateFrame} / 事件 #{health.LastEventSequence}");
            EditorGUILayout.LabelField(
                "捕获",
                $"通道={BattleDebugDisplayText.EventChannel(health.EnabledChannels)} / 已冻结={BattleDebugDisplayText.Bool(health.IsFrozen)}");
            EditorGUILayout.LabelField(
                "事件仓库",
                $"{metrics.Count}/{metrics.Capacity} / 已接收={metrics.AcceptedCount} / " +
                $"已淘汰={metrics.EvictedCount} / 已拒绝={metrics.RejectedCount}");

            if (!health.HasProducedState)
            {
                EditorGUILayout.HelpBox("状态轨道尚未产生完整快照。", MessageType.Info);
            }
            if (!health.HasProducedEvents)
            {
                EditorGUILayout.HelpBox("事件轨道尚未产生事件。", MessageType.Info);
            }
            if (health.HasErrors)
            {
                var message = string.Empty;
                if (!string.IsNullOrEmpty(health.LastStateSampleError))
                {
                    message += $"状态 [{health.StateSampleFailureCount}] {health.LastStateSampleError}";
                }
                if (!string.IsNullOrEmpty(health.LastEventCollectError))
                {
                    if (!string.IsNullOrEmpty(message)) message += "\n";
                    message += $"事件 [{health.EventCollectFailureCount}] {health.LastEventCollectError}";
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
                    $"#{evt.Sequence}  F{evt.Frame}  {BattleDebugDisplayText.EventKind(evt.Kind)}  {BattleDebugDisplayText.EventOutcome(evt.Outcome)}",
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

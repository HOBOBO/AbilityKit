using AbilityKit.Demo.Moba.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugSelectionInspector
    {
        private readonly BattleDebugSelectionInspectorViewModel _viewModel =
            new BattleDebugSelectionInspectorViewModel();
        private Vector2 _scroll;

        public void InvalidateCache()
        {
            _viewModel.InvalidateCache();
        }

        public void SelectConfig(
            in BattleDebugConfigReference reference,
            in BattleDiagnosticSelection sourceSelection)
        {
            _viewModel.SelectConfig(in reference, in sourceSelection);
        }

        public void Draw(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Selection Inspector", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("↻", "重新查询当前稳定选择"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
            {
                _viewModel.InvalidateCache();
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawContent(in ctx);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawContent(in BattleDebugContext ctx)
        {
            var workspaceState = ctx.WorkspaceState;
            var selection = workspaceState?.Selection ?? default;
            if (_viewModel.RefreshConfigIfActive(in selection))
            {
                DrawConfig(in ctx);
                return;
            }

            DrawSelectionIdentity(in selection);
            if (ctx.DiagnosticSession == null)
            {
                EditorGUILayout.HelpBox("当前没有可查询的诊断会话。", MessageType.Info);
                return;
            }

            _viewModel.RefreshIfNeeded(ctx.DiagnosticSession, in selection);
            if (_viewModel.Actor.HasValue)
            {
                DrawActor(in ctx, _viewModel.Actor.Value);
                return;
            }

            if (_viewModel.Event.HasValue)
            {
                DrawEvent(
                    in ctx,
                    _viewModel.Event.Value,
                    _viewModel.SourceActorObject,
                    _viewModel.TargetActorObject,
                    _viewModel.SubjectObject);
                return;
            }

            if (_viewModel.TraceNode.HasValue)
            {
                DrawTraceNode(in ctx, _viewModel.TraceNode.Value);
                return;
            }

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(
                    _viewModel.StatusMessage,
                    ResolveMessageType(_viewModel.QueryStatus));
            }
        }

        private static void DrawSelectionIdentity(in BattleDiagnosticSelection selection)
        {
            if (!selection.IsValid)
            {
                EditorGUILayout.LabelField("选择", "无", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField("类型 / ID", $"{selection.Kind} / {selection.Id}");
            EditorGUILayout.LabelField(
                "选择帧",
                BattleDiagnosticFrames.IsValid(selection.Frame)
                    ? selection.Frame.ToString()
                    : "未指定");
            if (selection.RelatedId != 0)
            {
                EditorGUILayout.LabelField("关联 ID", selection.RelatedId.ToString());
            }
            EditorGUILayout.Space(4f);
        }

        private static void DrawActor(
            in BattleDebugContext ctx,
            in BattleDiagnosticActorSummary actor)
        {
            EditorGUILayout.LabelField("名称", string.IsNullOrEmpty(actor.DisplayName) ? "（未命名）" : actor.DisplayName);
            EditorGUILayout.LabelField("类型 / 队伍", $"{actor.Kind} / {actor.TeamId}");
            EditorGUILayout.LabelField("Config", actor.ConfigId.ToString());
            EditorGUILayout.LabelField("帧", actor.Frame.ToString());
            EditorGUILayout.LabelField("生命", $"{actor.Health:0.##} / {actor.MaximumHealth:0.##}");
            EditorGUILayout.LabelField("存活", actor.IsAlive ? "是" : "否");
            EditorGUILayout.LabelField(
                "位置",
                $"({actor.PositionX:0.##}, {actor.PositionY:0.##}, {actor.PositionZ:0.##})");

            EditorGUI.BeginDisabledGroup(ctx.SelectActor == null);
            if (GUILayout.Button("在 Actor 工作区定位"))
            {
                ctx.SelectActor?.Invoke(actor.ActorId);
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawEvent(
            in BattleDebugContext ctx,
            in BattleDiagnosticEvent diagnosticEvent,
            BattleDiagnosticRuntimeObject? sourceActor,
            BattleDiagnosticRuntimeObject? targetActor,
            BattleDiagnosticRuntimeObject? subjectObject)
        {
            EditorGUILayout.LabelField("序列 / 帧", $"{diagnosticEvent.Sequence} / {diagnosticEvent.Frame}");
            EditorGUILayout.LabelField(
                "类型 / 通道 / 结果",
                $"{diagnosticEvent.Kind} / {diagnosticEvent.Channel} / {diagnosticEvent.Outcome}");
            EditorGUILayout.LabelField("Actor", $"{diagnosticEvent.SourceActorId} -> {diagnosticEvent.TargetActorId}");
            EditorGUILayout.LabelField(
                "Source object",
                FormatRuntimeObject(diagnosticEvent.SourceActor, sourceActor));
            EditorGUILayout.LabelField(
                "Target object",
                FormatRuntimeObject(diagnosticEvent.TargetActor, targetActor));
            if (diagnosticEvent.SubjectObject.HasRuntimeId)
            {
                EditorGUILayout.LabelField(
                    "Subject object",
                    FormatRuntimeObject(diagnosticEvent.SubjectObject, subjectObject));
            }
            EditorGUILayout.LabelField("Root / Context", $"{diagnosticEvent.RootContextId} / {diagnosticEvent.ContextId}");
            EditorGUILayout.LabelField("Config / Attack", $"{diagnosticEvent.ConfigId} / {diagnosticEvent.AttackId}");
            EditorGUILayout.LabelField("Skill Runtime", diagnosticEvent.SkillRuntime.ToString());
            EditorGUILayout.LabelField("摘要", diagnosticEvent.Summary);

            EditorGUILayout.BeginHorizontal();
            DrawActorButton("来源", diagnosticEvent.SourceActorId, in ctx);
            DrawActorButton("目标", diagnosticEvent.TargetActorId, in ctx);
            EditorGUILayout.EndHorizontal();

            var hasConfig = BattleDebugConfigReferenceMapper.TryFromEvent(
                in diagnosticEvent,
                out var configReference);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!hasConfig || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置"))
            {
                ctx.OpenConfig?.Invoke(configReference);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(diagnosticEvent.RootContextId == 0 || ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace"))
            {
                ctx.OpenTrace?.Invoke(diagnosticEvent.RootContextId, diagnosticEvent.ContextId);
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("复制"))
            {
                EditorGUIUtility.systemCopyBuffer =
                    BattleDebugDiagnosticEventsPanel.BuildClipboardText(in diagnosticEvent);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string FormatRuntimeObject(
            in BattleDiagnosticRuntimeObjectReference reference,
            BattleDiagnosticRuntimeObject? runtimeObject)
        {
            if (!reference.HasRuntimeId) return "-";
            if (!runtimeObject.HasValue) return reference.ToString();

            var value = runtimeObject.Value;
            var label = string.IsNullOrEmpty(value.DisplayName)
                ? value.DefinitionKind + " " + value.DefinitionId
                : value.DisplayName;
            var provenance = string.Empty;
            if (value.DiscoveryKind == BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill)
            {
                provenance = " [backfilled @ " + value.BackfilledFrame + "; earlier lifetime unknown]";
            }
            else if (value.DiscoveryKind == BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly)
            {
                provenance = " [end observed; earlier lifetime unknown]";
            }
            if (value.Completeness != BattleDiagnosticDataCompleteness.Complete)
            {
                provenance += " [" + value.Completeness + "]";
            }
            return label + " (" + reference + ")" + provenance;
        }

        private static void DrawTraceNode(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node)
        {
            EditorGUILayout.LabelField("Context / Parent", $"{node.ContextId} / {node.ParentContextId}");
            EditorGUILayout.LabelField("Root", node.RootContextId.ToString());
            EditorGUILayout.LabelField("Kind / State", $"{node.Kind} / {node.State}");
            EditorGUILayout.LabelField(
                "Frames",
                BattleDiagnosticFrames.IsValid(node.EndFrame)
                    ? $"{node.StartFrame} -> {node.EndFrame}"
                    : $"{node.StartFrame} -> active");
            EditorGUILayout.LabelField("Actor / Config", $"{node.ActorId} / {node.ConfigId}");
            if (!string.IsNullOrEmpty(node.EndReason))
            {
                EditorGUILayout.LabelField("结束原因", node.EndReason);
            }

            var hasConfig = BattleDebugConfigReferenceMapper.TryFromTraceNode(
                in node,
                out var configReference);
            EditorGUILayout.BeginHorizontal();
            DrawActorButton("Actor", node.ActorId, in ctx);
            EditorGUI.BeginDisabledGroup(!hasConfig || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置"))
            {
                ctx.OpenConfig?.Invoke(configReference);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace"))
            {
                ctx.OpenTrace?.Invoke(node.RootContextId, node.ContextId);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawFrameButton("起始帧", node.StartFrame, in ctx);
            DrawFrameButton("结束帧", node.EndFrame, in ctx);
            if (GUILayout.Button("复制"))
            {
                EditorGUIUtility.systemCopyBuffer =
                    $"Root={node.RootContextId}\nContext={node.ContextId}\nParent={node.ParentContextId}\n" +
                    $"Kind={node.Kind}\nState={node.State}\nFrames={node.StartFrame}->{node.EndFrame}\n" +
                    $"Actor={node.ActorId}\nConfig={node.ConfigId}\nEndReason={node.EndReason}";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfig(in BattleDebugContext ctx)
        {
            var reference = _viewModel.ConfigReference;
            EditorGUILayout.LabelField("类型 / ID", $"ConfigAsset / {reference.Id}");
            EditorGUILayout.LabelField("配置类型", reference.Kind.ToString());
            if (!string.IsNullOrEmpty(reference.PhaseId))
            {
                EditorGUILayout.LabelField("Phase", reference.PhaseId);
            }

            EditorGUILayout.Space(4f);
            if (_viewModel.ConfigLocation.HasValue)
            {
                var location = _viewModel.ConfigLocation.Value;
                EditorGUILayout.LabelField("源文件", location.AssetPath);
                EditorGUILayout.LabelField("行号", location.LineNumber.ToString());
            }
            else
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_viewModel.ConfigStatusMessage)
                        ? "配置源尚未解析。"
                        : $"配置源不可用：{_viewModel.ConfigStatusMessage}",
                    MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置"))
            {
                ctx.OpenConfig?.Invoke(reference);
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("重新解析"))
            {
                _viewModel.InvalidateConfigCache();
                ctx.RequestRepaint?.Invoke();
            }
            if (GUILayout.Button("复制引用"))
            {
                EditorGUIUtility.systemCopyBuffer = reference.ToString();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawActorButton(
            string label,
            long actorId,
            in BattleDebugContext ctx)
        {
            EditorGUI.BeginDisabledGroup(actorId == 0 || ctx.SelectActor == null);
            if (GUILayout.Button(label))
            {
                ctx.SelectActor?.Invoke(actorId);
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawFrameButton(
            string label,
            int frame,
            in BattleDebugContext ctx)
        {
            EditorGUI.BeginDisabledGroup(!BattleDiagnosticFrames.IsValid(frame));
            if (GUILayout.Button(label))
            {
                ctx.WorkspaceState?.SetFrame(frame);
                ctx.SeekReplayFrame?.Invoke(frame);
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static MessageType ResolveMessageType(BattleDiagnosticQueryStatus status)
        {
            if (status.Phase == BattleDiagnosticQueryPhase.Error) return MessageType.Error;
            if (status.Phase == BattleDiagnosticQueryPhase.Partial ||
                status.Availability == BattleDiagnosticDataAvailability.Evicted ||
                status.Availability == BattleDiagnosticDataAvailability.Truncated)
            {
                return MessageType.Warning;
            }

            return MessageType.Info;
        }
    }
}

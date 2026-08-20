using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.RuntimeObjects,
        "Runtime",
        RequiredCapabilities = BattleDiagnosticCapabilities.RuntimeObjects,
        Selections = BattleDebugModuleSelectionSupport.Frame |
                     BattleDebugModuleSelectionSupport.Actor |
                     BattleDebugModuleSelectionSupport.Event |
                     BattleDebugModuleSelectionSupport.Trace |
                     BattleDebugModuleSelectionSupport.RuntimeObject |
                     BattleDebugModuleSelectionSupport.Config)]
    internal sealed class BattleDebugRuntimeObjectsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private static readonly string[] KindLabels = { "All kinds", "Actor", "Projectile", "Area", "Summon" };
        private static readonly string[] StateLabels = { "All states", "Active", "Ended" };
        private static readonly string[] CompletenessLabels = { "All completeness", "Complete", "Partial", "Unreliable" };

        private readonly BattleDebugRuntimeObjectsViewModel _viewModel =
            new BattleDebugRuntimeObjectsViewModel();
        private Vector2 _listScroll;

        public string Name => "Objects";
        public int Order => 407;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "No diagnostic session is available. Start a battle or open a Battle Diagnostics artifact.",
                    MessageType.Info);
                return;
            }

            var contentWidth = ctx.AvailableContentWidth > 0f
                ? ctx.AvailableContentWidth
                : EditorGUIUtility.currentViewWidth;
            DrawFilterToolbar(in ctx, contentWidth);
            _viewModel.RefreshIfNeeded(session);

            if (!_viewModel.IsSupported)
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Info);
                return;
            }

            DrawPanelHeader();
            DrawSummary(contentWidth);
            DrawCatalogWarnings();
            EditorGUILayout.Space(4f);
            var useSplitLayout = _viewModel.Selected.HasValue &&
                                 contentWidth >= 900f;
            if (useSplitLayout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(480f));
                DrawObjectList(in ctx, session, expandHeight: true, compact: true);
                EditorGUILayout.EndVertical();
                GUILayout.Space(8f);
                EditorGUILayout.BeginVertical(GUILayout.Width(340f));
                DrawSelectionDetails(in ctx, session);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var compact = contentWidth < 820f;
                DrawObjectList(in ctx, session, expandHeight: false, compact: compact);
                DrawSelectionDetails(in ctx, session);
            }
        }

        private void DrawFilterToolbar(in BattleDebugContext ctx, float contentWidth)
        {
            var compact = contentWidth < 620f;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Kind", GUILayout.Width(30f));
            var kind = (BattleDiagnosticRuntimeObjectKind)EditorGUILayout.Popup(
                (int)_viewModel.Kind,
                KindLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(90f));
            if (kind != _viewModel.Kind)
            {
                _viewModel.Kind = kind;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.Label("State", GUILayout.Width(34f));
            var state = (BattleDiagnosticRuntimeObjectState)EditorGUILayout.Popup(
                (int)_viewModel.State,
                StateLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(82f));
            if (state != _viewModel.State)
            {
                _viewModel.State = state;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.Label("Quality", GUILayout.Width(42f));
            var completeness = (BattleDiagnosticDataCompleteness)EditorGUILayout.Popup(
                (int)_viewModel.Completeness,
                CompletenessLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(112f));
            if (completeness != _viewModel.Completeness)
            {
                _viewModel.Completeness = completeness;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            if (!compact)
            {
                DrawFilterActions(in ctx);
            }
            EditorGUILayout.EndHorizontal();

            if (compact)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawFilterActions(in ctx);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFilterActions(in BattleDebugContext ctx)
        {
            var unreliableActive = _viewModel.Completeness == BattleDiagnosticDataCompleteness.Unreliable;
            var newUnreliableActive = GUILayout.Toggle(
                unreliableActive,
                "Unreliable",
                EditorStyles.toolbarButton,
                GUILayout.Width(78f));
            if (newUnreliableActive != unreliableActive)
            {
                _viewModel.Completeness = newUnreliableActive
                    ? BattleDiagnosticDataCompleteness.Unreliable
                    : BattleDiagnosticDataCompleteness.Unknown;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!HasActiveFilter());
            if (GUILayout.Button(
                    new GUIContent("Clear", "Clear all object catalog filters"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(44f)))
            {
                ClearFilters();
                _listScroll = Vector2.zero;
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(
                    new GUIContent("Refresh", "Refresh the catalog at its latest revision"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(58f)))
            {
                _viewModel.Invalidate();
                ctx.RequestRepaint?.Invoke();
            }
        }

        private void DrawPanelHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Runtime Object Catalog", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var total = _viewModel.Summary.HasValue
                ? _viewModel.Summary.Value.TotalCount.ToString()
                : "?";
            GUILayout.Label(
                $"Showing {_viewModel.LoadedCount} / {total}   rev {_viewModel.WorksetRevision}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary(float contentWidth)
        {
            if (!_viewModel.Summary.HasValue)
            {
                if (_viewModel.SummaryQueryStatus.Phase == BattleDiagnosticQueryPhase.Unavailable ||
                    _viewModel.SummaryQueryStatus.Phase == BattleDiagnosticQueryPhase.Error)
                {
                    EditorGUILayout.HelpBox(
                        "Object summary unavailable: " + _viewModel.SummaryQueryStatus.Message,
                        MessageType.Warning);
                }
                return;
            }

            var summary = _viewModel.Summary.Value;
            var compact = contentWidth < 620f;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (DrawSummaryButton(
                    "Total",
                    summary.TotalCount,
                    !HasActiveFilter(),
                    Color.white,
                    72f))
            {
                ClearFilters();
            }
            if (DrawSummaryButton(
                    "Complete",
                    summary.CompleteCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Complete,
                    new Color(0.7f, 0.92f, 0.76f),
                    88f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Complete);
            }
            if (DrawSummaryButton(
                    "Partial",
                    summary.PartialCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Partial,
                    new Color(1f, 0.86f, 0.55f),
                    76f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Partial);
            }
            if (DrawSummaryButton(
                    "Unreliable",
                    summary.UnreliableCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Unreliable,
                    new Color(1f, 0.58f, 0.58f),
                    94f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Unreliable);
            }
            if (compact)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("Lifecycle", EditorStyles.miniLabel, GUILayout.Width(58f));
            }
            if (DrawSummaryButton(
                    "Active",
                    summary.ActiveCount,
                    _viewModel.State == BattleDiagnosticRuntimeObjectState.Active,
                    new Color(0.65f, 0.88f, 1f),
                    74f))
            {
                ToggleState(BattleDiagnosticRuntimeObjectState.Active);
            }
            if (DrawSummaryButton(
                    "Ended",
                    summary.EndedCount,
                    _viewModel.State == BattleDiagnosticRuntimeObjectState.Ended,
                    Color.white,
                    74f))
            {
                ToggleState(BattleDiagnosticRuntimeObjectState.Ended);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Catalog {summary.Completeness}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawSummaryButton(
            string label,
            int count,
            bool active,
            Color color,
            float width)
        {
            var oldColor = GUI.color;
            GUI.color = active ? color : Color.Lerp(Color.white, color, 0.42f);
            var clicked = GUILayout.Button(
                new GUIContent($"{label} {count}", $"Filter by {label.ToLowerInvariant()} objects"),
                active ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                GUILayout.Width(width),
                GUILayout.Height(18f));
            GUI.color = oldColor;
            return clicked;
        }

        private void DrawCatalogWarnings()
        {
            if (_viewModel.Summary.HasValue)
            {
                var summary = _viewModel.Summary.Value;
                if (summary.BackfillFailureCount > 0L)
                {
                    EditorGUILayout.HelpBox(
                        $"{summary.BackfillFailureCount} of {summary.BackfillAttemptCount} object backfills failed. " +
                        "Some runtime IDs cannot be explained reliably.",
                        MessageType.Error);
                }
                else if (summary.Truncated)
                {
                    EditorGUILayout.HelpBox(
                        "The runtime object catalog was truncated. Missing objects may have been evicted.",
                        MessageType.Warning);
                }
                else if (summary.PartialCount > 0 || summary.UnreliableCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"The catalog contains {summary.PartialCount} partial and " +
                        $"{summary.UnreliableCount} unreliable objects.",
                        MessageType.Warning);
                }
            }

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                var type = _viewModel.QueryStatus.Phase == BattleDiagnosticQueryPhase.Error
                    ? MessageType.Error
                    : _viewModel.QueryStatus.Phase == BattleDiagnosticQueryPhase.Partial
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, type);
            }
        }

        private void DrawObjectList(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            bool expandHeight,
            bool compact)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Objects", EditorStyles.boldLabel, GUILayout.Width(64f));
            if (HasActiveFilter())
                GUILayout.Label(BuildFilterLabel(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            DrawColumnHeader(compact);
            _listScroll = expandHeight
                ? EditorGUILayout.BeginScrollView(
                    _listScroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.ExpandHeight(true))
                : EditorGUILayout.BeginScrollView(
                    _listScroll,
                    GUILayout.MinHeight(180f),
                    GUILayout.MaxHeight(420f));

            var items = _viewModel.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                DrawObjectRow(in ctx, in item, compact);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                _viewModel.HasMore
                    ? $"{_viewModel.LoadedCount} loaded, more available"
                    : $"{_viewModel.LoadedCount} loaded",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (_viewModel.HasMore)
            {
                if (GUILayout.Button("Load more", GUILayout.Width(100f)))
                {
                    _viewModel.LoadMore(session);
                    ctx.RequestRepaint?.Invoke();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_viewModel.PagingStatusMessage))
                EditorGUILayout.LabelField(_viewModel.PagingStatusMessage, EditorStyles.miniLabel);
        }

        private static void DrawColumnHeader(bool compact)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Kind", GUILayout.Width(compact ? 66f : 72f));
            GUILayout.Label("Runtime ID", GUILayout.Width(compact ? 104f : 112f));
            GUILayout.Label("Name", GUILayout.MinWidth(90f), GUILayout.MaxWidth(150f));
            if (!compact)
            {
                GUILayout.Label("Definition", GUILayout.Width(125f));
            }
            GUILayout.Label("State", GUILayout.Width(62f));
            GUILayout.Label("Quality", GUILayout.Width(78f));
            if (!compact)
            {
                GUILayout.Label("Frames", GUILayout.Width(104f));
                GUILayout.Label("Source / Owner", GUILayout.Width(118f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectRow(
            in BattleDebugContext ctx,
            in BattleDiagnosticRuntimeObject runtimeObject,
            bool compact)
        {
            var selected = _viewModel.Selected.HasValue &&
                           SameIdentity(_viewModel.Selected.Value, runtimeObject);
            var oldColor = GUI.color;
            GUI.color = GetCompletenessColor(runtimeObject.Completeness);
            EditorGUILayout.BeginHorizontal(
                selected ? EditorStyles.helpBox : GUIStyle.none,
                GUILayout.Height(22f));
            GUILayout.Label(runtimeObject.Kind.ToString(), GUILayout.Width(compact ? 66f : 72f));
            if (GUILayout.Button(
                    new GUIContent(
                        $"{runtimeObject.RuntimeId}:{runtimeObject.Generation}",
                        "Select this runtime object"),
                    selected ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                    GUILayout.Width(compact ? 104f : 112f)))
            {
                _viewModel.Select(in runtimeObject);
                ctx.RequestRepaint?.Invoke();
            }
            GUILayout.Label(
                EmptyAsDash(runtimeObject.DisplayName),
                EditorStyles.miniLabel,
                GUILayout.MinWidth(90f),
                GUILayout.MaxWidth(150f));
            if (!compact)
            {
                GUILayout.Label(
                    $"{runtimeObject.DefinitionKind}:{runtimeObject.DefinitionId}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(125f));
            }
            GUILayout.Label(runtimeObject.State.ToString(), GUILayout.Width(62f));
            GUILayout.Label(runtimeObject.Completeness.ToString(), GUILayout.Width(78f));
            if (!compact)
            {
                GUILayout.Label(FormatFrames(in runtimeObject), EditorStyles.miniLabel, GUILayout.Width(104f));
                GUILayout.Label(
                    $"{runtimeObject.SourceActorId} / {runtimeObject.OwnerActorId}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(118f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUI.color = oldColor;
        }

        private void DrawSelectionDetails(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            if (!_viewModel.Selected.HasValue) return;
            var runtimeObject = _viewModel.Selected.Value;
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel, GUILayout.Width(105f));
            var selectedIndex = _viewModel.SelectedIndex;
            GUILayout.Label(
                selectedIndex >= 0 ? $"{selectedIndex + 1} / {_viewModel.LoadedCount}" : string.Empty,
                EditorStyles.miniLabel,
                GUILayout.Width(58f));
            EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
            if (GUILayout.Button(new GUIContent("<", "Select previous object"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                SelectAdjacent(in ctx, -1);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedIndex < 0 || selectedIndex >= _viewModel.LoadedCount - 1);
            if (GUILayout.Button(new GUIContent(">", "Select next object"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                SelectAdjacent(in ctx, 1);
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Copy", "Copy all object diagnostic fields"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                EditorGUIUtility.systemCopyBuffer = BuildClipboardText(in runtimeObject);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Identity", runtimeObject.Reference.ToString());
            EditorGUILayout.LabelField("Display name", EmptyAsDash(runtimeObject.DisplayName));
            EditorGUILayout.LabelField(
                "Definition",
                $"{runtimeObject.DefinitionKind}:{runtimeObject.DefinitionId}");
            EditorGUILayout.LabelField("State / quality", $"{runtimeObject.State} / {runtimeObject.Completeness}");
            EditorGUILayout.LabelField("Discovery", runtimeObject.DiscoveryKind.ToString());
            EditorGUILayout.LabelField("Frames", FormatFrames(in runtimeObject));
            if (runtimeObject.WasBackfilled)
                EditorGUILayout.LabelField("Backfilled frame", runtimeObject.BackfilledFrame.ToString());
            EditorGUILayout.LabelField(
                "Related / source / owner / target",
                $"{runtimeObject.RelatedActorId} / {runtimeObject.SourceActorId} / " +
                $"{runtimeObject.OwnerActorId} / {runtimeObject.TargetActorId}");
            EditorGUILayout.LabelField(
                "Root / context",
                $"{runtimeObject.RootContextId} / {runtimeObject.ContextId}");
            if (runtimeObject.EndReason != 0)
                EditorGUILayout.LabelField("End reason", runtimeObject.EndReason.ToString());

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(ctx.OpenEvent == null);
            if (GUILayout.Button("Related event", GUILayout.Width(100f)))
            {
                if (_viewModel.TryFindRelatedEvent(session, in runtimeObject, out var diagnosticEvent))
                    ctx.OpenEvent?.Invoke(diagnosticEvent);
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(
                ctx.OpenTrace == null || runtimeObject.RootContextId == 0L);
            if (GUILayout.Button("Open Trace", GUILayout.Width(88f)))
                ctx.OpenTrace?.Invoke(runtimeObject.RootContextId, runtimeObject.ContextId);
            EditorGUI.EndDisabledGroup();

            var actorId = _viewModel.GetPreferredActorId();
            EditorGUI.BeginDisabledGroup(ctx.SelectActor == null || actorId == 0L);
            if (GUILayout.Button("Select Actor", GUILayout.Width(88f)))
                ctx.SelectActor?.Invoke(actorId);
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_viewModel.RelatedEventStatusMessage))
                EditorGUILayout.HelpBox(_viewModel.RelatedEventStatusMessage, MessageType.Info);
        }

        private void SelectAdjacent(in BattleDebugContext ctx, int offset)
        {
            if (!_viewModel.SelectAdjacent(offset)) return;
            var selectedIndex = _viewModel.SelectedIndex;
            if (selectedIndex >= 0)
            {
                _listScroll.y = Mathf.Max(0f, selectedIndex * 22f - 44f);
            }
            ctx.RequestRepaint?.Invoke();
        }

        private bool HasActiveFilter()
        {
            return _viewModel.Kind != BattleDiagnosticRuntimeObjectKind.Unknown ||
                   _viewModel.State != BattleDiagnosticRuntimeObjectState.Unknown ||
                   _viewModel.Completeness != BattleDiagnosticDataCompleteness.Unknown;
        }

        private string BuildFilterLabel()
        {
            var text = new StringBuilder(64);
            if (_viewModel.Kind != BattleDiagnosticRuntimeObjectKind.Unknown)
                text.Append(_viewModel.Kind);
            if (_viewModel.State != BattleDiagnosticRuntimeObjectState.Unknown)
            {
                if (text.Length > 0) text.Append(" / ");
                text.Append(_viewModel.State);
            }
            if (_viewModel.Completeness != BattleDiagnosticDataCompleteness.Unknown)
            {
                if (text.Length > 0) text.Append(" / ");
                text.Append(_viewModel.Completeness);
            }
            return text.ToString();
        }

        private void ClearFilters()
        {
            _viewModel.Kind = BattleDiagnosticRuntimeObjectKind.Unknown;
            _viewModel.State = BattleDiagnosticRuntimeObjectState.Unknown;
            _viewModel.Completeness = BattleDiagnosticDataCompleteness.Unknown;
            _viewModel.Invalidate();
        }

        private void ToggleCompleteness(BattleDiagnosticDataCompleteness completeness)
        {
            _viewModel.Completeness = _viewModel.Completeness == completeness
                ? BattleDiagnosticDataCompleteness.Unknown
                : completeness;
            _viewModel.Invalidate();
            _listScroll = Vector2.zero;
        }

        private void ToggleState(BattleDiagnosticRuntimeObjectState state)
        {
            _viewModel.State = _viewModel.State == state
                ? BattleDiagnosticRuntimeObjectState.Unknown
                : state;
            _viewModel.Invalidate();
            _listScroll = Vector2.zero;
        }

        private static bool SameIdentity(
            in BattleDiagnosticRuntimeObject left,
            in BattleDiagnosticRuntimeObject right)
        {
            return left.Kind == right.Kind &&
                   left.RuntimeId == right.RuntimeId &&
                   left.Generation == right.Generation;
        }

        private static string FormatFrames(in BattleDiagnosticRuntimeObject runtimeObject)
        {
            var start = BattleDiagnosticFrames.IsValid(runtimeObject.CreatedFrame)
                ? runtimeObject.CreatedFrame.ToString()
                : "?";
            var end = BattleDiagnosticFrames.IsValid(runtimeObject.DestroyedFrame)
                ? runtimeObject.DestroyedFrame.ToString()
                : runtimeObject.State == BattleDiagnosticRuntimeObjectState.Active ? "active" : "?";
            return start + " -> " + end;
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static Color GetCompletenessColor(BattleDiagnosticDataCompleteness completeness)
        {
            switch (completeness)
            {
                case BattleDiagnosticDataCompleteness.Partial:
                    return new Color(1f, 0.86f, 0.55f);
                case BattleDiagnosticDataCompleteness.Unreliable:
                    return new Color(1f, 0.58f, 0.58f);
                default:
                    return Color.white;
            }
        }

        private static string BuildClipboardText(in BattleDiagnosticRuntimeObject runtimeObject)
        {
            var text = new StringBuilder(256);
            text.Append("Object=").Append(runtimeObject.Reference)
                .Append(" Name=").Append(runtimeObject.DisplayName)
                .Append(" Definition=").Append(runtimeObject.DefinitionKind).Append(':').Append(runtimeObject.DefinitionId)
                .Append(" State=").Append(runtimeObject.State)
                .Append(" Completeness=").Append(runtimeObject.Completeness)
                .Append(" Discovery=").Append(runtimeObject.DiscoveryKind)
                .Append(" Frames=").Append(FormatFrames(in runtimeObject))
                .Append(" RelatedActor=").Append(runtimeObject.RelatedActorId)
                .Append(" SourceActor=").Append(runtimeObject.SourceActorId)
                .Append(" OwnerActor=").Append(runtimeObject.OwnerActorId)
                .Append(" TargetActor=").Append(runtimeObject.TargetActorId)
                .Append(" RootContext=").Append(runtimeObject.RootContextId)
                .Append(" Context=").Append(runtimeObject.ContextId);
            return text.ToString();
        }
    }
}

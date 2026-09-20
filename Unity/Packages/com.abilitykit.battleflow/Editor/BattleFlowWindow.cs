#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbilityKit.BattleFlow;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Scenario;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BattleFlow.Editor
{
    /// <summary>
    /// 战斗流程编辑器：三段横排（构建前置 / 流程驱动 / 验收断言）+ 底部结果。
    /// 积木按 <see cref="BattleBlock.Section"/> 自动落入对应区块，对应 IR 的 Actors/Setup/Obstacles vs Timeline vs Expectations 三段，
    /// 避免把声明式的世界定义、真正的时间线、跑完才判的断言混在一个扁平时间轴里。
    /// 运行（headless verdict + trace）是项目级插桩，编辑器只负责「编排 → 编译 → 展示 IR」。
    /// </summary>
    public sealed class BattleFlowWindow : EditorWindow
    {
        private readonly List<BattleBlock> _settings = new List<BattleBlock>();
        private readonly List<BattleBlock> _authoring = new List<BattleBlock>();
        private readonly List<BattleBlock> _setup = new List<BattleBlock>();
        private readonly List<BattleBlock> _timeline = new List<BattleBlock>();
        private readonly List<BattleBlock> _assertions = new List<BattleBlock>();
        private readonly List<string> _tags = new List<string>();
        private readonly List<SceneLibraryEntry> _sceneLibrary = new List<SceneLibraryEntry>();
        private readonly Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>();
        private string _flowDirectory = "Assets/BattleFlows";
        private string _caseId = "preview-case";
        private string _displayName = string.Empty;
        private string _executionProfileId = "default";
        private string _scenarioRef = string.Empty;
        private string _scenePath = string.Empty;
        private string _sceneId = string.Empty;
        private string _sceneDisplayName = string.Empty;
        private string _casePath = string.Empty;
        private string _result = string.Empty;
        private IReadOnlyList<BattleFlowTraceNode>? _traceNodes;
        private readonly Dictionary<long, bool> _traceFoldouts = new Dictionary<long, bool>();
        private readonly Stack<EditorSnapshot> _undoStack = new Stack<EditorSnapshot>();
        private List<BattleBlock>? _dragSourceList;
        private int _dragSourceIndex = -1;
        private bool _developerMode;
        private Vector2 _paletteScroll;
        private Vector2 _authoringScroll;
        private Vector2 _setupScroll;
        private Vector2 _timelineScroll;
        private Vector2 _assertionsScroll;
        private Vector2 _resultScroll;
        private Vector2 _traceScroll;

        [MenuItem("Window/AbilityKit/Battle Flow")]
        public static void Open()
        {
            var window = GetWindow<BattleFlowWindow>("Battle Flow");
            window.minSize = new Vector2(1180, 560);
            window.Show();
        }

        private void OnEnable() => RefreshSceneLibrary();

        private void OnGUI()
        {
            HandleUndoShortcut();
            DrawToolbar();
            DrawSettings();
            var internalWorkspace = _developerMode && _authoring.Count == 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPalette(internalWorkspace);
                if (internalWorkspace)
                {
                    DrawSection(_setup, "① 构建前置", ref _setupScroll);
                    DrawSection(_timeline, "② 流程驱动", ref _timelineScroll);
                    DrawSection(_assertions, "③ 验收断言", ref _assertionsScroll);
                }
                else
                {
                    DrawSection(_authoring, "测试步骤", ref _authoringScroll);
                }
            }
            DrawAuthoringSummary();
            DrawResult();
            ResetDragIfReleasedOutside();
        }

        private void ResetDragIfReleasedOutside()
        {
            var evt = Event.current;
            if (evt != null && evt.type == EventType.MouseUp && _dragSourceList != null)
            {
                _dragSourceList = null;
                _dragSourceIndex = -1;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("预览", EditorStyles.toolbarButton, GUILayout.Width(48))) Preview();
                if (GUILayout.Button("运行", EditorStyles.toolbarButton, GUILayout.Width(48))) Run();
                if (GUILayout.Button("批量运行", EditorStyles.toolbarButton, GUILayout.Width(60))) RunBatch();
                if (GUILayout.Button("存为模板", EditorStyles.toolbarButton, GUILayout.Width(60))) SaveAsTemplate();
                if (_developerMode && GUILayout.Button("DSL", EditorStyles.toolbarButton, GUILayout.Width(40))) ParseDsl();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48))) SaveFlow();
                if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(48))) LoadFlow();
                if (GUILayout.Button("保存场景", EditorStyles.toolbarButton, GUILayout.Width(60))) SaveScene();
                if (GUILayout.Button("加载场景", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadScene();
                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48)))
                {
                    PushUndo();
                    _settings.Clear();
                    _authoring.Clear();
                    _setup.Clear();
                    _timeline.Clear();
                    _assertions.Clear();
                    _result = string.Empty;
                    _traceNodes = null;
                    _displayName = string.Empty;
                    _executionProfileId = "default";
                    _tags.Clear();
                    _scenarioRef = string.Empty;
                    _scenePath = string.Empty;
                    _sceneId = string.Empty;
                    _sceneDisplayName = string.Empty;
                    _casePath = string.Empty;
                }
                GUILayout.FlexibleSpace();
                _developerMode = GUILayout.Toggle(_developerMode, "开发者模式", EditorStyles.toolbarButton, GUILayout.Width(76));
                if (_developerMode)
                {
                    EditorGUILayout.LabelField("Scenario", EditorStyles.miniLabel, GUILayout.Width(48));
                    var scenarioRef = EditorGUILayout.TextField(_scenarioRef, EditorStyles.toolbarTextField, GUILayout.Width(140));
                    if (!string.Equals(scenarioRef, _scenarioRef, StringComparison.Ordinal))
                    {
                        _scenarioRef = scenarioRef;
                        _scenePath = string.Empty;
                        _sceneId = string.Empty;
                        _sceneDisplayName = string.Empty;
                    }
                }
                EditorGUILayout.LabelField("CaseId", EditorStyles.miniLabel, GUILayout.Width(40));
                _caseId = EditorGUILayout.TextField(_caseId, EditorStyles.toolbarTextField, GUILayout.Width(140));
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("用例信息", EditorStyles.boldLabel);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            var tags = string.Join(", ", _tags);
            EditorGUI.BeginChangeCheck();
            tags = EditorGUILayout.TextField("Tags", tags);
            if (EditorGUI.EndChangeCheck())
            {
                _tags.Clear();
                foreach (var tag in tags.Split(','))
                {
                    var normalized = tag.Trim();
                    if (normalized.Length != 0 && !_tags.Contains(normalized)) _tags.Add(normalized);
                }
            }
            DrawSceneSelector();
            var profiles = BattleExecutionProfileCatalog.Profiles.OrderBy(profile => profile.Id).ToArray();
            var profileLabels = profiles.Select(profile => string.IsNullOrEmpty(profile.DisplayName)
                ? profile.Id
                : profile.DisplayName + " (" + profile.Id + ")").ToArray();
            var profileIndex = Array.FindIndex(profiles,
                profile => string.Equals(profile.Id, _executionProfileId, StringComparison.OrdinalIgnoreCase));
            if (profileIndex < 0) profileIndex = 0;
            profileIndex = EditorGUILayout.Popup("Execution Profile", profileIndex, profileLabels);
            if (profiles.Length != 0) _executionProfileId = profiles[profileIndex].Id;
            if (_developerMode)
            {
                if (_settings.Count == 0)
                    EditorGUILayout.HelpBox("显式执行设置会覆盖 Execution Profile。", MessageType.None);
                for (var i = 0; i < _settings.Count; i++) DrawBlockItem(_settings, i);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneSelector()
        {
            var labels = new string[_sceneLibrary.Count + 1];
            labels[0] = "未选择";
            var selected = 0;
            for (var i = 0; i < _sceneLibrary.Count; i++)
            {
                labels[i + 1] = _sceneLibrary[i].Label;
                if (!string.IsNullOrEmpty(_scenePath) && PathsEqual(_scenePath, _sceneLibrary[i].Path))
                    selected = i + 1;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Battle Scene");
                EditorGUI.BeginChangeCheck();
                var next = EditorGUILayout.Popup(selected, labels);
                if (EditorGUI.EndChangeCheck())
                {
                    PushUndo();
                    try
                    {
                        if (next == 0) ClearSceneBinding();
                        else BindScene(_sceneLibrary[next - 1].Path);
                    }
                    catch (Exception ex)
                    {
                        _result = "场景绑定失败：" + ex.Message;
                    }
                }
                var refresh = new GUIContent(EditorGUIUtility.IconContent("Refresh"))
                {
                    tooltip = "刷新场景库",
                };
                if (GUILayout.Button(refresh, GUILayout.Width(24))) RefreshSceneLibrary();
            }
        }

        private void DrawAuthoringSummary()
        {
            if (_authoring.Count == 0) return;
            try
            {
                var expanded = BattleFlowAuthoringExpander.Expand(_authoring);
                EditorGUILayout.LabelField(
                    $"展开结果：构建 {expanded.Setup.Count} · 动作 {expanded.Timeline.Count} · 验证 {expanded.Assertions.Count}",
                    EditorStyles.miniLabel);
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox("作者节点展开失败：" + ex.Message, MessageType.Warning);
            }
        }

        private void DrawPalette(bool includeInternal)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190));
            EditorGUILayout.LabelField("积木调色板", EditorStyles.boldLabel);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);

            foreach (var group in BattleBlockPalette.Groups)
            {
                var templates = group.Value
                    .Where(template => includeInternal || BattleBlockPalette.IsAuthorFacing(template))
                    .ToArray();
                if (templates.Length == 0) continue;
                var expanded = _groupFoldouts.TryGetValue(group.Key, out var e) ? e : true;
                expanded = EditorGUILayout.Foldout(expanded, group.Key, true);
                _groupFoldouts[group.Key] = expanded;
                if (!expanded) continue;

                EditorGUI.indentLevel++;
                foreach (var template in templates)
                {
                    if (GUILayout.Button(template.DisplayName))
                    {
                        PushUndo();
                        AddTemplate(template);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // 流程库：列出 .battleflow 文件，点击加载
            EditorGUILayout.Space();
            var libExpanded = _groupFoldouts.TryGetValue("__flowlib__", out var le) ? le : true;
            libExpanded = EditorGUILayout.Foldout(libExpanded, "流程库", true);
            _groupFoldouts["__flowlib__"] = libExpanded;
            if (libExpanded)
            {
                if (!Directory.Exists(_flowDirectory))
                {
                    EditorGUILayout.HelpBox("目录不存在: " + _flowDirectory, MessageType.None);
                }
                else
                {
                    var files = Directory.GetFiles(_flowDirectory, "*.battleflow");
                    if (files.Length == 0) EditorGUILayout.HelpBox("暂无 .battleflow 文件", MessageType.None);
                    foreach (var file in files)
                    {
                        if (GUILayout.Button(Path.GetFileNameWithoutExtension(file)))
                        {
                            PushUndo();
                            var doc = BattleFlowCodec.Load(file);
                            LoadDocument(doc, file);
                            _result = "已加载: " + file;
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(List<BattleBlock> blocks, string title, ref Vector2 scroll)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(240));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (var i = 0; i < blocks.Count; i++)
            {
                DrawBlockItem(blocks, i);
            }

            if (blocks.Count == 0)
                EditorGUILayout.HelpBox("（空）", MessageType.None);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBlockItem(List<BattleBlock> blocks, int index)
        {
            var block = blocks[index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            var handleRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Width(16));
            EditorGUI.LabelField(handleRect, "≡");
            EditorGUILayout.LabelField(BlockLabel(block), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var remove = GUILayout.Button("×", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            HandleDrag(blocks, index, handleRect);

            if (remove)
            {
                PushUndo();
                blocks.RemoveAt(index);
            }
            else
            {
                DrawBlockFields(block);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>拖拽手柄重排：在「≡」按下开始拖拽，拖到同列表另一个「≡」上释放即重排。</summary>
        private void HandleDrag(List<BattleBlock> blocks, int index, Rect handleRect)
        {
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition))
            {
                _dragSourceList = blocks;
                _dragSourceIndex = index;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && ReferenceEquals(_dragSourceList, blocks))
            {
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && _dragSourceList != null)
            {
                if (ReferenceEquals(_dragSourceList, blocks) && handleRect.Contains(evt.mousePosition) && _dragSourceIndex != index)
                {
                    PushUndo();
                    MoveBlock(blocks, _dragSourceIndex, index);
                }
                _dragSourceList = null;
                _dragSourceIndex = -1;
                evt.Use();
            }
        }

        private static void MoveBlock(List<BattleBlock> blocks, int from, int to)
        {
            var item = blocks[from];
            blocks.RemoveAt(from);
            blocks.Insert(to, item);
        }

        private void HandleUndoShortcut()
        {
            var evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Z && evt.control)
            {
                Undo();
                evt.Use();
            }
        }

        /// <summary>把当前三段列表快照压栈（每次增/删/移动/清空/加载前调用）。</summary>
        private void PushUndo()
        {
            var doc = new BattleFlowDocument
            {
                CaseId = _caseId,
                DisplayName = _displayName,
                ScenarioRef = _scenarioRef,
                Tags = new List<string>(_tags),
                Authoring = new List<BattleBlock>(_authoring),
                ExecutionProfileId = _executionProfileId,
                Sections = CaptureSections(includeSetup: true),
            };
            _undoStack.Push(new EditorSnapshot
            {
                DocumentJson = BattleFlowCodec.Serialize(doc),
                ScenePath = _scenePath,
                SceneId = _sceneId,
                SceneDisplayName = _sceneDisplayName,
                CasePath = _casePath,
            });
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                _result = "没有可撤销的操作";
                return;
            }

            var snapshot = _undoStack.Pop();
            var doc = BattleFlowCodec.Parse(snapshot.DocumentJson);
            _caseId = doc.CaseId;
            _displayName = doc.DisplayName;
            _executionProfileId = doc.ExecutionProfileId;
            _scenarioRef = doc.ScenarioRef;
            _tags.Clear();
            _tags.AddRange(doc.Tags);
            _scenePath = snapshot.ScenePath;
            _sceneId = snapshot.SceneId;
            _sceneDisplayName = snapshot.SceneDisplayName;
            _casePath = snapshot.CasePath;
            _authoring.Clear();
            _authoring.AddRange(doc.Authoring);
            LoadSections(doc.Sections);
            _traceNodes = null;
            _result = "已撤销";
        }

        private static string BlockLabel(BattleBlock block) =>
            string.IsNullOrEmpty(block.DisplayName) ? block.GetType().Name : block.DisplayName;

        private static readonly string[] SkillActions = { "cast_skill", "wait", "move_to", "cancel", "press", "release", "hold" };
        private static readonly string[] EndConditions = { TestEndConditionKinds.TimelineComplete, TestEndConditionKinds.Duration };

        private void DrawBlockFields(BattleBlock block)
        {
            if (BattleBlockFieldRendererRegistry.Renderer is IContextualBattleBlockFieldRenderer contextualRenderer &&
                contextualRenderer.TryDrawFields(
                    block,
                    new BattleBlockFieldContext(_authoring, _setup)))
            {
                return;
            }

            switch (block)
            {
                case ExecutionSettingsBlock execution:
                    execution.TickRate = EditorGUILayout.IntField("TickRate", execution.TickRate);
                    execution.MaxDurationMs = EditorGUILayout.IntField("MaxDurationMs", execution.MaxDurationMs);
                    execution.SettleDurationMs = EditorGUILayout.IntField("SettleDurationMs", execution.SettleDurationMs);
                    var endIndex = System.Array.IndexOf(EndConditions, execution.EndCondition);
                    if (endIndex < 0) endIndex = 0;
                    endIndex = EditorGUILayout.Popup("EndCondition", endIndex, EndConditions);
                    execution.EndCondition = EndConditions[endIndex];
                    if (execution.EndCondition == TestEndConditionKinds.Duration)
                        execution.DurationMs = EditorGUILayout.IntField("DurationMs", execution.DurationMs);
                    return;
                case DuelSetupBlock duel:
                    duel.EnvironmentProfileId = EditorGUILayout.TextField("Environment", duel.EnvironmentProfileId);
                    duel.CasterAlias = EditorGUILayout.TextField("Caster Alias", duel.CasterAlias);
                    duel.CasterHeroId = EditorGUILayout.IntField("Caster Hero", duel.CasterHeroId);
                    duel.CasterAttributeTemplateId = EditorGUILayout.IntField("Caster Attributes", duel.CasterAttributeTemplateId);
                    duel.TargetAlias = EditorGUILayout.TextField("Target Alias", duel.TargetAlias);
                    duel.TargetHeroId = EditorGUILayout.IntField("Target Hero", duel.TargetHeroId);
                    duel.TargetAttributeTemplateId = EditorGUILayout.IntField("Target Attributes", duel.TargetAttributeTemplateId);
                    duel.TargetDistance = EditorGUILayout.FloatField("Distance", duel.TargetDistance);
                    return;
                case CastSkillBlock cast:
                    cast.CasterAlias = EditorGUILayout.TextField("Caster", cast.CasterAlias);
                    cast.TargetAlias = EditorGUILayout.TextField("Target", cast.TargetAlias);
                    cast.Slot = EditorGUILayout.IntField("Skill Slot", cast.Slot);
                    cast.AtMs = EditorGUILayout.IntField("AtMs", cast.AtMs);
                    return;
                case SetEnvironmentBlock env:
                    env.ProfileId = EditorGUILayout.TextField("ProfileId", env.ProfileId);
                    return;
                case SpawnActorBlock spawn:
                    spawn.Alias = EditorGUILayout.TextField("Alias", spawn.Alias);
                    spawn.PlayerId = EditorGUILayout.TextField("PlayerId", spawn.PlayerId);
                    spawn.HeroId = EditorGUILayout.IntField("HeroId", spawn.HeroId);
                    spawn.AttributeTemplateId = EditorGUILayout.IntField("AttributeTemplateId", spawn.AttributeTemplateId);
                    var skillIdsText = string.Join(",", spawn.SkillIds ?? Array.Empty<int>());
                    skillIdsText = EditorGUILayout.TextField("SkillIds (逗号分隔)", skillIdsText);
                    spawn.SkillIds = ParseIntArray(skillIdsText);
                    spawn.TeamId = EditorGUILayout.IntField("TeamId", spawn.TeamId);
                    return;
                case TimelineStepBlock step:
                    step.AtMs = EditorGUILayout.IntField("AtMs", step.AtMs);
                    var actionIndex = System.Array.IndexOf(SkillActions, step.Action);
                    if (actionIndex < 0) actionIndex = 0;
                    actionIndex = EditorGUILayout.Popup("Action", actionIndex, SkillActions);
                    step.Action = SkillActions[actionIndex];
                    step.ActorAlias = EditorGUILayout.TextField("ActorAlias", step.ActorAlias);
                    step.TargetAlias = EditorGUILayout.TextField("TargetAlias", step.TargetAlias);
                    step.Slot = EditorGUILayout.IntField("Slot", step.Slot);
                    return;
                case WaitBlock wait:
                    wait.AtMs = EditorGUILayout.IntField("AtMs", wait.AtMs);
                    wait.DurationMs = EditorGUILayout.IntField("DurationMs", wait.DurationMs);
                    return;
                case MoveToBlock move:
                    move.AtMs = EditorGUILayout.IntField("AtMs", move.AtMs);
                    move.ActorAlias = EditorGUILayout.TextField("ActorAlias", move.ActorAlias);
                    return;
                case PlaceObstacleBlock obstacle:
                    obstacle.Id = EditorGUILayout.TextField("Id", obstacle.Id);
                    obstacle.Shape = EditorGUILayout.TextField("Shape", obstacle.Shape);
                    return;
                case BattleCompositeBlock composite:
                    EditorGUILayout.LabelField($"复合积木（{composite.Children.Count} 个子积木）");
                    return;
            }

            // 项目自定义积木：先查注册的渲染器（如 MOBA 断言积木的下拉框）
            if (BattleBlockFieldRendererRegistry.Renderer?.TryDrawFields(block) == true) return;

            // 未知积木（项目自定义）：反射渲染可编辑字段（string/int/float/bool/enum）
            foreach (var prop in block.GetType().GetProperties())
            {
                if (!prop.CanWrite || IsInitOnly(prop)) continue;
                var value = prop.GetValue(block);
                if (value is string s) prop.SetValue(block, EditorGUILayout.TextField(prop.Name, s));
                else if (value is int i) prop.SetValue(block, EditorGUILayout.IntField(prop.Name, i));
                else if (value is float f) prop.SetValue(block, EditorGUILayout.FloatField(prop.Name, f));
                else if (value is bool b) prop.SetValue(block, EditorGUILayout.Toggle(prop.Name, b));
                else if (value is Enum en) prop.SetValue(block, EditorGUILayout.EnumPopup(prop.Name, en));
            }
        }

        private static bool IsInitOnly(System.Reflection.PropertyInfo property)
        {
            var setter = property.SetMethod;
            if (setter == null) return false;
            foreach (var modifier in setter.ReturnParameter.GetRequiredCustomModifiers())
            {
                if (modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit") return true;
            }
            return false;
        }

        private static int[] ParseIntArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<int>();
            var parts = text.Split(',');
            var result = new List<int>(parts.Length);
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out var value)) result.Add(value);
            }
            return result.ToArray();
        }

        private void DrawResult()
        {
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(340));
            _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.Height(170));
            EditorGUILayout.TextArea(_result, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            _traceScroll = EditorGUILayout.BeginScrollView(_traceScroll, GUILayout.Height(170));
            DrawTraceTree();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTraceTree()
        {
            if (_traceNodes == null || _traceNodes.Count == 0)
            {
                EditorGUILayout.HelpBox("运行后这里展示命中链路（trace 树）", MessageType.None);
                return;
            }

            var childrenByParent = new Dictionary<long, List<BattleFlowTraceNode>>();
            foreach (var node in _traceNodes)
            {
                if (!childrenByParent.TryGetValue(node.ParentId, out var list))
                    childrenByParent[node.ParentId] = list = new List<BattleFlowTraceNode>();
                list.Add(node);
            }

            var roots = _traceNodes.Where(n => n.ParentId == 0 || n.Id == n.RootId).OrderBy(n => n.Frame).ToList();
            foreach (var root in roots)
                DrawTraceNode(root, childrenByParent, 0);
        }

        private void DrawTraceNode(BattleFlowTraceNode node, Dictionary<long, List<BattleFlowTraceNode>> childrenByParent, int depth)
        {
            var label = node.Kind + (node.ConfigId != 0 ? "(" + node.ConfigId + ")" : "");
            var hasChildren = childrenByParent.TryGetValue(node.Id, out var children) && children.Count > 0;

            EditorGUI.indentLevel = depth;
            if (hasChildren)
            {
                var expanded = _traceFoldouts.TryGetValue(node.Id, out var e) ? e : true;
                expanded = EditorGUILayout.Foldout(expanded, label, true);
                _traceFoldouts[node.Id] = expanded;
                if (expanded)
                    foreach (var child in children.OrderBy(c => c.Frame))
                        DrawTraceNode(child, childrenByParent, depth + 1);
            }
            else
            {
                EditorGUILayout.LabelField(label);
            }
            EditorGUI.indentLevel = 0;
        }

        private void Preview()
        {
            _traceNodes = null;
            try
            {
                var scenario = CompileCurrent();
                var errors = TestScenarioValidator.Validate(scenario);
                _result = errors.Count == 0
                    ? FormatScenario(scenario)
                    : "校验失败：\n" + string.Join("\n", errors);
            }
            catch (Exception ex)
            {
                _result = "预览异常：" + ex.Message;
            }
        }

        private void Run()
        {
            TestScenario scenario;
            try
            {
                scenario = CompileCurrent();
            }
            catch (Exception ex)
            {
                _result = "编译异常：" + ex.Message;
                return;
            }

            var errors = TestScenarioValidator.Validate(scenario);
            if (errors.Count > 0)
            {
                _result = "编译校验失败：\n" + string.Join("\n", errors);
                return;
            }

            var runner = BattleFlowRunnerRegistry.Runner;
            if (runner == null)
            {
                _result = "未注册 IBattleFlowRunner（项目在编辑器里注册自己的 runner，如 MobaBattleFlowRunner）。";
                return;
            }

            try
            {
                var runResult = runner.Run(scenario);
                _result = (runResult.Passed ? "[通过] " : "[未通过] ") + runResult.Summary;
                _traceNodes = runResult.Trace;
            }
            catch (Exception ex)
            {
                _result = "运行异常：" + ex.Message;
                _traceNodes = null;
            }
        }

        private void RunBatch()
        {
            var batchRunner = BattleFlowRunnerRegistry.BatchRunner;
            if (batchRunner == null)
            {
                _result = "未注册 IBattleFlowBatchRunner（项目在编辑器里注册自己的批量运行器，如 MobaBattleFlowRunner）。";
                _traceNodes = null;
                return;
            }

            var directory = EditorUtility.OpenFolderPanel("选择 .battleflow 目录", _flowDirectory, "");
            if (string.IsNullOrEmpty(directory))
            {
                _result = "已取消批量运行。";
                return;
            }

            _traceNodes = null;
            try
            {
                _result = "批量运行: " + directory + "\n\n" + batchRunner.RunDirectory(directory);
            }
            catch (Exception ex)
            {
                _result = "批量运行异常：" + ex.Message;
            }
        }

        private void SaveAsTemplate()
        {
            if (_authoring.Count == 0 && _settings.Count == 0 && _setup.Count == 0 && _timeline.Count == 0 && _assertions.Count == 0)
            {
                _result = "当前没有积木可存为模板";
                return;
            }

            var dialog = CreateInstance<SaveTemplateDialog>();
            dialog.OnConfirm = name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _result = "模板名不能为空";
                    return;
                }

                // 快照当前三段（克隆，避免之后编辑流程污染模板）。
                var blocks = (_authoring.Count != 0 ? _authoring : AllBlocks())
                    .Select(BattleFlowCodec.CloneBlock)
                    .ToList();
                BattleFlowTemplateStore.Save(name, blocks);
                BattleBlockPalette.Register(BattleFlowTemplateStore.Category, new BattleCompositeBlock { Id = name, DisplayName = name, Children = blocks });
                _result = "已存为模板: " + name;
            };
            dialog.ShowUtility();
        }

        private void ParseDsl()
        {
            var dialog = CreateInstance<DslDialog>();
            dialog.OnParse = text =>
            {
                try
                {
                    var document = BattleFlowRunnerRegistry.DocumentDslParser(_caseId, text);
                    PushUndo();
                    _caseId = document.CaseId;
                    _displayName = document.DisplayName;
                    _executionProfileId = document.ExecutionProfileId;
                    _scenarioRef = document.ScenarioRef;
                    _tags.Clear();
                    _tags.AddRange(document.Tags);
                    _scenePath = string.Empty;
                    _sceneId = string.Empty;
                    _sceneDisplayName = string.Empty;
                    _casePath = string.Empty;
                    _authoring.Clear();
                    LoadSections(document.Sections.HasBlocks
                        ? document.Sections
                        : BattleFlowSections.FromBlocks(document.Blocks));
                    _traceNodes = null;
                    _result = "已从 DSL 解析 " + document.GetOrderedBlocks().Count + " 个积木";
                }
                catch (Exception ex)
                {
                    _result = "DSL 解析失败：" + ex.Message;
                }
            };
            dialog.ShowUtility();
        }

        private void SaveFlow()
        {
            if (!string.IsNullOrWhiteSpace(_scenarioRef) && _setup.Count != 0 && string.IsNullOrEmpty(_scenePath))
            {
                _result = "当前 Setup 尚未绑定场景资产，请先保存或加载场景。";
                return;
            }
            if (!string.IsNullOrWhiteSpace(_scenarioRef) && _setup.Count != 0 && !SceneSetupMatchesFile())
            {
                _result = "当前 Setup 包含尚未保存的场景修改，请先保存场景。";
                return;
            }
            var path = EditorUtility.SaveFilePanel("保存战斗流程", _flowDirectory, _caseId + ".battleflow", "battleflow");
            if (string.IsNullOrEmpty(path)) return;
            if (!string.IsNullOrEmpty(_scenePath))
                _scenarioRef = MakeRelativeAssetReference(path, _scenePath);
            var document = CreateCaseDocument();
            BattleFlowDocumentValidator.ThrowIfInvalid(document);
            EditorAtomicFileWriter.WriteAllText(path, BattleFlowCodec.Serialize(document));
            _casePath = path;
            _result = "已保存: " + path;
        }

        private void LoadFlow()
        {
            var path = EditorUtility.OpenFilePanel("加载战斗流程", _flowDirectory, "battleflow");
            if (string.IsNullOrEmpty(path)) return;
            var doc = BattleFlowCodec.Load(path);
            PushUndo();
            LoadDocument(doc, path);
            _result = "已加载: " + path;
        }

        private void SaveScene()
        {
            var defaultName = string.IsNullOrWhiteSpace(_scenarioRef)
                ? "battle-scene"
                : Path.GetFileNameWithoutExtension(_scenarioRef);
            var path = EditorUtility.SaveFilePanel("保存战斗场景", _flowDirectory, defaultName + ".battlescene", "battlescene");
            if (string.IsNullOrEmpty(path)) return;

            if (string.IsNullOrWhiteSpace(_sceneId)) _sceneId = Path.GetFileNameWithoutExtension(path);
            var scene = CreateSceneDocument(_sceneId);
            BattleFlowDocumentValidator.ThrowIfInvalid(scene);
            EditorAtomicFileWriter.WriteAllText(path, BattleFlowCodec.SerializeScene(scene));
            _scenePath = path;
            _scenarioRef = _casePath.Length == 0
                ? Path.GetFileName(path)
                : MakeRelativeAssetReference(_casePath, path);
            RefreshSceneLibrary();
            _result = "已保存场景: " + path;
        }

        private void LoadScene()
        {
            var path = EditorUtility.OpenFilePanel("加载战斗场景", _flowDirectory, "battlescene");
            if (string.IsNullOrEmpty(path)) return;
            var scene = BattleFlowCodec.LoadScene(path);
            BattleFlowDocumentValidator.ThrowIfInvalid(scene);
            PushUndo();
            BindScene(path, scene);
            _result = "已加载场景: " + path;
        }

        private void RefreshSceneLibrary()
        {
            _sceneLibrary.Clear();
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(_flowDirectory)) directories.Add(Path.GetFullPath(_flowDirectory));
            if (!string.IsNullOrEmpty(_casePath))
            {
                var caseDirectory = Path.GetDirectoryName(Path.GetFullPath(_casePath));
                if (!string.IsNullOrEmpty(caseDirectory) && Directory.Exists(caseDirectory))
                    directories.Add(caseDirectory);
            }
            if (!string.IsNullOrEmpty(_scenePath))
            {
                var sceneDirectory = Path.GetDirectoryName(Path.GetFullPath(_scenePath));
                if (!string.IsNullOrEmpty(sceneDirectory) && Directory.Exists(sceneDirectory))
                    directories.Add(sceneDirectory);
            }

            foreach (var directory in directories)
            foreach (var path in Directory.GetFiles(directory, "*.battlescene", SearchOption.AllDirectories)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var scene = BattleFlowCodec.LoadScene(path);
                    BattleFlowDocumentValidator.ThrowIfInvalid(scene);
                    var name = string.IsNullOrWhiteSpace(scene.DisplayName) ? scene.SceneId : scene.DisplayName;
                    _sceneLibrary.Add(new SceneLibraryEntry
                    {
                        Path = Path.GetFullPath(path),
                        Label = name + "  [" + Path.GetFileName(path) + "]",
                    });
                }
                catch
                {
                    // Invalid assets remain loadable through the developer file picker for diagnosis.
                }
            }
        }

        private void BindScene(string path, BattleSceneDocument? loadedScene = null)
        {
            var scene = loadedScene ?? BattleFlowCodec.LoadScene(path);
            BattleFlowDocumentValidator.ThrowIfInvalid(scene);
            _setup.Clear();
            _setup.AddRange(scene.GetOrderedBlocks());
            _scenePath = Path.GetFullPath(path);
            _sceneId = scene.SceneId;
            _sceneDisplayName = scene.DisplayName;
            var ownerPath = string.IsNullOrEmpty(_casePath)
                ? Path.Combine(_flowDirectory, _caseId + ".battleflow")
                : _casePath;
            _scenarioRef = MakeRelativeAssetReference(ownerPath, _scenePath);
            RefreshSceneLibrary();
        }

        private void ClearSceneBinding()
        {
            _setup.Clear();
            _scenarioRef = string.Empty;
            _scenePath = string.Empty;
            _sceneId = string.Empty;
            _sceneDisplayName = string.Empty;
        }

        private static bool PathsEqual(string left, string right) => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

        /// <summary>按规范顺序（构建前置 → 流程驱动 → 验收断言）拼回一份积木列表，供编译/保存。</summary>
        private List<BattleBlock> AllBlocks()
        {
            var all = new List<BattleBlock>(_settings.Count + _setup.Count + _timeline.Count + _assertions.Count);
            all.AddRange(_settings);
            all.AddRange(_setup);
            all.AddRange(_timeline);
            all.AddRange(_assertions);
            return all;
        }

        private List<BattleBlock> ListFor(BattleBlock block) => block.Section switch
        {
            BattleBlockSection.Settings => _settings,
            BattleBlockSection.Timeline => _timeline,
            BattleBlockSection.Assertion => _assertions,
            _ => _setup,
        };

        /// <summary>把调色板模板加入流程：复合积木（宏）递归展开成子积木；原子积木克隆后按 Section 落入对应区块。</summary>
        private void AddTemplate(BattleBlock template)
        {
            if (!_developerMode || _authoring.Count != 0)
            {
                _authoring.Add(BattleFlowCodec.CloneBlock(template));
            }
            else if (template is BattleCompositeBlock composite)
            {
                foreach (var child in composite.Children)
                    AddTemplate(child);
            }
            else
            {
                ListFor(template.Clone()).Add(template.Clone());
            }
        }

        private void LoadBlocks(IEnumerable<BattleBlock> blocks)
        {
            _settings.Clear();
            _setup.Clear();
            _timeline.Clear();
            _assertions.Clear();
            foreach (var block in blocks) ListFor(block).Add(block);
        }

        private BattleFlowSections CaptureSections(bool includeSetup)
        {
            return new BattleFlowSections
            {
                Settings = new List<BattleBlock>(_settings),
                Setup = includeSetup ? new List<BattleBlock>(_setup) : new List<BattleBlock>(),
                Timeline = new List<BattleBlock>(_timeline),
                Assertions = new List<BattleBlock>(_assertions),
            };
        }

        private void LoadSections(BattleFlowSections sections)
        {
            _settings.Clear();
            _setup.Clear();
            _timeline.Clear();
            _assertions.Clear();
            _settings.AddRange(sections.Settings);
            _setup.AddRange(sections.Setup);
            _timeline.AddRange(sections.Timeline);
            _assertions.AddRange(sections.Assertions);
        }

        private BattleFlowDocument CreateCaseDocument()
        {
            return new BattleFlowDocument
            {
                CaseId = _caseId,
                DisplayName = _displayName,
                ScenarioRef = _scenarioRef,
                Tags = new List<string>(_tags),
                Authoring = new List<BattleBlock>(_authoring),
                ExecutionProfileId = _executionProfileId,
                Sections = _authoring.Count == 0
                    ? CaptureSections(includeSetup: string.IsNullOrWhiteSpace(_scenarioRef))
                    : new BattleFlowSections(),
            };
        }

        private BattleSceneDocument CreateSceneDocument(string sceneId)
        {
            return new BattleSceneDocument
            {
                SceneId = sceneId,
                DisplayName = _sceneDisplayName,
                Sections = new BattleFlowSections { Setup = new List<BattleBlock>(_setup) },
            };
        }

        private TestScenario CompileCurrent()
        {
            var document = CreateCaseDocument();
            if (string.IsNullOrWhiteSpace(document.ScenarioRef))
                return BattleFlowCompiler.Compile(document);

            return BattleFlowCompiler.Compile(document, reference =>
            {
                if (_setup.Count != 0)
                {
                    if (string.IsNullOrEmpty(_scenePath))
                        throw new InvalidOperationException("当前 Setup 尚未绑定场景资产，请先保存或加载场景。");
                    return CreateSceneDocument(Path.GetFileNameWithoutExtension(reference));
                }
                var ownerPath = string.IsNullOrEmpty(_casePath)
                    ? Path.Combine(_flowDirectory, _caseId + ".battleflow")
                    : _casePath;
                return BattleFlowCodec.LoadScene(BattleFlowCodec.ResolveScenePath(ownerPath, reference));
            });
        }

        private void LoadDocument(BattleFlowDocument document, string ownerPath)
        {
            BattleFlowDocumentValidator.ThrowIfInvalid(document);
            _caseId = document.CaseId;
            _displayName = document.DisplayName;
            _executionProfileId = document.ExecutionProfileId;
            _scenarioRef = document.ScenarioRef;
            _tags.Clear();
            _tags.AddRange(document.Tags);
            _casePath = ownerPath;
            _scenePath = string.Empty;
            _sceneId = string.Empty;
            _sceneDisplayName = string.Empty;
            RefreshSceneLibrary();

            _authoring.Clear();
            _authoring.AddRange(document.Authoring);
            if (_authoring.Count == 0)
            {
                var sections = document.Sections.HasBlocks
                    ? document.Sections
                    : BattleFlowSections.FromBlocks(document.Blocks);
                LoadSections(sections);
                _developerMode = true;
            }
            else
            {
                LoadSections(new BattleFlowSections());
                _developerMode = false;
            }
            if (string.IsNullOrWhiteSpace(document.ScenarioRef)) return;

            _scenePath = BattleFlowCodec.ResolveScenePath(ownerPath, document.ScenarioRef);
            var scene = BattleFlowCodec.LoadScene(_scenePath);
            BattleFlowDocumentValidator.ThrowIfInvalid(scene);
            _sceneId = scene.SceneId;
            _sceneDisplayName = scene.DisplayName;
            _setup.Clear();
            _setup.AddRange(scene.GetOrderedBlocks());
            RefreshSceneLibrary();
        }

        private bool SceneSetupMatchesFile()
        {
            if (string.IsNullOrEmpty(_scenePath) || !File.Exists(_scenePath)) return false;
            var savedScene = BattleFlowCodec.LoadScene(_scenePath);
            BattleFlowDocumentValidator.ThrowIfInvalid(savedScene);
            return string.Equals(
                SerializeSetup(savedScene.GetOrderedBlocks()),
                SerializeSetup(_setup),
                StringComparison.Ordinal);
        }

        private static string SerializeSetup(IEnumerable<BattleBlock> blocks)
        {
            return BattleFlowCodec.SerializeScene(new BattleSceneDocument
            {
                SceneId = "comparison",
                Sections = new BattleFlowSections { Setup = new List<BattleBlock>(blocks) },
            });
        }

        private static string MakeRelativeAssetReference(string ownerPath, string referencedPath)
        {
            var ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(ownerPath)) ?? Directory.GetCurrentDirectory();
            var ownerUri = new Uri(ownerDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var referenceUri = new Uri(Path.GetFullPath(referencedPath));
            return Uri.UnescapeDataString(ownerUri.MakeRelativeUri(referenceUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private sealed class EditorSnapshot
        {
            public string DocumentJson = string.Empty;
            public string ScenePath = string.Empty;
            public string SceneId = string.Empty;
            public string SceneDisplayName = string.Empty;
            public string CasePath = string.Empty;
        }

        private sealed class SceneLibraryEntry
        {
            public string Path = string.Empty;
            public string Label = string.Empty;
        }

        private static string FormatScenario(TestScenario scenario)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CaseId: " + scenario.CaseId);
            sb.AppendLine("EnvironmentProfileId: " + (scenario.EnvironmentProfileId ?? "(未设置)"));
            var execution = scenario.ResolveExecution();
            sb.AppendLine($"Execution: tickRate={execution.TickRate}, max={execution.MaxDurationMs}ms, settle={execution.SettleDurationMs}ms");
            sb.AppendLine($"EndCondition: {execution.EndCondition.Kind}" +
                          (execution.EndCondition.Kind == TestEndConditionKinds.Duration
                              ? $" ({execution.EndCondition.DurationMs}ms)"
                              : string.Empty));
            sb.AppendLine();
            sb.AppendLine($"Actors ({scenario.Actors.Count})：");
            foreach (var actor in scenario.Actors)
                sb.AppendLine($"  {actor.Alias}  heroId={actor.HeroId}  teamId={actor.TeamId}");
            sb.AppendLine();
            sb.AppendLine($"Timeline ({scenario.Timeline.Count})：");
            foreach (var step in scenario.Timeline)
                sb.AppendLine($"  t={step.AtMs}ms  {step.Action}  {step.ActorAlias ?? "-"} -> {step.TargetAlias ?? "-"}  slot={step.Slot}");
            if (scenario.Expectations != null)
            {
                sb.AppendLine();
                sb.AppendLine("断言: " + scenario.Expectations.GetType().Name);
            }
            return sb.ToString();
        }

        /// <summary>「存为模板」的名字输入对话框。</summary>
        private sealed class SaveTemplateDialog : EditorWindow
        {
            public string TemplateName = "新模板";
            public Action<string>? OnConfirm;

            private void OnGUI()
            {
                EditorGUILayout.LabelField("把当前流程存为模板", EditorStyles.boldLabel);
                TemplateName = EditorGUILayout.TextField("名称", TemplateName);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("确定"))
                {
                    OnConfirm?.Invoke(TemplateName);
                    Close();
                }
                if (GUILayout.Button("取消")) Close();
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>「DSL」文本输入对话框：用一行行命令描述场景，解析成积木。</summary>
        private sealed class DslDialog : EditorWindow
        {
            public string DslText = string.Empty;
            public Action<string>? OnParse;

            private void OnGUI()
            {
                EditorGUILayout.LabelField("DSL 场景描述", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("每行一个命令：env / spawn / cast / wait / obstacle / assert…", MessageType.Info);
                DslText = EditorGUILayout.TextArea(DslText, GUILayout.Height(220));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("解析"))
                {
                    OnParse?.Invoke(DslText);
                    Close();
                }
                if (GUILayout.Button("取消")) Close();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif

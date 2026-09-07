#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbilityKit.BattleFlow;
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
        private readonly List<BattleBlock> _setup = new List<BattleBlock>();
        private readonly List<BattleBlock> _timeline = new List<BattleBlock>();
        private readonly List<BattleBlock> _assertions = new List<BattleBlock>();
        private readonly Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>();
        private string _flowDirectory = "Assets/BattleFlows";
        private string _caseId = "preview-case";
        private string _result = string.Empty;
        private IReadOnlyList<BattleFlowTraceNode>? _traceNodes;
        private readonly Dictionary<long, bool> _traceFoldouts = new Dictionary<long, bool>();
        private readonly Stack<string> _undoStack = new Stack<string>();
        private List<BattleBlock>? _dragSourceList;
        private int _dragSourceIndex = -1;
        private Vector2 _paletteScroll;
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

        private void OnGUI()
        {
            HandleUndoShortcut();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawPalette();
            DrawSection(_setup, "① 构建前置", ref _setupScroll);
            DrawSection(_timeline, "② 流程驱动", ref _timelineScroll);
            DrawSection(_assertions, "③ 验收断言", ref _assertionsScroll);
            EditorGUILayout.EndHorizontal();
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
                if (GUILayout.Button("DSL", EditorStyles.toolbarButton, GUILayout.Width(40))) ParseDsl();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48))) SaveFlow();
                if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(48))) LoadFlow();
                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48)))
                {
                    PushUndo();
                    _setup.Clear();
                    _timeline.Clear();
                    _assertions.Clear();
                    _result = string.Empty;
                    _traceNodes = null;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("CaseId", EditorStyles.miniLabel, GUILayout.Width(40));
                _caseId = EditorGUILayout.TextField(_caseId, EditorStyles.toolbarTextField, GUILayout.Width(140));
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190));
            EditorGUILayout.LabelField("积木调色板", EditorStyles.boldLabel);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);

            foreach (var group in BattleBlockPalette.Groups)
            {
                var expanded = _groupFoldouts.TryGetValue(group.Key, out var e) ? e : true;
                expanded = EditorGUILayout.Foldout(expanded, group.Key, true);
                _groupFoldouts[group.Key] = expanded;
                if (!expanded) continue;

                EditorGUI.indentLevel++;
                foreach (var template in group.Value)
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
                            var doc = BattleFlowCodec.Load(file);
                            _caseId = doc.CaseId;
                            PushUndo();
                            LoadBlocks(doc.Blocks);
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
            var doc = new BattleFlowDocument { CaseId = _caseId, Blocks = AllBlocks() };
            _undoStack.Push(BattleFlowCodec.Serialize(doc));
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                _result = "没有可撤销的操作";
                return;
            }

            var doc = BattleFlowCodec.Parse(_undoStack.Pop());
            LoadBlocks(doc.Blocks);
            _traceNodes = null;
            _result = "已撤销";
        }

        private static string BlockLabel(BattleBlock block) =>
            string.IsNullOrEmpty(block.DisplayName) ? block.GetType().Name : block.DisplayName;

        private static readonly string[] SkillActions = { "cast_skill", "wait", "move_to", "cancel", "press", "release", "hold" };

        private static void DrawBlockFields(BattleBlock block)
        {
            switch (block)
            {
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
                var scenario = BattleFlowCompiler.Compile(_caseId, AllBlocks());
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
                scenario = BattleFlowCompiler.Compile(_caseId, AllBlocks());
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
            if (_setup.Count == 0 && _timeline.Count == 0 && _assertions.Count == 0)
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
                var blocks = AllBlocks().Select(b => b.Clone()).ToList();
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
                    var blocks = BattleFlowDslParser.Parse(text);
                    PushUndo();
                    LoadBlocks(blocks);
                    _traceNodes = null;
                    _result = "已从 DSL 解析 " + blocks.Count + " 个积木";
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
            var path = EditorUtility.SaveFilePanel("保存战斗流程", _flowDirectory, _caseId + ".battleflow", "battleflow");
            if (string.IsNullOrEmpty(path)) return;
            BattleFlowCodec.Save(path, new BattleFlowDocument { CaseId = _caseId, Blocks = AllBlocks() });
            _result = "已保存: " + path;
        }

        private void LoadFlow()
        {
            var path = EditorUtility.OpenFilePanel("加载战斗流程", _flowDirectory, "battleflow");
            if (string.IsNullOrEmpty(path)) return;
            var doc = BattleFlowCodec.Load(path);
            _caseId = doc.CaseId;
            PushUndo();
            LoadBlocks(doc.Blocks);
            _result = "已加载: " + path;
        }

        /// <summary>按规范顺序（构建前置 → 流程驱动 → 验收断言）拼回一份积木列表，供编译/保存。</summary>
        private List<BattleBlock> AllBlocks()
        {
            var all = new List<BattleBlock>(_setup.Count + _timeline.Count + _assertions.Count);
            all.AddRange(_setup);
            all.AddRange(_timeline);
            all.AddRange(_assertions);
            return all;
        }

        private List<BattleBlock> ListFor(BattleBlock block) => block.Section switch
        {
            BattleBlockSection.Timeline => _timeline,
            BattleBlockSection.Assertion => _assertions,
            _ => _setup,
        };

        /// <summary>把调色板模板加入流程：复合积木（宏）递归展开成子积木；原子积木克隆后按 Section 落入对应区块。</summary>
        private void AddTemplate(BattleBlock template)
        {
            if (template is BattleCompositeBlock composite)
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
            _setup.Clear();
            _timeline.Clear();
            _assertions.Clear();
            foreach (var block in blocks) ListFor(block).Add(block);
        }

        private static string FormatScenario(TestScenario scenario)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CaseId: " + scenario.CaseId);
            sb.AppendLine("EnvironmentProfileId: " + (scenario.EnvironmentProfileId ?? "(未设置)"));
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

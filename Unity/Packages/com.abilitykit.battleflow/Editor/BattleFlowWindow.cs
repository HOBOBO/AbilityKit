#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.BattleFlow;
using AbilityKit.Scenario;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BattleFlow.Editor
{
    /// <summary>
    /// 战斗流程编辑器（线性时间轴，test 模式）：积木调色板 + 线性时间轴 + 编译结果。
    /// 沿用 base.editor 的窗口约定（Window/AbilityKit 菜单、toolbar 风格），但不继承主从列表 PlugableWindow——编排窗口是三栏形态。
    /// 运行（headless verdict + trace）是项目级插桩，编辑器只负责「编排 → 编译 → 展示 IR」。
    /// </summary>
    public sealed class BattleFlowWindow : EditorWindow
    {
        private readonly List<BattleBlock> _timeline = new List<BattleBlock>();
        private string _caseId = "preview-case";
        private string _result = string.Empty;
        private Vector2 _paletteScroll;
        private Vector2 _timelineScroll;
        private Vector2 _resultScroll;

        [MenuItem("Window/AbilityKit/Battle Flow")]
        public static void Open()
        {
            var window = GetWindow<BattleFlowWindow>("Battle Flow");
            window.minSize = new Vector2(960, 520);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawPalette();
            DrawTimeline();
            DrawResult();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("编译", EditorStyles.toolbarButton, GUILayout.Width(48))) Compile();
                if (GUILayout.Button("运行", EditorStyles.toolbarButton, GUILayout.Width(48))) Run();
                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48)))
                {
                    _timeline.Clear();
                    _result = string.Empty;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("CaseId", EditorStyles.miniLabel, GUILayout.Width(40));
                _caseId = EditorGUILayout.TextField(_caseId, EditorStyles.toolbarTextField, GUILayout.Width(140));
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            EditorGUILayout.LabelField("积木调色板", EditorStyles.boldLabel);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);

            foreach (var template in BattleBlockPalette.Templates)
            {
                if (GUILayout.Button(template.DisplayName))
                {
                    // 复合积木是可复用宏，直接加入；原子积木克隆成可编辑实例。
                    _timeline.Add(template is BattleCompositeBlock ? template : template.Clone());
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTimeline()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(360));
            EditorGUILayout.LabelField("线性时间轴", EditorStyles.boldLabel);
            _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll);

            for (var i = 0; i < _timeline.Count; i++)
            {
                DrawTimelineItem(i);
            }

            if (_timeline.Count == 0)
                EditorGUILayout.HelpBox("从左侧调色板添加积木。", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTimelineItem(int index)
        {
            var block = _timeline[index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{index}] {block.DisplayName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↑", GUILayout.Width(22)) && index > 0) MoveBlock(index, index - 1);
            if (GUILayout.Button("↓", GUILayout.Width(22)) && index < _timeline.Count - 1) MoveBlock(index, index + 1);
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                _timeline.RemoveAt(index);
                return;
            }
            EditorGUILayout.EndHorizontal();

            DrawBlockFields(block);
            EditorGUILayout.EndVertical();
        }

        private void MoveBlock(int from, int to)
        {
            var item = _timeline[from];
            _timeline.RemoveAt(from);
            _timeline.Insert(to, item);
        }

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

            // 未知积木（项目自定义，如断言积木）：用 Odin PropertyTree 画 inspect 风格字段
            var tree = Sirenix.OdinInspector.Editor.PropertyTree.Create(block);
            tree.Draw(false);
        }

        private void DrawResult()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("编译结果", EditorStyles.boldLabel);
            _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll);
            EditorGUILayout.TextArea(_result, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void Compile()
        {
            try
            {
                var scenario = BattleFlowCompiler.Compile(_caseId, _timeline);
                var errors = TestScenarioValidator.Validate(scenario);
                _result = errors.Count == 0
                    ? FormatScenario(scenario)
                    : "编译校验失败：\n" + string.Join("\n", errors);
            }
            catch (Exception ex)
            {
                _result = "编译异常：" + ex.Message;
            }
        }

        private void Run()
        {
            TestScenario scenario;
            try
            {
                scenario = BattleFlowCompiler.Compile(_caseId, _timeline);
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
            }
            catch (Exception ex)
            {
                _result = "运行异常：" + ex.Message;
            }
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
            return sb.ToString();
        }
    }
}
#endif

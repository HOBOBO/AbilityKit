#if UNITY_EDITOR
#nullable enable annotations
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BattleScenario.Editor
{
    public sealed partial class BattleScenarioWindow
    {
        internal static IReadOnlyList<(string Label, LocalStateSyncReplay.SkillReplayCase Recipe)> StateSyncShowcases { get; } = Array.AsReadOnly(new[]
        {
            ("施法与移动/移动＋前摇 · 恢复后追平", LocalStateSyncReplay.SkillReplayCase.BattleCastMoveWindup),
            ("施法与移动/拉克丝 Q · 发射前拒绝并保留转向", LocalStateSyncReplay.SkillReplayCase.BattleCastMoveRejectBeforeRelease),
            ("施法与移动/恢复在途前摇", LocalStateSyncReplay.SkillReplayCase.BattleCastWindup),
            ("施法与移动/权威确认 · 保留 W 冷却，重演 E", LocalStateSyncReplay.SkillReplayCase.BattleCastConfirmed),
            ("施法与移动/权威拒绝 · 撤销 W，重演 E", LocalStateSyncReplay.SkillReplayCase.BattleCastRejected),
            ("逻辑弹体/恢复权威在途弹体", LocalStateSyncReplay.SkillReplayCase.BattleFlightRestore),
            ("逻辑弹体/重演输入并生成", LocalStateSyncReplay.SkillReplayCase.BattleFlightReplay),
            ("逻辑弹体/权威拒绝且不复活", LocalStateSyncReplay.SkillReplayCase.BattleFlightReject),
        });

        private void DrawStateSyncSamplesToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("状态同步复现样例", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("选样例", "选择后自动预览；需要时可切换逐 Tick 检查"), EditorStyles.toolbarDropDown))
                        DrawStateSyncSampleMenu(GUILayoutUtility.GetLastRect());
                    GUILayout.Space(12);
                    if (GUILayout.Button(new GUIContent("导入验收…", "只读打开本项目测试导出的回放，不执行当前草稿"), EditorStyles.toolbarButton))
                        ImportStateSyncReplay();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("本地运行 · 不修改草稿", EditorStyles.miniLabel);
            }
        }

        private void DrawStateSyncSampleMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            foreach (var sample in StateSyncShowcases)
            {
                var item = new GUIContent(sample.Label);
                if (capture != null)
                    menu.AddItem(item, false, () => OpenCapturedStateSyncReplay(() => capture(sample.Recipe), sample.Label));
                else
                    menu.AddDisabledItem(item);
            }
            menu.DropDown(anchor);
        }

        private void OpenCapturedStateSyncReplay(Func<LocalStateSyncReplay> capture,
            string label)
        {
            try
            {
                var replay = capture();
                replay.Validate();
                BattleScenarioLocalReplayWindow.OpenAcceptance(replay, "本地执行 · 正式状态同步客户端",
                    label, autoPreview: true);
                _result = "已在本地运行正式状态同步客户端并自动预览，未修改草稿。";
            }
            catch (Exception error)
            {
                _result = "无法运行本地状态同步样例：" + error.Message;
                Debug.LogException(error);
            }
        }

        private void DrawDraftReplayMenu()
        {
            if (!GUILayout.Button(new GUIContent("纠偏演练", "使用当前草稿生成离线演练，或载入一份可编辑样例"),
                    EditorStyles.toolbarDropDown, GUILayout.Width(84)))
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("从当前草稿回放"), false, PreviewCorrectionDraft);
            menu.AddItem(new GUIContent("载入纠偏样例…"), false, LoadCorrectionRecipe);
            menu.DropDown(GUILayoutUtility.GetLastRect());
        }

        private void LoadCorrectionRecipe()
        {
            if (IsDraftDirty() && !EditorUtility.DisplayDialog("载入纠偏样例",
                "当前修改尚未保存。要放弃修改并载入一份样例草稿吗？", "放弃并载入", "取消"))
                return;

            LoadDocument(LocalCorrectionPreview.CreateRecipe(), string.Empty, string.Empty, null);
            _savedCaseSource = null;
            _savedScenePath = string.Empty;
            _savedSceneSource = null;
            _undoStack.Clear();
            _traceNodes = null;
            _result = "已载入本地纠偏草稿。修改移动量、交付时间或权威位置后，选择「纠偏演练 → 从当前草稿回放」。";
        }

        private void PreviewCorrectionDraft()
        {
            _traceNodes = null;
            try
            {
                var scenario = CompileCurrent();
                var frames = LocalCorrectionPreview.Compile(scenario);
                BattleScenarioLocalReplayWindow.Open(scenario.CaseId, frames);
                _result = $"已从当前草稿生成 {frames.Count} 个回放步骤，不改写资产。";
            }
            catch (Exception error)
            {
                _result = "草稿演练未启动：" + error.Message + " 可从「纠偏演练」载入可编辑样例。";
            }
        }

        private void ImportStateSyncReplay()
        {
            var replay = BattleScenarioLocalReplayWindow.SelectAcceptance(out var source, out var error);
            if (replay != null)
            {
                BattleScenarioLocalReplayWindow.OpenAcceptance(replay, source);
                _result = $"已加载 {replay.CaseId} / {replay.Variant} 的 {replay.Checkpoints.Length} 个检查点，未运行场景或改写草稿。";
            }
            else if (error != null)
                _result = error;
        }

        private static void DrawCorrectionCommand(CommandBlock command)
        {
            command.AtMs = EditorGUILayout.IntField("虚拟时间 (ms)", command.AtMs);
            if (command.Name == LocalCorrectionPreview.Move)
            {
                DrawPreviewParameter(command, LocalCorrectionPreview.Delta, "移动量");
                return;
            }
            DrawPreviewParameter(command, LocalCorrectionPreview.AnchorTick, "权威锚点 Tick");
            DrawPreviewParameter(command, LocalCorrectionPreview.Position, "权威位置");
        }

        private static void DrawPreviewParameter(CommandBlock command, string key, string label)
        {
            command.Parameters.TryGetValue(key, out var previous);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUILayout.TextField(label, previous ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
                command.Parameters = new Dictionary<string, string>(command.Parameters, StringComparer.Ordinal) { [key] = value };
        }
    }
}
#endif

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
        private const string StateSyncSampleDirectory = "Packages/com.abilitykit.battlescenario/Editor/ReplaySamples/";

        private void DrawStateSyncSamplesToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("状态同步复现样例", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("选样例", "查看本地状态同步测试录制"), EditorStyles.toolbarDropDown))
                        DrawStateSyncSampleMenu(GUILayoutUtility.GetLastRect());
                    GUILayout.Space(12);
                    if (GUILayout.Button(new GUIContent("导入验收…", "只读打开本项目测试导出的回放，不执行当前草稿"), EditorStyles.toolbarButton))
                        ImportStateSyncReplay();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("只读 · 本地测试录制", EditorStyles.miniLabel);
            }
        }

        private void DrawStateSyncSampleMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            Add("导入失败后重试 · 预测回退并重演", "import-failure-default");
            Add("固定基线恢复 · 有终结证明", "fixed-baseline-recovery-with-proof");
            Add("固定基线恢复 · 无终结证明", "fixed-baseline-recovery-without-proof");
            Add("重演历史缺口 · 暂停预测后恢复", "replay-history-gap-default");
            Add("会话换代 · 忽略旧代际结果", "generation-switch-default");
            Add("部分提交失败 · 等待新全量", "partial-commit-failure-default");
            Add("MOBA · 古拉加斯 E/权威确认 · 无需重演", "gragas-e-local-statesync.confirmed");
            Add("MOBA · 古拉加斯 E/权威命中 · 纠偏并重演移动", "gragas-e-local-statesync.corrected");
            Add("MOBA · 古拉加斯 E/缺少终结证明 · 仅显示权威", "gragas-e-local-statesync.without-proof");
            Add("MOBA · 古拉加斯 E/移动历史缺口 · 仅显示权威", "gragas-e-local-statesync.history-gap");
            menu.DropDown(anchor);

            void Add(string label, string fileName) =>
                menu.AddItem(new GUIContent(label), false, () => OpenStateSyncSample(fileName, label));
        }

        private void OpenStateSyncSample(string fileName, string label)
        {
            var path = StateSyncSampleDirectory + fileName + ".statesync-replay.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                _result = "找不到内置状态同步样例，请刷新项目资源后重试：" + path;
                return;
            }

            try
            {
                var replay = LocalStateSyncReplay.Parse(asset.text);
                BattleScenarioLocalReplayWindow.OpenAcceptance(replay, path, label);
                _result = $"已打开内置录制：{label}。未运行场景或改写草稿。";
            }
            catch (Exception error) when (error is InvalidDataException or JsonException)
            {
                _result = "内置状态同步样例无法打开：" + error.Message;
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

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Game.Editor;
using AbilityKit.Game.Flow;
using AbilityKit.Scenario;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    /// <summary>
    /// 将仓库内的技能场景交给正式 MOBA Demo 的本地会话播放。
    /// </summary>
    [InitializeOnLoad]
    public static class MobaBattleScenarioPreview
    {
        private const string PendingKey = "AbilityKit.Moba.SkillScenarioPreview.Path";
        private const string PreviewOnlyKey = "AbilityKit.Moba.SkillScenarioPreview.PreviewOnly";
        private const string PlayMenuPath = "Tools/AbilityKit/Demos/MOBA/Play Skill Scenario";
        private const string PreviewMenuPath = "Tools/AbilityKit/Demos/MOBA/Preview Skill Scenario";
        private static string FlowsDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../../src",
            "AbilityKit.Demo.Moba.BattleScenario.Runner/Flows"));
        private static MobaConfigDatabase _searchConfig;
        private static double _deadline;
        private static TestScenario _scenario;

        static MobaBattleScenarioPreview()
        {
            BattleScenarioEditorIntegration.CaseDirectories = new[] { FlowsDirectory };
            BattleScenarioEditorIntegration.SearchTerms = GetSearchTerms;
            BattleScenarioEditorIntegration.PlaySavedCase = StartPreview;
            BattleScenarioEditorIntegration.CreateActorReplayPreview = CreateActorReplayPreview;
            BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay = MobaStateSyncClientReplay.CaptureSkill;
            BattleScenarioEditorIntegration.CaptureProjectilePresentationReplay = MobaBattleScenarioRunner.CaptureProjectilePresentationReplay;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            var path = SessionState.GetString(PendingKey, "");
            if (path != "") Arm(path);
        }

        private static IActorReplayPreview CreateActorReplayPreview(int modelId)
        {
            var preview = BattleActorReplayPreview.Create(modelId);
            return preview == null ? null : new ActorReplayPreviewAdapter(preview);
        }

        private sealed class ActorReplayPreviewAdapter : IFlightReplayPreview
        {
            private readonly BattleActorReplayPreview _preview;

            internal ActorReplayPreviewAdapter(BattleActorReplayPreview preview) => _preview = preview;

            public void Draw(Rect rect, Bounds recordedBounds, Pose? authority, Pose? prediction) =>
                _preview.Draw(rect, recordedBounds, authority, prediction);

            public void SetFlightPositions(Vector3? authority, Vector3? prediction) =>
                _preview.SetFlightPositions(authority, prediction);

            public void Dispose() => _preview.Dispose();
        }

        private static IEnumerable<string> GetSearchTerms(BattleScenarioDocument document)
        {
            var blocks = document.GetOrderedBlocks();
            var steps = blocks.OfType<TimelineStepBlock>().Where(step => step.Slot > 0).ToArray();
            if (steps.Length == 0) yield break;

            const string slotNames = " QWER";
            foreach (var step in steps)
            {
                if (step.Slot < slotNames.Length) yield return slotNames[step.Slot].ToString();
            }

            foreach (var actor in blocks.OfType<SpawnActorBlock>())
            {
                var actorSteps = steps.Where(step => step.ActorAlias == actor.Alias).ToArray();
                if (actorSteps.Length == 0) continue;
                _searchConfig ??= MobaConfigLoader.LoadDefault();
                if (!_searchConfig.TryGetCharacter(actor.HeroId, out var hero)) continue;
                yield return hero.Name;
                foreach (var step in actorSteps)
                {
                    if (step.Slot < slotNames.Length) yield return hero.Name + slotNames[step.Slot];
                }
            }
        }

        [MenuItem(PlayMenuPath, priority = 14)]
        private static void SelectInteractiveCase() => SelectCase(previewOnly: false);

        [MenuItem(PreviewMenuPath, priority = 15)]
        private static void SelectPreviewCase() => SelectCase(previewOnly: true);

        private static void SelectCase(bool previewOnly)
        {
            var directory = Path.Combine(FlowsDirectory, "SkillPresentation");
            if (!Directory.Exists(directory))
            {
                Debug.LogError($"找不到技能预览场景目录：{directory}");
                return;
            }
            var path = EditorUtility.OpenFilePanel("选择仓库内可信的技能预览场景", directory, "battlescenario");
            if (string.IsNullOrEmpty(path)) return;
            try { StartPreview(path, previewOnly); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem(PlayMenuPath, validate = true)]
        [MenuItem(PreviewMenuPath, validate = true)]
        private static bool CanStartPreview() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// 预检可信仓库用例，并启动一场新的本地战斗。返回 false 表示未进入 Play Mode。
        /// </summary>
        public static bool StartPreview(string path, bool previewOnly = false)
        {
            if (!PreparePreview(path, previewOnly)) return false;
            EditorApplication.EnterPlaymode();
            return true;
        }

        /// <summary>
        /// 打开正式场景并准备下一次 Play Mode；允许 Unity 测试运行器负责切换模式。
        /// 纯预览不装配战斗 HUD 和手动输入，并在时间线结束后暂停编辑器。
        /// </summary>
        public static bool PreparePreview(string path, bool previewOnly = false)
        {
            if (!CanStartPreview()) return false;
            var scenario = BattleScenarioCompiler.CompileFile(path);
            MobaBattleScenarioPlayback.Validate(scenario);
            if (!MobaDemoSceneMenu.TryOpenExistingDemoScene()) return false;
            SessionState.SetString(PendingKey, Path.GetFullPath(path));
            SessionState.SetBool(PreviewOnlyKey, previewOnly);
            _scenario = scenario;
            MobaBattleScenarioPlayback.SetPendingScenario(scenario, previewOnly);
            MobaDemoSceneMenu.ScheduleLocalDemoLaunch();
            return true;
        }

        private static void Arm(string path)
        {
            try
            {
                _scenario = BattleScenarioCompiler.CompileFile(path);
                MobaBattleScenarioPlayback.Validate(_scenario);
                MobaBattleScenarioPlayback.SetPendingScenario(_scenario, SessionState.GetBool(PreviewOnlyKey, false));
            }
            catch (Exception error)
            {
                MobaBattleScenarioPlayback.SetPendingScenario(null);
                _scenario = null;
                Debug.LogException(error);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(PendingKey, "") != "")
            {
                _deadline = EditorApplication.timeSinceStartup + 60d;
                EditorApplication.update -= TryPlay;
                EditorApplication.update += TryPlay;
            }
            else if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= TryPlay;
                ClearPendingKeys();
                MobaBattleScenarioPlayback.SetPendingScenario(null);
                _scenario = null;
            }
        }

        private static void ClearPendingKeys()
        {
            SessionState.EraseString(PendingKey);
            SessionState.EraseBool(PreviewOnlyKey);
        }

        private static void TryPlay()
        {
            if (!GameEntry.IsInitialized && EditorApplication.timeSinceStartup <= _deadline) return;
            EditorApplication.update -= TryPlay;
            var previewOnly = SessionState.GetBool(PreviewOnlyKey, false);
            ClearPendingKeys();
            try
            {
                if (!GameEntry.IsInitialized) throw new TimeoutException("未能启动正式 MOBA Demo 场景。");
                var scenario = _scenario ?? throw new InvalidOperationException("技能预览没有已准备的用例。");
                _scenario = null;
                Debug.Log($"[SkillScenarioPreview] 开始：{scenario.CaseId}");
                var entry = GameEntry.Instance;
                entry.RunCoroutine(MobaBattleScenarioPlayback.Play(entry, scenario,
                    error => FinishPlayback(scenario.CaseId, previewOnly, error), previewOnly));
            }
            catch (Exception error)
            {
                _scenario = null;
                MobaBattleScenarioPlayback.SetPendingScenario(null);
                FinishPlayback(null, previewOnly, error);
            }
        }

        private static void FinishPlayback(string caseId, bool previewOnly, Exception error)
        {
            if (error != null)
            {
                Debug.LogException(error);
                if (previewOnly) EditorApplication.ExitPlaymode();
                return;
            }
            if (previewOnly) EditorApplication.isPaused = true;
            Debug.Log(previewOnly
                ? $"[SkillScenarioPreview] 纯预览完成：{caseId}。已暂停；停止 Play Mode 后可重新播放。"
                : $"[SkillScenarioPreview] 播放完成：{caseId}。画面需在 Game 视图复核。");
        }
    }
}
#endif

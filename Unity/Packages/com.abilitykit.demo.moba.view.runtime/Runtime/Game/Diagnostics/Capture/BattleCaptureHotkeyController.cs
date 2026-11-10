using System;
using System.IO;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Diagnostics.Capture
{
    /// <summary>Development-only F8 switch for the generic L1 HUD capture.</summary>
    internal sealed class BattleCaptureHotkeyController
    {
        public const KeyCode ToggleKey = KeyCode.F8;
        private readonly string _rootDirectory;
        private readonly string _caseId;
        private string _activeRunId;
        private string _activeDirectory;

        public BattleCaptureHotkeyController(string rootDirectory = null, string caseId = "runtime-input")
        {
            _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "moba-capture")
                : rootDirectory;
            _caseId = string.IsNullOrWhiteSpace(caseId) ? "runtime-input" : caseId;
        }

        public void Tick(BattleHudFeature hud, int frame)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hud == null || !Input.GetKeyDown(ToggleKey)) return;
            if (hud.IsInputCaptureActive)
            {
                hud.MarkInputCapture("capture.stop.hotkey", frame);
                hud.StopInputCapture();
                Debug.Log($"[BattleCapture] OFF caseId={_caseId} runId={_activeRunId ?? "unknown"} directory={_activeDirectory ?? _rootDirectory} status=closed");
                _activeRunId = null;
                _activeDirectory = null;
                return;
            }

            var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            if (!hud.TryStartInputCapture(_rootDirectory, _caseId, runId, out var error))
            {
                var intendedDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, _caseId, runId));
                Debug.LogWarning($"[BattleCapture] START FAILED caseId={_caseId} runId={runId} directory={intendedDirectory} error={error}");
                return;
            }

            _activeRunId = runId;
            _activeDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, _caseId, runId));
            hud.MarkInputCapture("capture.start.hotkey", frame);
            Debug.Log($"[BattleCapture] ON caseId={_caseId} runId={runId} directory={_activeDirectory} status=active");
#endif
        }
    }
}

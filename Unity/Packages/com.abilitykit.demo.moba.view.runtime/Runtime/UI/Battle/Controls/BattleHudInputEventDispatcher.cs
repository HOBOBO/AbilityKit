using System.Collections.Generic;
using AbilityKit.Game.Battle.Diagnostics.Capture;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputEventDispatcher
    {
        private readonly IBattleHudInputSink _hudInput;
        private IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> _skillSpecs;
        private IBattleCaptureSink _capture;
        private IBattleCaptureBindingHost _captureFrameHost;

        public BattleHudInputEventDispatcher(IBattleHudInputSink hudInput) { _hudInput = hudInput; }
        public void SetSkillSpecs(IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs) { _skillSpecs = skillSpecs; }
        public void SetCaptureSink(IBattleCaptureSink capture)
        {
            if (_capture != null && _hudInput is IBattleCaptureBindingHost previousHost)
                previousHost.ClearBattleCapture(_capture);
            _capture = capture;
            _captureFrameHost = _hudInput as IBattleCaptureBindingHost;
            if (_captureFrameHost != null) _captureFrameHost.BindBattleCapture(capture);
        }
        private int CaptureFrame => _captureFrameHost?.BattleCaptureFrame ?? 0;
        public void ResetHudAim() { _hudInput?.CancelHudSkillAim(); }
        public void OnMoveBegin() { _capture?.RecordHudEvent(CaptureFrame, "move.begin", 0, 0, 0f, 0f, 0f, 0f); _hudInput?.BeginHudMove(); }
        public void OnMoveEnd() { _capture?.RecordHudEvent(CaptureFrame, "move.end", 0, 0, 0f, 0f, 0f, 0f); _hudInput?.EndHudMove(); }
        public void OnMoveDxDzChanged(float dx, float dz) { _capture?.RecordHudEvent(CaptureFrame, "move.update", 0, 0, dx, dz, dx, dz); _hudInput?.SetHudMove(dx, dz); }
        public void OnSkillClick(int slot)
        {
            _capture?.RecordHudEvent(CaptureFrame, "skill.click", slot, ResolveSkillId(slot), 0f, 0f, 0f, 0f);
            _hudInput?.CancelHudSkillAim();
            _hudInput?.SubmitHudSkillClick(slot);
        }
        public event System.Action<int> SkillPreviewPressed;
        public event System.Action<int> SkillPreviewReleased;
        public void OnSkillPreviewPressed(int slot) => SkillPreviewPressed?.Invoke(slot);
        public void OnSkillPreviewReleased(int slot) => SkillPreviewReleased?.Invoke(slot);
        public void OnSkillAimStart(int slot, Vector2 aim)
        {
            var raw = aim; aim = ResolveWorldAim(slot, aim);
            _capture?.RecordHudEvent(CaptureFrame, "skill.aim_start", slot, ResolveSkillId(slot), raw.x, raw.y, aim.x, aim.y);
            _hudInput?.SetHudSkillAim(slot, aim.x, aim.y, aiming: true);
        }
        public void OnSkillAimUpdate(int slot, Vector2 aim)
        {
            var raw = aim; aim = ResolveWorldAim(slot, aim);
            _capture?.RecordHudEvent(CaptureFrame, "skill.aim_update", slot, ResolveSkillId(slot), raw.x, raw.y, aim.x, aim.y);
            _hudInput?.SetHudSkillAim(slot, aim.x, aim.y, aiming: true);
        }
        public void OnSkillAimCancel()
        {
            _capture?.RecordHudEvent(CaptureFrame, "skill.aim_cancel", 0, 0, 0f, 0f, 0f, 0f);
            _hudInput?.CancelHudSkillAim();
        }
        public void OnSkillAimEnd(int slot, Vector2 aim)
        {
            var raw = aim; aim = ResolveWorldAim(slot, aim);
            _capture?.RecordHudEvent(CaptureFrame, "skill.aim_end", slot, ResolveSkillId(slot), raw.x, raw.y, aim.x, aim.y);
            // PointerUp may arrive at a different coordinate than the last Drag event.
            // Keep the visible preview on the exact release snapshot before submitting it.
            _hudInput?.SetHudSkillAim(slot, aim.x, aim.y, aiming: true);
            var includeAimPosition = _skillSpecs == null
                                     || !_skillSpecs.TryGetValue(slot, out var spec)
                                     || spec.AimMode == AbilityKit.Game.Battle.View.Lib.Skill.SkillAimMode.Point
                                     || spec.UsesTargetPoint;
            _hudInput?.SubmitHudSkillAim(slot, aim.x, aim.y, includeAimPosition);
        }
        private Vector2 ResolveWorldAim(int slot, Vector2 aim)
        {
            if (_skillSpecs == null || !_skillSpecs.TryGetValue(slot, out var spec)) return aim;
            if (!spec.UsesTargetPoint
                && spec.AimMode != AbilityKit.Game.Battle.View.Lib.Skill.SkillAimMode.Point) return aim;
            var range = Mathf.Max(0f, spec.Range);
            if (range <= 0f) return aim;
            var magnitude = aim.magnitude;
            if (magnitude <= 0.0001f) return Vector2.zero;
            var normalized = magnitude > 1f ? aim / magnitude : aim;
            return normalized * range;
        }

        private int ResolveSkillId(int slot)
        {
            return _skillSpecs != null && _skillSpecs.TryGetValue(slot, out var spec)
                ? spec.SkillId
                : 0;
        }
    }
}

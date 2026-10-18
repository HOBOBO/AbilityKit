using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputEventDispatcher
    {
        private readonly IBattleHudInputSink _hudInput;
        private IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> _skillSpecs;
 
        public BattleHudInputEventDispatcher(IBattleHudInputSink hudInput)
        {
            _hudInput = hudInput;
        }

        public void SetSkillSpecs(IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs)
        {
            _skillSpecs = skillSpecs;
        }

        public void ResetHudAim()
        {
            _hudInput?.CancelHudSkillAim();
        }

        public void OnMoveBegin()
        {
            _hudInput?.BeginHudMove();
        }

        public void OnMoveEnd()
        {
            _hudInput?.EndHudMove();
        }

        public void OnMoveDxDzChanged(float dx, float dz)
        {
            _hudInput?.SetHudMove(dx, dz);
        }

        public void OnSkillClick(int slot)
        {
            _hudInput?.CancelHudSkillAim();
            _hudInput?.SubmitHudSkillClick(slot);
        }

        public void OnSkillAimStart(int slot, Vector2 aim)
        {
            aim = ResolveWorldAim(slot, aim);
            _hudInput?.SetHudSkillAim(slot, aim.x, aim.y, aiming: true);
        }
 
        public void OnSkillAimUpdate(int slot, Vector2 aim)
        {
            aim = ResolveWorldAim(slot, aim);
            _hudInput?.SetHudSkillAim(slot, aim.x, aim.y, aiming: true);
        }
 
        public void OnSkillAimCancel()
        {
            _hudInput?.CancelHudSkillAim();
        }

        public void OnSkillAimEnd(int slot, Vector2 aim)
        {
            aim = ResolveWorldAim(slot, aim);
            _hudInput?.SubmitHudSkillAim(slot, aim.x, aim.y);
        }

        private Vector2 ResolveWorldAim(int slot, Vector2 aim)
        {
            if (_skillSpecs == null || !_skillSpecs.TryGetValue(slot, out var spec)) return aim;
            if (!spec.UsesTargetPoint) return aim;

            var range = Mathf.Max(0f, spec.Range);
            if (range <= 0f) return aim;

            var magnitude = aim.magnitude;
            if (magnitude <= 0.0001f) return Vector2.zero;

            var normalized = magnitude > 1f ? aim / magnitude : aim;
            return normalized * range;
        }
    }
}

using System.Collections.Generic;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudAimPreview
    {
        private readonly BattleHudAimPreviewPositionResolver _positions;
        private readonly BattleHudAimPreviewObjectFactory _objects;
        private const float SubmittedPreviewDurationSeconds = 0.45f;

        private IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> _skillSpecs;
        private BattleHudAimPreviewObject _preview;
        private int _lastSubmissionVersion;
        private float _submittedPreviewRemainingSeconds;

        public BattleHudAimPreview(
            BattleHudAimPreviewPositionResolver positions = null,
            BattleHudAimPreviewObjectFactory objects = null)
        {
            _positions = positions ?? new BattleHudAimPreviewPositionResolver();
            _objects = objects ?? new BattleHudAimPreviewObjectFactory();
        }

        internal GameObject PreviewRoot => _preview?.Root;

        public void SetSkillSpecs(IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs)
        {
            _skillSpecs = skillSpecs;
        }

        public void Tick(BattleContext ctx, float deltaTime = 0f)
        {
            if (!_positions.TryResolve(ctx, out var state) || !TryGetSpec(state.Slot, out var spec))
            {
                Hide();
                return;
            }

            if (state.SubmissionVersion > 0)
            {
                if (state.SubmissionVersion != _lastSubmissionVersion)
                {
                    _lastSubmissionVersion = state.SubmissionVersion;
                    // LockProjectile 使用 spec.LockOnDurationSeconds 控制瞄准停留时间；其它形状用统一的短停留
                    _submittedPreviewRemainingSeconds = spec.PreviewShape == BattleHudSkillPreviewShape.LockProjectile
                        ? Mathf.Max(SubmittedPreviewDurationSeconds, spec.LockOnDurationSeconds)
                        : SubmittedPreviewDurationSeconds;
                }
                else
                {
                    _submittedPreviewRemainingSeconds -= Mathf.Max(0f, deltaTime);
                }

                if (_submittedPreviewRemainingSeconds <= 0f)
                {
                    Hide();
                    return;
                }
            }
            else
            {
                _submittedPreviewRemainingSeconds = 0f;
            }

            EnsurePreview();
            _preview.Apply(state, spec);
        }

        public void Clear()
        {
            if (_preview != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_preview.Root);
                }
                else
                {
                    Object.DestroyImmediate(_preview.Root);
                }
            }

            _preview = null;
            _lastSubmissionVersion = 0;
            _submittedPreviewRemainingSeconds = 0f;
        }

        private bool TryGetSpec(int slot, out BattleHudSkillPresentationSpec spec)
        {
            if (_skillSpecs != null &&
                _skillSpecs.TryGetValue(slot, out spec) &&
                spec.PreviewShape != BattleHudSkillPreviewShape.None)
            {
                return true;
            }

            spec = default;
            return false;
        }

        private void Hide()
        {
            if (_preview != null)
            {
                _preview.SetVisible(false);
            }
        }

        private void EnsurePreview()
        {
            if (_preview != null) return;

            _preview = _objects.Create();
        }
    }
}

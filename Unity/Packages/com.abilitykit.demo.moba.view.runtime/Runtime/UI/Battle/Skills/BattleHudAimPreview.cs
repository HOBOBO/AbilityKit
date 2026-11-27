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
        private readonly BattleViewResourceProvider _resources;
        private const float SubmittedPreviewDurationSeconds = 0.45f;

        private IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> _skillSpecs;
        private IReadOnlyDictionary<int, int> _skillLevels;
        private BattleHudAimPreviewObject _preview;
        private int _lastSubmissionVersion;
        private float _submittedPreviewRemainingSeconds;
        private int _pressedSlot;

        public BattleHudAimPreview(
            BattleHudAimPreviewPositionResolver positions = null,
            BattleHudAimPreviewObjectFactory objects = null,
            BattleViewResourceProvider resources = null)
        {
            _positions = positions ?? new BattleHudAimPreviewPositionResolver();
            _objects = objects ?? new BattleHudAimPreviewObjectFactory();
            _resources = BattleViewResourceProvider.OrDefault(resources);
        }

        internal GameObject PreviewRoot => _preview?.Root;

        public void SetSkillSpecs(IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs)
        {
            _skillSpecs = skillSpecs;
        }

        public void ShowPressed(int slot, IBattleHudAimPreviewReadPort input)
        {
            TryShowPressed(slot, input, allowVfx: true);
        }

        internal void SetSkillLevels(IReadOnlyDictionary<int, int> skillLevels) => _skillLevels = skillLevels;

        public void HidePressed(int slot)
        {
            if (_pressedSlot != slot) return;
            _pressedSlot = 0;
            Hide();
        }

        public void Tick(IBattleHudAimPreviewReadPort input, float deltaTime = 0f)
        {
            if (!_positions.TryResolve(input, out var state) || !TryGetSpec(state.Slot, out var spec))
            {
                if (_pressedSlot > 0 && TryShowPressed(_pressedSlot, input, allowVfx: false)) return;
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

            EnsurePreview(spec.PreviewVfxId);
            Apply(state, spec);
        }

        private bool TryShowPressed(int slot, IBattleHudAimPreviewReadPort input, bool allowVfx)
        {
            if (!TryGetSpec(slot, out var spec) || input == null ||
                !(allowVfx && spec.PreviewVfxId > 0 || !spec.EnableAim && spec.PreviewShape == BattleHudSkillPreviewShape.SelfCircle) ||
                !input.TryResolveLocalActorWorldPosition(out var x, out var y, out var z)) return false;

            var state = new BattleHudAimPreviewState(slot, new Vector3(x, y, z), Vector3.forward, spec.Range);
            EnsurePreview(spec.PreviewVfxId);
            _pressedSlot = slot;
            Apply(state, spec);
            return true;
        }

        private void Apply(in BattleHudAimPreviewState state, in BattleHudSkillPresentationSpec spec)
        {
            var length = 0f;
            var parameter = _preview.RectangleLengthParameter;
            if (!string.IsNullOrEmpty(parameter))
            {
                var level = 1;
                if (_skillLevels != null && _skillLevels.TryGetValue(state.Slot, out var observed))
                    level = Mathf.Max(1, observed);
                var configs = _resources.GetOrLoadConfigs();
                if (configs != null && configs.TryGetSkill(spec.SkillId, out var skill) &&
                    configs.TryGetSkillLevelTable(skill.LevelTableId, out var table))
                    table.TryGetParameter(level, parameter, out length);
            }
            _preview.Apply(state, spec, length);
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
            _previewVfxId = 0;
            _pressedSlot = 0;
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

        private void EnsurePreview(int previewVfxId)
        {
            if (_preview != null)
            {
                if (_previewVfxId == previewVfxId) return;
                Clear();
            }

            _previewVfxId = previewVfxId;
            _preview = _objects.Create(previewVfxId);
        }

        private int _previewVfxId;
    }
}

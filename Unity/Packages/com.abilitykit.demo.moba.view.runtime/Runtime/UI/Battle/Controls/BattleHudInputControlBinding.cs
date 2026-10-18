using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    internal sealed class BattleHudInputControlBinding
    {
        private readonly BattleHudSkillButtonBridgeSet _skillButtons;

        private JoystickAreaView _moveJoystick;
        private SkillButtonView[] _skills = Array.Empty<SkillButtonView>();
        private bool _isBound;

        public BattleHudInputControlBinding(BattleHudSkillButtonBridgeSet skillButtons = null)
        {
            _skillButtons = skillButtons ?? new BattleHudSkillButtonBridgeSet();
        }

        public event Action<JoystickOutput> MoveChanged;

        public event Action<int> SkillClick
        {
            add => _skillButtons.Click += value;
            remove => _skillButtons.Click -= value;
        }

        public event Action<int> SkillLongPress
        {
            add => _skillButtons.LongPress += value;
            remove => _skillButtons.LongPress -= value;
        }

        public event Action<int, Vector2> SkillAimStart
        {
            add => _skillButtons.AimStart += value;
            remove => _skillButtons.AimStart -= value;
        }

        public event Action<int, Vector2> SkillAimUpdate
        {
            add => _skillButtons.AimUpdate += value;
            remove => _skillButtons.AimUpdate -= value;
        }

        public event Action<int, Vector2> SkillAimEnd
        {
            add => _skillButtons.AimEnd += value;
            remove => _skillButtons.AimEnd -= value;
        }

        public event Action SkillAimCancel
        {
            add => _skillButtons.AimCancel += value;
            remove => _skillButtons.AimCancel -= value;
        }

        public void Bind(JoystickAreaView moveJoystick, IReadOnlyList<SkillButtonView> skills)
        {
            if (_isBound && _moveJoystick == moveJoystick && SameSkills(_skills, skills))
            {
                return;
            }

            Unbind();

            _moveJoystick = moveJoystick;
            _skills = CopySkills(skills);
            _isBound = true;

            if (_moveJoystick != null)
            {
                _moveJoystick.OnValueChanged += OnMoveChanged;
            }

            _skillButtons.Bind(_skills);
        }

        public void Bind(
            JoystickAreaView moveJoystick,
            SkillButtonView skill1,
            SkillButtonView skill2,
            SkillButtonView skill3)
        {
            Bind(moveJoystick, new[] { skill1, skill2, skill3 });
        }

        public void Unbind()
        {
            if (!_isBound) return;

            if (_moveJoystick != null)
            {
                _moveJoystick.OnValueChanged -= OnMoveChanged;
            }

            _skillButtons.Unbind();

            _moveJoystick = null;
            _skills = Array.Empty<SkillButtonView>();
            _isBound = false;
        }

        private void OnMoveChanged(JoystickOutput output)
        {
            MoveChanged?.Invoke(output);
        }

        private static SkillButtonView[] CopySkills(IReadOnlyList<SkillButtonView> skills)
        {
            if (skills == null || skills.Count == 0) return Array.Empty<SkillButtonView>();
            var copy = new SkillButtonView[skills.Count];
            for (var i = 0; i < skills.Count; i++) copy[i] = skills[i];
            return copy;
        }

        private static bool SameSkills(IReadOnlyList<SkillButtonView> left, IReadOnlyList<SkillButtonView> right)
        {
            var leftCount = left != null ? left.Count : 0;
            var rightCount = right != null ? right.Count : 0;
            if (leftCount != rightCount) return false;
            for (var i = 0; i < leftCount; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }
    }
}

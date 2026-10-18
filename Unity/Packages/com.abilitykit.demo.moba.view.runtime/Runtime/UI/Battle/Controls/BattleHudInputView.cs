using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudInputView : MonoBehaviour
    {
        [SerializeField] private JoystickAreaView _moveJoystick;
        [SerializeField] private SkillButtonView[] _skills;

        private readonly BattleHudInputControlBinding _binding = new BattleHudInputControlBinding();

        public event Action<JoystickOutput> MoveChanged
        {
            add => _binding.MoveChanged += value;
            remove => _binding.MoveChanged -= value;
        }

        public event Action<int> SkillClick
        {
            add => _binding.SkillClick += value;
            remove => _binding.SkillClick -= value;
        }

        public event Action<int> SkillLongPress
        {
            add => _binding.SkillLongPress += value;
            remove => _binding.SkillLongPress -= value;
        }

        public event Action<int, Vector2> SkillAimStart
        {
            add => _binding.SkillAimStart += value;
            remove => _binding.SkillAimStart -= value;
        }

        public event Action<int, Vector2> SkillAimUpdate
        {
            add => _binding.SkillAimUpdate += value;
            remove => _binding.SkillAimUpdate -= value;
        }

        public event Action<int, Vector2> SkillAimEnd
        {
            add => _binding.SkillAimEnd += value;
            remove => _binding.SkillAimEnd -= value;
        }

        public event Action SkillAimCancel
        {
            add => _binding.SkillAimCancel += value;
            remove => _binding.SkillAimCancel -= value;
        }

        public void Initialize(JoystickAreaView moveJoystick, IReadOnlyList<SkillButtonView> skills)
        {
            UnhookAll();

            _moveJoystick = moveJoystick;
            _skills = CopySkills(skills);

            if (isActiveAndEnabled)
            {
                HookAll();
            }
        }

        public void Initialize(
            JoystickAreaView moveJoystick,
            SkillButtonView skill1,
            SkillButtonView skill2,
            SkillButtonView skill3)
        {
            Initialize(moveJoystick, new[] { skill1, skill2, skill3 });
        }

        private void OnEnable()
        {
            HookAll();
        }

        private void OnDisable()
        {
            UnhookAll();
        }

        private void OnDestroy()
        {
            UnhookAll();
        }

        private void HookAll()
        {
            _binding.Bind(_moveJoystick, _skills);
        }

        private void UnhookAll()
        {
            _binding.Unbind();
        }

        private static SkillButtonView[] CopySkills(IReadOnlyList<SkillButtonView> skills)
        {
            if (skills == null || skills.Count == 0) return Array.Empty<SkillButtonView>();
            var copy = new SkillButtonView[skills.Count];
            for (var i = 0; i < skills.Count; i++) copy[i] = skills[i];
            return copy;
        }
    }
}

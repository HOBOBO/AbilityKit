using System;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudInputView : MonoBehaviour
    {
        [SerializeField] private JoystickAreaView _moveJoystick;
        [SerializeField] private SkillButtonView _skill1;
        [SerializeField] private SkillButtonView _skill2;
        [SerializeField] private SkillButtonView _skill3;

        private Action _skill1Click;
        private Action _skill2Click;
        private Action _skill3Click;

        private Action _skill1LongPress;
        private Action _skill2LongPress;
        private Action _skill3LongPress;

        private Action<Vector2> _skill1AimStart;
        private Action<Vector2> _skill2AimStart;
        private Action<Vector2> _skill3AimStart;

        private Action<Vector2> _skill1AimUpdate;
        private Action<Vector2> _skill2AimUpdate;
        private Action<Vector2> _skill3AimUpdate;

        private Action<Vector2> _skill1AimEnd;
        private Action<Vector2> _skill2AimEnd;
        private Action<Vector2> _skill3AimEnd;

        public event Action<JoystickOutput> MoveChanged;

        public event Action<int> SkillClick;
        public event Action<int> SkillLongPress;

        public event Action<int, Vector2> SkillAimStart;
        public event Action<int, Vector2> SkillAimUpdate;
        public event Action<int, Vector2> SkillAimEnd;

        private void OnEnable()
        {
            if (_moveJoystick != null)
            {
                _moveJoystick.OnValueChanged += OnMoveChanged;
            }

            HookSkill(_skill1, 1);
            HookSkill(_skill2, 2);
            HookSkill(_skill3, 3);
        }

        private void OnDisable()
        {
            if (_moveJoystick != null)
            {
                _moveJoystick.OnValueChanged -= OnMoveChanged;
            }

            UnhookSkill(_skill1, 1);
            UnhookSkill(_skill2, 2);
            UnhookSkill(_skill3, 3);
        }

        private void OnMoveChanged(JoystickOutput output)
        {
            MoveChanged?.Invoke(output);
        }

        private void HookSkill(SkillButtonView view, int slot)
        {
            if (view == null) return;

            if (slot == 1)
            {
                _skill1Click ??= () => SkillClick?.Invoke(1);
                _skill1LongPress ??= () => SkillLongPress?.Invoke(1);
                _skill1AimStart ??= dir => SkillAimStart?.Invoke(1, dir);
                _skill1AimUpdate ??= dir => SkillAimUpdate?.Invoke(1, dir);
                _skill1AimEnd ??= dir => SkillAimEnd?.Invoke(1, dir);

                view.OnClick += _skill1Click;
                view.OnLongPress += _skill1LongPress;
                view.OnAimStart += _skill1AimStart;
                view.OnAimUpdate += _skill1AimUpdate;
                view.OnAimEnd += _skill1AimEnd;
                return;
            }

            if (slot == 2)
            {
                _skill2Click ??= () => SkillClick?.Invoke(2);
                _skill2LongPress ??= () => SkillLongPress?.Invoke(2);
                _skill2AimStart ??= dir => SkillAimStart?.Invoke(2, dir);
                _skill2AimUpdate ??= dir => SkillAimUpdate?.Invoke(2, dir);
                _skill2AimEnd ??= dir => SkillAimEnd?.Invoke(2, dir);

                view.OnClick += _skill2Click;
                view.OnLongPress += _skill2LongPress;
                view.OnAimStart += _skill2AimStart;
                view.OnAimUpdate += _skill2AimUpdate;
                view.OnAimEnd += _skill2AimEnd;
                return;
            }

            if (slot == 3)
            {
                _skill3Click ??= () => SkillClick?.Invoke(3);
                _skill3LongPress ??= () => SkillLongPress?.Invoke(3);
                _skill3AimStart ??= dir => SkillAimStart?.Invoke(3, dir);
                _skill3AimUpdate ??= dir => SkillAimUpdate?.Invoke(3, dir);
                _skill3AimEnd ??= dir => SkillAimEnd?.Invoke(3, dir);

                view.OnClick += _skill3Click;
                view.OnLongPress += _skill3LongPress;
                view.OnAimStart += _skill3AimStart;
                view.OnAimUpdate += _skill3AimUpdate;
                view.OnAimEnd += _skill3AimEnd;
            }
        }

        private void UnhookSkill(SkillButtonView view, int slot)
        {
            if (view == null) return;

            if (slot == 1)
            {
                if (_skill1Click != null) view.OnClick -= _skill1Click;
                if (_skill1LongPress != null) view.OnLongPress -= _skill1LongPress;
                if (_skill1AimStart != null) view.OnAimStart -= _skill1AimStart;
                if (_skill1AimUpdate != null) view.OnAimUpdate -= _skill1AimUpdate;
                if (_skill1AimEnd != null) view.OnAimEnd -= _skill1AimEnd;
                return;
            }

            if (slot == 2)
            {
                if (_skill2Click != null) view.OnClick -= _skill2Click;
                if (_skill2LongPress != null) view.OnLongPress -= _skill2LongPress;
                if (_skill2AimStart != null) view.OnAimStart -= _skill2AimStart;
                if (_skill2AimUpdate != null) view.OnAimUpdate -= _skill2AimUpdate;
                if (_skill2AimEnd != null) view.OnAimEnd -= _skill2AimEnd;
                return;
            }

            if (slot == 3)
            {
                if (_skill3Click != null) view.OnClick -= _skill3Click;
                if (_skill3LongPress != null) view.OnLongPress -= _skill3LongPress;
                if (_skill3AimStart != null) view.OnAimStart -= _skill3AimStart;
                if (_skill3AimUpdate != null) view.OnAimUpdate -= _skill3AimUpdate;
                if (_skill3AimEnd != null) view.OnAimEnd -= _skill3AimEnd;
            }
        }
    }
}

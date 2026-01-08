using System;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudMoveInputMapper : MonoBehaviour
    {
        [SerializeField] private BattleHudInputView _hud;
        [SerializeField] private Transform _cameraTransform;

        public event Action<float, float> MoveDxDzChanged;

        private void Awake()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (_hud != null)
            {
                _hud.MoveChanged += OnMoveChanged;
            }
        }

        private void OnDisable()
        {
            if (_hud != null)
            {
                _hud.MoveChanged -= OnMoveChanged;
            }
        }

        private void OnMoveChanged(JoystickOutput output)
        {
            var v = output.Value;

            var dx = v.x;
            var dz = v.y;

            if (_cameraTransform != null)
            {
                var forward = _cameraTransform.forward;
                forward.y = 0f;
                var forwardLen = forward.magnitude;
                if (forwardLen > 0.0001f) forward /= forwardLen;

                var right = _cameraTransform.right;
                right.y = 0f;
                var rightLen = right.magnitude;
                if (rightLen > 0.0001f) right /= rightLen;

                var world = right * dx + forward * dz;
                dx = world.x;
                dz = world.z;
            }

            MoveDxDzChanged?.Invoke(dx, dz);
        }
    }
}

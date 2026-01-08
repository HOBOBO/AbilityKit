using System;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudSkillAimInputMapper : MonoBehaviour
    {
        [SerializeField] private BattleHudInputView _hud;
        [SerializeField] private Transform _cameraTransform;

        public event Action<int, Vector2> SkillAimStart;
        public event Action<int, Vector2> SkillAimUpdate;
        public event Action<int, Vector2> SkillAimEnd;

        private void Awake()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (_hud == null) return;
            _hud.SkillAimStart += OnAimStart;
            _hud.SkillAimUpdate += OnAimUpdate;
            _hud.SkillAimEnd += OnAimEnd;
        }

        private void OnDisable()
        {
            if (_hud == null) return;
            _hud.SkillAimStart -= OnAimStart;
            _hud.SkillAimUpdate -= OnAimUpdate;
            _hud.SkillAimEnd -= OnAimEnd;
        }

        private void OnAimStart(int slot, Vector2 dir)
        {
            SkillAimStart?.Invoke(slot, TransformDir(dir));
        }

        private void OnAimUpdate(int slot, Vector2 dir)
        {
            SkillAimUpdate?.Invoke(slot, TransformDir(dir));
        }

        private void OnAimEnd(int slot, Vector2 dir)
        {
            SkillAimEnd?.Invoke(slot, TransformDir(dir));
        }

        private Vector2 TransformDir(Vector2 dir)
        {
            var dx = dir.x;
            var dz = dir.y;

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

            var len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len <= 0.0001f) return Vector2.zero;
            return new Vector2(dx / len, dz / len);
        }
    }
}

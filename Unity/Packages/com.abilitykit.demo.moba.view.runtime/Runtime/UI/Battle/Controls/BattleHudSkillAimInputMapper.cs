using System;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudSkillAimInputMapper : IDisposable
    {
        private BattleHudInputView _hud;
        private Transform _cameraTransform;
        private readonly BattleHudSkillAimInputMapperFactory _factory = new BattleHudSkillAimInputMapperFactory();
        private BattleHudCameraTransformResolver _cameraResolver;
        private BattleHudCameraPlaneInputProjector _projector;
        private BattleHudInputViewSubscription _hudSubscription;

        public event Action<int, Vector2> SkillAimStart;
        public event Action<int, Vector2> SkillAimUpdate;
        public event Action<int, Vector2> SkillAimEnd;
        public event Action SkillAimCancel;

        public void Initialize(BattleHudInputView hud, Transform cameraTransform)
        {
            EnsureHudSubscription();
            _hudSubscription.Unhook();

            _hud = hud;
            _cameraTransform = cameraTransform;
            _hudSubscription.SetHud(_hud);

            ResolveCameraTransform();
            _hudSubscription.Hook();
        }

        public void Dispose()
        {
            _hudSubscription?.Clear();
            _hudSubscription = null;
            _hud = null;
            _cameraTransform = null;
        }

        private void ResolveCameraTransform()
        {
            EnsureDependencies();
            _cameraTransform = _cameraResolver.Resolve(_cameraTransform);
        }

        private void EnsureHudSubscription()
        {
            EnsureDependencies();
            _hudSubscription ??= _factory.CreateSubscription(OnAimStart, OnAimUpdate, OnAimEnd, OnAimCancel);
        }

        private void OnAimStart(int slot, Vector2 dir)
        {
            SkillAimStart?.Invoke(slot, TransformAim(dir));
        }

        private void OnAimUpdate(int slot, Vector2 dir)
        {
            SkillAimUpdate?.Invoke(slot, TransformAim(dir));
        }

        private void OnAimEnd(int slot, Vector2 dir)
        {
            SkillAimEnd?.Invoke(slot, TransformAim(dir));
        }

        private void OnAimCancel()
        {
            SkillAimCancel?.Invoke();
        }

        private Vector2 TransformAim(Vector2 dir)
        {
            EnsureDependencies();
            return _projector.ToCameraPlane(dir, _cameraTransform);
        }

        private void EnsureDependencies()
        {
            _cameraResolver ??= _factory.CreateCameraResolver();
            _projector ??= _factory.CreateProjector();
        }
    }

    internal sealed class BattleHudSkillAimInputMapperFactory
    {
        public BattleHudCameraTransformResolver CreateCameraResolver()
        {
            return new BattleHudCameraTransformResolver();
        }

        public BattleHudCameraPlaneInputProjector CreateProjector()
        {
            return new BattleHudCameraPlaneInputProjector();
        }

        public BattleHudInputViewSubscription CreateSubscription(
            Action<int, Vector2> aimStart,
            Action<int, Vector2> aimUpdate,
            Action<int, Vector2> aimEnd,
            Action aimCancel)
        {
            return new BattleHudInputViewSubscription(
                hud =>
                {
                    hud.SkillAimStart += aimStart;
                    hud.SkillAimUpdate += aimUpdate;
                    hud.SkillAimEnd += aimEnd;
                    hud.SkillAimCancel += aimCancel;
                },
                hud =>
                {
                    hud.SkillAimStart -= aimStart;
                    hud.SkillAimUpdate -= aimUpdate;
                    hud.SkillAimEnd -= aimEnd;
                    hud.SkillAimCancel -= aimCancel;
                });
        }
    }
}

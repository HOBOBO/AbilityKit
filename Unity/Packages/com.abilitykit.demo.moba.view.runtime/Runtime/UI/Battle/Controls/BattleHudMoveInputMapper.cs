using System;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    public sealed class BattleHudMoveInputMapper : IDisposable
    {
        private BattleHudInputView _hud;
        private Transform _cameraTransform;
        private readonly BattleHudMoveInputMapperFactory _factory = new BattleHudMoveInputMapperFactory();
        private BattleHudCameraTransformResolver _cameraResolver;
        private BattleHudCameraPlaneInputProjector _projector;
        private BattleHudInputViewSubscription _hudSubscription;

        public event Action<float, float> MoveDxDzChanged;

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
            _hudSubscription ??= _factory.CreateSubscription(OnMoveChanged);
        }

        private void OnMoveChanged(JoystickOutput output)
        {
            EnsureDependencies();
            var world = _projector.ToCameraPlane(output.Value, _cameraTransform);
            MoveDxDzChanged?.Invoke(world.x, world.y);
        }

        private void EnsureDependencies()
        {
            _cameraResolver ??= _factory.CreateCameraResolver();
            _projector ??= _factory.CreateProjector();
        }
    }

    internal sealed class BattleHudMoveInputMapperFactory
    {
        public BattleHudCameraTransformResolver CreateCameraResolver()
        {
            return new BattleHudCameraTransformResolver();
        }

        public BattleHudCameraPlaneInputProjector CreateProjector()
        {
            return new BattleHudCameraPlaneInputProjector();
        }

        public BattleHudInputViewSubscription CreateSubscription(Action<JoystickOutput> changed)
        {
            return new BattleHudInputViewSubscription(
                hud => hud.MoveChanged += changed,
                hud => hud.MoveChanged -= changed);
        }
    }

    internal sealed class BattleHudCameraPlaneInputProjector
    {
        public Vector2 ToCameraPlane(Vector2 input, Transform cameraTransform)
        {
            return BattleHudInputProjection.ToCameraPlane(input, cameraTransform);
        }
    }
}

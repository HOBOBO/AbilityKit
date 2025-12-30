using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public sealed class InputMoveTask : IMoveTask
    {
        private Vec3 _input;
        private readonly float _speed;

        public InputMoveTask(float speed)
        {
            _speed = speed;
            _input = Vec3.Zero;
        }

        public int Priority => 0;
        public MobaMoveGroup Group => MobaMoveGroup.Locomotion;
        public MobaMoveKind Kind => MobaMoveKind.Input;
        public MobaMoveStacking Stacking => MobaMoveStacking.Override;
        public bool IsFinished => false;

        public Vec3 Input => _input;

        public void SetInput(float dx, float dy, float dz)
        {
            _input = new Vec3(dx, dy, dz);
        }

        public Vec3 Tick(float dt)
        {
            if (_input.SqrMagnitude <= 0.0000001f) return Vec3.Zero;
            return _input * (_speed * dt);
        }

        public void Cancel()
        {
        }

        public bool TryMergeFrom(IMoveTask other)
        {
            return false;
        }
    }

    public sealed class DashMoveTask : IMoveTask
    {
        private readonly Vec3 _velocity;
        private float _timeLeft;

        public DashMoveTask(in Vec3 velocity, float duration, int priority)
        {
            _velocity = velocity;
            _timeLeft = duration;
            Priority = priority;
        }

        public int Priority { get; }
        public MobaMoveGroup Group => MobaMoveGroup.Ability;
        public MobaMoveKind Kind => MobaMoveKind.Dash;
        public MobaMoveStacking Stacking => MobaMoveStacking.Override;
        public bool IsFinished => _timeLeft <= 0f;

        public Vec3 Velocity => _velocity;
        public float TimeLeft => _timeLeft;

        public Vec3 Tick(float dt)
        {
            if (_timeLeft <= 0f) return Vec3.Zero;
            var step = dt;
            if (step > _timeLeft) step = _timeLeft;
            _timeLeft -= dt;
            return _velocity * step;
        }

        public void Cancel()
        {
            _timeLeft = 0f;
        }

        public bool TryMergeFrom(IMoveTask other)
        {
            return false;
        }
    }

    public sealed class KnockMoveTask : IMoveTask
    {
        private Vec3 _velocity;
        private float _timeLeft;
        private readonly float _gravity;

        public KnockMoveTask(in Vec3 velocity, float duration, float gravity, int priority)
        {
            _velocity = velocity;
            _timeLeft = duration;
            _gravity = gravity;
            Priority = priority;
        }

        public int Priority { get; }
        public MobaMoveGroup Group => MobaMoveGroup.Control;
        public MobaMoveKind Kind => MobaMoveKind.Knock;
        public MobaMoveStacking Stacking => MobaMoveStacking.Override;
        public bool IsFinished => _timeLeft <= 0f;

        public Vec3 Velocity => _velocity;
        public float TimeLeft => _timeLeft;
        public float Gravity => _gravity;

        public Vec3 Tick(float dt)
        {
            if (_timeLeft <= 0f) return Vec3.Zero;
            var step = dt;
            if (step > _timeLeft) step = _timeLeft;
            _timeLeft -= dt;

            var delta = _velocity * step;
            _velocity = new Vec3(_velocity.X, _velocity.Y - _gravity * dt, _velocity.Z);
            return delta;
        }

        public void Cancel()
        {
            _timeLeft = 0f;
        }

        public bool TryMergeFrom(IMoveTask other)
        {
            return false;
        }
    }
}

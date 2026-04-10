using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Core
{
    public struct MotionState
    {
        public Vec3 Position;
        public Vec3 Velocity;
        public Vec3 Forward;
        public float Time;

        public MotionState(in Vec3 position)
        {
            Position = position;
            Velocity = Vec3.Zero;
            Forward = new Vec3(0f, 0f, 1f);
            Time = 0f;
        }
    }
}

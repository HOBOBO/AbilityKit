using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
{
    public interface IMotionSource
    {
        int GroupId { get; }

        MotionStacking Stacking { get; }

        int Priority { get; }
        bool IsActive { get; }

        void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta);

        void Cancel();
    }
}
